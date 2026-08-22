using Detara.Application.Abstracoes;
using Detara.Domain.Agenda;
using FluentValidation;
using MediatR;
using System.Linq.Expressions;

namespace Detara.Application.Agenda;

public sealed record ItemAgendamentoEntrada(TipoItemAgendamento TipoItem, Guid ItemCatalogoId);
public sealed record CriarAgendamentoCommand(Guid ClienteId, Guid VeiculoId, DateTime InicioLocal, int DuracaoPlanejadaMinutos, string? ObservacaoSolicitante, string? ObservacaoInterna, IReadOnlyCollection<ItemAgendamentoEntrada> Itens) : IRequest<AgendamentoDetalheVisualizacao>;
public sealed record AtualizarAgendamentoCommand(Guid Id, Guid ClienteId, Guid VeiculoId, DateTime InicioLocal, int DuracaoPlanejadaMinutos, string? ObservacaoSolicitante, string? ObservacaoInterna, IReadOnlyCollection<ItemAgendamentoEntrada> Itens) : IRequest<AgendamentoDetalheVisualizacao>;
public sealed record ReagendarAgendamentoCommand(Guid Id, DateTime InicioLocal, int DuracaoPlanejadaMinutos) : IRequest<AgendamentoDetalheVisualizacao>;
public sealed record ObterAgendamentoQuery(Guid Id) : IRequest<AgendamentoDetalheVisualizacao>;
public sealed record ListarAgendaPeriodoQuery(FiltroAgendaPeriodo Filtro) : IRequest<IReadOnlyCollection<AgendamentoPeriodoVisualizacao>>;
public sealed record ListarHistoricoAgendamentosQuery(FiltroHistoricoAgendamentos Filtro) : IRequest<PaginacaoResultado<AgendamentoListaVisualizacao>>;
public sealed record BuscarClientesAgendaQuery(string Pesquisa, int Limite = 15) : IRequest<IReadOnlyCollection<ClienteAgendaInterno>>;
public sealed record ListarVeiculosAgendaQuery(Guid ClienteId, bool IncluirInativos = false) : IRequest<IReadOnlyCollection<VeiculoAgendaInterno>>;
public sealed record BuscarCatalogoAgendaQuery(string? Pesquisa, bool IncluirInativos = false, int Limite = 30) : IRequest<IReadOnlyCollection<ItemCatalogoAgendaInterno>>;
public sealed record ContarSobreposicoesAgendaQuery(DateTime InicioLocal, int DuracaoPlanejadaMinutos, Guid? IgnorarAgendamentoId) : IRequest<int>;
public sealed record ObterContextoAgendaQuery : IRequest<ContextoAgendaVisualizacao>;

public sealed record AgendamentoPeriodoVisualizacao(AgendamentoPeriodoResultado Agendamento, DateTime InicioLocal);
public sealed record AgendamentoListaVisualizacao(AgendamentoListaResultado Agendamento, DateTime InicioLocal);
public sealed record AgendamentoItemVisualizacao(AgendamentoItemResultado Item, bool ItemAtivoNoCatalogo);
public sealed record AgendamentoDetalheVisualizacao(AgendamentoDetalheResultado Agendamento, DateTime InicioLocal, string FusoHorario, int QuantidadeSobreposicoes, IReadOnlyCollection<AgendamentoItemVisualizacao> Itens);
public sealed record ContextoAgendaVisualizacao(string FusoHorario, DateOnly HojeLocal, DateTime AgoraLocal);

internal abstract class SalvarAgendamentoValidatorBase<T> : AbstractValidator<T>
{
    protected void Regras(Expression<Func<T, Guid>> cliente, Expression<Func<T, Guid>> veiculo, Expression<Func<T, DateTime>> inicio, Expression<Func<T, int>> duracao, Expression<Func<T, string?>> observacaoSolicitante, Expression<Func<T, string?>> observacaoInterna, Expression<Func<T, IEnumerable<ItemAgendamentoEntrada>>> itens)
    {
        RuleFor(cliente).NotEmpty(); RuleFor(veiculo).NotEmpty();
        RuleFor(inicio).NotEmpty(); RuleFor(duracao).InclusiveBetween(1, 43200);
        RuleFor(observacaoSolicitante).MaximumLength(2000); RuleFor(observacaoInterna).MaximumLength(4000);
        RuleFor(itens).NotEmpty().Must(x => x.Select(i => (i.TipoItem, i.ItemCatalogoId)).Distinct().Count() == x.Count()).WithMessage("Os itens não podem se repetir.");
        RuleForEach<ItemAgendamentoEntrada>(itens).ChildRules(item => { item.RuleFor(i => i.TipoItem).IsInEnum(); item.RuleFor(i => i.ItemCatalogoId).NotEmpty(); });
    }
}
internal sealed class CriarAgendamentoValidator : SalvarAgendamentoValidatorBase<CriarAgendamentoCommand> { public CriarAgendamentoValidator() => Regras(x => x.ClienteId, x => x.VeiculoId, x => x.InicioLocal, x => x.DuracaoPlanejadaMinutos, x => x.ObservacaoSolicitante, x => x.ObservacaoInterna, x => x.Itens); }
internal sealed class AtualizarAgendamentoValidator : SalvarAgendamentoValidatorBase<AtualizarAgendamentoCommand> { public AtualizarAgendamentoValidator() { RuleFor(x => x.Id).NotEmpty(); Regras(x => x.ClienteId, x => x.VeiculoId, x => x.InicioLocal, x => x.DuracaoPlanejadaMinutos, x => x.ObservacaoSolicitante, x => x.ObservacaoInterna, x => x.Itens); } }
internal sealed class ReagendarAgendamentoValidator : AbstractValidator<ReagendarAgendamentoCommand> { public ReagendarAgendamentoValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.InicioLocal).NotEmpty(); RuleFor(x => x.DuracaoPlanejadaMinutos).InclusiveBetween(1, 43200); } }
internal sealed class ListarAgendaPeriodoValidator : AbstractValidator<ListarAgendaPeriodoQuery> { public ListarAgendaPeriodoValidator() { RuleFor(x => x.Filtro.FimUtc).GreaterThan(x => x.Filtro.InicioUtc); RuleFor(x => x.Filtro).Must(x => x.FimUtc - x.InicioUtc <= TimeSpan.FromDays(31)).WithMessage("O período máximo da agenda é de 31 dias."); RuleFor(x => x.Filtro.Pesquisa).MaximumLength(160); } }
internal sealed class ListarHistoricoValidator : AbstractValidator<ListarHistoricoAgendamentosQuery> { public ListarHistoricoValidator() { RuleFor(x => x.Filtro.Pagina).GreaterThanOrEqualTo(1); RuleFor(x => x.Filtro.TamanhoPagina).Must(x => x is 10 or 25 or 50); RuleFor(x => x.Filtro.Pesquisa).MaximumLength(160); } }

internal sealed class CriarAgendamentoHandler(IUsuarioContexto usuario, IClientesAgendaConsulta clientes, ICatalogoAgendaConsulta catalogo, IFusoHorarioEmpresaConsulta fusos, IConversorFusoHorario conversor, IAgendaRepositorio agenda) : IRequestHandler<CriarAgendamentoCommand, AgendamentoDetalheVisualizacao>
{
    public async Task<AgendamentoDetalheVisualizacao> Handle(CriarAgendamentoCommand request, CancellationToken ct)
    {
        var clienteVeiculo = await AgendaFluxo.ValidarClienteVeiculoAsync(clientes, usuario.EmpresaId, request.ClienteId, request.VeiculoId, exigirAtivos: true, ct);
        var snapshots = await AgendaFluxo.PrepararItensAsync(catalogo, usuario.EmpresaId, request.Itens, new Dictionary<(TipoItemAgendamento, Guid), ItemAgendamentoSnapshot>(), exigirAtivosNovos: true, ct);
        var fuso = await AgendaFluxo.ObterFusoAsync(fusos, usuario.EmpresaId, ct);
        var entidade = new Agendamento(usuario.EmpresaId, request.ClienteId, clienteVeiculo.Cliente.Nome, request.VeiculoId, clienteVeiculo.Veiculo.Descricao, clienteVeiculo.Veiculo.Placa, conversor.ParaUtc(request.InicioLocal, fuso), request.DuracaoPlanejadaMinutos, request.ObservacaoSolicitante, request.ObservacaoInterna, snapshots);
        agenda.Adicionar(entidade); await agenda.SalvarAsync(ct);
        return await AgendaFluxo.ObterDetalheAsync(entidade.Id, usuario.EmpresaId, agenda, catalogo, fusos, conversor, ct);
    }
}

internal sealed class AtualizarAgendamentoHandler(IUsuarioContexto usuario, ICatalogoAgendaConsulta catalogo, IFusoHorarioEmpresaConsulta fusos, IConversorFusoHorario conversor, IAgendaRepositorio agenda) : IRequestHandler<AtualizarAgendamentoCommand, AgendamentoDetalheVisualizacao>
{
    public async Task<AgendamentoDetalheVisualizacao> Handle(AtualizarAgendamentoCommand request, CancellationToken ct)
    {
        var entidade = await agenda.ObterParaAlteracaoAsync(request.Id, ct) ?? throw new RecursoNaoEncontradoException("Agendamento não encontrado.");
        if (entidade.ClienteId != request.ClienteId || entidade.VeiculoId != request.VeiculoId) throw new ConflitoRegraNegocioException("Cliente e veículo não podem ser trocados na edição. Crie outro agendamento.");
        var antigos = entidade.Itens.ToDictionary(x => (x.TipoItem, x.ItemCatalogoId), x => new ItemAgendamentoSnapshot(x.TipoItem, x.ItemCatalogoId, x.NomeSnapshot, x.DescricaoSnapshot, x.TipoPrecificacaoSnapshot, x.PrecoReferenciaSnapshot, x.DuracaoReferenciaMinutosSnapshot));
        var snapshots = await AgendaFluxo.PrepararItensAsync(catalogo, usuario.EmpresaId, request.Itens, antigos, exigirAtivosNovos: true, ct);
        var fuso = await AgendaFluxo.ObterFusoAsync(fusos, usuario.EmpresaId, ct);
        agenda.RemoverItensAtuais(entidade);
        entidade.AtualizarPlanejamento(conversor.ParaUtc(request.InicioLocal, fuso), request.DuracaoPlanejadaMinutos, request.ObservacaoSolicitante, request.ObservacaoInterna, snapshots);
        agenda.AdicionarItensAtuais(entidade); await agenda.SalvarAsync(ct);
        return await AgendaFluxo.ObterDetalheAsync(entidade.Id, usuario.EmpresaId, agenda, catalogo, fusos, conversor, ct);
    }
}

internal sealed class ReagendarAgendamentoHandler(IUsuarioContexto usuario, IFusoHorarioEmpresaConsulta fusos, IConversorFusoHorario conversor, IAgendaRepositorio agenda, ICatalogoAgendaConsulta catalogo) : IRequestHandler<ReagendarAgendamentoCommand, AgendamentoDetalheVisualizacao>
{
    public async Task<AgendamentoDetalheVisualizacao> Handle(ReagendarAgendamentoCommand request, CancellationToken ct) { var entidade = await agenda.ObterParaAlteracaoAsync(request.Id, ct) ?? throw new RecursoNaoEncontradoException("Agendamento não encontrado."); var fuso = await AgendaFluxo.ObterFusoAsync(fusos, usuario.EmpresaId, ct); entidade.Reagendar(conversor.ParaUtc(request.InicioLocal, fuso), request.DuracaoPlanejadaMinutos); await agenda.SalvarAsync(ct); return await AgendaFluxo.ObterDetalheAsync(entidade.Id, usuario.EmpresaId, agenda, catalogo, fusos, conversor, ct); }
}
internal sealed class ObterAgendamentoHandler(IUsuarioContexto usuario, IAgendaRepositorio agenda, ICatalogoAgendaConsulta catalogo, IFusoHorarioEmpresaConsulta fusos, IConversorFusoHorario conversor) : IRequestHandler<ObterAgendamentoQuery, AgendamentoDetalheVisualizacao> { public Task<AgendamentoDetalheVisualizacao> Handle(ObterAgendamentoQuery request, CancellationToken ct) => AgendaFluxo.ObterDetalheAsync(request.Id, usuario.EmpresaId, agenda, catalogo, fusos, conversor, ct); }

internal sealed class ListarAgendaPeriodoHandler(IUsuarioContexto usuario, IAgendaRepositorio agenda, IFusoHorarioEmpresaConsulta fusos, IConversorFusoHorario conversor) : IRequestHandler<ListarAgendaPeriodoQuery, IReadOnlyCollection<AgendamentoPeriodoVisualizacao>>
{ public async Task<IReadOnlyCollection<AgendamentoPeriodoVisualizacao>> Handle(ListarAgendaPeriodoQuery request, CancellationToken ct) { var fuso = await AgendaFluxo.ObterFusoAsync(fusos, usuario.EmpresaId, ct); var itens = await agenda.ListarPeriodoAsync(request.Filtro, ct); return itens.Select(x => new AgendamentoPeriodoVisualizacao(x, conversor.ParaLocal(x.InicioUtc, fuso))).ToArray(); } }
internal sealed class ListarHistoricoHandler(IUsuarioContexto usuario, IAgendaRepositorio agenda, IFusoHorarioEmpresaConsulta fusos, IConversorFusoHorario conversor) : IRequestHandler<ListarHistoricoAgendamentosQuery, PaginacaoResultado<AgendamentoListaVisualizacao>>
{ public async Task<PaginacaoResultado<AgendamentoListaVisualizacao>> Handle(ListarHistoricoAgendamentosQuery request, CancellationToken ct) { var fuso = await AgendaFluxo.ObterFusoAsync(fusos, usuario.EmpresaId, ct); var pagina = await agenda.ListarHistoricoAsync(request.Filtro, ct); return new(pagina.Itens.Select(x => new AgendamentoListaVisualizacao(x, conversor.ParaLocal(x.InicioUtc, fuso))).ToArray(), pagina.Pagina, pagina.TamanhoPagina, pagina.TotalItens); } }
internal sealed class BuscarClientesAgendaHandler(IUsuarioContexto usuario, IClientesAgendaConsulta clientes) : IRequestHandler<BuscarClientesAgendaQuery, IReadOnlyCollection<ClienteAgendaInterno>> { public Task<IReadOnlyCollection<ClienteAgendaInterno>> Handle(BuscarClientesAgendaQuery request, CancellationToken ct) => clientes.BuscarClientesAsync(usuario.EmpresaId, request.Pesquisa.Trim(), request.Limite, ct); }
internal sealed class ListarVeiculosAgendaHandler(IUsuarioContexto usuario, IClientesAgendaConsulta clientes) : IRequestHandler<ListarVeiculosAgendaQuery, IReadOnlyCollection<VeiculoAgendaInterno>> { public Task<IReadOnlyCollection<VeiculoAgendaInterno>> Handle(ListarVeiculosAgendaQuery request, CancellationToken ct) => clientes.ListarVeiculosAsync(usuario.EmpresaId, request.ClienteId, request.IncluirInativos, ct); }
internal sealed class BuscarCatalogoAgendaHandler(IUsuarioContexto usuario, ICatalogoAgendaConsulta catalogo) : IRequestHandler<BuscarCatalogoAgendaQuery, IReadOnlyCollection<ItemCatalogoAgendaInterno>> { public Task<IReadOnlyCollection<ItemCatalogoAgendaInterno>> Handle(BuscarCatalogoAgendaQuery request, CancellationToken ct) => catalogo.BuscarItensAsync(usuario.EmpresaId, request.Pesquisa, request.IncluirInativos, request.Limite, ct); }
internal sealed class ContarSobreposicoesHandler(IUsuarioContexto usuario, IFusoHorarioEmpresaConsulta fusos, IConversorFusoHorario conversor, IAgendaRepositorio agenda) : IRequestHandler<ContarSobreposicoesAgendaQuery, int> { public async Task<int> Handle(ContarSobreposicoesAgendaQuery request, CancellationToken ct) { var fuso = await AgendaFluxo.ObterFusoAsync(fusos, usuario.EmpresaId, ct); var inicio = conversor.ParaUtc(request.InicioLocal, fuso); return await agenda.ContarSobreposicoesAsync(inicio, inicio.AddMinutes(request.DuracaoPlanejadaMinutos), request.IgnorarAgendamentoId, ct); } }
internal sealed class ObterContextoAgendaHandler(IUsuarioContexto usuario, IFusoHorarioEmpresaConsulta fusos, IConversorFusoHorario conversor) : IRequestHandler<ObterContextoAgendaQuery, ContextoAgendaVisualizacao> { public async Task<ContextoAgendaVisualizacao> Handle(ObterContextoAgendaQuery request, CancellationToken ct) { var fuso = await AgendaFluxo.ObterFusoAsync(fusos, usuario.EmpresaId, ct); var agoraLocal = conversor.ParaLocal(DateTime.UtcNow, fuso); return new(fuso, DateOnly.FromDateTime(agoraLocal), agoraLocal); } }

internal static class AgendaFluxo
{
    public static async Task<ClienteVeiculoAgendaInterno> ValidarClienteVeiculoAsync(IClientesAgendaConsulta consulta, Guid empresaId, Guid clienteId, Guid veiculoId, bool exigirAtivos, CancellationToken ct)
    { var resultado = await consulta.ObterClienteVeiculoAsync(empresaId, clienteId, veiculoId, ct) ?? throw new RecursoNaoEncontradoException("Cliente ou veículo não encontrado na empresa atual."); if (resultado.Veiculo.ClienteId != clienteId) throw new ConflitoRegraNegocioException("O veículo não pertence ao cliente selecionado."); if (exigirAtivos && (!resultado.Cliente.EhAtivo || !resultado.Veiculo.EhAtivo)) throw new ConflitoRegraNegocioException("Cliente e veículo devem estar ativos para um novo agendamento."); return resultado; }

    public static async Task<IReadOnlyCollection<ItemAgendamentoSnapshot>> PrepararItensAsync(ICatalogoAgendaConsulta catalogo, Guid empresaId, IReadOnlyCollection<ItemAgendamentoEntrada> entradas, IReadOnlyDictionary<(TipoItemAgendamento, Guid), ItemAgendamentoSnapshot> snapshotsExistentes, bool exigirAtivosNovos, CancellationToken ct)
    {
        var novos = entradas.Where(x => !snapshotsExistentes.ContainsKey((x.TipoItem, x.ItemCatalogoId))).ToArray();
        var encontrados = novos.Length == 0 ? [] : await catalogo.ObterItensAsync(empresaId, novos.Select(x => (x.TipoItem, x.ItemCatalogoId)).ToArray(), ct);
        var mapa = encontrados.ToDictionary(x => (x.TipoItem, x.Id));
        var resultado = new List<ItemAgendamentoSnapshot>(entradas.Count);
        foreach (var entrada in entradas)
        {
            if (snapshotsExistentes.TryGetValue((entrada.TipoItem, entrada.ItemCatalogoId), out var snapshot)) { resultado.Add(snapshot); continue; }
            if (!mapa.TryGetValue((entrada.TipoItem, entrada.ItemCatalogoId), out var item)) throw new RecursoNaoEncontradoException("Um ou mais itens do catálogo não existem na empresa atual.");
            if (exigirAtivosNovos && !item.EhAtivo) throw new ConflitoRegraNegocioException($"{item.Nome} está inativo e não pode ser incluído em um novo agendamento.");
            resultado.Add(new ItemAgendamentoSnapshot(item.TipoItem, item.Id, item.Nome, item.Descricao, item.TipoPrecificacao, item.PrecoReferencia, item.DuracaoReferenciaMinutos));
        }
        return resultado;
    }

    public static async Task<string> ObterFusoAsync(IFusoHorarioEmpresaConsulta fusos, Guid empresaId, CancellationToken ct) => await fusos.ObterAsync(empresaId, ct) ?? throw new RecursoNaoEncontradoException("Empresa não encontrada para determinar o fuso horário.");

    public static async Task<AgendamentoDetalheVisualizacao> ObterDetalheAsync(Guid id, Guid empresaId, IAgendaRepositorio agenda, ICatalogoAgendaConsulta catalogo, IFusoHorarioEmpresaConsulta fusos, IConversorFusoHorario conversor, CancellationToken ct)
    {
        var detalhe = await agenda.ObterDetalheAsync(id, ct) ?? throw new RecursoNaoEncontradoException("Agendamento não encontrado.");
        var fuso = await ObterFusoAsync(fusos, empresaId, ct);
        var catalogoAtual = await catalogo.ObterItensAsync(empresaId, detalhe.Itens.Select(x => (x.TipoItem, x.ItemCatalogoId)).ToArray(), ct);
        var ativos = catalogoAtual.ToDictionary(x => (x.TipoItem, x.Id), x => x.EhAtivo);
        var itens = detalhe.Itens.Select(x => new AgendamentoItemVisualizacao(x, ativos.GetValueOrDefault((x.TipoItem, x.ItemCatalogoId)))).ToArray();
        var sobreposicoes = await agenda.ContarSobreposicoesAsync(detalhe.InicioUtc, detalhe.InicioUtc.AddMinutes(detalhe.DuracaoPlanejadaMinutos), detalhe.Id, ct);
        return new(detalhe, conversor.ParaLocal(detalhe.InicioUtc, fuso), fuso, sobreposicoes, itens);
    }
}
