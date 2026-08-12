namespace Detara.Api.Autenticacao;

public sealed class JwtOptions
{
    public const string Secao = "Jwt";

    public string Emissor { get; init; } = "Detara.Api";
    public string Audiencia { get; init; } = "Detara.Web";
    public string ChaveAssinatura { get; init; } = string.Empty;
    public int ExpiracaoMinutos { get; init; } = 480;
}
