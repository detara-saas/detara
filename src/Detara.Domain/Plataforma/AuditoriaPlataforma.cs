using Detara.Domain.Entidades;

namespace Detara.Domain.Plataforma;

public static class AcoesAuditoriaPlataforma
{
    public const string PlatformAdminBootstrapCriado = nameof(PlatformAdminBootstrapCriado);
    public const string PlatformAdminSenhaResetada = nameof(PlatformAdminSenhaResetada);
    public const string PlatformAdminMfaResetado = nameof(PlatformAdminMfaResetado);
    public const string MfaConfigurado = nameof(MfaConfigurado);
    public const string RecoveryCodesRegenerados = nameof(RecoveryCodesRegenerados);
    public const string EmpresaProvisionada = nameof(EmpresaProvisionada);
    public const string EmpresaSuspensa = nameof(EmpresaSuspensa);
    public const string EmpresaReativada = nameof(EmpresaReativada);
    public const string ConviteReenviado = nameof(ConviteReenviado);
    public const string ConviteAceito = nameof(ConviteAceito);
    public const string LoginRealizado = nameof(LoginRealizado);
}

public sealed class AuditoriaPlataforma : EntidadeBase
{
    private AuditoriaPlataforma()
    {
    }

    public AuditoriaPlataforma(
        Guid? administradorPlataformaId,
        string tipoAcao,
        Guid? empresaAlvoId,
        Guid? entidadeAlvoId,
        string? traceId,
        string? descricaoSegura)
        : base(Guid.NewGuid())
    {
        AdministradorPlataformaId = administradorPlataformaId;
        TipoAcao = Limitar(tipoAcao, 120, nameof(tipoAcao))!;
        EmpresaAlvoId = empresaAlvoId;
        EntidadeAlvoId = entidadeAlvoId;
        TraceId = Limitar(traceId, 160, nameof(traceId));
        DescricaoSegura = Limitar(descricaoSegura, 500, nameof(descricaoSegura));
    }

    public Guid? AdministradorPlataformaId { get; private set; }
    public string TipoAcao { get; private set; } = string.Empty;
    public Guid? EmpresaAlvoId { get; private set; }
    public Guid? EntidadeAlvoId { get; private set; }
    public string? TraceId { get; private set; }
    public string? DescricaoSegura { get; private set; }

    private static string? Limitar(string? valor, int maximo, string parametro)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var texto = valor.Trim();
        return texto.Length <= maximo
            ? texto
            : throw new ArgumentException($"O valor deve possuir no máximo {maximo} caracteres.", parametro);
    }
}
