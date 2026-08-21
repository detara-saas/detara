using Detara.Application.Abstracoes;
using Detara.Application.Plataforma;
using Detara.Contracts.Autorizacao;
using Detara.Domain.Entidades;
using Detara.Infrastructure.AdministracaoTenant;
using Detara.Infrastructure.Autenticacao;
using Detara.Infrastructure.Persistencia;
using Detara.Infrastructure.Plataforma;
using Detara.Domain.Plataforma;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.AdministracaoTenant;

public sealed class AdministracaoTenantPersistenciaTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly SenhaServico _senhas = new(new PasswordHasher<Usuario>());
    private DbContextOptions<DetaraDbContext> _options = null!;
    private Empresa _empresaA = null!;
    private Empresa _empresaB = null!;
    private Perfil _adminA = null!;
    private Perfil _adminB = null!;
    private Usuario _usuarioA = null!;
    private Usuario _usuarioB = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(_connection).Options;
        _empresaA = new("Empresa A", "Empresa A Ltda", "12345678000190", "empresa-a");
        _empresaB = new("Empresa B", "Empresa B Ltda", "98765432000190", "empresa-b");
        await using (var sistema = new DetaraDbContext(_options, Contexto.Anonimo))
        {
            await sistema.Database.EnsureCreatedAsync();
            sistema.Empresas.AddRange(_empresaA, _empresaB);
            sistema.Permissoes.AddRange(
                new Permissao(Permissoes.AdministracaoUsuario, "Administrar usuários"),
                new Permissao(Permissoes.ClientesVisualizar, "Visualizar clientes"),
                new Permissao(Permissoes.FinanceiroEstornarPagamento, "Estornar pagamentos"));
            await sistema.SaveChangesAsync();
        }

        (_adminA, _usuarioA) = await CriarAdministradorAsync(_empresaA, "admin@empresa-a.test", "SenhaAtual123!");
        (_adminB, _usuarioB) = await CriarAdministradorAsync(_empresaB, "compartilhado@exemplo.test", "SenhaAtual123!");
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Empresa_AtualizaSomenteEmpresaDoContextoESemAlterarSlug()
    {
        await using var db = CriarContexto(_empresaA.Id, _usuarioA.Id);
        var servico = new AdministracaoEmpresaTenantServico(db, new Contexto(_empresaA.Id, _usuarioA.Id));

        var resultado = await servico.AtualizarAsync("Empresa A Nova", "Empresa A Ltda",
            "12.345.678/0001-90", "contato@a.test", "11999999999", "America/Sao_Paulo", 1,
            CancellationToken.None);

        Assert.Equal("Empresa A Nova", resultado.NomeFantasia);
        Assert.Equal("empresa-a", resultado.Slug);
        await using var sistema = new DetaraDbContext(_options, Contexto.Anonimo);
        Assert.Equal("Empresa B", (await sistema.Empresas.SingleAsync(x => x.Id == _empresaB.Id)).NomeFantasia);
    }

    [Fact]
    public async Task Usuario_ConviteRejeitaDuplicadoNoMesmoTenant()
    {
        await using var db = CriarContexto(_empresaA.Id, _usuarioA.Id);
        var servico = CriarUsuariosServico(db, _empresaA.Id, _usuarioA.Id);

        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => servico.ConvidarAsync(
            "Outro admin", _usuarioA.Email.ToUpperInvariant(), _adminA.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Usuario_MesmoEmailDeOutroTenantEhPermitidoEConviteFicaTenantScoped()
    {
        await using var db = CriarContexto(_empresaA.Id, _usuarioA.Id);
        var servico = CriarUsuariosServico(db, _empresaA.Id, _usuarioA.Id);

        var resultado = await servico.ConvidarAsync(
            "Usuário compartilhado", _usuarioB.Email, _adminA.Id, CancellationToken.None);

        Assert.Equal("Convite pendente", resultado.Status);
        Assert.Equal(_usuarioB.Email, resultado.Email);
        Assert.Single(await db.ConvitesAdministradoresEmpresa.ToArrayAsync());
    }

    [Fact]
    public async Task ConviteTenant_AtivaContaUmaUnicaVezESemAuditoriaPlatform()
    {
        const string token = "token-tenant-seguro";
        await using (var db = CriarContexto(_empresaA.Id, _usuarioA.Id))
        {
            var usuarios = CriarUsuariosServico(db, _empresaA.Id, _usuarioA.Id);
            var criado = await usuarios.ConvidarAsync(
                "Convidado", "convidado@a.test", _adminA.Id, CancellationToken.None);
            var convite = await db.ConvitesAdministradoresEmpresa.SingleAsync(x => x.UsuarioId == criado.Id);
            var agora = DateTime.UtcNow;
            convite.IniciarEnvio(ConvitesAdministradoresEmpresaServico.HashToken(token), agora.AddHours(24), agora);
            convite.RegistrarEnvio("provider-id", agora);
            await db.SaveChangesAsync();
        }

        await using (var db = CriarContexto(_empresaA.Id, _usuarioA.Id))
        {
            var convites = new ConvitesAdministradoresEmpresaServico(
                db, _options, new PasswordHasher<Usuario>());
            await convites.AceitarAsync(token, "SenhaDefinida123!", "trace-tenant", CancellationToken.None);
            await Assert.ThrowsAsync<ConviteAdministradorInvalidoException>(() =>
                convites.AceitarAsync(token, "OutraSenha123!", null, CancellationToken.None));
        }

        await using var verificacao = new DetaraDbContext(_options, Contexto.Anonimo);
        var usuario = await verificacao.Usuarios.IgnoreQueryFilters()
            .SingleAsync(x => x.Email == "convidado@a.test");
        Assert.True(usuario.EhAtivo);
        Assert.Empty(await verificacao.AuditoriasPlataforma.ToArrayAsync());
        Assert.Equal(StatusConviteAdministradorEmpresa.Aceito,
            (await verificacao.ConvitesAdministradoresEmpresa.SingleAsync(x => x.UsuarioId == usuario.Id)).Status);
    }

    [Fact]
    public async Task Usuario_NaoPodeInativarOuAlterarOProprioPerfil()
    {
        await using var db = CriarContexto(_empresaA.Id, _usuarioA.Id);
        var servico = CriarUsuariosServico(db, _empresaA.Id, _usuarioA.Id);

        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => servico.AlterarStatusAsync(
            _usuarioA.Id, false, _usuarioA.Versao, CancellationToken.None));
        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => servico.AlterarPerfilAsync(
            _usuarioA.Id, _adminA.Id, _usuarioA.Versao, CancellationToken.None));
    }

    [Fact]
    public async Task Usuario_UltimoAdministradorNaoPodeSerInativado()
    {
        var contextoExterno = new Contexto(_empresaA.Id, Guid.NewGuid());
        await using var db = new DetaraDbContext(_options, contextoExterno);
        var servico = new AdministracaoUsuariosTenantServico(db, contextoExterno, _senhas);

        var erro = await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => servico.AlterarStatusAsync(
            _usuarioA.Id, false, _usuarioA.Versao, CancellationToken.None));

        Assert.Contains("último administrador", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PerfilSistemaEhProtegidoEPerfilCrossTenantNaoAparece()
    {
        await using var db = CriarContexto(_empresaA.Id, _usuarioA.Id);
        var servico = new AdministracaoPerfisTenantServico(db, new Contexto(_empresaA.Id, _usuarioA.Id));

        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => servico.AtualizarAsync(
            _adminA.Id, "Administrador alterado", null, [Permissoes.AdministracaoUsuario],
            _adminA.Versao, CancellationToken.None));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            servico.ObterAsync(_adminB.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Perfil_RejeitaPermissaoDesconhecida()
    {
        await using var db = CriarContexto(_empresaA.Id, _usuarioA.Id);
        var servico = new AdministracaoPerfisTenantServico(db, new Contexto(_empresaA.Id, _usuarioA.Id));

        await Assert.ThrowsAsync<ArgumentException>(() => servico.CriarAsync(
            "Forjado", null, ["Admin.Tudo"], CancellationToken.None));
    }

    [Fact]
    public async Task Perfil_CallerNaoConcedePermissaoQueNaoPossui()
    {
        var (perfilLimitado, caller) = await CriarUsuarioLimitadoAsync();
        await using var db = CriarContexto(_empresaA.Id, caller.Id);
        var servico = new AdministracaoPerfisTenantServico(db, new Contexto(_empresaA.Id, caller.Id));

        var erro = await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => servico.CriarAsync(
            "Financeiro elevado", null,
            [Permissoes.AdministracaoUsuario, Permissoes.FinanceiroEstornarPagamento],
            CancellationToken.None));

        Assert.NotNull(perfilLimitado);
        Assert.Contains("não pode conceder", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MinhaConta_EmailExigeSenhaAtualEPermiteEmailExistenteEmOutroTenant()
    {
        await using var db = CriarContexto(_empresaA.Id, _usuarioA.Id);
        var servico = new MinhaContaTenantServico(db, new Contexto(_empresaA.Id, _usuarioA.Id), _senhas);
        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => servico.AtualizarEmailAsync(
            "novo@exemplo.test", "senha-incorreta", _usuarioA.Versao, CancellationToken.None));

        await servico.AtualizarEmailAsync(
            _usuarioB.Email, "SenhaAtual123!", _usuarioA.Versao, CancellationToken.None);

        var atualizado = await db.Usuarios.SingleAsync(x => x.Id == _usuarioA.Id);
        Assert.Equal(_usuarioB.Email, atualizado.Email);
        Assert.True(atualizado.VersaoSeguranca > _usuarioA.VersaoSeguranca);
    }

    [Fact]
    public async Task MinhaConta_AlterarSenhaRevogaVersaoEInvalidaSenhaAnterior()
    {
        await using var db = CriarContexto(_empresaA.Id, _usuarioA.Id);
        var servico = new MinhaContaTenantServico(db, new Contexto(_empresaA.Id, _usuarioA.Id), _senhas);
        var versaoSeguranca = _usuarioA.VersaoSeguranca;

        await servico.AlterarSenhaAsync("SenhaAtual123!", "NovaSenha456!", _usuarioA.Versao,
            CancellationToken.None);

        var atualizado = await db.Usuarios.SingleAsync(x => x.Id == _usuarioA.Id);
        Assert.True(atualizado.VersaoSeguranca > versaoSeguranca);
        Assert.False(_senhas.Verificar(atualizado, atualizado.SenhaHash, "SenhaAtual123!"));
        Assert.True(_senhas.Verificar(atualizado, atualizado.SenhaHash, "NovaSenha456!"));
    }

    private async Task<(Perfil Perfil, Usuario Usuario)> CriarAdministradorAsync(
        Empresa empresa, string email, string senha)
    {
        var perfil = new Perfil(empresa.Id, "Administrador", "Acesso administrativo", true);
        var usuario = new Usuario(empresa.Id, perfil.Id, "Administrador", email, "temporario");
        await using var db = CriarContexto(empresa.Id, usuario.Id);
        var permissoes = await db.Permissoes.ToArrayAsync();
        foreach (var permissao in permissoes) perfil.ConcederPermissao(permissao);
        usuario.AlterarSenhaHash(_senhas.GerarHash(usuario, senha));
        db.AddRange(perfil, usuario);
        await db.SaveChangesAsync();
        return (perfil, usuario);
    }

    private async Task<(Perfil Perfil, Usuario Usuario)> CriarUsuarioLimitadoAsync()
    {
        var perfil = new Perfil(_empresaA.Id, "Gestor limitado");
        var usuario = new Usuario(_empresaA.Id, perfil.Id, "Gestor limitado", "limitado@a.test", "temporario");
        await using var db = CriarContexto(_empresaA.Id, usuario.Id);
        perfil.ConcederPermissao(await db.Permissoes.SingleAsync(x => x.Codigo == Permissoes.AdministracaoUsuario));
        usuario.AlterarSenhaHash(_senhas.GerarHash(usuario, "SenhaAtual123!"));
        db.AddRange(perfil, usuario);
        await db.SaveChangesAsync();
        return (perfil, usuario);
    }

    private AdministracaoUsuariosTenantServico CriarUsuariosServico(
        DetaraDbContext db, Guid empresaId, Guid usuarioId) =>
        new(db, new Contexto(empresaId, usuarioId), _senhas);

    private DetaraDbContext CriarContexto(Guid empresaId, Guid usuarioId) =>
        new(_options, new Contexto(empresaId, usuarioId));

    private sealed class Contexto(Guid empresaId, Guid usuarioId, bool autenticado = true) : IUsuarioContexto
    {
        public static Contexto Anonimo { get; } = new(Guid.Empty, Guid.Empty, false);
        public Guid UsuarioId { get; } = usuarioId;
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado { get; } = autenticado;
    }
}
