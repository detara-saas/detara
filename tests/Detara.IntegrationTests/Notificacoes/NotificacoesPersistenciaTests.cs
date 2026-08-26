using Detara.Application.Abstracoes;
using Detara.Application.Notificacoes;
using Detara.Application.Comunicacao;
using Detara.Domain.Atendimento;
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
        var cliente = new Cliente(_empresaA, "Marina Souza", TipoPessoa.PessoaFisica, null, null, "11999998888",
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
        Assert.Equal(CanalComunicacaoVeiculoPronto.Nenhum, r.CanalAutomaticoVeiculoPronto);
        Assert.Equal(0, await db.ConfiguracoesNotificacaoEmpresa.CountAsync());
    }

    [Fact]
    public async Task IntegracaoDesativada_NaoCriaIntencao()
    {
        await using var db = Db(_empresaA); var repo = new NotificacoesRepositorio(db);
        await Integracao(db, repo).PrepararNotificacaoAsync(Evento(Guid.NewGuid()), default);
        await db.SaveChangesAsync(); Assert.Equal(0, await db.NotificacoesEmail.CountAsync());
        Assert.Equal(0, await db.ComunicacoesCliente.CountAsync());
    }

    [Fact]
    public async Task IntegracaoConfiguradaWhatsApp_CriaSomenteComunicacaoWhatsApp()
    {
        await using var db = Db(_empresaA);
        var repo = new NotificacoesRepositorio(db);
        repo.Adicionar(new ConfiguracaoNotificacaoEmpresa(_empresaA,
            CanalComunicacaoVeiculoPronto.WhatsApp, null, _usuarioA));
        await db.SaveChangesAsync();

        await Integracao(db, repo).PrepararNotificacaoAsync(Evento(Guid.NewGuid()), default);
        await db.SaveChangesAsync();

        var comunicacao = await db.ComunicacoesCliente.SingleAsync();
        Assert.Equal(CanalComunicacaoCliente.WhatsApp, comunicacao.Canal);
        Assert.Equal(StatusComunicacaoCliente.Pendente, comunicacao.Status);
        Assert.Equal("11999998888", comunicacao.DestinatarioSnapshot);
        Assert.Equal(OrigemComunicacaoCliente.Automatica, comunicacao.Origem);
        Assert.Equal(0, await db.NotificacoesEmail.CountAsync());
    }

    [Fact]
    public async Task IntegracaoAutomatica_NuncaCriaDoisCanais()
    {
        await using var db = Db(_empresaA);
        var repo = new NotificacoesRepositorio(db);
        repo.Adicionar(new ConfiguracaoNotificacaoEmpresa(_empresaA,
            CanalComunicacaoVeiculoPronto.Email, null, _usuarioA));
        await db.SaveChangesAsync();
        var evento = Evento(Guid.NewGuid());

        await Integracao(db, repo).PrepararNotificacaoAsync(evento, default);
        await db.SaveChangesAsync();

        var comunicacao = Assert.Single(await db.ComunicacoesCliente.ToListAsync());
        Assert.Equal(CanalComunicacaoCliente.Email, comunicacao.Canal);
        Assert.Single(await db.NotificacoesEmail.ToListAsync());
    }

    [Fact]
    public async Task SessaoWhatsApp_QueryFilterIsolaEmpresas()
    {
        await using (var dbA = Db(_empresaA))
        {
            dbA.SessoesWhatsAppEmpresa.Add(new SessaoWhatsAppEmpresa(
                _empresaA, $"tenant-{_empresaA:N}"));
            await dbA.SaveChangesAsync();
        }
        await using (var dbB = Db(_empresaB))
        {
            dbB.SessoesWhatsAppEmpresa.Add(new SessaoWhatsAppEmpresa(
                _empresaB, $"tenant-{_empresaB:N}"));
            await dbB.SaveChangesAsync();
        }

        await using var consultarA = Db(_empresaA);
        await using var consultarB = Db(_empresaB);
        var sessaoA = await new NotificacoesRepositorio(consultarA)
            .ObterSessaoWhatsAppAsync(false, default);
        var sessaoB = await new NotificacoesRepositorio(consultarB)
            .ObterSessaoWhatsAppAsync(false, default);

        Assert.Equal(_empresaA, sessaoA?.EmpresaId);
        Assert.Equal($"tenant-{_empresaA:N}", sessaoA?.SessionKey);
        Assert.Equal(_empresaB, sessaoB?.EmpresaId);
        Assert.Equal($"tenant-{_empresaB:N}", sessaoB?.SessionKey);
    }

    [Fact]
    public async Task IniciarConexaoWhatsApp_DerivaTenantDoUsuarioAutenticado()
    {
        await using var db = Db(_empresaA);
        var provider = new WhatsAppProviderFake(
            new(true, false, "id", null));
        var handler = new IniciarConexaoWhatsAppHandler(
            new Contexto(_empresaA, _usuarioA), new NotificacoesRepositorio(db), provider);

        var resultado = await handler.Handle(new(), default);

        Assert.Equal(StatusSessaoWhatsApp.Conectada, resultado.Status);
        Assert.Equal([_empresaA], provider.EmpresasConectadas);
        var persistida = await db.SessoesWhatsAppEmpresa.SingleAsync();
        Assert.Equal(_empresaA, persistida.EmpresaId);
        Assert.Equal($"tenant-{_empresaA:N}", persistida.SessionKey);
    }

    [Fact]
    public async Task EnvioManual_ComAutomaticoDesativado_CriaUmaIntencaoPendente()
    {
        var osId = Guid.NewGuid();
        await using var db = Db(_empresaA);
        var repo = new NotificacoesRepositorio(db);

        var resultado = await CriarHandlerEnvio(db, repo, Ordem(osId)).Handle(
            new EnviarAvisoVeiculoProntoCommand(osId), default);

        Assert.Equal(osId, resultado.Id);
        Assert.Equal(StatusNotificacaoEmail.Pendente, resultado.Status);
        var persistida = await db.NotificacoesEmail.SingleAsync();
        Assert.Equal(TipoTentativaNotificacaoEmail.Manual, persistida.TipoProximaTentativa);
        Assert.Equal(_usuarioA, persistida.ProximaTentativaSolicitadaPorUsuarioId);
        var comunicacao = await db.ComunicacoesCliente.SingleAsync();
        Assert.Equal(CanalComunicacaoCliente.Email, comunicacao.Canal);
        Assert.Equal(OrigemComunicacaoCliente.Manual, comunicacao.Origem);
    }

    [Fact]
    public async Task EnvioManualWhatsApp_CriaHistoricoSemCriarEmail()
    {
        var osId = Guid.NewGuid();
        await using var db = Db(_empresaA);
        var repo = new NotificacoesRepositorio(db);
        var servico = CriarServico(db, repo, Ordem(osId));

        var resultado = await servico.PrepararManualAsync(osId,
            CanalComunicacaoCliente.WhatsApp, Guid.NewGuid(), default);

        Assert.Equal(CanalComunicacaoCliente.WhatsApp, resultado.Canal);
        Assert.Equal(StatusComunicacaoCliente.Pendente, resultado.Status);
        Assert.Contains("disponível para retirada", resultado.Mensagem);
        Assert.Equal(0, await db.NotificacoesEmail.CountAsync());
        Assert.Single(await db.ComunicacoesCliente.ToListAsync());
    }

    [Fact]
    public async Task EnvioManual_RepetidoQuandoPendente_NaoDuplica()
    {
        var osId = Guid.NewGuid();
        await using var db = Db(_empresaA);
        var repo = new NotificacoesRepositorio(db);
        var handler = CriarHandlerEnvio(db, repo, Ordem(osId));
        await handler.Handle(new EnviarAvisoVeiculoProntoCommand(osId), default);

        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() =>
            handler.Handle(new EnviarAvisoVeiculoProntoCommand(osId), default));

        Assert.Equal(1, await db.NotificacoesEmail.CountAsync());
    }

    [Fact]
    public async Task EnvioManual_QuandoProcessando_NaoDuplica()
    {
        var osId = Guid.NewGuid();
        await using var db = Db(_empresaA);
        var notificacao = CriarNotificacao(osId);
        notificacao.MarcarProcessando(DateTime.UtcNow);
        db.NotificacoesEmail.Add(notificacao);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() =>
            CriarHandlerEnvio(db, new NotificacoesRepositorio(db), Ordem(osId)).Handle(
                new EnviarAvisoVeiculoProntoCommand(osId), default));

        Assert.Equal(1, await db.NotificacoesEmail.CountAsync());
    }

    [Theory]
    [InlineData(StatusOrdemServico.Aberta)]
    [InlineData(StatusOrdemServico.EmExecucao)]
    [InlineData(StatusOrdemServico.Cancelada)]
    [InlineData(StatusOrdemServico.Concluida)]
    public async Task EnvioManual_EmEstadoInvalido_EhRejeitado(StatusOrdemServico status)
    {
        var osId = Guid.NewGuid();
        await using var db = Db(_empresaA);
        var handler = CriarHandlerEnvio(db, new NotificacoesRepositorio(db), Ordem(osId, status));

        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() =>
            handler.Handle(new EnviarAvisoVeiculoProntoCommand(osId), default));

        Assert.Equal(0, await db.NotificacoesEmail.CountAsync());
    }

    [Fact]
    public async Task EnvioManual_ClienteSemEmail_EhRejeitadoSemCriarIntencao()
    {
        var osId = Guid.NewGuid();
        await using var db = Db(_empresaA);
        var cliente = await db.Clientes.SingleAsync(x => x.Id == _clienteA);
        cliente.Atualizar(cliente.Nome, cliente.TipoPessoa, null, null, null, null, null, null);
        await db.SaveChangesAsync();
        var handler = CriarHandlerEnvio(db, new NotificacoesRepositorio(db), Ordem(osId));

        var erro = await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() =>
            handler.Handle(new EnviarAvisoVeiculoProntoCommand(osId), default));

        Assert.Contains("e-mail válido", erro.Message);
        Assert.Equal(0, await db.NotificacoesEmail.CountAsync());
    }

    [Fact]
    public async Task IntegracaoAtivada_CriaSnapshotNoMesmoSaveEIdempotente()
    {
        await using var db = Db(_empresaA); var repo = new NotificacoesRepositorio(db);
        repo.Adicionar(new ConfiguracaoNotificacaoEmpresa(_empresaA,
            CanalComunicacaoVeiculoPronto.Email, "respostas@empresa-a.com", _usuarioA));
        await db.SaveChangesAsync(); var evento = Evento(Guid.NewGuid());
        await Integracao(db, repo).PrepararNotificacaoAsync(evento, default);
        Assert.Single(db.ChangeTracker.Entries<NotificacaoEmail>(), x => x.State == EntityState.Added);
        await db.SaveChangesAsync();
        await Integracao(db, repo).PrepararNotificacaoAsync(evento, default); await db.SaveChangesAsync();
        var n = await db.NotificacoesEmail.SingleAsync();
        Assert.Equal(StatusNotificacaoEmail.Pendente, n.Status); Assert.Equal("marina@cliente.com", n.DestinatarioEmailSnapshot);
        Assert.Equal("respostas@empresa-a.com", n.ResponderParaSnapshot); Assert.Equal(OrigemTemplateEmail.PadraoDetara, n.OrigemTemplate);
        Assert.Equal(1, await db.NotificacoesEmail.CountAsync());
        var comunicacao = await db.ComunicacoesCliente.SingleAsync();
        Assert.Equal(CanalComunicacaoCliente.Email, comunicacao.Canal);
        Assert.Equal(StatusComunicacaoCliente.Pendente, comunicacao.Status);
    }

    [Fact]
    public async Task AutomaticoJaCriado_BloqueiaSegundoEnvioInicialManual()
    {
        var osId = Guid.NewGuid();
        await using var db = Db(_empresaA);
        var repo = new NotificacoesRepositorio(db);
        repo.Adicionar(new ConfiguracaoNotificacaoEmpresa(_empresaA,
            CanalComunicacaoVeiculoPronto.Email, null, _usuarioA));
        await db.SaveChangesAsync();
        await Integracao(db, repo).PrepararNotificacaoAsync(Evento(osId), default);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() =>
            CriarHandlerEnvio(db, repo, Ordem(osId)).Handle(
                new EnviarAvisoVeiculoProntoCommand(osId), default));

        Assert.Equal(1, await db.NotificacoesEmail.CountAsync());
    }

    [Fact]
    public async Task ClienteSemEmail_NaoBloqueiaERegistraSemDestinatario()
    {
        await using var db = Db(_empresaA); var cliente = await db.Clientes.SingleAsync(x => x.Id == _clienteA);
        cliente.Atualizar(cliente.Nome, cliente.TipoPessoa, null, null, null, null, null, null);
        var repo = new NotificacoesRepositorio(db); repo.Adicionar(new ConfiguracaoNotificacaoEmpresa(
            _empresaA, CanalComunicacaoVeiculoPronto.Email, null, _usuarioA));
        await db.SaveChangesAsync(); await Integracao(db, repo).PrepararNotificacaoAsync(Evento(Guid.NewGuid()), default); await db.SaveChangesAsync();
        Assert.Equal(StatusNotificacaoEmail.SemDestinatario, (await db.NotificacoesEmail.SingleAsync()).Status);
    }

    [Fact]
    public async Task TemplatePersonalizado_GeraSnapshotQueNaoMudaDepois()
    {
        await using var db = Db(_empresaA); var repo = new NotificacoesRepositorio(db);
        repo.Adicionar(new ConfiguracaoNotificacaoEmpresa(_empresaA,
            CanalComunicacaoVeiculoPronto.Email, null, _usuarioA));
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
    public async Task RetryManual_ReagendaMesmaNotificacaoFalhada()
    {
        var osId = Guid.NewGuid();
        await using var db = Db(_empresaA);
        var notificacao = CriarNotificacao(osId);
        notificacao.MarcarProcessando(DateTime.UtcNow);
        var tentativa = notificacao.RegistrarFalha("rejeitada", false, 4, DateTime.UtcNow,
            null, TipoTentativaNotificacaoEmail.Automatica, null);
        db.NotificacoesEmail.Add(notificacao);
        db.TentativasNotificacaoEmail.Add(tentativa);
        await db.SaveChangesAsync();
        var repo = new NotificacoesRepositorio(db);
        var handler = new TentarNovamenteNotificacaoHandler(new Contexto(_empresaA, _usuarioA),
            repo, new ClientesNotificacoesConsulta(db), new AtendimentoFake(_empresaA, Ordem(osId)));

        var resultado = await handler.Handle(new TentarNovamenteNotificacaoCommand(osId), default);

        Assert.Equal(notificacao.Id, resultado.Id);
        Assert.Equal(StatusNotificacaoEmail.Pendente, resultado.Status);
        Assert.Equal(1, await db.NotificacoesEmail.CountAsync());
    }

    [Fact]
    public async Task ReenvioDepoisDeSucesso_CriaNovoRegistroEPreservaAnterior()
    {
        var osId = Guid.NewGuid();
        await using var db = Db(_empresaA);
        var anterior = CriarNotificacao(osId);
        anterior.MarcarProcessando(DateTime.UtcNow);
        var tentativa = anterior.RegistrarSucesso("provider-1", DateTime.UtcNow,
            TipoTentativaNotificacaoEmail.Automatica, null);
        db.NotificacoesEmail.Add(anterior);
        db.TentativasNotificacaoEmail.Add(tentativa);
        await db.SaveChangesAsync();
        var repo = new NotificacoesRepositorio(db);
        var solicitacaoId = Guid.NewGuid();
        var handler = CriarHandlerReenvio(db, repo, Ordem(osId));

        var resultado = await handler.Handle(
            new ReenviarAvisoVeiculoProntoCommand(osId, solicitacaoId), default);

        Assert.Equal(solicitacaoId, resultado.Id);
        Assert.Equal(StatusNotificacaoEmail.Pendente, resultado.Status);
        Assert.Equal(2, await db.NotificacoesEmail.CountAsync());
        Assert.Equal(StatusNotificacaoEmail.Enviada,
            (await db.NotificacoesEmail.AsNoTracking().SingleAsync(x => x.Id == anterior.Id)).Status);
    }

    [Fact]
    public async Task ReenvioComMesmaSolicitacao_EhIdempotente()
    {
        var osId = Guid.NewGuid();
        await using var db = Db(_empresaA);
        var anterior = CriarNotificacao(osId);
        anterior.MarcarProcessando(DateTime.UtcNow);
        db.NotificacoesEmail.Add(anterior);
        db.TentativasNotificacaoEmail.Add(anterior.RegistrarSucesso("provider-1",
            DateTime.UtcNow, TipoTentativaNotificacaoEmail.Automatica, null));
        await db.SaveChangesAsync();
        var repo = new NotificacoesRepositorio(db);
        var handler = CriarHandlerReenvio(db, repo, Ordem(osId));
        var command = new ReenviarAvisoVeiculoProntoCommand(osId, Guid.NewGuid());

        var primeiro = await handler.Handle(command, default);
        var repetido = await handler.Handle(command, default);

        Assert.Equal(primeiro.Id, repetido.Id);
        Assert.Equal(2, await db.NotificacoesEmail.CountAsync());
    }

    [Fact]
    public async Task EnvioManual_ComTemplateCustomizadoEVeiculoSemPlaca_RenderizaSemNull()
    {
        var osId = Guid.NewGuid();
        await using var db = Db(_empresaA);
        var repo = new NotificacoesRepositorio(db);
        repo.Adicionar(new TemplateEmailEmpresa(_empresaA,
            TipoTemplateEmail.VeiculoProntoRetirada,
            "Pronto: {{OrdemServicoCodigo}}",
            "<p>{{ClienteNome}} · {{VeiculoDescricao}} · {{Placa}}</p>", _usuarioA));
        await db.SaveChangesAsync();
        var ordem = Ordem(osId) with { VeiculoDescricao = "Sea-Doo GTX 300 · DEMO-JET-01", VeiculoPlaca = null };

        await CriarHandlerEnvio(db, repo, ordem).Handle(
            new EnviarAvisoVeiculoProntoCommand(osId), default);

        var persistida = await db.NotificacoesEmail.AsNoTracking().SingleAsync();
        Assert.Equal("Pronto: OS-2026-42", persistida.AssuntoSnapshot);
        Assert.Contains("Sea-Doo GTX 300 · DEMO-JET-01", persistida.CorpoHtmlSnapshot);
        Assert.DoesNotContain("null", persistida.CorpoHtmlSnapshot, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("undefined", persistida.CorpoHtmlSnapshot, StringComparison.OrdinalIgnoreCase);
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
            var osId = Guid.NewGuid();
            a.NotificacoesEmail.Add(new NotificacaoEmail(_empresaA, osId, _clienteA,
                TipoTemplateEmail.VeiculoProntoRetirada, "a@cliente.com", "A", "Assunto", "<p>Corpo</p>", OrigemTemplateEmail.PadraoDetara, null));
            a.ComunicacoesCliente.Add(new ComunicacaoCliente(Guid.NewGuid(), _empresaA,
                _clienteA, osId, CanalComunicacaoCliente.Email,
                TipoComunicacaoCliente.VeiculoPronto, "<p>Corpo</p>", "a@cliente.com",
                OrigemComunicacaoCliente.Automatica, null));
            await a.SaveChangesAsync();
        }
        await using var b = Db(_empresaB);
        Assert.Equal(0, await b.NotificacoesEmail.CountAsync());
        Assert.Equal(0, await b.ComunicacoesCliente.CountAsync());
    }

    [Fact]
    public async Task TenantA_NaoEnviaTentaNovamenteOuReenviaAvisoParaOrdemDoTenantB()
    {
        var osId = Guid.NewGuid();
        await using var db = Db(_empresaA);
        var repo = new NotificacoesRepositorio(db);
        var ordem = Ordem(osId);
        var atendimento = new AtendimentoFake(_empresaB, ordem);
        var envio = CriarHandlerEnvio(db, repo, ordem, _empresaB);
        var comunicacaoWhatsApp = CriarServico(db, repo, ordem, _empresaB);
        var retry = new TentarNovamenteNotificacaoHandler(new Contexto(_empresaA, _usuarioA),
            repo, new ClientesNotificacoesConsulta(db), atendimento);
        var reenvio = new ReenviarAvisoVeiculoProntoHandler(
            CriarServico(db, repo, ordem, _empresaB), repo);

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            envio.Handle(new EnviarAvisoVeiculoProntoCommand(osId), default));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            comunicacaoWhatsApp.PrepararManualAsync(osId,
                CanalComunicacaoCliente.WhatsApp, Guid.NewGuid(), default));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            retry.Handle(new TentarNovamenteNotificacaoCommand(osId), default));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            reenvio.Handle(new ReenviarAvisoVeiculoProntoCommand(osId, Guid.NewGuid()), default));

        Assert.Equal(0, await db.NotificacoesEmail.CountAsync());
        Assert.Equal(0, await db.ComunicacoesCliente.CountAsync());
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
        await Task.Delay(20); // Garante vencimento após a precisão temporal menor do SQLite.
        var fake = new ProvedorFake();
        await using (var conferir = Db(_empresaA))
            Assert.Equal(1, await conferir.NotificacoesEmail.CountAsync(x => x.Status == StatusNotificacaoEmail.Pendente && x.ProximaTentativaEmUtc <= DateTime.UtcNow));
        var services = new ServiceCollection(); services.AddSingleton(_options);
        services.AddSingleton<IEmailClienteProvider>(fake);
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

    [Fact]
    public async Task WorkerEmail_SucessoAtualizaHistoricoUnificado()
    {
        var osId = Guid.NewGuid();
        await using (var db = Db(_empresaA))
        {
            await CriarServico(db, new NotificacoesRepositorio(db), Ordem(osId))
                .PrepararManualAsync(osId, CanalComunicacaoCliente.Email,
                    Guid.NewGuid(), default);
        }
        await Task.Delay(20);
        var fake = new ProvedorFake();
        var services = new ServiceCollection(); services.AddSingleton(_options);
        services.AddSingleton<IEmailClienteProvider>(fake);
        await using var provider = services.BuildServiceProvider();
        var fila = new FilaNotificacoesServico(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new FilaNotificacoesOptions { TamanhoLote = 10 }),
            NullLogger<FilaNotificacoesServico>.Instance);

        Assert.Equal(1, await fila.ProcessarLoteAsync(default));

        await using var verificar = Db(_empresaA);
        var comunicacao = await verificar.ComunicacoesCliente.SingleAsync();
        Assert.Equal(StatusComunicacaoCliente.Enviado, comunicacao.Status);
        Assert.NotNull(comunicacao.DataEnvioUtc);
        Assert.Equal("fake-id", comunicacao.ProvedorMensagemId);
    }

    [Fact]
    public async Task WorkerWhatsApp_ProviderNaoConfiguradoRegistraFalha()
    {
        var osId = Guid.NewGuid();
        await using (var db = Db(_empresaA))
        {
            await CriarServico(db, new NotificacoesRepositorio(db), Ordem(osId))
                .PrepararManualAsync(osId, CanalComunicacaoCliente.WhatsApp,
                    Guid.NewGuid(), default);
        }
        var fake = new WhatsAppProviderFake(
            new(false, false, null, "WhatsApp indisponível para teste."));
        var services = new ServiceCollection(); services.AddSingleton(_options);
        services.AddSingleton<IWhatsAppClienteProvider>(fake);
        await using var provider = services.BuildServiceProvider();
        var fila = new FilaNotificacoesServico(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new FilaNotificacoesOptions { TamanhoLote = 10 }),
            NullLogger<FilaNotificacoesServico>.Instance);

        Assert.Equal(1, await fila.ProcessarLoteAsync(default));

        await using var verificar = Db(_empresaA);
        var comunicacao = await verificar.ComunicacoesCliente.SingleAsync();
        Assert.Equal(StatusComunicacaoCliente.Falhou, comunicacao.Status);
        Assert.Equal("WhatsApp indisponível para teste.", comunicacao.UltimoErroSeguro);
        Assert.Equal(_empresaA, Assert.Single(fake.Mensagens).EmpresaId);
    }

    private ComunicacaoClienteService Integracao(DetaraDbContext db, NotificacoesRepositorio repo) =>
        CriarServico(db, repo, Ordem(Guid.NewGuid()));
    private EnviarAvisoVeiculoProntoHandler CriarHandlerEnvio(DetaraDbContext db,
        NotificacoesRepositorio repo, OrdemServicoNotificacoesInterna ordem, Guid? empresaDaOrdem = null) =>
        new(CriarServico(db, repo, ordem, empresaDaOrdem), repo);
    private ReenviarAvisoVeiculoProntoHandler CriarHandlerReenvio(DetaraDbContext db,
        NotificacoesRepositorio repo, OrdemServicoNotificacoesInterna ordem) =>
        new(CriarServico(db, repo, ordem), repo);
    private ComunicacaoClienteService CriarServico(DetaraDbContext db,
        NotificacoesRepositorio repo, OrdemServicoNotificacoesInterna ordem,
        Guid? empresaDaOrdem = null) => new(new Contexto(_empresaA, _usuarioA), repo,
            new ClientesNotificacoesConsulta(db), new PlataformaNotificacoesConsulta(db),
            new AtendimentoFake(empresaDaOrdem ?? _empresaA, ordem),
            new RenderizadorTemplateEmail(), new RenderizadorTemplateWhatsApp());
    private OrdemServicoNotificacoesInterna Ordem(Guid osId,
        StatusOrdemServico status = StatusOrdemServico.AguardandoRetirada) =>
        new(osId, "OS-2026-42", status, _clienteA, "Marina Souza", "Honda Civic · ABC1D23", "ABC1D23");
    private NotificacaoEmail CriarNotificacao(Guid osId) => new(_empresaA, osId, _clienteA,
        TipoTemplateEmail.VeiculoProntoRetirada, "marina@cliente.com", "Marina Souza",
        "Assunto", "<p>Corpo</p>", OrigemTemplateEmail.PadraoDetara, null);
    private OrdemServicoFinalizadaNotificacoes Evento(Guid osId) => new(_empresaA, osId, "OS-2026-42", _clienteA,
        "Marina Souza", "Honda Civic", "ABC1D23");
    private DetaraDbContext Db(Guid empresa) => new(_options, new Contexto(empresa, empresa == _empresaA ? _usuarioA : Guid.NewGuid()));
    private sealed class Contexto(Guid empresaId, Guid? usuarioId = null) : IUsuarioContexto
    { public static Contexto Anonimo { get; } = new(Guid.Empty, Guid.Empty); public Guid UsuarioId { get; } = usuarioId ?? Guid.NewGuid(); public Guid EmpresaId { get; } = empresaId; public bool EstaAutenticado => EmpresaId != Guid.Empty; }
    private sealed class AtendimentoFake(Guid empresaId, OrdemServicoNotificacoesInterna ordem)
        : IAtendimentoNotificacoesConsulta
    {
        public Task<OrdemServicoNotificacoesInterna?> ObterOrdemServicoAsync(Guid empresa,
            Guid ordemServicoId, CancellationToken cancellationToken) =>
            Task.FromResult(empresa == empresaId && ordem.Id == ordemServicoId
                ? ordem
                : null);
    }
    private sealed class ProvedorFake : IProvedorEmail, IEmailClienteProvider
    {
        public List<MensagemEmailProvedor> Mensagens { get; } = [];
        public Task<ResultadoEnvioEmail> EnviarAsync(MensagemEmailProvedor mensagem,
            CancellationToken cancellationToken)
        {
            Mensagens.Add(mensagem);
            return Task.FromResult(new ResultadoEnvioEmail(true, false, "fake-id", null));
        }
        public Task<ResultadoEnvioComunicacaoCliente> EnviarAsync(
            MensagemEmailClienteProvider mensagem, CancellationToken cancellationToken)
        {
            Mensagens.Add(new(mensagem.Destinatario, mensagem.Assunto,
                mensagem.CorpoHtml, mensagem.ResponderPara, mensagem.ChaveIdempotencia));
            return Task.FromResult(new ResultadoEnvioComunicacaoCliente(
                true, false, "fake-id", null));
        }
    }
    private sealed class WhatsAppProviderFake(ResultadoEnvioComunicacaoCliente resultado)
        : IWhatsAppClienteProvider
    {
        public List<MensagemWhatsAppClienteProvider> Mensagens { get; } = [];
        public List<Guid> EmpresasConectadas { get; } = [];
        public Task<EstadoConexaoWhatsAppClienteProvider> IniciarConexaoAsync(
            Guid empresaId, CancellationToken cancellationToken)
        {
            EmpresasConectadas.Add(empresaId);
            return ObterStatusAsync(empresaId, cancellationToken);
        }
        public Task<EstadoConexaoWhatsAppClienteProvider> ObterStatusAsync(
            Guid empresaId, CancellationToken cancellationToken) =>
            Task.FromResult(new EstadoConexaoWhatsAppClienteProvider(
                StatusSessaoWhatsApp.Conectada, null, DateTime.UtcNow,
                DateTime.UtcNow, null));
        public Task<ResultadoEnvioComunicacaoCliente> EnviarAsync(
            MensagemWhatsAppClienteProvider mensagem, CancellationToken cancellationToken)
        {
            Mensagens.Add(mensagem);
            return Task.FromResult(resultado);
        }
    }
}
