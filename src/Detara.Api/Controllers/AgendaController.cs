using Detara.Application.Agenda;
using Detara.Application.FluxoOperacional;
using Detara.Contracts.Agenda;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Catalogo;
using Detara.Contracts.Comum;
using Detara.Domain.Agenda;
using Detara.Domain.Catalogo;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class AgendaController(ISender sender) : ControllerBase
{
    [HttpGet("agenda"), Authorize(Policy = Permissoes.AgendaVisualizar)]
    public async Task<ActionResult<RespostaApi<IReadOnlyCollection<AgendamentoPeriodoResponse>>>> ListarPeriodo([FromQuery] DateTime inicioUtc, [FromQuery] DateTime fimUtc, [FromQuery] StatusAgendamentoContrato? status = null, [FromQuery] string? pesquisa = null, CancellationToken ct = default)
    {
        var itens = await sender.Send(new ListarAgendaPeriodoQuery(new(ComoUtc(inicioUtc), ComoUtc(fimUtc), status.HasValue ? Mapear(status.Value) : null, pesquisa)), ct);
        return Ok(RespostaApi<IReadOnlyCollection<AgendamentoPeriodoResponse>>.Ok(itens.Select(MapearPeriodo).ToArray()));
    }

    [HttpGet("agenda/contexto"), Authorize(Policy = Permissoes.AgendaVisualizar)]
    public async Task<ActionResult<RespostaApi<ContextoAgendaResponse>>> Contexto(CancellationToken ct) { var contexto = await sender.Send(new ObterContextoAgendaQuery(), ct); return Ok(RespostaApi<ContextoAgendaResponse>.Ok(new(contexto.FusoHorario, contexto.HojeLocal, contexto.AgoraLocal))); }

    [HttpGet("agendamentos"), Authorize(Policy = Permissoes.AgendaVisualizar)]
    public async Task<ActionResult<RespostaApi<PaginaResponse<AgendamentoListaResponse>>>> ListarHistorico([FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 25, [FromQuery] DateTime? inicioUtc = null, [FromQuery] DateTime? fimUtc = null, [FromQuery] StatusAgendamentoContrato? status = null, [FromQuery] string? pesquisa = null, CancellationToken ct = default)
    {
        var resultado = await sender.Send(new ListarHistoricoAgendamentosQuery(new(pagina, tamanhoPagina, inicioUtc.HasValue ? ComoUtc(inicioUtc.Value) : null, fimUtc.HasValue ? ComoUtc(fimUtc.Value) : null, status.HasValue ? Mapear(status.Value) : null, pesquisa)), ct);
        var paginaResponse = new PaginaResponse<AgendamentoListaResponse>(resultado.Itens.Select(MapearLista).ToArray(), resultado.Pagina, resultado.TamanhoPagina, resultado.TotalItens, resultado.TotalPaginas);
        return Ok(RespostaApi<PaginaResponse<AgendamentoListaResponse>>.Ok(paginaResponse));
    }

    [HttpGet("agendamentos/{id:guid}"), Authorize(Policy = Permissoes.AgendaVisualizar)]
    public async Task<ActionResult<RespostaApi<AgendamentoDetalheResponse>>> Obter(Guid id, CancellationToken ct) => Ok(RespostaApi<AgendamentoDetalheResponse>.Ok(MapearDetalhe(await sender.Send(new ObterAgendamentoQuery(id), ct))));

    [HttpPost("agendamentos"), Authorize(Policy = Permissoes.AgendaCriar)]
    public async Task<ActionResult<RespostaApi<AgendamentoDetalheResponse>>> Criar(SalvarAgendamentoRequest request, CancellationToken ct)
    {
        var resultado = await sender.Send(new CriarAgendamentoCommand(request.ClienteId, request.VeiculoId, request.InicioLocal, request.DuracaoPlanejadaMinutos, request.ObservacaoSolicitante, request.ObservacaoInterna, request.Itens.Select(Mapear).ToArray()), ct);
        return CreatedAtAction(nameof(Obter), new { id = resultado.Agendamento.Id }, RespostaApi<AgendamentoDetalheResponse>.Ok(MapearDetalhe(resultado), resultado.QuantidadeSobreposicoes > 0 ? "Agendamento criado com aviso de sobreposição." : "Agendamento criado com sucesso."));
    }

    [HttpPut("agendamentos/{id:guid}"), Authorize(Policy = Permissoes.AgendaEditar)]
    public async Task<ActionResult<RespostaApi<AgendamentoDetalheResponse>>> Atualizar(Guid id, SalvarAgendamentoRequest request, CancellationToken ct)
    {
        var resultado = await sender.Send(new AtualizarAgendamentoCommand(id, request.ClienteId, request.VeiculoId, request.InicioLocal, request.DuracaoPlanejadaMinutos, request.ObservacaoSolicitante, request.ObservacaoInterna, request.Itens.Select(Mapear).ToArray()), ct);
        return Ok(RespostaApi<AgendamentoDetalheResponse>.Ok(MapearDetalhe(resultado), resultado.QuantidadeSobreposicoes > 0 ? "Agendamento atualizado com aviso de sobreposição." : "Agendamento atualizado com sucesso."));
    }

    [HttpPatch("agendamentos/{id:guid}/reagendar"), Authorize(Policy = Permissoes.AgendaEditar)]
    public async Task<ActionResult<RespostaApi<AgendamentoDetalheResponse>>> Reagendar(Guid id, ReagendarAgendamentoRequest request, CancellationToken ct)
    { var resultado = await sender.Send(new ReagendarAgendamentoCommand(id, request.InicioLocal, request.DuracaoPlanejadaMinutos), ct); return Ok(RespostaApi<AgendamentoDetalheResponse>.Ok(MapearDetalhe(resultado), resultado.QuantidadeSobreposicoes > 0 ? "Agendamento reagendado com aviso de sobreposição." : "Agendamento reagendado com sucesso.")); }

    [HttpPatch("agendamentos/{id:guid}/status"), Authorize(Policy = Permissoes.AgendaEditar)]
    public async Task<ActionResult<RespostaApi<AgendamentoDetalheResponse>>> AlterarStatus(Guid id, AlterarStatusAgendamentoRequest request, CancellationToken ct)
    { var resultado = await sender.Send(new AlterarStatusAgendaOperacionalCommand(id, Mapear(request.Status), request.MotivoCancelamento), ct); return Ok(RespostaApi<AgendamentoDetalheResponse>.Ok(MapearDetalhe(resultado), "Status atualizado com sucesso.")); }

    [HttpGet("agenda/clientes"), Authorize(Policy = Permissoes.AgendaCriar)]
    public async Task<ActionResult<RespostaApi<IReadOnlyCollection<ClienteAgendaResponse>>>> BuscarClientes([FromQuery] string pesquisa, CancellationToken ct) => Ok(RespostaApi<IReadOnlyCollection<ClienteAgendaResponse>>.Ok((await sender.Send(new BuscarClientesAgendaQuery(pesquisa), ct)).Select(x => new ClienteAgendaResponse(x.Id, x.Nome, x.Telefone)).ToArray()));

    [HttpGet("agenda/clientes/{clienteId:guid}/veiculos"), Authorize(Policy = Permissoes.AgendaCriar)]
    public async Task<ActionResult<RespostaApi<IReadOnlyCollection<VeiculoAgendaResponse>>>> ListarVeiculos(Guid clienteId, [FromQuery] bool incluirInativos = false, CancellationToken ct = default) => Ok(RespostaApi<IReadOnlyCollection<VeiculoAgendaResponse>>.Ok((await sender.Send(new ListarVeiculosAgendaQuery(clienteId, incluirInativos), ct)).Select(x => new VeiculoAgendaResponse(x.Id, x.Descricao, x.Placa)).ToArray()));

    [HttpGet("agenda/catalogo"), Authorize(Policy = Permissoes.AgendaCriar)]
    public async Task<ActionResult<RespostaApi<IReadOnlyCollection<ItemCatalogoAgendaResponse>>>> BuscarCatalogo([FromQuery] string? pesquisa = null, [FromQuery] bool incluirInativos = false, CancellationToken ct = default) => Ok(RespostaApi<IReadOnlyCollection<ItemCatalogoAgendaResponse>>.Ok((await sender.Send(new BuscarCatalogoAgendaQuery(pesquisa, incluirInativos), ct)).Select(x => new ItemCatalogoAgendaResponse(Mapear(x.TipoItem), x.Id, x.Nome, x.Descricao, x.Categoria, Mapear(x.TipoPrecificacao), x.PrecoReferencia, x.DuracaoReferenciaMinutos, x.EhAtivo)).ToArray()));

    [HttpGet("agenda/sobreposicoes"), Authorize(Policy = Permissoes.AgendaCriar)]
    public async Task<ActionResult<RespostaApi<int>>> Sobreposicoes([FromQuery] DateTime inicioLocal, [FromQuery] int duracaoPlanejadaMinutos, [FromQuery] Guid? ignorarAgendamentoId = null, CancellationToken ct = default) => Ok(RespostaApi<int>.Ok(await sender.Send(new ContarSobreposicoesAgendaQuery(inicioLocal, duracaoPlanejadaMinutos, ignorarAgendamentoId), ct)));

    private static AgendamentoPeriodoResponse MapearPeriodo(AgendamentoPeriodoVisualizacao x) => new(x.Agendamento.Id, x.Agendamento.InicioUtc, x.InicioLocal, x.Agendamento.DuracaoPlanejadaMinutos, x.Agendamento.ClienteNome, x.Agendamento.VeiculoDescricao, x.Agendamento.VeiculoPlaca, Mapear(x.Agendamento.Status), x.Agendamento.PrincipaisItens, Mapear(x.Agendamento.Referencia));
    private static AgendamentoListaResponse MapearLista(AgendamentoListaVisualizacao x) => new(x.Agendamento.Id, x.Agendamento.InicioUtc, x.InicioLocal, x.Agendamento.DuracaoPlanejadaMinutos, x.Agendamento.ClienteNome, x.Agendamento.VeiculoDescricao, x.Agendamento.VeiculoPlaca, Mapear(x.Agendamento.Status), x.Agendamento.Itens);
    private static AgendamentoDetalheResponse MapearDetalhe(AgendamentoDetalheVisualizacao x) => new(x.Agendamento.Id, x.Agendamento.ClienteId, x.Agendamento.ClienteNome, x.Agendamento.VeiculoId, x.Agendamento.VeiculoDescricao, x.Agendamento.VeiculoPlaca, x.Agendamento.InicioUtc, x.InicioLocal, x.FusoHorario, x.Agendamento.DuracaoPlanejadaMinutos, Mapear(x.Agendamento.Status), x.Agendamento.ObservacaoSolicitante, x.Agendamento.ObservacaoInterna, x.Agendamento.MotivoCancelamento, x.Agendamento.CriadoEmUtc, x.Agendamento.AtualizadoEmUtc, x.QuantidadeSobreposicoes, Mapear(x.Agendamento.Referencia), x.Itens.Select(i => new AgendamentoItemResponse(i.Item.Id, Mapear(i.Item.TipoItem), i.Item.ItemCatalogoId, i.Item.Nome, i.Item.Descricao, Mapear(i.Item.TipoPrecificacao), i.Item.PrecoReferencia, i.Item.DuracaoReferenciaMinutos, i.Item.Ordem, i.ItemAtivoNoCatalogo)).ToArray());
    private static ResumoReferenciaAgendamentoResponse Mapear(ResumoReferenciaAgenda x) { var texto = x.PossuiSobConsulta ? "Itens sujeitos à avaliação" : x.SomaReferencias.HasValue ? x.PossuiAPartirDe ? $"A partir de {x.SomaReferencias.Value:C2}" : x.SomaReferencias.Value.ToString("C2") : "Sob consulta"; return new(x.SomaReferencias, x.PossuiAPartirDe, x.PossuiSobConsulta, texto); }
    private static ItemAgendamentoEntrada Mapear(AgendamentoItemRequest x) => new(Mapear(x.TipoItem), x.ItemCatalogoId);
    private static TipoItemAgendamento Mapear(TipoItemAgendamentoContrato x) => (TipoItemAgendamento)(int)x;
    private static TipoItemAgendamentoContrato Mapear(TipoItemAgendamento x) => (TipoItemAgendamentoContrato)(int)x;
    private static StatusAgendamento Mapear(StatusAgendamentoContrato x) => (StatusAgendamento)(int)x;
    private static StatusAgendamentoContrato Mapear(StatusAgendamento x) => (StatusAgendamentoContrato)(int)x;
    private static TipoPrecificacaoCatalogo Mapear(TipoPrecificacao x) => (TipoPrecificacaoCatalogo)(int)x;
    private static DateTime ComoUtc(DateTime valor) => DateTime.SpecifyKind(valor, DateTimeKind.Utc);
}
