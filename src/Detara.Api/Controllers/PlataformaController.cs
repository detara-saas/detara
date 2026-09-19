using Detara.Api.Autenticacao;
using Detara.Application.Plataforma;
using Detara.Contracts.Comum;
using Detara.Contracts.Plataforma;
using Detara.Contracts.Assinaturas;
using Detara.Application.Assinaturas;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[ApiController]
[Authorize(Policy = EsquemasAutenticacao.PolicyAdministradorPlataforma)]
[Route("api/plataforma")]
public sealed class PlataformaController(
    ISender sender,
    IAssinaturasPlataformaServico assinaturas,
    IContextoAdministradorPlataforma contextoPlataforma) : ControllerBase
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

    [HttpGet("assinaturas")]
    public async Task<ActionResult<RespostaApi<IReadOnlyCollection<AssinaturaPlataformaResponse>>>> Assinaturas(
        CancellationToken cancellationToken) =>
        Ok(RespostaApi<IReadOnlyCollection<AssinaturaPlataformaResponse>>.Ok(
            (await assinaturas.ListarAsync(cancellationToken)).Select(MapearAssinatura).ToArray()));

    [HttpGet("empresas/{id:guid}/assinatura")]
    public async Task<ActionResult<RespostaApi<AssinaturaPlataformaResponse>>> Assinatura(
        Guid id, CancellationToken cancellationToken) =>
        Ok(RespostaApi<AssinaturaPlataformaResponse>.Ok(MapearAssinatura(await assinaturas.ObterAsync(id, cancellationToken))));

    [HttpPost("empresas/{id:guid}/assinatura")]
    public async Task<ActionResult<RespostaApi<AssinaturaPlataformaResponse>>> CriarAssinatura(
        Guid id, CriarAssinaturaPlataformaRequest request, CancellationToken cancellationToken) =>
        Ok(RespostaApi<AssinaturaPlataformaResponse>.Ok(MapearAssinatura(await assinaturas.CriarAsync(
            contextoPlataforma.AdministradorPlataformaId, id, new(request.ValorMensal, request.InicioTeste,
                request.DiaVencimento, request.AsaasCustomerId, request.AsaasSubscriptionId), cancellationToken)), "Assinatura criada."));

    [HttpPut("empresas/{id:guid}/assinatura")]
    public async Task<ActionResult<RespostaApi<AssinaturaPlataformaResponse>>> AlterarAssinatura(
        Guid id, AlterarCondicoesAssinaturaRequest request, CancellationToken cancellationToken) =>
        Ok(RespostaApi<AssinaturaPlataformaResponse>.Ok(MapearAssinatura(await assinaturas.AlterarCondicoesAsync(
            contextoPlataforma.AdministradorPlataformaId, id, new(request.ValorMensal, request.ProximoVencimento,
                request.DiaVencimento, request.AsaasCustomerId, request.AsaasSubscriptionId, request.Versao,
                request.Motivo), cancellationToken)), "Condições atualizadas."));

    [HttpPost("empresas/{id:guid}/assinatura/pagamentos")]
    public async Task<ActionResult<RespostaApi<AssinaturaPlataformaResponse>>> ConfirmarPagamento(
        Guid id, ConfirmarPagamentoAssinaturaRequest request, CancellationToken cancellationToken) =>
        Ok(RespostaApi<AssinaturaPlataformaResponse>.Ok(MapearAssinatura(await assinaturas.ConfirmarPagamentoAsync(
            contextoPlataforma.AdministradorPlataformaId, id, new(request.DataPagamento,
                request.ReferenciaPagamento, request.AsaasCustomerId, request.AsaasSubscriptionId,
                request.Versao, request.Motivo), cancellationToken)), "Pagamento confirmado."));

    [HttpPost("empresas/{id:guid}/assinatura/confirmacao-comercial")]
    public async Task<ActionResult<RespostaApi<AssinaturaPlataformaResponse>>> ConfirmarComercialmente(
        Guid id, ConfirmarComercialmenteAssinaturaRequest request, CancellationToken cancellationToken) =>
        Ok(RespostaApi<AssinaturaPlataformaResponse>.Ok(MapearAssinatura(
            await assinaturas.ConfirmarComercialmenteAsync(contextoPlataforma.AdministradorPlataformaId,
                id, new(request.DataConfirmacaoComercial, request.Versao, request.Motivo), cancellationToken)),
            "Confirmação comercial registrada."));

    [HttpPost("empresas/{id:guid}/assinatura/atraso")]
    public async Task<ActionResult<RespostaApi<AssinaturaPlataformaResponse>>> MarcarAtraso(
        Guid id, AlterarStatusAssinaturaRequest request, CancellationToken cancellationToken) =>
        Ok(RespostaApi<AssinaturaPlataformaResponse>.Ok(MapearAssinatura(await assinaturas.MarcarAtrasoAsync(
            contextoPlataforma.AdministradorPlataformaId, id, new(request.Versao, request.Motivo), cancellationToken))));

    [HttpPost("empresas/{id:guid}/assinatura/suspender")]
    public async Task<ActionResult<RespostaApi<AssinaturaPlataformaResponse>>> SuspenderAssinatura(
        Guid id, AlterarStatusAssinaturaRequest request, CancellationToken cancellationToken) =>
        Ok(RespostaApi<AssinaturaPlataformaResponse>.Ok(MapearAssinatura(await assinaturas.SuspenderAsync(
            contextoPlataforma.AdministradorPlataformaId, id, new(request.Versao, request.Motivo), cancellationToken))));

    [HttpPost("empresas/{id:guid}/assinatura/reativar")]
    public async Task<ActionResult<RespostaApi<AssinaturaPlataformaResponse>>> ReativarAssinatura(
        Guid id, AlterarStatusAssinaturaRequest request, CancellationToken cancellationToken) =>
        Ok(RespostaApi<AssinaturaPlataformaResponse>.Ok(MapearAssinatura(await assinaturas.ReativarAsync(
            contextoPlataforma.AdministradorPlataformaId, id, new(request.Versao, request.Motivo), cancellationToken))));

    [HttpPost("empresas/{id:guid}/assinatura/cancelar")]
    public async Task<ActionResult<RespostaApi<AssinaturaPlataformaResponse>>> CancelarAssinatura(
        Guid id, AlterarStatusAssinaturaRequest request, CancellationToken cancellationToken) =>
        Ok(RespostaApi<AssinaturaPlataformaResponse>.Ok(MapearAssinatura(await assinaturas.CancelarAsync(
            contextoPlataforma.AdministradorPlataformaId, id, new(request.Versao, request.Motivo), cancellationToken))));

    [HttpGet("empresas/{id:guid}/assinatura/termo")]
    public async Task<IActionResult> TermoAssinatura(Guid id, CancellationToken cancellationToken)
    {
        var documento = await assinaturas.AbrirTermoAceitoAsync(id, cancellationToken);
        return File(documento.Conteudo, documento.ContentType, documento.NomeArquivo, enableRangeProcessing: false);
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
        item.UltimoErroEnvioSeguro,
        item.SegmentoCodigo,
        item.Capacidades.Select(capacidade => new CapacidadeEmpresaPlataformaResponse(
            capacidade.Codigo,
            capacidade.Nome,
            capacidade.Categoria,
            capacidade.Habilitada,
            capacidade.Configuravel,
            capacidade.Ordem)).ToArray());

    private static AssinaturaPlataformaResponse MapearAssinatura(AssinaturaPlataformaResultado item) => new(
        item.EmpresaId, item.EmpresaNome,
        new(item.Assinatura.PossuiAssinatura, item.Assinatura.Id, item.Assinatura.Status,
            item.Assinatura.ValorMensal, item.Assinatura.InicioTeste, item.Assinatura.FimTeste,
            item.Assinatura.DataConfirmacaoComercial, item.Assinatura.ConfirmacaoComercialRegistradaEmUtc,
            item.Assinatura.DiaVencimento, item.Assinatura.PrimeiroVencimento,
            item.Assinatura.ProximoVencimento, item.Assinatura.Versao,
            item.Assinatura.AsaasCustomerId, item.Assinatura.AsaasSubscriptionId,
            item.Assinatura.TermoAceito is null ? null : new(item.Assinatura.TermoAceito.Id,
                item.Assinatura.TermoAceito.VersaoTermo, item.Assinatura.TermoAceito.AceitoEmUtc,
                item.Assinatura.TermoAceito.HashSha256, item.Assinatura.TermoAceito.ResponsavelNome,
                item.Assinatura.TermoAceito.ResponsavelEmail)),
        item.Historico.Select(x => new HistoricoAssinaturaResponse(x.Id, x.TipoEvento,
            x.StatusAnterior, x.StatusNovo, x.OcorridoEmUtc, x.Motivo, x.Responsavel,
            x.ReferenciaPagamento)).ToArray());
}
