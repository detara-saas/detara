using Detara.Api.Autenticacao;
using Detara.Application.Plataforma;
using Detara.Contracts.Comum;
using Detara.Contracts.Plataforma;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[ApiController]
[Authorize(Policy = EsquemasAutenticacao.PolicyAdministradorPlataforma)]
[Route("api/plataforma")]
public sealed class PlataformaController(ISender sender) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<RespostaApi<DashboardPlataformaResponse>>> Dashboard(
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(new ObterDashboardPlataformaQuery(), cancellationToken);
        return Ok(RespostaApi<DashboardPlataformaResponse>.Ok(new(
            resultado.EmpresasAtivas,
            resultado.EmpresasSuspensas,
            resultado.ConvitesPendentes,
            resultado.ConvitesComFalha)));
    }

    [HttpGet("empresas")]
    public async Task<ActionResult<RespostaApi<PaginaResponse<EmpresaPlataformaResumoResponse>>>> Empresas(
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 25,
        [FromQuery] string? pesquisa = null,
        [FromQuery] bool? ativa = null,
        CancellationToken cancellationToken = default)
    {
        var resultado = await sender.Send(
            new ListarEmpresasPlataformaQuery(pagina, tamanhoPagina, pesquisa, ativa),
            cancellationToken);
        return Ok(RespostaApi<PaginaResponse<EmpresaPlataformaResumoResponse>>.Ok(new(
            resultado.Itens.Select(MapearResumo).ToArray(),
            resultado.Pagina,
            resultado.TamanhoPagina,
            resultado.TotalItens,
            resultado.TotalPaginas)));
    }

    [HttpGet("empresas/{id:guid}")]
    public async Task<ActionResult<RespostaApi<EmpresaPlataformaDetalheResponse>>> Empresa(
        Guid id,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(new ObterEmpresaPlataformaQuery(id), cancellationToken);
        return Ok(RespostaApi<EmpresaPlataformaDetalheResponse>.Ok(MapearDetalhe(resultado)));
    }

    [HttpPost("empresas")]
    public async Task<ActionResult<RespostaApi<EmpresaPlataformaDetalheResponse>>> Provisionar(
        ProvisionarEmpresaRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(new ProvisionarEmpresaCommand(
            request.NomeFantasia,
            request.RazaoSocial,
            request.CpfCnpj,
            request.EmailContato,
            request.Telefone,
            request.FusoHorario,
            request.AdministradorNome,
            request.AdministradorEmail,
            HttpContext.TraceIdentifier), cancellationToken);
        return CreatedAtAction(nameof(Empresa), new { id = resultado.Id },
            RespostaApi<EmpresaPlataformaDetalheResponse>.Ok(
                MapearDetalhe(resultado),
                "Empresa provisionada. O convite será enviado fora da transação."));
    }

    [HttpPost("empresas/{id:guid}/suspender")]
    public async Task<IActionResult> Suspender(
        Guid id,
        AlterarStatusEmpresaPlataformaRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(new SuspenderEmpresaPlataformaCommand(
            id,
            request.Motivo,
            HttpContext.TraceIdentifier), cancellationToken);
        return NoContent();
    }

    [HttpPost("empresas/{id:guid}/reativar")]
    public async Task<IActionResult> Reativar(
        Guid id,
        AlterarStatusEmpresaPlataformaRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(new ReativarEmpresaPlataformaCommand(
            id,
            request.Motivo,
            HttpContext.TraceIdentifier), cancellationToken);
        return NoContent();
    }

    [HttpPost("empresas/{id:guid}/convite/reenviar")]
    public async Task<IActionResult> ReenviarConvite(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new ReenviarConviteAdministradorEmpresaCommand(
            id,
            HttpContext.TraceIdentifier), cancellationToken);
        return NoContent();
    }

    [HttpGet("auditoria")]
    public async Task<ActionResult<RespostaApi<PaginaResponse<AuditoriaPlataformaItemResponse>>>> Auditoria(
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 25,
        [FromQuery] DateTime? inicioUtc = null,
        [FromQuery] DateTime? fimUtc = null,
        [FromQuery] string? tipo = null,
        [FromQuery] Guid? empresaId = null,
        CancellationToken cancellationToken = default)
    {
        var resultado = await sender.Send(new ListarAuditoriaPlataformaQuery(
            pagina,
            tamanhoPagina,
            inicioUtc,
            fimUtc,
            tipo,
            empresaId), cancellationToken);
        return Ok(RespostaApi<PaginaResponse<AuditoriaPlataformaItemResponse>>.Ok(new(
            resultado.Itens.Select(x => new AuditoriaPlataformaItemResponse(
                x.Id,
                x.TipoAcao,
                x.EmpresaAlvoId,
                x.EmpresaNome,
                x.AdministradorNome,
                x.CriadoEmUtc,
                x.TraceId,
                x.DescricaoSegura)).ToArray(),
            resultado.Pagina,
            resultado.TamanhoPagina,
            resultado.TotalItens,
            resultado.TotalPaginas)));
    }

    private static EmpresaPlataformaResumoResponse MapearResumo(EmpresaPlataformaResumo item) => new(
        item.Id,
        item.NomeFantasia,
        item.RazaoSocial,
        item.CpfCnpj,
        item.Slug,
        item.EhAtivo,
        item.AdministradorNome,
        item.AdministradorEmail,
        item.StatusConvite,
        item.CriadoEmUtc);

    private static EmpresaPlataformaDetalheResponse MapearDetalhe(EmpresaPlataformaDetalhe item) => new(
        item.Id,
        item.NomeFantasia,
        item.RazaoSocial,
        item.CpfCnpj,
        item.Email,
        item.Telefone,
        item.Slug,
        item.FusoHorario,
        item.EhAtivo,
        item.CriadoEmUtc,
        item.AdministradorUsuarioId,
        item.AdministradorNome,
        item.AdministradorEmail,
        item.AdministradorAtivo,
        item.ConviteId,
        item.StatusConvite,
        item.ConviteExpiraEmUtc,
        item.TentativasEnvio,
        item.UltimoErroEnvioSeguro);
}
