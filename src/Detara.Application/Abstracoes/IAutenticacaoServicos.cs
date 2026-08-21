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
    long PerfilAtualizadoEmTicks);

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
