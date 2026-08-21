using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Detara.Application.Abstracoes;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Detara.Api.Autenticacao;

internal sealed class JwtTokenServico(IOptions<JwtOptions> options) : ITokenServico
{
    private readonly JwtOptions _options = options.Value;

    public TokenGerado Gerar(CandidatoLoginTenant candidato)
    {
        var usuario = candidato.Usuario;
        var agora = DateTime.UtcNow;
        var expiracao = agora.AddMinutes(_options.ExpiracaoMinutos);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Name, usuario.Nome),
            new(JwtRegisteredClaimNames.Email, usuario.Email),
            new("empresa_id", candidato.Empresa.Id.ToString()),
            new("perfil_id", candidato.Perfil.Id.ToString()),
            new("usuario_versao_seguranca", usuario.VersaoSeguranca.ToString()),
            new("empresa_versao_seguranca", candidato.Empresa.VersaoSeguranca.ToString()),
            new("perfil", candidato.Perfil.Nome)
        };

        claims.AddRange(candidato.Perfil.PermissoesAtivas.Select(
            permissao => new Claim("permissao", permissao)));

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

        return new TokenGerado(new JwtSecurityTokenHandler().WriteToken(token), expiracao);
    }
}
