namespace Detara.Application.Autenticacao;

public sealed class SessaoAutenticacaoOptions
{
    public const string Secao = "SessaoAutenticacao";
    public const string CookieTenantPadrao = "detara.refresh";
    public const string CookiePlataformaPadrao = "detara.platform.refresh";

    public int DuracaoSessaoHoras { get; init; } = 12;
    public int DuracaoPersistenteDias { get; init; } = 30;
    public int DuracaoPlataformaHoras { get; init; } = 8;
    public int JanelaConcorrenciaSegundos { get; init; } = 30;
    public string CookieTenant { get; init; } = CookieTenantPadrao;
    public string CookiePlataforma { get; init; } = CookiePlataformaPadrao;
}
