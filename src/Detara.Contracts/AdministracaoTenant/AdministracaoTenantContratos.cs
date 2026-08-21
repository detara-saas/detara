namespace Detara.Contracts.AdministracaoTenant;

public sealed record EmpresaTenantResponse(
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

public sealed record AtualizarEmpresaTenantRequest(
    string NomeFantasia,
    string RazaoSocial,
    string CpfCnpj,
    string? Email,
    string? Telefone,
    string FusoHorario,
    long Versao);

public sealed record UsuarioTenantListaResponse(
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

public sealed record UsuarioTenantDetalheResponse(
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

public sealed record ConvidarUsuarioTenantRequest(string Nome, string Email, Guid PerfilId);
public sealed record AlterarPerfilUsuarioTenantRequest(Guid PerfilId, long Versao);
public sealed record AlterarStatusUsuarioTenantRequest(long Versao);

public sealed record PerfilTenantResumoResponse(
    Guid Id,
    string Nome,
    string? Descricao,
    bool EhAtivo,
    bool EhSistema,
    int QuantidadeUsuarios,
    int QuantidadePermissoes,
    long Versao);

public sealed record PerfilTenantDetalheResponse(
    Guid Id,
    string Nome,
    string? Descricao,
    bool EhAtivo,
    bool EhSistema,
    int QuantidadeUsuarios,
    IReadOnlyCollection<string> Permissoes,
    long Versao);

public sealed record PermissaoTenantResponse(
    string Codigo,
    string Descricao,
    string Grupo,
    bool PodeConceder);

public sealed record SalvarPerfilTenantRequest(
    string Nome,
    string? Descricao,
    IReadOnlyCollection<string> Permissoes,
    long? Versao = null);

public sealed record AlterarStatusPerfilTenantRequest(long Versao);

public sealed record MinhaContaResponse(
    string Nome,
    string Email,
    string EmpresaNome,
    string PerfilNome,
    long Versao);

public sealed record AtualizarNomeMinhaContaRequest(string Nome, long Versao);
public sealed record AtualizarEmailMinhaContaRequest(string NovoEmail, string SenhaAtual, long Versao);
public sealed record AlterarSenhaMinhaContaRequest(
    string SenhaAtual,
    string NovaSenha,
    string ConfirmacaoNovaSenha,
    long Versao);
