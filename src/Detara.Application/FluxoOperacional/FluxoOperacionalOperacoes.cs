using Detara.Application.Abstracoes;
using Detara.Application.Agenda;
using Detara.Application.Atendimento;
using Detara.Domain.Agenda;
using Detara.Domain.Atendimento;
using FluentValidation;
using MediatR;

namespace Detara.Application.FluxoOperacional;

public sealed record AlterarStatusAgendaOperacionalCommand(Guid Id, StatusAgendamento Status,
    string? MotivoCancelamento) : IRequest<AgendamentoDetalheVisualizacao>;

internal sealed class AlterarStatusAgendaOperacionalValidator
    : AbstractValidator<AlterarStatusAgendaOperacionalCommand>
{
    public AlterarStatusAgendaOperacionalValidator()
    {
        RuleFor(item => item.Id).NotEmpty();
        RuleFor(item => item.Status).IsInEnum();
        RuleFor(item => item.MotivoCancelamento).MaximumLength(1000);
    }
}

internal sealed class AlterarStatusAgendaOperacionalHandler(IUsuarioContexto usuario,
    IAgendaRepositorio agenda, ICatalogoAgendaConsulta catalogo, IFusoHorarioEmpresaConsulta fusos,
    IConversorFusoHorario conversor, IOrdensServicoRepositorio ordens)
    : IRequestHandler<AlterarStatusAgendaOperacionalCommand, AgendamentoDetalheVisualizacao>
{
    public async Task<AgendamentoDetalheVisualizacao> Handle(
        AlterarStatusAgendaOperacionalCommand request, CancellationToken ct)
    {
        var entidade = await agenda.ObterParaAlteracaoAsync(request.Id, ct)
            ?? throw new RecursoNaoEncontradoException("Agendamento não encontrado.");
        var ordem = await ordens.ObterPorAgendamentoAsync(request.Id, ct);
        if (request.Status == StatusAgendamento.Compareceu && ordem?.Status == StatusOrdemServico.Aberta)
            throw new ConflitoRegraNegocioException("Inicie a Ordem de Serviço para colocar o agendamento em atendimento.");
        if (request.Status == StatusAgendamento.Cancelado && ordem is not null
            && ordem.Status != StatusOrdemServico.Cancelada)
            throw new ConflitoRegraNegocioException("Cancele a Ordem de Serviço antes de cancelar este agendamento.");
        if (request.Status == StatusAgendamento.Concluido && ordem?.Status != StatusOrdemServico.Concluida)
            throw new ConflitoRegraNegocioException("A conclusão do atendimento deve ser registrada pela Ordem de Serviço.");
        try { entidade.AlterarStatus(request.Status, request.MotivoCancelamento); }
        catch (InvalidOperationException exception) { throw new ConflitoRegraNegocioException(exception.Message); }
        await agenda.SalvarAsync(ct);
        return await AgendaFluxo.ObterDetalheAsync(entidade.Id, usuario.EmpresaId, agenda, catalogo,
            fusos, conversor, ct);
    }
}

public sealed record AgendarOrcamentoCommand(Guid OrcamentoId, DateTime InicioLocal,
    int DuracaoPlanejadaMinutos, string? ObservacaoSolicitante, string? ObservacaoInterna)
    : IRequest<AgendamentoAtendimentoInterno>;

internal sealed class AgendarOrcamentoValidator : AbstractValidator<AgendarOrcamentoCommand>
{
    public AgendarOrcamentoValidator()
    {
        RuleFor(item => item.OrcamentoId).NotEmpty();
        RuleFor(item => item.InicioLocal).NotEmpty();
        RuleFor(item => item.DuracaoPlanejadaMinutos).InclusiveBetween(1, 43200);
        RuleFor(item => item.ObservacaoSolicitante).MaximumLength(2000);
        RuleFor(item => item.ObservacaoInterna).MaximumLength(4000);
    }
}

internal sealed class AgendarOrcamentoHandler(IUsuarioContexto usuario, IOrcamentosRepositorio orcamentos,
    IAgendaAtendimentoIntegracao agenda, IPlataformaAtendimentoConsulta plataforma,
    IConversorFusoHorario conversor) : IRequestHandler<AgendarOrcamentoCommand, AgendamentoAtendimentoInterno>
{
    public async Task<AgendamentoAtendimentoInterno> Handle(AgendarOrcamentoCommand request, CancellationToken ct)
    {
        var orcamento = await orcamentos.ObterParaAlteracaoAsync(request.OrcamentoId, ct)
            ?? throw new RecursoNaoEncontradoException("Orçamento não encontrado.");
        if (orcamento.Status != StatusOrcamento.Aprovado)
            throw new ConflitoRegraNegocioException("Somente um orçamento aprovado pode ser agendado.");
        if (orcamento.OrdemServicoOrigemId.HasValue)
            throw new ConflitoRegraNegocioException("Um orçamento adicional não pode originar um agendamento.");
        if (orcamento.AgendamentoId.HasValue)
            throw new ConflitoRegraNegocioException("Este orçamento já está vinculado a um agendamento.");

        var empresa = await plataforma.ObterEmpresaAsync(usuario.EmpresaId, ct)
            ?? throw new RecursoNaoEncontradoException("Empresa não encontrada.");
        var itens = orcamento.Itens
            .Where(item => item.ItemCatalogoId.HasValue
                && item.TipoItem is TipoItemOrcamento.Servico or TipoItemOrcamento.Pacote)
            .Select(item => new ItemAgendamentoAtendimentoInterno(item.TipoItem,
                item.ItemCatalogoId!.Value, item.NomeSnapshot, item.DescricaoSnapshot,
                item.TipoPrecificacaoReferenciaSnapshot
                    ?? throw new ConflitoRegraNegocioException("Um item de catálogo do orçamento não possui referência de precificação."),
                item.PrecoReferenciaSnapshot))
            .ToArray();
        var criado = await agenda.AdicionarDeOrcamentoAsync(usuario.EmpresaId,
            new(orcamento.Id, orcamento.ClienteId, orcamento.ClienteNomeSnapshot,
                orcamento.VeiculoId, orcamento.VeiculoDescricaoSnapshot, orcamento.VeiculoPlacaSnapshot,
                conversor.ParaUtc(request.InicioLocal, empresa.FusoHorario), request.DuracaoPlanejadaMinutos,
                request.ObservacaoSolicitante, request.ObservacaoInterna, itens), ct);
        OrcamentoFluxo.ExecutarRegra(() => orcamento.VincularAgendamento(criado.Id));
        await orcamentos.SalvarAsync(ct);
        return criado;
    }
}
