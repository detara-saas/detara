using Detara.Application.Abstracoes;
using Detara.Application.Notificacoes;
using Detara.Domain.Entidades;
using Detara.Domain.Notificacoes;
using Detara.Infrastructure.Notificacoes;
using Detara.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Detara.IntegrationTests.Notificacoes;

public sealed class NotificacoesPersistenciaTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<DetaraDbContext> _options = null!;
    private Guid _empresaA, _empresaB, _usuarioA, _clienteA;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(_connection).Options;
        var a = new Empresa("Estética A", "Estética A Ltda", "11111111000111", "email-a");
        var b = new Empresa("Estética B", "Estética B Ltda", "22222222000122", "email-b");
        _empresaA = a.Id; _empresaB = b.Id;
        await using (var sistema = new DetaraDbContext(_options, Contexto.Anonimo))
        {
            await sistema.Database.EnsureCreatedAsync(); sistema.Empresas.AddRange(a, b); await sistema.SaveChangesAsync();
        }
        await using var tenant = Db(_empresaA);
        var perfil = new Perfil(_empresaA, "Administrador"); tenant.Perfis.Add(perfil);
        var usuario = new Usuario(_empresaA, perfil.Id, "Admin A", "admin@empresa-a.com", "hash");
        var cliente = new Cliente(_empresaA, "Marina Souza", TipoPessoa.PessoaFisica, null, null, null,
            "marina@cliente.com", null, null);
        _usuarioA = usuario.Id; _clienteA = cliente.Id;
        tenant.AddRange(usuario, cliente); await tenant.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task GetAusente_RetornaDefaultDesativadoSemEscrever()
    {
        await using var db = Db(_empresaA); var repo = new NotificacoesRepositorio(db);
        var r = await new ObterConfiguracaoNotificacaoHandler(repo).Handle(new(), default);
        Assert.False(r.EnviarVeiculoProntoAutomaticamente);
        Assert.Equal(0, await db.ConfiguracoesNotificacaoEmpresa.CountAsync());
    }

    [Fact]
    public async Task IntegracaoDesativada_NaoCriaIntencao()
    {
        await using var db = Db(_empresaA); var repo = new NotificacoesRepositorio(db);
        await Integracao(db, repo).PrepararNotificacaoAsync(Evento(Guid.NewGuid()), default);
        await db.SaveChangesAsync(); Assert.Equal(0, await db.NotificacoesEmail.CountAsync());
    }

    [Fact]
    public async Task IntegracaoAtivada_CriaSnapshotNoMesmoSaveEIdempotente()
    {
        await using var db = Db(_empresaA); var repo = new NotificacoesRepositorio(db);
        repo.Adicionar(new ConfiguracaoNotificacaoEmpresa(_empresaA, true, "respostas@empresa-a.com", _usuarioA));
        await db.SaveChangesAsync(); var evento = Evento(Guid.NewGuid());
        await Integracao(db, repo).PrepararNotificacaoAsync(evento, default);
        Assert.Single(db.ChangeTracker.Entries<NotificacaoEmail>(), x => x.State == EntityState.Added);
        await db.SaveChangesAsync();
        await Integracao(db, repo).PrepararNotificacaoAsync(evento, default); await db.SaveChangesAsync();
        var n = await db.NotificacoesEmail.SingleAsync();
        Assert.Equal(StatusNotificacaoEmail.Pendente, n.Status); Assert.Equal("marina@cliente.com", n.DestinatarioEmailSnapshot);
        Assert.Equal("respostas@empresa-a.com", n.ResponderParaSnapshot); Assert.Equal(OrigemTemplateEmail.PadraoDetara, n.OrigemTemplate);
        Assert.Equal(1, await db.NotificacoesEmail.CountAsync());
    }

    [Fact]
    public async Task ClienteSemEmail_NaoBloqueiaERegistraSemDestinatario()
    {
        await using var db = Db(_empresaA); var cliente = await db.Clientes.SingleAsync(x => x.Id == _clienteA);
        cliente.Atualizar(cliente.Nome, cliente.TipoPessoa, null, null, null, null, null, null);
        var repo = new NotificacoesRepositorio(db); repo.Adicionar(new ConfiguracaoNotificacaoEmpresa(_empresaA, true, null, _usuarioA));
        await db.SaveChangesAsync(); await Integracao(db, repo).PrepararNotificacaoAsync(Evento(Guid.NewGuid()), default); await db.SaveChangesAsync();
        Assert.Equal(StatusNotificacaoEmail.SemDestinatario, (await db.NotificacoesEmail.SingleAsync()).Status);
    }

    [Fact]
    public async Task TemplatePersonalizado_GeraSnapshotQueNaoMudaDepois()
    {
        await using var db = Db(_empresaA); var repo = new NotificacoesRepositorio(db);
        repo.Adicionar(new ConfiguracaoNotificacaoEmpresa(_empresaA, true, null, _usuarioA));
        var template = new TemplateEmailEmpresa(_empresaA, TipoTemplateEmail.VeiculoProntoRetirada,
            "Pronto: {{OrdemServicoCodigo}}", "<p>Olá {{ClientePrimeiroNome}} <strong>{{Placa}}</strong></p>", _usuarioA);
        repo.Adicionar(template); await db.SaveChangesAsync();
        await Integracao(db, repo).PrepararNotificacaoAsync(Evento(Guid.NewGuid()), default); await db.SaveChangesAsync();
        var snapshot = await db.NotificacoesEmail.AsNoTracking().SingleAsync();
        template = await db.TemplatesEmailEmpresa.SingleAsync(); template.Atualizar("Outro assunto", "<p>Outro</p>", _usuarioA); await db.SaveChangesAsync();
        var persistida = await db.NotificacoesEmail.AsNoTracking().SingleAsync();
        Assert.Equal("Pronto: OS-2026-42", persistida.AssuntoSnapshot); Assert.Equal(snapshot.CorpoHtmlSnapshot, persistida.CorpoHtmlSnapshot);
        Assert.Equal(OrigemTemplateEmail.PersonalizadoEmpresa, persistida.OrigemTemplate);
    }

    [Fact]
    public async Task RestaurarTemplate_ExcluiCustomizacaoEFallbackVoltaDinamico()
    {
        await using var db = Db(_empresaA); var repo = new NotificacoesRepositorio(db);
        repo.Adicionar(new TemplateEmailEmpresa(_empresaA, TipoTemplateEmail.VeiculoProntoRetirada, "Custom", "<p>Custom</p>", _usuarioA));
        await db.SaveChangesAsync(); var r = await new RestaurarTemplateVeiculoProntoHandler(repo, new RenderizadorTemplateEmail()).Handle(new(), default);
        Assert.Equal(OrigemTemplateEmail.PadraoDetara, r.Origem); Assert.Equal(0, await db.TemplatesEmailEmpresa.CountAsync());
    }

    [Fact]
    public async Task LeituraTenant_NaoExibeNotificacaoDeOutraEmpresa()
    {
        await using (var a = Db(_empresaA))
        {
            a.NotificacoesEmail.Add(new NotificacaoEmail(_empresaA, Guid.NewGuid(), _clienteA,
                TipoTemplateEmail.VeiculoProntoRetirada, "a@cliente.com", "A", "Assunto", "<p>Corpo</p>", OrigemTemplateEmail.PadraoDetara, null));
            await a.SaveChangesAsync();
        }
        await using var b = Db(_empresaB); Assert.Equal(0, await b.NotificacoesEmail.CountAsync());
    }

    [Fact]
    public async Task EnvioTeste_UsaEmailDoUsuarioENaoCriaOsOuNotificacao()
    {
        await using var db = Db(_empresaA); var provedor = new ProvedorFake();
        await new EnviarTesteVeiculoProntoHandler(new Contexto(_empresaA, _usuarioA), new NotificacoesRepositorio(db),
            new RenderizadorTemplateEmail(), new PlataformaNotificacoesConsulta(db), provedor).Handle(new(), default);
        Assert.Equal("admin@empresa-a.com", Assert.Single(provedor.Mensagens).Destinatario);
        Assert.Equal(0, await db.NotificacoesEmail.CountAsync()); Assert.Equal(0, await db.OrdensServico.CountAsync());
    }

    [Fact]
    public async Task Worker_ProcessaUmaVezEUsaChaveIdempotenteEstavel()
    {
        Guid notificacaoId;
        await using (var db = Db(_empresaA))
        {
            var n = new NotificacaoEmail(_empresaA, Guid.NewGuid(), _clienteA,
                TipoTemplateEmail.VeiculoProntoRetirada, "marina@cliente.com", "Marina", "Assunto",
                "<p>Corpo snapshot</p>", OrigemTemplateEmail.PadraoDetara, null);
            notificacaoId = n.Id; db.NotificacoesEmail.Add(n); await db.SaveChangesAsync();
        }
        var fake = new ProvedorFake();
        await using (var conferir = Db(_empresaA))
            Assert.Equal(1, await conferir.NotificacoesEmail.CountAsync(x => x.Status == StatusNotificacaoEmail.Pendente && x.ProximaTentativaEmUtc <= DateTime.UtcNow));
        var services = new ServiceCollection(); services.AddSingleton(_options); services.AddSingleton<IProvedorEmail>(fake);
        await using var provider = services.BuildServiceProvider();
        var fila = new FilaNotificacoesServico(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new FilaNotificacoesOptions { TamanhoLote = 10, MaximoTentativas = 4 }),
            NullLogger<FilaNotificacoesServico>.Instance);
        var processadas = await fila.ProcessarLoteAsync(default);
        var enviada = Assert.Single(fake.Mensagens);
        Assert.Equal($"notificacao-email/{notificacaoId:N}", enviada.ChaveIdempotencia);
        await using var verificar = Db(_empresaA);
        Assert.Equal(StatusNotificacaoEmail.Enviada, (await verificar.NotificacoesEmail.SingleAsync()).Status);
        Assert.Equal(1, await verificar.TentativasNotificacaoEmail.CountAsync());
        Assert.Equal(1, processadas);
        Assert.Equal(0, await fila.ProcessarLoteAsync(default));
    }

    private IntegracaoNotificacoesOrdensServico Integracao(DetaraDbContext db, NotificacoesRepositorio repo) =>
        new(repo, new ClientesNotificacoesConsulta(db), new PlataformaNotificacoesConsulta(db), new RenderizadorTemplateEmail());
    private OrdemServicoFinalizadaNotificacoes Evento(Guid osId) => new(_empresaA, osId, "OS-2026-42", _clienteA,
        "Marina Souza", "Honda Civic", "ABC1D23");
    private DetaraDbContext Db(Guid empresa) => new(_options, new Contexto(empresa, empresa == _empresaA ? _usuarioA : Guid.NewGuid()));
    private sealed class Contexto(Guid empresaId, Guid? usuarioId = null) : IUsuarioContexto
    { public static Contexto Anonimo { get; } = new(Guid.Empty, Guid.Empty); public Guid UsuarioId { get; } = usuarioId ?? Guid.NewGuid(); public Guid EmpresaId { get; } = empresaId; public bool EstaAutenticado => EmpresaId != Guid.Empty; }
    private sealed class ProvedorFake : IProvedorEmail
    { public List<MensagemEmailProvedor> Mensagens { get; } = []; public Task<ResultadoEnvioEmail> EnviarAsync(MensagemEmailProvedor mensagem, CancellationToken cancellationToken) { Mensagens.Add(mensagem); return Task.FromResult(new ResultadoEnvioEmail(true, false, "fake-id", null)); } }
}
