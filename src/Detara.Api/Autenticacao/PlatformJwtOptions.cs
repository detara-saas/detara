namespace Detara.Api.Autenticacao;

public sealed class PlatformJwtOptions
{
    public const string Secao = "PlatformJwt";
    public string Emissor { get; init; } = "Detara.Api";
    public string Audiencia { get; init; } = "detara-platform";
    public string ChaveAssinatura { get; init; } = string.Empty;
    public int ExpiracaoMinutos { get; init; } = 45;
}
