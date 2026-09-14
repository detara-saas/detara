using Detara.Api.Autenticacao;
using Detara.Application.AdministracaoTenant;
using Detara.Contracts.AdministracaoTenant;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Comum;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[ApiController]
[Route("api/empresa")]
[Authorize(AuthenticationSchemes = EsquemasAutenticacao.Tenant)]
public sealed class EmpresaTenantController(ISender sender, ILogoEmpresaTenantServico logos) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissoes.ConfiguracoesVisualizar)]
    public async Task<ActionResult<RespostaApi<EmpresaTenantResponse>>> Obter(
        CancellationToken cancellationToken) =>
        Ok(RespostaApi<EmpresaTenantResponse>.Ok(Mapear(
            await sender.Send(new ObterEmpresaTenantQuery(), cancellationToken))));

    [HttpPut]
    [Authorize(Policy = Permissoes.ConfiguracoesEditar)]
    public async Task<ActionResult<RespostaApi<EmpresaTenantResponse>>> Atualizar(
        AtualizarEmpresaTenantRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(new AtualizarEmpresaTenantCommand(
            request.NomeFantasia,
            request.RazaoSocial,
            request.CpfCnpj,
            request.Email,
            request.Telefone,
            request.FusoHorario,
            request.Versao), cancellationToken);
        return Ok(RespostaApi<EmpresaTenantResponse>.Ok(
            Mapear(resultado),
            "Dados da empresa atualizados."));
    }

    [HttpPut("logo")]
    [Authorize(Policy = Permissoes.ConfiguracoesEditar)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2 * 1024 * 1024 + 64 * 1024)]
    public async Task<ActionResult<RespostaApi<LogoEmpresaResponse>>> SalvarLogo(
        [FromForm] IFormFile arquivo, CancellationToken cancellationToken)
    {
        await using var conteudo = arquivo.OpenReadStream();
        var resultado = await logos.SalvarAsync(conteudo, arquivo.ContentType, arquivo.Length, cancellationToken);
        return Ok(RespostaApi<LogoEmpresaResponse>.Ok(MapearLogo(resultado), "Logo atualizada."));
    }

    [HttpGet("logo")]
    [Authorize(Policy = Permissoes.ConfiguracoesVisualizar)]
    public async Task<IActionResult> ObterLogo(CancellationToken cancellationToken)
    {
        var arquivo = await logos.AbrirAsync(cancellationToken);
        if (arquivo is null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        Response.Headers.ETag = $"\"logo-{arquivo.Versao}\"";
        return File(arquivo.Conteudo, arquivo.ContentType);
    }

    [HttpGet("logo-publica/{token:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> ObterLogoPublica(Guid token, CancellationToken cancellationToken)
    {
        var arquivo = await logos.AbrirPublicaAsync(token, cancellationToken);
        if (arquivo is null) return NotFound();
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        Response.Headers.ETag = $"\"logo-{arquivo.Versao}\"";
        return File(arquivo.Conteudo, arquivo.ContentType);
    }

    [HttpDelete("logo")]
    [Authorize(Policy = Permissoes.ConfiguracoesEditar)]
    public async Task<ActionResult<RespostaApi<LogoEmpresaResponse>>> RemoverLogo(CancellationToken cancellationToken) =>
        Ok(RespostaApi<LogoEmpresaResponse>.Ok(MapearLogo(await logos.RemoverAsync(cancellationToken)), "Logo removida."));

    private EmpresaTenantResponse Mapear(EmpresaTenantResultado resultado) => new(
        resultado.NomeFantasia,
        resultado.RazaoSocial,
        resultado.CpfCnpj,
        resultado.Email,
        resultado.Telefone,
        resultado.Slug,
        resultado.FusoHorario,
        resultado.EhAtiva,
        resultado.CriadoEmUtc,
        resultado.Versao,
        MapearLogo(resultado.Logo));

    private LogoEmpresaResponse MapearLogo(LogoEmpresaResultado resultado) => new(
        resultado.PossuiLogo,
        resultado.PossuiLogo && resultado.TokenPublico.HasValue
            ? Url.ActionLink(nameof(ObterLogoPublica), values: new { token = resultado.TokenPublico.Value })
            : null,
        resultado.Versao,
        resultado.AtualizadaEmUtc);
}
