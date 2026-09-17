using Detara.Application.Abstracoes;

namespace Detara.IntegrationTests.Support;

internal sealed class SessoesAutenticacaoTeste : ISessoesAutenticacaoServico
{
    public List<(Guid UsuarioId, string Motivo)> RevogacoesUsuario { get; } = [];
    public List<(Guid UsuarioId, Guid EmpresaId, bool Persistente)> CriacoesTenant { get; } = [];

    public Task<RefreshTokenCriado> CriarTenantAsync(
        Guid usuarioId,
        Guid empresaId,
        bool persistente,
        CancellationToken cancellationToken)
    {
        CriacoesTenant.Add((usuarioId, empresaId, persistente));
        return Task.FromResult(new RefreshTokenCriado(
            "refresh-teste",
            DateTime.UtcNow.AddHours(1),
            persistente));
    }

    public Task<SessaoTenantRenovada> RenovarTenantAsync(
        string refreshToken,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<RefreshTokenCriado> CriarPlataformaAsync(
        Guid administradorId,
        CancellationToken cancellationToken) =>
        Task.FromResult(new RefreshTokenCriado(
            "refresh-platform-teste",
            DateTime.UtcNow.AddHours(1),
            false));

    public Task<SessaoPlataformaRenovada> RenovarPlataformaAsync(
        string refreshToken,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task RevogarTenantAsync(string? refreshToken, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task RevogarPlataformaAsync(string? refreshToken, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task RevogarTodasDoUsuarioAsync(
        Guid usuarioId,
        string motivo,
        CancellationToken cancellationToken)
    {
        RevogacoesUsuario.Add((usuarioId, motivo));
        return Task.CompletedTask;
    }
}
