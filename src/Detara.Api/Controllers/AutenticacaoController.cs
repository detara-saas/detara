using Detara.Application.Autenticacao;
using Detara.Contracts.Autenticacao;
using Detara.Contracts.Comum;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Detara.Api.Controllers;

[ApiController]
[Route("api/autenticacao")]
public sealed class AutenticacaoController(ISender sender) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    [ProducesResponseType(typeof(RespostaApi<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RespostaApi<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(RespostaApi<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(RespostaApi<object>), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<RespostaApi<LoginResponse>>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(
            new AutenticarCommand(request.Email, request.Senha),
            cancellationToken);
        var response = Mapear(resultado);

        var mensagem = resultado is SelecaoEmpresaNecessariaResultado
            ? "Escolha uma empresa para continuar."
            : "Login realizado com sucesso.";
        return Ok(RespostaApi<LoginResponse>.Ok(response, mensagem));
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("selecionar-empresa")]
    [ProducesResponseType(typeof(RespostaApi<LoginAutenticadoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RespostaApi<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(RespostaApi<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(RespostaApi<object>), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<RespostaApi<LoginAutenticadoResponse>>> SelecionarEmpresa(
        SelecionarEmpresaRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(
            new SelecionarEmpresaCommand(request.Challenge, request.EmpresaId),
            cancellationToken);
        var response = MapearSessao(resultado);

        return Ok(RespostaApi<LoginAutenticadoResponse>.Ok(
            response,
            "Empresa selecionada com sucesso."));
    }

    private static LoginResponse Mapear(ResultadoAutenticacao resultado) => resultado switch
    {
        SessaoTenantResultado sessao => MapearSessao(sessao),
        SelecaoEmpresaNecessariaResultado selecao => new SelecaoEmpresaNecessariaResponse(
            selecao.Challenge,
            selecao.ExpiraEmUtc,
            selecao.Empresas
                .Select(empresa => new EmpresaSelecaoResponse(
                    empresa.EmpresaId,
                    empresa.NomeExibicao))
                .ToArray()),
        _ => throw new InvalidOperationException("Resultado de autenticação desconhecido.")
    };

    private static LoginAutenticadoResponse MapearSessao(SessaoTenantResultado resultado) => new(
        resultado.Token,
        resultado.ExpiraEmUtc,
        resultado.UsuarioId,
        resultado.EmpresaId,
        resultado.Nome,
        resultado.Perfil,
        resultado.Permissoes);
}
