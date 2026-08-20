namespace Detara.Infrastructure.Plataforma;

public sealed class PlataformaOptions
{
    public const string Secao = "Plataforma";
    public int DesafioMfaExpiracaoMinutos { get; init; } = 5;
    public int ConviteExpiracaoHoras { get; init; } = 72;
    public int ConvitesTamanhoLote { get; init; } = 20;
    public int ConvitesIntervaloSegundos { get; init; } = 15;
    public int ConvitesMaximoTentativas { get; init; } = 4;
}

public sealed class WebPublicaOptions
{
    public const string Secao = "Web";
    public string PublicBaseUrl { get; init; } = string.Empty;
}
