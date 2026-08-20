using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Detara.Application.Plataforma;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Detara.Api.Autenticacao;

internal sealed class PlatformJwtTokenServico(IOptions<PlatformJwtOptions> options)
    : ITokenPlataformaServico
{
    private readonly PlatformJwtOptions _options = options.Value;

    public TokenPlataformaGerado Gerar(IdentidadeAdministradorPlataformaResultado identidade)
    {
        var agora = DateTime.UtcNow;
        var expiracao = agora.AddMinutes(_options.ExpiracaoMinutos);
        var claims = new Claim[]
        {
            new(JwtRegisteredClaimNames.Sub, identidade.Id.ToString()),
            new(JwtRegisteredClaimNames.Name, identidade.Nome),
            new(JwtRegisteredClaimNames.Email, identidade.Email),
            new("identidade", "platform_admin"),
            new("amr", "mfa"),
            new("versao_seguranca", identidade.VersaoSeguranca.ToString())
        };
        var credenciais = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.ChaveAssinatura)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _options.Emissor,
            _options.Audiencia,
            claims,
            agora,
            expiracao,
            credenciais);
        return new(new JwtSecurityTokenHandler().WriteToken(token), expiracao);
    }
}
