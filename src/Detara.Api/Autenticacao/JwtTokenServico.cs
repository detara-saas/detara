using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Detara.Api.Autenticacao;

internal sealed class JwtTokenServico(IOptions<JwtOptions> options) : ITokenServico
{
    private readonly JwtOptions _options = options.Value;

    public TokenGerado Gerar(Usuario usuario)
    {
        var agora = DateTime.UtcNow;
        var expiracao = agora.AddMinutes(_options.ExpiracaoMinutos);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Name, usuario.Nome),
            new(JwtRegisteredClaimNames.Email, usuario.Email),
            new("empresa_id", usuario.EmpresaId.ToString()),
            new("perfil_id", usuario.PerfilId.ToString()),
            new("usuario_atualizado_ticks", (usuario.AtualizadoEmUtc?.Ticks ?? 0).ToString()),
            new("empresa_versao_seguranca", usuario.Empresa.VersaoSeguranca.ToString()),
            new("perfil", usuario.Perfil.Nome)
        };

        claims.AddRange(usuario.Perfil.Permissoes.Where(permissao => permissao.EhAtivo).Select(
            permissao => new Claim("permissao", permissao.Codigo)));

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
