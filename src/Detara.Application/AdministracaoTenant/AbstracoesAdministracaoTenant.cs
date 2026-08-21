namespace Detara.Application.AdministracaoTenant;

public sealed record PaginaTenant<T>(
    IReadOnlyCollection<T> Itens,
    int Pagina,
    int TamanhoPagina,
    int TotalItens,
    int TotalPaginas);

public sealed record EmpresaTenantResultado(
    string NomeFantasia,
    string RazaoSocial,
    string CpfCnpj,
    string? Email,
    string? Telefone,
    string Slug,
    string FusoHorario,
    bool EhAtiva,
    DateTime CriadoEmUtc,
    long Versao);

public sealed record UsuarioTenantResultado(
    Guid Id,
    string Nome,
    string Email,
    Guid PerfilId,
    string PerfilNome,
    string Status,
    DateTime? ConviteExpiraEmUtc,
    bool PodeReenviarConvite,
    bool EhUsuarioAtual,
    long Versao);

public sealed record PerfilTenantResumoResultado(
    Guid Id,
    string Nome,
    string? Descricao,
    bool EhAtivo,
    bool EhSistema,
    int QuantidadeUsuarios,
    int QuantidadePermissoes,
    long Versao);

public sealed record PerfilTenantDetalheResultado(
    Guid Id,
    string Nome,
    string? Descricao,
    bool EhAtivo,
    bool EhSistema,
    int QuantidadeUsuarios,
    IReadOnlyCollection<string> Permissoes,
    long Versao);

public sealed record PermissaoTenantResultado(
    string Codigo,
    string Descricao,
    string Grupo,
    bool PodeConceder);

public sealed record MinhaContaResultado(
    string Nome,
    string Email,
    string EmpresaNome,
    string PerfilNome,
    long Versao);

public interface IAdministracaoEmpresaTenantServico
{
    Task<EmpresaTenantResultado> ObterAsync(CancellationToken cancellationToken);
    Task<EmpresaTenantResultado> AtualizarAsync(
        string nomeFantasia,
        string razaoSocial,
        string cpfCnpj,
        string? email,
        string? telefone,
        string fusoHorario,
        long versao,
        CancellationToken cancellationToken);
}

public interface IAdministracaoUsuariosTenantServico
{
    Task<PaginaTenant<UsuarioTenantResultado>> ListarAsync(
        int pagina,
        int tamanhoPagina,
        string? pesquisa,
        string? status,
        CancellationToken cancellationToken);
    Task<UsuarioTenantResultado> ObterAsync(Guid id, CancellationToken cancellationToken);
    Task<UsuarioTenantResultado> ConvidarAsync(
        string nome,
        string email,
        Guid perfilId,
        CancellationToken cancellationToken);
    Task<UsuarioTenantResultado> AlterarPerfilAsync(
        Guid id,
        Guid perfilId,
        long versao,
        CancellationToken cancellationToken);
    Task<UsuarioTenantResultado> AlterarStatusAsync(
        Guid id,
        bool ativar,
        long versao,
        CancellationToken cancellationToken);
    Task<UsuarioTenantResultado> ReenviarConviteAsync(
        Guid id,
        CancellationToken cancellationToken);
}

public interface IAdministracaoPerfisTenantServico
{
    Task<IReadOnlyCollection<PerfilTenantResumoResultado>> ListarAsync(CancellationToken cancellationToken);
    Task<PerfilTenantDetalheResultado> ObterAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PermissaoTenantResultado>> ListarPermissoesAsync(
        CancellationToken cancellationToken);
    Task<PerfilTenantDetalheResultado> CriarAsync(
        string nome,
        string? descricao,
        IReadOnlyCollection<string> permissoes,
        CancellationToken cancellationToken);
    Task<PerfilTenantDetalheResultado> AtualizarAsync(
        Guid id,
        string nome,
        string? descricao,
        IReadOnlyCollection<string> permissoes,
        long versao,
        CancellationToken cancellationToken);
    Task<PerfilTenantDetalheResultado> AlterarStatusAsync(
        Guid id,
        bool ativar,
        long versao,
        CancellationToken cancellationToken);
}

public interface IMinhaContaTenantServico
{
    Task<MinhaContaResultado> ObterAsync(CancellationToken cancellationToken);
    Task<MinhaContaResultado> AtualizarNomeAsync(
        string nome,
        long versao,
        CancellationToken cancellationToken);
    Task AtualizarEmailAsync(
        string novoEmail,
        string senhaAtual,
        long versao,
        CancellationToken cancellationToken);
    Task AlterarSenhaAsync(
        string senhaAtual,
        string novaSenha,
        long versao,
        CancellationToken cancellationToken);
}
