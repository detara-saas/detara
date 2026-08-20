namespace Detara.Application.Plataforma;

public interface IContextoAdministradorPlataforma
{
    Guid AdministradorPlataformaId { get; }
    bool EstaAutenticado { get; }
}

public sealed record InicioAutenticacaoPlataformaResultado(
    string Desafio,
    DateTime ExpiraEmUtc,
    bool MfaConfigurado);

public sealed record ConfiguracaoMfaPlataformaResultado(
    string ChaveManual,
    string OtpAuthUri,
    string QrCodeSvgDataUrl);

public sealed record IdentidadeAdministradorPlataformaResultado(
    Guid Id,
    string Nome,
    string Email,
    long VersaoSeguranca);

public sealed record AutenticacaoMfaPlataformaResultado(
    IdentidadeAdministradorPlataformaResultado Identidade,
    IReadOnlyCollection<string> CodigosRecuperacao);

public sealed record TokenPlataformaGerado(string Valor, DateTime ExpiraEmUtc);

public interface ITokenPlataformaServico
{
    TokenPlataformaGerado Gerar(IdentidadeAdministradorPlataformaResultado identidade);
}

public interface IAutenticacaoPlataformaServico
{
    Task<InicioAutenticacaoPlataformaResultado> IniciarAsync(
        string email,
        string senha,
        CancellationToken cancellationToken);

    Task<ConfiguracaoMfaPlataformaResultado> ObterConfiguracaoMfaAsync(
        string desafio,
        CancellationToken cancellationToken);

    Task<AutenticacaoMfaPlataformaResultado> AtivarMfaAsync(
        string desafio,
        string codigo,
        string? traceId,
        CancellationToken cancellationToken);

    Task<AutenticacaoMfaPlataformaResultado> VerificarMfaAsync(
        string desafio,
        string codigo,
        string? traceId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> RegenerarCodigosRecuperacaoAsync(
        Guid administradorPlataformaId,
        string senhaAtual,
        string codigoTotp,
        string? traceId,
        CancellationToken cancellationToken);

    Task<bool> RevalidarAsync(
        Guid administradorPlataformaId,
        long versaoSeguranca,
        CancellationToken cancellationToken);
}

public sealed record ProvisionarEmpresaEntrada(
    string NomeFantasia,
    string RazaoSocial,
    string CpfCnpj,
    string? EmailContato,
    string? Telefone,
    string FusoHorario,
    string AdministradorNome,
    string AdministradorEmail);

public sealed record EmpresaPlataformaResumo(
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

public sealed record EmpresaPlataformaDetalhe(
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

public sealed record PaginaPlataforma<T>(
    IReadOnlyCollection<T> Itens,
    int Pagina,
    int TamanhoPagina,
    int TotalItens,
    int TotalPaginas);

public sealed record DashboardPlataformaResultado(
    int EmpresasAtivas,
    int EmpresasSuspensas,
    int ConvitesPendentes,
    int ConvitesComFalha);

public sealed record AuditoriaPlataformaItemResultado(
    Guid Id,
    string TipoAcao,
    Guid? EmpresaAlvoId,
    string? EmpresaNome,
    string? AdministradorNome,
    DateTime CriadoEmUtc,
    string? TraceId,
    string? DescricaoSegura);

public interface IAdministracaoPlataformaServico
{
    Task<DashboardPlataformaResultado> ObterDashboardAsync(CancellationToken cancellationToken);
    Task<PaginaPlataforma<EmpresaPlataformaResumo>> ListarEmpresasAsync(
        int pagina,
        int tamanhoPagina,
        string? pesquisa,
        bool? ativa,
        CancellationToken cancellationToken);
    Task<EmpresaPlataformaDetalhe> ObterEmpresaAsync(Guid id, CancellationToken cancellationToken);
    Task<EmpresaPlataformaDetalhe> ProvisionarEmpresaAsync(
        Guid administradorPlataformaId,
        ProvisionarEmpresaEntrada entrada,
        string? traceId,
        CancellationToken cancellationToken);
    Task SuspenderEmpresaAsync(
        Guid administradorPlataformaId,
        Guid empresaId,
        string motivo,
        string? traceId,
        CancellationToken cancellationToken);
    Task ReativarEmpresaAsync(
        Guid administradorPlataformaId,
        Guid empresaId,
        string motivo,
        string? traceId,
        CancellationToken cancellationToken);
    Task ReenviarConviteAsync(
        Guid administradorPlataformaId,
        Guid empresaId,
        string? traceId,
        CancellationToken cancellationToken);
    Task<PaginaPlataforma<AuditoriaPlataformaItemResultado>> ListarAuditoriaAsync(
        int pagina,
        int tamanhoPagina,
        DateTime? inicioUtc,
        DateTime? fimUtc,
        string? tipo,
        Guid? empresaId,
        CancellationToken cancellationToken);
}

public sealed record ConviteAdministradorValidadoResultado(
    string EmpresaNome,
    string EmailMascarado,
    DateTime ExpiraEmUtc);

public interface IConvitesAdministradoresEmpresaServico
{
    Task<ConviteAdministradorValidadoResultado> ValidarAsync(
        string token,
        CancellationToken cancellationToken);
    Task AceitarAsync(
        string token,
        string senha,
        string? traceId,
        CancellationToken cancellationToken);
}

public interface IFilaConvitesAdministradoresEmpresaServico
{
    Task<int> ProcessarLoteAsync(CancellationToken cancellationToken);
}

public sealed class CredenciaisPlataformaInvalidasException : Exception
{
    public CredenciaisPlataformaInvalidasException()
        : base("Não foi possível autenticar com as credenciais informadas.")
    {
    }
}

public sealed class CodigoMfaInvalidoException : Exception
{
    public CodigoMfaInvalidoException()
        : base("Código inválido ou expirado.")
    {
    }
}

public sealed class ConviteAdministradorInvalidoException : Exception
{
    public ConviteAdministradorInvalidoException()
        : base("O convite é inválido, expirou ou já foi utilizado.")
    {
    }
}
