using Detara.Domain.Entidades;

namespace Detara.Application.Abstracoes;

public interface IConsultaIdentidadeLoginTenant
{
    Task<IReadOnlyCollection<CandidatoLoginTenant>> ObterCandidatosPorEmailAsync(
        string email,
        CancellationToken cancellationToken);

    Task<CandidatoLoginTenant?> ObterMembershipAsync(
        Guid usuarioId,
        Guid empresaId,
        CancellationToken cancellationToken);
}

public sealed record CandidatoLoginTenant(
    Usuario Usuario,
    EmpresaLoginTenant Empresa,
    PerfilLoginTenant Perfil);

public sealed record EmpresaLoginTenant(
    Guid Id,
    string NomeExibicao,
    bool EhAtiva,
    long VersaoSeguranca);

public sealed record PerfilLoginTenant(
    Guid Id,
    string Nome,
    bool EhAtivo,
    long AtualizadoEmTicks,
    IReadOnlyCollection<string> PermissoesAtivas);

public interface IChallengeSelecaoEmpresaTenant
{
    ChallengeSelecaoEmpresaCriado Criar(
        IReadOnlyCollection<MembershipLoginTenantAutorizada> memberships);

    IReadOnlyCollection<MembershipLoginTenantAutorizada> Validar(string challenge);
}

public sealed record MembershipLoginTenantAutorizada(
    Guid UsuarioId,
    Guid EmpresaId,
    long UsuarioVersaoSeguranca,
    long EmpresaVersaoSeguranca,
    long PerfilAtualizadoEmTicks,
    bool ManterConectado = false);

public sealed record ChallengeSelecaoEmpresaCriado(string Valor, DateTime ExpiraEmUtc);

public interface ISenhaServico
{
    string GerarHash(Usuario usuario, string senha);
    bool Verificar(Usuario usuario, string senhaHash, string senha);
    void VerificarContraHashFicticio(string senha);
}

public interface IValidadorIdentidadeAutenticada
{
    Task<bool> EhValidaAsync(
        IdentidadeToken identidade,
        CancellationToken cancellationToken);
}

public sealed record IdentidadeToken(
    Guid UsuarioId,
    Guid EmpresaId,
    Guid PerfilId,
    long UsuarioVersaoSeguranca,
    long EmpresaVersaoSeguranca,
    IReadOnlyCollection<string> Permissoes);

public interface ITokenServico
{
    TokenGerado Gerar(CandidatoLoginTenant candidato);
}

public sealed record TokenGerado(string Valor, DateTime ExpiraEmUtc);

public sealed record RefreshTokenCriado(
    string Valor,
    DateTime ExpiraEmUtc,
    bool Persistente);

public sealed record SessaoTenantRenovada(
    CandidatoLoginTenant Candidato,
    RefreshTokenCriado RefreshToken);

public sealed record SessaoPlataformaRenovada(
    Guid AdministradorId,
    string Nome,
    string Email,
    long VersaoSeguranca,
    RefreshTokenCriado RefreshToken);

public interface ISessoesAutenticacaoServico
{
    Task<RefreshTokenCriado> CriarTenantAsync(
        Guid usuarioId,
        Guid empresaId,
        bool persistente,
        CancellationToken cancellationToken);

    Task<SessaoTenantRenovada> RenovarTenantAsync(
        string refreshToken,
        CancellationToken cancellationToken);

    Task<RefreshTokenCriado> CriarPlataformaAsync(
        Guid administradorId,
        CancellationToken cancellationToken);

    Task<SessaoPlataformaRenovada> RenovarPlataformaAsync(
        string refreshToken,
        CancellationToken cancellationToken);

    Task RevogarTenantAsync(string? refreshToken, CancellationToken cancellationToken);
    Task RevogarPlataformaAsync(string? refreshToken, CancellationToken cancellationToken);
    Task RevogarTodasDoUsuarioAsync(Guid usuarioId, string motivo, CancellationToken cancellationToken);
}

public sealed class SessaoAutenticacaoInvalidaException : Exception
{
    public SessaoAutenticacaoInvalidaException()
        : base("A sessão expirou ou não é mais válida.")
    {
    }
}
