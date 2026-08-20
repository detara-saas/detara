namespace Detara.Contracts.Plataforma;

public sealed record LoginPlataformaRequest(string Email, string Senha);
public sealed record DesafioMfaPlataformaResponse(
    string Desafio,
    DateTime ExpiraEmUtc,
    bool MfaConfigurado);
public sealed record DesafioMfaRequest(string Desafio);
public sealed record VerificarMfaPlataformaRequest(string Desafio, string Codigo);
public sealed record ConfiguracaoMfaPlataformaResponse(
    string ChaveManual,
    string OtpAuthUri,
    string QrCodeSvgDataUrl);
public sealed record SessaoPlataformaResponse(
    string Token,
    DateTime ExpiraEmUtc,
    Guid AdministradorId,
    string Nome,
    string Email,
    IReadOnlyCollection<string> CodigosRecuperacao);
public sealed record RegenerarCodigosRecuperacaoRequest(string SenhaAtual, string CodigoTotp);
public sealed record CodigosRecuperacaoResponse(IReadOnlyCollection<string> Codigos);

public sealed record DashboardPlataformaResponse(
    int EmpresasAtivas,
    int EmpresasSuspensas,
    int ConvitesPendentes,
    int ConvitesComFalha);

public sealed record ProvisionarEmpresaRequest(
    string NomeFantasia,
    string RazaoSocial,
    string CpfCnpj,
    string? EmailContato,
    string? Telefone,
    string FusoHorario,
    string AdministradorNome,
    string AdministradorEmail);

public sealed record AlterarStatusEmpresaPlataformaRequest(string Motivo);

public sealed record EmpresaPlataformaResumoResponse(
    Guid Id,
    string NomeFantasia,
    string RazaoSocial,
    string CpfCnpj,
    string Slug,
    bool EhAtivo,
    string AdministradorNome,
    string AdministradorEmail,
    string StatusConvite,
    DateTime CriadoEmUtc);

public sealed record EmpresaPlataformaDetalheResponse(
    Guid Id,
    string NomeFantasia,
    string RazaoSocial,
    string CpfCnpj,
    string? Email,
    string? Telefone,
    string Slug,
    string FusoHorario,
    bool EhAtivo,
    DateTime CriadoEmUtc,
    Guid AdministradorUsuarioId,
    string AdministradorNome,
    string AdministradorEmail,
    bool AdministradorAtivo,
    Guid ConviteId,
    string StatusConvite,
    DateTime? ConviteExpiraEmUtc,
    int TentativasEnvio,
    string? UltimoErroEnvioSeguro);

public sealed record AuditoriaPlataformaItemResponse(
    Guid Id,
    string TipoAcao,
    Guid? EmpresaAlvoId,
    string? EmpresaNome,
    string? AdministradorNome,
    DateTime CriadoEmUtc,
    string? TraceId,
    string? DescricaoSegura);

public sealed record ValidarConviteAdministradorRequest(string Token);
public sealed record ConviteAdministradorValidadoResponse(
    string EmpresaNome,
    string EmailMascarado,
    DateTime ExpiraEmUtc);
public sealed record AceitarConviteAdministradorRequest(string Token, string Senha);
