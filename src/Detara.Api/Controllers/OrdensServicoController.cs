using Detara.Application.Abstracoes;
using Detara.Application.Atendimento;
using Detara.Contracts.Atendimento;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Comum;
using Detara.Domain.Atendimento;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[ApiController]
[Route("api/ordens-servico")]
public sealed class OrdensServicoController(ISender sender) : ControllerBase
{
    [HttpGet, Authorize(Policy = Permissoes.OrdemServicoVisualizar)]
    public async Task<ActionResult<RespostaApi<PaginaResponse<OrdemServicoListaResponse>>>> Listar(
        [FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 25,
        [FromQuery] StatusOrdemServicoContrato? status = null, [FromQuery] DateOnly? dataInicial = null,
        [FromQuery] DateOnly? dataFinal = null, [FromQuery] string? pesquisa = null, CancellationToken ct = default)
    {
        var resultado = await sender.Send(new ListarOrdensServicoQuery(pagina, tamanhoPagina,
            status.HasValue ? (StatusOrdemServico)(int)status.Value : null, dataInicial, dataFinal, pesquisa), ct);
        var resposta = new PaginaResponse<OrdemServicoListaResponse>(resultado.Itens.Select(item => new OrdemServicoListaResponse(
            item.Id, item.Codigo, item.ClienteNome, item.VeiculoDescricao, item.VeiculoPlaca,
            (StatusOrdemServicoContrato)(int)item.Status, item.TotalAutorizado, item.CriadoEmUtc)).ToArray(),
            resultado.Pagina, resultado.TamanhoPagina, resultado.TotalItens, resultado.TotalPaginas);
        return Ok(RespostaApi<PaginaResponse<OrdemServicoListaResponse>>.Ok(resposta));
    }

    [HttpGet("{id:guid}"), Authorize(Policy = Permissoes.OrdemServicoVisualizar)]
    public async Task<ActionResult<RespostaApi<OrdemServicoDetalheResponse>>> Obter(Guid id, CancellationToken ct) =>
        Ok(RespostaApi<OrdemServicoDetalheResponse>.Ok(Mapear(await sender.Send(new ObterOrdemServicoQuery(id), ct))));

    [HttpPost, Authorize(Policy = Permissoes.OrdemServicoCriar)]
    public async Task<ActionResult<RespostaApi<OrdemServicoDetalheResponse>>> Criar(CriarOrdemServicoRequest request, CancellationToken ct)
    {
        var resultado = await sender.Send(new CriarOrdemServicoCommand(request.OrcamentoOrigemId,
            request.AgendamentoOrigemId, request.ClienteId, request.VeiculoId, request.DuracaoPlanejadaMinutos,
            request.Desconto, request.Acrescimo, request.ObservacaoAutorizacaoDireta,
            request.Itens.Select(Mapear).ToArray()), ct);
        return CreatedAtAction(nameof(Obter), new { id = resultado.OrdemServico.Id },
            RespostaApi<OrdemServicoDetalheResponse>.Ok(Mapear(resultado), "Ordem de serviço criada com sucesso."));
    }

    [HttpPost("{id:guid}/check-in"), Authorize(Policy = Permissoes.OrdemServicoEditar)]
    public Task<ActionResult<RespostaApi<OrdemServicoDetalheResponse>>> CheckIn(Guid id, RealizarCheckInRequest request,
        CancellationToken ct) => Responder(sender.Send(new RealizarCheckInCommand(id, request.QuilometragemEntrada,
            request.ObservacaoEntrada), ct), "Check-in realizado com sucesso.");

    [HttpPut("{id:guid}/checklist"), Authorize(Policy = Permissoes.OrdemServicoEditar)]
    public Task<ActionResult<RespostaApi<OrdemServicoDetalheResponse>>> Checklist(Guid id,
        AtualizarChecklistOrdemServicoRequest request, CancellationToken ct) => Responder(sender.Send(
            new AtualizarChecklistOrdemServicoCommand(id, request.Respostas.Select(item => new RespostaChecklistSnapshot(
                item.ItemId, (RespostaChecklistOrdemServico)(int)item.Resposta, item.Observacao)).ToArray()), ct),
            "Checklist atualizado com sucesso.");

    [HttpPost("{id:guid}/iniciar-execucao"), Authorize(Policy = Permissoes.OrdemServicoFinalizar)]
    public Task<ActionResult<RespostaApi<OrdemServicoDetalheResponse>>> Iniciar(Guid id,
        TransicaoOrdemServicoRequest request, CancellationToken ct) => Responder(
            sender.Send(new TransicaoOrdemServicoCommand(id, request.Observacao), ct), "Execução iniciada com sucesso.");

    [HttpPost("{id:guid}/finalizar-execucao"), Authorize(Policy = Permissoes.OrdemServicoFinalizar)]
    public Task<ActionResult<RespostaApi<OrdemServicoDetalheResponse>>> Finalizar(Guid id,
        TransicaoOrdemServicoRequest request, CancellationToken ct) => Responder(
            sender.Send(new FinalizarExecucaoOrdemServicoCommand(id, request.Observacao), ct), "Veículo marcado como aguardando retirada.");

    [HttpPost("{id:guid}/concluir"), Authorize(Policy = Permissoes.OrdemServicoFinalizar)]
    public Task<ActionResult<RespostaApi<OrdemServicoDetalheResponse>>> Concluir(Guid id,
        TransicaoOrdemServicoRequest request, CancellationToken ct) => Responder(
            sender.Send(new ConcluirOrdemServicoCommand(id, request.Observacao), ct), "Atendimento concluído e veículo entregue.");

    [HttpPost("{id:guid}/cancelar"), Authorize(Policy = Permissoes.OrdemServicoFinalizar)]
    public Task<ActionResult<RespostaApi<OrdemServicoDetalheResponse>>> Cancelar(Guid id,
        CancelarOrdemServicoRequest request, CancellationToken ct) => Responder(
            sender.Send(new CancelarOrdemServicoCommand(id, request.Motivo), ct), "Ordem de serviço cancelada.");

    [HttpPost("{id:guid}/fotos"), Authorize(Policy = Permissoes.OrdemServicoEditar)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PoliticaImagemUpload.TamanhoMaximoBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = PoliticaImagemUpload.TamanhoMaximoBytes + 1024 * 1024)]
    public async Task<ActionResult<RespostaApi<OrdemServicoFotoResponse>>> EnviarFoto(Guid id,
        [FromForm] CategoriaFotoOrdemServicoContrato categoria, IFormFile arquivo, CancellationToken ct)
    {
        await using var stream = arquivo.OpenReadStream();
        var foto = await sender.Send(new EnviarFotoOrdemServicoCommand(id,
            (CategoriaFotoOrdemServico)(int)categoria, arquivo.FileName, arquivo.Length, stream), ct);
        return Ok(RespostaApi<OrdemServicoFotoResponse>.Ok(Mapear(foto), "Foto anexada com sucesso."));
    }

    [HttpGet("{id:guid}/fotos/{fotoId:guid}"), Authorize(Policy = Permissoes.OrdemServicoVisualizar)]
    public async Task<IActionResult> ObterFoto(Guid id, Guid fotoId, CancellationToken ct)
    {
        var foto = await sender.Send(new ObterFotoOrdemServicoQuery(id, fotoId), ct);
        return File(foto.Conteudo, foto.ContentType, foto.NomeOriginal, enableRangeProcessing: true);
    }

    [HttpDelete("{id:guid}/fotos/{fotoId:guid}"), Authorize(Policy = Permissoes.OrdemServicoEditar)]
    public async Task<IActionResult> ExcluirFoto(Guid id, Guid fotoId, CancellationToken ct)
    {
        await sender.Send(new ExcluirFotoOrdemServicoCommand(id, fotoId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/orcamento-adicional"), Authorize(Policy = Permissoes.OrcamentosCriar)]
    public async Task<ActionResult<RespostaApi<OrcamentoDetalheResponse>>> OrcamentoAdicional(Guid id,
        CriarOrcamentoAdicionalRequest request, CancellationToken ct)
    {
        var resultado = await sender.Send(new CriarOrcamentoAdicionalCommand(id, request.ValidoAte,
            request.ObservacaoCliente, request.ObservacaoInterna, request.Condicoes, request.Desconto,
            request.Acrescimo, request.Itens.Select(item => new ItemOrcamentoEntrada(
                (TipoItemOrcamento)(int)item.TipoItem, item.ItemCatalogoId, item.Nome, item.Descricao,
                item.ValorUnitario, item.Quantidade, item.Observacao)).ToArray()), ct);
        return Created($"/api/orcamentos/{resultado.Orcamento.Id}",
            RespostaApi<OrcamentoDetalheResponse>.Ok(OrcamentosController.MapearDetalhePublico(resultado),
                "Orçamento adicional criado como rascunho."));
    }

    [HttpPost("{id:guid}/cortesias"), Authorize(Policy = Permissoes.OrdemServicoEditar)]
    public Task<ActionResult<RespostaApi<OrdemServicoDetalheResponse>>> Cortesia(Guid id,
        ItemOrdemServicoRequest request, CancellationToken ct) => Responder(sender.Send(
            new AdicionarCortesiaOrdemServicoCommand(id, Mapear(request)), ct), "Cortesia adicionada com sucesso.");

    private async Task<ActionResult<RespostaApi<OrdemServicoDetalheResponse>>> Responder(
        Task<OrdemServicoDetalheVisualizacao> tarefa, string mensagem) =>
        Ok(RespostaApi<OrdemServicoDetalheResponse>.Ok(Mapear(await tarefa), mensagem));

    private static ItemOrdemServicoEntrada Mapear(ItemOrdemServicoRequest item) => new(
        (TipoItemOrcamento)(int)item.TipoItem, item.ItemCatalogoId, item.Nome, item.Descricao,
        item.ValorUnitarioAutorizado, item.Quantidade, item.ObservacaoAutorizacao);
    private static OrdemServicoFotoResponse Mapear(OrdemServicoFoto foto) => new(foto.Id,
        (CategoriaFotoOrdemServicoContrato)(int)foto.Categoria, foto.NomeOriginal, foto.ContentType,
        foto.TamanhoBytes, foto.EnviadaPorUsuarioId, foto.CriadoEmUtc);

    private static OrdemServicoDetalheResponse Mapear(OrdemServicoDetalheVisualizacao resultado)
    {
        var ordem = resultado.OrdemServico;
        string Nome(Guid id) => resultado.Usuarios.TryGetValue(id, out var nome) ? nome : "Usuário Detara";
        return new(ordem.Id, ordem.Codigo, (OrigemOrdemServicoContrato)(int)ordem.Origem,
            ordem.OrcamentoOrigemId, ordem.AgendamentoOrigemId, ordem.ClienteId, ordem.ClienteNomeSnapshot,
            ordem.ClienteDocumentoSnapshot, ordem.ClienteTelefoneSnapshot, ordem.VeiculoId,
            ordem.VeiculoDescricaoSnapshot, ordem.VeiculoPlacaSnapshot, ordem.DuracaoPlanejadaMinutos,
            (StatusOrdemServicoContrato)(int)ordem.Status, ordem.SubtotalAutorizado, ordem.DescontoAutorizado,
            ordem.AcrescimoAutorizado, ordem.TotalAutorizado, ordem.AutorizacaoDiretaEmUtc,
            ordem.AutorizacaoDiretaPorUsuarioId, ordem.ObservacaoAutorizacaoDireta, ordem.CheckInEmUtc,
            ordem.QuilometragemEntrada, ordem.ObservacaoEntrada,
            ordem.ChecklistEntradaSnapshot.HasValue ? (NivelExigenciaOperacionalContrato)(int)ordem.ChecklistEntradaSnapshot.Value : null,
            ordem.FotosEntradaSnapshot.HasValue ? (NivelExigenciaOperacionalContrato)(int)ordem.FotosEntradaSnapshot.Value : null,
            ordem.FotosSaidaSnapshot.HasValue ? (NivelExigenciaOperacionalContrato)(int)ordem.FotosSaidaSnapshot.Value : null,
            ordem.IniciadaEmUtc, ordem.ExecucaoFinalizadaEmUtc, ordem.ConcluidaEmUtc, ordem.CanceladaEmUtc,
            ordem.MotivoCancelamento, ordem.CriadoEmUtc,
            ordem.Itens.OrderBy(item => item.Ordem).Select(item => new OrdemServicoItemResponse(item.Id,
                (TipoItemOrcamentoContrato)(int)item.TipoItem, item.ItemCatalogoId, item.OrcamentoOrigemId,
                item.OrcamentoItemOrigemId, item.NomeSnapshot, item.DescricaoSnapshot,
                item.ValorUnitarioAutorizado, item.Quantidade, item.Subtotal, item.Ordem,
                (OrigemComercialOrdemServicoContrato)(int)item.OrigemComercial, item.AutorizadoEmUtc,
                item.AutorizadoPorUsuarioId, Nome(item.AutorizadoPorUsuarioId), item.ObservacaoAutorizacao)).ToArray(),
            ordem.Checklist is null ? null : new OrdemServicoChecklistResponse(ordem.Checklist.Id,
                ordem.Checklist.NomeSnapshot, ordem.Checklist.EstaCompleto, ordem.Checklist.Itens.OrderBy(item => item.Ordem)
                    .Select(item => new OrdemServicoChecklistItemResponse(item.Id, item.DescricaoSnapshot, item.Ordem,
                        item.Resposta.HasValue ? (RespostaChecklistOrdemServicoContrato)(int)item.Resposta.Value : null,
                        item.Observacao)).ToArray()),
            ordem.Fotos.OrderBy(item => item.Categoria).ThenBy(item => item.CriadoEmUtc).Select(Mapear).ToArray(),
            resultado.OrcamentosAdicionais.Select(item => new OrcamentoAdicionalOrdemServicoResponse(item.Id,
                item.Codigo, (StatusOrcamentoContrato)(int)item.Status, item.Total, item.CriadoEmUtc,
                item.Itens.OrderBy(i => i.Ordem).Select(i => i.NomeSnapshot).ToArray())).ToArray(),
            ordem.Historico.OrderBy(item => item.DataUtc).Select(item => new HistoricoStatusOrdemServicoResponse(
                item.Id, (StatusOrdemServicoContrato)(int)item.Status, item.DataUtc, item.UsuarioId,
                Nome(item.UsuarioId), item.Observacao)).ToArray());
    }
}
