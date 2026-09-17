using Detara.Api.Autenticacao;
using Detara.Application.Plataforma;
using Detara.Application.Abstracoes;
using Detara.Application.Autenticacao;
using Detara.Contracts.Comum;
using Detara.Contracts.Plataforma;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Detara.Api.Controllers;

[ApiController]
[Route("api/plataforma/autenticacao")]
public sealed class PlataformaAutenticacaoController(
    ISender sender,
    RefreshCookieServico refreshCookie) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("platform-login")]
    [HttpPost("login")]
    public async Task<ActionResult<RespostaApi<DesafioMfaPlataformaResponse>>> Login(
        LoginPlataformaRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(
            new IniciarAutenticacaoPlataformaCommand(request.Email, request.Senha),
            cancellationToken);
        return Ok(RespostaApi<DesafioMfaPlataformaResponse>.Ok(new(
            resultado.Desafio,
            resultado.ExpiraEmUtc,
            resultado.MfaConfigurado),
            "Credenciais validadas. Conclua a autenticação multifator."));
    }

    [AllowAnonymous]
    [EnableRateLimiting("platform-mfa")]
    [HttpPost("mfa/configuracao")]
    public async Task<ActionResult<RespostaApi<ConfiguracaoMfaPlataformaResponse>>> ConfiguracaoMfa(
        DesafioMfaRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(
            new ObterConfiguracaoMfaPlataformaQuery(request.Desafio),
            cancellationToken);
        return Ok(RespostaApi<ConfiguracaoMfaPlataformaResponse>.Ok(new(
            resultado.ChaveManual,
            resultado.OtpAuthUri,
            resultado.QrCodeSvgDataUrl)));
    }

    [AllowAnonymous]
    [EnableRateLimiting("platform-mfa")]
    [HttpPost("mfa/ativar")]
    public async Task<ActionResult<RespostaApi<SessaoPlataformaResponse>>> AtivarMfa(
        VerificarMfaPlataformaRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(new AtivarMfaPlataformaCommand(
            request.Desafio,
            request.Codigo,
            HttpContext.TraceIdentifier), cancellationToken);
        refreshCookie.EscreverPlataforma(Response, resultado.RefreshToken);
        return Ok(RespostaApi<SessaoPlataformaResponse>.Ok(MapearSessao(resultado),
            "MFA configurado. Salve os códigos de recuperação agora."));
    }

    [AllowAnonymous]
    [EnableRateLimiting("platform-mfa")]
    [HttpPost("mfa/verificar")]
    public async Task<ActionResult<RespostaApi<SessaoPlataformaResponse>>> VerificarMfa(
        VerificarMfaPlataformaRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(new VerificarMfaPlataformaCommand(
            request.Desafio,
            request.Codigo,
            HttpContext.TraceIdentifier), cancellationToken);
        refreshCookie.EscreverPlataforma(Response, resultado.RefreshToken);
        return Ok(RespostaApi<SessaoPlataformaResponse>.Ok(
            MapearSessao(resultado),
            "Autenticação administrativa concluída."));
    }

    [AllowAnonymous]
    [EnableRateLimiting("refresh")]
    [HttpPost("refresh")]
    public async Task<ActionResult<RespostaApi<SessaoPlataformaResponse>>> Refresh(
        CancellationToken cancellationToken)
    {
        ValidarOrigem();
        var token = refreshCookie.ObterPlataforma(Request)
            ?? throw new SessaoAutenticacaoInvalidaException();
        var resultado = await sender.Send(
            new RenovarSessaoPlataformaCommand(token),
            cancellationToken);
        refreshCookie.EscreverPlataforma(Response, resultado.RefreshToken);
        return Ok(RespostaApi<SessaoPlataformaResponse>.Ok(
            MapearSessao(resultado),
            "Sessão administrativa renovada."));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<ActionResult> Logout(CancellationToken cancellationToken)
    {
        ValidarOrigem();
        await sender.Send(
            new EncerrarSessaoPlataformaCommand(refreshCookie.ObterPlataforma(Request)),
            cancellationToken);
        refreshCookie.LimparPlataforma(Response);
        return NoContent();
    }

    [Authorize(Policy = EsquemasAutenticacao.PolicyAdministradorPlataforma)]
    [HttpPost("recovery-codes/regenerar")]
    public async Task<ActionResult<RespostaApi<CodigosRecuperacaoResponse>>> RegenerarCodigos(
        RegenerarCodigosRecuperacaoRequest request,
        CancellationToken cancellationToken)
    {
        var codigos = await sender.Send(new RegenerarCodigosRecuperacaoPlataformaCommand(
            request.SenhaAtual,
            request.CodigoTotp,
            HttpContext.TraceIdentifier), cancellationToken);
        return Ok(RespostaApi<CodigosRecuperacaoResponse>.Ok(
            new(codigos),
            "Novos códigos gerados. Os anteriores foram invalidados."));
    }

    private static SessaoPlataformaResponse MapearSessao(SessaoPlataformaResultado resultado) => new(
        resultado.Token,
        resultado.ExpiraEmUtc,
        resultado.AdministradorId,
        resultado.Nome,
        resultado.Email,
        resultado.CodigosRecuperacao);

    private void ValidarOrigem()
    {
        if (!refreshCookie.OrigemPermitida(Request))
        {
            throw new OrigemAutenticacaoInvalidaException();
        }
    }
}
