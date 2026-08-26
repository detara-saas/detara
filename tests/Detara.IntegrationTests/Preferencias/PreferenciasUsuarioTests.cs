using Detara.Application;
using Detara.Application.Abstracoes;
using Detara.Application.Preferencias;
using Detara.Domain.Entidades;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Detara.IntegrationTests.Preferencias;

public sealed class PreferenciasUsuarioTests
{
    [Fact]
    public async Task AtualizacaoUsaUsuarioETenantDoContextoAutenticado()
    {
        var contexto = new UsuarioContextoTeste();
        var repositorio = new RepositorioMemoria();
        using var provider = CriarServicos(contexto, repositorio);

        var resultado = await provider.GetRequiredService<ISender>().Send(
            new AtualizarPreferenciasUsuarioCommand(
                "Escuro",
                "pt-BR",
                true,
                "agenda",
                ["agenda", "clientes"]));

        Assert.Equal(contexto.UsuarioId, repositorio.UsuarioConsultado);
        Assert.Equal(contexto.UsuarioId, repositorio.Preferencia!.UsuarioId);
        Assert.Equal(contexto.EmpresaId, repositorio.Preferencia.EmpresaId);
        Assert.Equal("Escuro", resultado.Tema);
        Assert.Equal(["agenda", "clientes"], resultado.Favoritos);
    }

    [Fact]
    public async Task AtualizacaoRejeitaUrlArbitrariaComoFavorito()
    {
        using var provider = CriarServicos(new UsuarioContextoTeste(), new RepositorioMemoria());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.GetRequiredService<ISender>().Send(
                new AtualizarPreferenciasUsuarioCommand(
                    "Sistema",
                    "pt-BR",
                    false,
                    "dashboard",
                    ["https://exemplo.com"])));
    }

    [Fact]
    public async Task AtualizacaoPersisteModoEmpresaComoPaginaInicialDoDashboard()
    {
        using var provider = CriarServicos(new UsuarioContextoTeste(), new RepositorioMemoria());

        var resultado = await provider.GetRequiredService<ISender>().Send(
            new AtualizarPreferenciasUsuarioCommand(
                "Sistema",
                "pt-BR",
                false,
                PaginasDetara.DashboardEmpresa,
                ["dashboard"]));

        Assert.Equal(PaginasDetara.DashboardEmpresa, resultado.PaginaInicial);
    }

    [Fact]
    public async Task ModoEmpresaNaoPodeSerAdicionadoComoFavorito()
    {
        using var provider = CriarServicos(new UsuarioContextoTeste(), new RepositorioMemoria());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.GetRequiredService<ISender>().Send(
                new AtualizarPreferenciasUsuarioCommand(
                    "Sistema",
                    "pt-BR",
                    false,
                    PaginasDetara.Dashboard,
                    [PaginasDetara.DashboardEmpresa])));
    }

    private static ServiceProvider CriarServicos(
        IUsuarioContexto contexto,
        IPreferenciasUsuarioRepositorio repositorio)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AdicionarApplication();
        services.AddSingleton(contexto);
        services.AddSingleton(repositorio);
        return services.BuildServiceProvider();
    }

    private sealed class UsuarioContextoTeste : IUsuarioContexto
    {
        public Guid UsuarioId { get; } = Guid.NewGuid();
        public Guid EmpresaId { get; } = Guid.NewGuid();
        public bool EstaAutenticado => true;
    }

    private sealed class RepositorioMemoria : IPreferenciasUsuarioRepositorio
    {
        private readonly List<UsuarioPaginaFavorita> _favoritos = [];
        public Guid UsuarioConsultado { get; private set; }
        public UsuarioPreferencia? Preferencia { get; private set; }

        public Task<UsuarioPreferencia?> ObterAsync(Guid usuarioId, CancellationToken cancellationToken)
        {
            UsuarioConsultado = usuarioId;
            return Task.FromResult(Preferencia);
        }

        public Task<IReadOnlyCollection<UsuarioPaginaFavorita>> ObterFavoritosAsync(
            Guid usuarioPreferenciaId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<UsuarioPaginaFavorita>>(_favoritos.ToArray());

        public void Adicionar(UsuarioPreferencia preferencia) => Preferencia = preferencia;

        public void SubstituirFavoritos(
            UsuarioPreferencia preferencia,
            IReadOnlyCollection<UsuarioPaginaFavorita> atuais,
            IReadOnlyCollection<string> paginas)
        {
            _favoritos.Clear();
            _favoritos.AddRange(paginas.Select((pagina, ordem) =>
                new UsuarioPaginaFavorita(preferencia.EmpresaId, preferencia.Id, pagina, ordem)));
        }

        public Task SalvarAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
