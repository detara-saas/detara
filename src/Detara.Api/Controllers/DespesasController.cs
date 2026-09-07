using Detara.Application.Abstracoes;
using Detara.Application.Agenda;
using Detara.Application.Financeiro;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Comum;
using Detara.Contracts.Financeiro;
using Detara.Domain.Financeiro;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[ApiController, Route("api/financeiro/despesas"), Authorize(Policy = Permissoes.FinanceiroVisualizar)]
public sealed class DespesasController(ISender sender, IUsuarioContexto usuario,
    IPlataformaFinanceiroConsulta plataforma, IConversorFusoHorario conversor, TimeProvider relogio) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RespostaApi<DespesasResponse>>> Listar(DateOnly? competencia = null,
        int pagina = 1, int tamanhoPagina = 25, StatusDespesaContrato? status = null,
        Guid? categoriaId = null, OrigemDespesaContrato? origem = null, string? pesquisa = null, CancellationToken ct = default)
    {
        var r = await sender.Send(new ListarDespesasQuery(competencia, pagina, tamanhoPagina,
            (FiltroStatusDespesa?)(int?)status, categoriaId, (OrigemContaPagar?)(int?)origem, pesquisa), ct);
        return Ok(RespostaApi<DespesasResponse>.Ok(new(new(r.Contas.Itens.Select(Mapear).ToArray(), r.Contas.Pagina,
            r.Contas.TamanhoPagina, r.Contas.TotalItens, r.Contas.TotalPaginas), new(r.Resumo.Total, r.Resumo.Pago, r.Resumo.APagar, r.Resumo.Vencido), r.Competencia, r.Hoje)));
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RespostaApi<DespesaDetalheResponse>>> Obter(Guid id, CancellationToken ct)
    {
        var c = await sender.Send(new ObterDespesaQuery(id), ct);
        var fuso = await plataforma.ObterFusoHorarioAsync(usuario.EmpresaId, ct) ?? "America/Sao_Paulo";
        var hoje = DateOnly.FromDateTime(conversor.ParaLocal(relogio.GetUtcNow().UtcDateTime, fuso));
        return Ok(RespostaApi<DespesaDetalheResponse>.Ok(new(new(c.Id, c.Descricao, c.CategoriaDespesaId,
            c.CategoriaNomeSnapshot, (OrigemDespesaContrato)c.Origem, c.DespesaRecorrenteId, c.Competencia,
            c.DataVencimento, c.Valor, (StatusDespesaContrato)c.Status, c.EstaVencidaEm(hoje), c.DataPagamento,
            c.ValorPago, c.Fornecedor, c.Observacao, c.Versao), c.Pagamentos.OrderByDescending(x => x.RegistradoEmUtc)
                .Select(x => new PagamentoDespesaResponse(x.Id, x.DataPagamento, x.Valor, x.RegistradoPorUsuarioId,
                    x.RegistradoEmUtc, x.Status == StatusPagamento.Estornado, x.MotivoEstorno,
                    x.EstornadoPorUsuarioId, x.EstornadoEmUtc)).ToArray())));
    }
    [HttpPost, Authorize(Policy = Permissoes.FinanceiroEditar)]
    public async Task<ActionResult<RespostaApi<DespesaIdResponse>>> Criar(SalvarDespesaRequest r, CancellationToken ct) =>
        Ok(Id(await sender.Send(new CriarDespesaCommand(Dados(r)), ct)));
    [HttpPut("{id:guid}"), Authorize(Policy = Permissoes.FinanceiroEditar)]
    public async Task<ActionResult<RespostaApi<DespesaIdResponse>>> Editar(Guid id, SalvarDespesaRequest r, CancellationToken ct) =>
        Ok(Id(await sender.Send(new EditarDespesaCommand(id, r.Versao, Dados(r)), ct)));
    [HttpPost("{id:guid}/pagar"), Authorize(Policy = Permissoes.FinanceiroRegistrarPagamento)]
    public async Task<ActionResult<RespostaApi<DespesaIdResponse>>> Pagar(Guid id, PagarDespesaRequest r, CancellationToken ct) =>
        Ok(Id(await sender.Send(new PagarDespesaCommand(id, r.Versao, r.DataPagamento, r.ValorPago), ct)));
    [HttpPost("{id:guid}/estornar"), Authorize(Policy = Permissoes.FinanceiroEstornarPagamento)]
    public async Task<ActionResult<RespostaApi<DespesaIdResponse>>> Estornar(Guid id, EstornarDespesaRequest r, CancellationToken ct) =>
        Ok(Id(await sender.Send(new EstornarDespesaCommand(id, r.Versao, r.Motivo), ct)));
    [HttpPost("{id:guid}/cancelar"), Authorize(Policy = Permissoes.FinanceiroEditar)]
    public async Task<ActionResult<RespostaApi<DespesaIdResponse>>> Cancelar(Guid id, VersaoDespesaRequest r, CancellationToken ct) =>
        Ok(Id(await sender.Send(new CancelarDespesaCommand(id, r.Versao), ct)));

    [HttpGet("recorrencias")]
    public async Task<ActionResult<RespostaApi<PaginaResponse<RecorrenciaDespesaResponse>>>> Recorrencias(int pagina = 1, int tamanhoPagina = 25, CancellationToken ct = default)
    {
        var r = await sender.Send(new ListarRecorrenciasQuery(pagina, tamanhoPagina), ct);
        return Ok(RespostaApi<PaginaResponse<RecorrenciaDespesaResponse>>.Ok(new(r.Itens.Select(x => new RecorrenciaDespesaResponse(x.Id,
            x.Descricao, x.CategoriaId, x.Categoria, x.Valor, x.DiaVencimento, x.CompetenciaInicial,
            x.CompetenciaFinal, x.ProximaCompetencia, x.Ativa, x.Fornecedor, x.Observacao, x.Versao)).ToArray(), r.Pagina, r.TamanhoPagina, r.TotalItens, r.TotalPaginas)));
    }
    [HttpGet("recorrencias/{id:guid}")]
    public async Task<ActionResult<RespostaApi<RecorrenciaDespesaResponse>>> Recorrencia(Guid id, CancellationToken ct)
    {
        var r = await sender.Send(new ObterRecorrenciaQuery(id), ct);
        var categorias = await sender.Send(new ListarCategoriasDespesaQuery(), ct);
        return Ok(RespostaApi<RecorrenciaDespesaResponse>.Ok(new(r.Id, r.Descricao, r.CategoriaDespesaId,
            categorias.Single(x => x.Id == r.CategoriaDespesaId).Nome, r.Valor, r.DiaVencimento,
            r.CompetenciaInicial, r.CompetenciaFinal, r.ProximaCompetencia, r.EhAtivo, r.Fornecedor, r.Observacao, r.Versao)));
    }
    [HttpPost("recorrencias"), Authorize(Policy = Permissoes.FinanceiroEditar)]
    public async Task<ActionResult<RespostaApi<DespesaIdResponse>>> CriarRecorrencia(SalvarRecorrenciaRequest r, CancellationToken ct) =>
        Ok(Id(await sender.Send(new CriarRecorrenciaCommand(Dados(r)), ct)));
    [HttpPut("recorrencias/{id:guid}"), Authorize(Policy = Permissoes.FinanceiroEditar)]
    public async Task<ActionResult<RespostaApi<DespesaIdResponse>>> EditarRecorrencia(Guid id, SalvarRecorrenciaRequest r, CancellationToken ct) =>
        Ok(Id(await sender.Send(new EditarRecorrenciaCommand(id, r.Versao, Dados(r)), ct)));
    [HttpPost("recorrencias/{id:guid}/atividade"), Authorize(Policy = Permissoes.FinanceiroEditar)]
    public async Task<ActionResult<RespostaApi<DespesaIdResponse>>> Atividade(Guid id, AtividadeRecorrenciaRequest r, CancellationToken ct) =>
        Ok(Id(await sender.Send(new AtividadeRecorrenciaCommand(id, r.Versao, r.Ativa), ct)));

    [HttpGet("categorias")]
    public async Task<ActionResult<RespostaApi<IReadOnlyCollection<CategoriaDespesaResponse>>>> Categorias(CancellationToken ct) =>
        Ok(RespostaApi<IReadOnlyCollection<CategoriaDespesaResponse>>.Ok((await sender.Send(new ListarCategoriasDespesaQuery(), ct))
            .Select(x => new CategoriaDespesaResponse(x.Id, x.Nome, x.Ativa, x.Versao)).ToArray()));
    [HttpPost("categorias"), Authorize(Policy = Permissoes.FinanceiroEditar)]
    public async Task<ActionResult<RespostaApi<DespesaIdResponse>>> CriarCategoria(CategoriaDespesaRequest r, CancellationToken ct) =>
        Ok(Id(await sender.Send(new CriarCategoriaDespesaCommand(r.Nome), ct)));
    [HttpPut("categorias/{id:guid}"), Authorize(Policy = Permissoes.FinanceiroEditar)]
    public async Task<ActionResult<RespostaApi<DespesaIdResponse>>> EditarCategoria(Guid id, CategoriaDespesaRequest r, CancellationToken ct) =>
        Ok(Id(await sender.Send(new EditarCategoriaDespesaCommand(id, r.Versao, r.Nome, r.Ativa), ct)));

    private static RespostaApi<DespesaIdResponse> Id(Guid id) => RespostaApi<DespesaIdResponse>.Ok(new(id), "Operação concluída.");
    private static DadosDespesa Dados(SalvarDespesaRequest r) => new(r.Descricao, r.CategoriaId, r.Valor, r.Competencia, r.Vencimento, r.Fornecedor, r.Observacao);
    private static DadosRecorrencia Dados(SalvarRecorrenciaRequest r) => new(r.Descricao, r.CategoriaId, r.Valor, r.DiaVencimento, r.CompetenciaInicial, r.CompetenciaFinal, r.Fornecedor, r.Observacao);
    private static DespesaResponse Mapear(DespesaListaResultado x) => new(x.Id, x.Descricao, x.CategoriaId, x.Categoria,
        (OrigemDespesaContrato)x.Origem, x.RecorrenciaId, x.Competencia, x.Vencimento, x.Valor,
        (StatusDespesaContrato)x.Status, x.Vencida, x.DataPagamento, x.ValorPago, x.Fornecedor, x.Observacao, x.Versao);
}
