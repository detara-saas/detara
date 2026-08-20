using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Detara.IntegrationTests.Autorizacao;

public sealed class DesenvolvimentoSeedTests
{
    [Fact]
    public async Task SeedIncremental_ConcedeNovasPermissoesAoAdministradorSemDuplicarRegistros()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection()
            .AddLogging()
            .AddDbContext<DetaraDbContext>(options => options.UseSqlite(connection))
            .AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>()
            .BuildServiceProvider();

        var options = services.GetRequiredService<DbContextOptions<DetaraDbContext>>();
        var empresa = new Empresa(
            "Empresa Seed",
            "Empresa Seed Ltda",
            "00000000000100",
            "empresa-seed",
            "admin@seed.local");

        await using (var contextSistema = new DetaraDbContext(options, UsuarioContextoTeste.Anonimo))
        {
            await contextSistema.Database.EnsureCreatedAsync();
            contextSistema.Empresas.Add(empresa);
            await contextSistema.SaveChangesAsync();
        }

        await using (var contextTenant = new DetaraDbContext(options, new UsuarioContextoTeste(empresa.Id)))
        {
            var perfil = new Perfil(empresa.Id, "Administrador");
            var permissaoLegada = new Permissao("Clientes.Visualizar", "Visualizar clientes");
            perfil.ConcederPermissao(permissaoLegada);
            contextTenant.AddRange(perfil, permissaoLegada);
            await contextTenant.SaveChangesAsync();
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:Enabled"] = "true",
                ["Seed:SenhaAdministrador"] = "senha-exclusiva-de-teste",
                ["Seed:SlugEmpresa"] = "empresa-seed",
                ["Seed:EmailAdministrador"] = "admin@seed.local"
            })
            .Build();

        await services.InicializarDesenvolvimentoAsync(configuration);
        var contagensPrimeiraExecucao = await ObterEstadoAsync(options, empresa.Id);

        await services.InicializarDesenvolvimentoAsync(configuration);
        var contagensSegundaExecucao = await ObterEstadoAsync(options, empresa.Id);

        Assert.Equal(contagensPrimeiraExecucao, contagensSegundaExecucao);
        Assert.Equal(2, contagensSegundaExecucao.PermissoesConfiguracao);
        Assert.Equal(2, contagensSegundaExecucao.PermissoesConfiguracaoDoAdministrador);
        Assert.Equal(1, contagensSegundaExecucao.PermissoesOrdemServicoEditar);
        Assert.Equal(1, contagensSegundaExecucao.PermissoesOrdemServicoEditarDoAdministrador);
        Assert.Equal(4, contagensSegundaExecucao.PermissoesFinanceiro);
        Assert.Equal(4, contagensSegundaExecucao.PermissoesFinanceiroDoAdministrador);
        Assert.Equal(1, contagensSegundaExecucao.PermissoesNotificacoes);
        Assert.Equal(1, contagensSegundaExecucao.PermissoesNotificacoesDoAdministrador);
        Assert.Equal(1, contagensSegundaExecucao.UsuariosAdministradores);
        Assert.Equal(
            contagensSegundaExecucao.TotalPermissoes,
            contagensSegundaExecucao.TotalCodigosDistintos);
    }

    private static async Task<EstadoSeed> ObterEstadoAsync(
        DbContextOptions<DetaraDbContext> options,
        Guid empresaId)
    {
        await using var context = new DetaraDbContext(options, new UsuarioContextoTeste(empresaId));
        var administrador = await context.Perfis
            .Include(perfil => perfil.Permissoes)
            .SingleAsync(perfil => perfil.Nome == "Administrador");

        return new EstadoSeed(
            await context.Permissoes.CountAsync(),
            await context.Permissoes.Select(permissao => permissao.Codigo).Distinct().CountAsync(),
            await context.Permissoes.CountAsync(permissao =>
                permissao.Codigo == "Configuracoes.Visualizar" ||
                permissao.Codigo == "Configuracoes.Editar"),
            administrador.Permissoes.Count(permissao =>
                permissao.Codigo == "Configuracoes.Visualizar" ||
                permissao.Codigo == "Configuracoes.Editar"),
            await context.Permissoes.CountAsync(permissao => permissao.Codigo == "OrdemServico.Editar"),
            administrador.Permissoes.Count(permissao => permissao.Codigo == "OrdemServico.Editar"),
            await context.Permissoes.CountAsync(permissao => permissao.Codigo.StartsWith("Financeiro.")),
            administrador.Permissoes.Count(permissao => permissao.Codigo.StartsWith("Financeiro.")),
            await context.Permissoes.CountAsync(permissao => permissao.Codigo == "Notificacoes.Reenviar"),
            administrador.Permissoes.Count(permissao => permissao.Codigo == "Notificacoes.Reenviar"),
            await context.Usuarios.CountAsync(usuario => usuario.Email == "admin@seed.local"));
    }

    private sealed record EstadoSeed(
        int TotalPermissoes,
        int TotalCodigosDistintos,
        int PermissoesConfiguracao,
        int PermissoesConfiguracaoDoAdministrador,
        int PermissoesOrdemServicoEditar,
        int PermissoesOrdemServicoEditarDoAdministrador,
        int PermissoesFinanceiro,
        int PermissoesFinanceiroDoAdministrador,
        int PermissoesNotificacoes,
        int PermissoesNotificacoesDoAdministrador,
        int UsuariosAdministradores);

    private sealed class UsuarioContextoTeste(Guid empresaId, bool estaAutenticado = true)
        : IUsuarioContexto
    {
        public static UsuarioContextoTeste Anonimo { get; } = new(Guid.Empty, false);
        public Guid UsuarioId { get; } = estaAutenticado ? Guid.NewGuid() : Guid.Empty;
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado { get; } = estaAutenticado;
    }
}
