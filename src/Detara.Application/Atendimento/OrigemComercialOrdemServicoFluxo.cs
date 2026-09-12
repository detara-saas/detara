using Detara.Application.Abstracoes;
using Detara.Domain.Atendimento;
using MediatR;

namespace Detara.Application.Atendimento;

public sealed record ObterOrigemComercialOrdemServicoQuery(Guid AgendamentoId) : IRequest<Guid?>;

internal sealed class ObterOrigemComercialOrdemServicoHandler(IUsuarioContexto usuario,
    IAgendaAtendimentoIntegracao agenda, IOrcamentosRepositorio orcamentos)
    : IRequestHandler<ObterOrigemComercialOrdemServicoQuery, Guid?>
{
    public async Task<Guid?> Handle(ObterOrigemComercialOrdemServicoQuery request, CancellationToken ct)
    {
        var agendamento = await agenda.ObterAsync(usuario.EmpresaId, request.AgendamentoId, ct)
            ?? throw new RecursoNaoEncontradoException("Agendamento não encontrado.");
        return (await OrigemComercialOrdemServicoFluxo.ResolverAsync(orcamentos, agendamento, null, ct))?.Id;
    }
}

internal static class OrigemComercialOrdemServicoFluxo
{
    // Repositório scoped pelo tenant; a consulta exclui adicionais e considera o vínculo
    // operacional atual, inclusive quando o orçamento foi aprovado antes de agendar.
    public static async Task<OrcamentoDetalheResultado?> ResolverAsync(IOrcamentosRepositorio orcamentos,
        AgendamentoAtendimentoInterno agendamento, Guid? orcamentoSolicitadoId, CancellationToken ct)
    {
        OrcamentoDetalheResultado? solicitado = null;
        if (orcamentoSolicitadoId.HasValue)
        {
            solicitado = await orcamentos.ObterDetalheAsync(orcamentoSolicitadoId.Value, ct)
                ?? throw new RecursoNaoEncontradoException("Orçamento não encontrado.");
            Validar(solicitado, agendamento);
        }

        var aprovados = (await orcamentos.ListarPorAgendamentoAsync(agendamento.Id, ct))
            .Where(item => item.Status == StatusOrcamento.Aprovado).ToArray();
        if (aprovados.Length > 1)
            throw new ConflitoRegraNegocioException(
                "Há mais de um orçamento aprovado para este agendamento. Revise as propostas antes de criar a ordem de serviço.");
        if (aprovados.Length == 0)
        {
            if (solicitado is not null)
                throw new ConflitoRegraNegocioException("O orçamento mudou. Atualize a página e confira a proposta aprovada.");
            return null;
        }

        if (solicitado is not null && solicitado.Id != aprovados[0].Id)
            throw new ConflitoRegraNegocioException("O orçamento mudou. Atualize a página e confira a proposta aprovada.");
        var selecionado = solicitado ?? await orcamentos.ObterDetalheAsync(aprovados[0].Id, ct)
            ?? throw new RecursoNaoEncontradoException("Orçamento não encontrado.");
        Validar(selecionado, agendamento);
        return selecionado;
    }

    private static void Validar(OrcamentoDetalheResultado orcamento, AgendamentoAtendimentoInterno agendamento)
    {
        if (orcamento.Status != StatusOrcamento.Aprovado)
            throw new ConflitoRegraNegocioException("Somente um orçamento aprovado pode originar uma ordem de serviço.");
        if (orcamento.OrdemServicoOrigemId.HasValue)
            throw new ConflitoRegraNegocioException("Um orçamento adicional não pode originar outra ordem de serviço.");
        if (orcamento.AgendamentoId != agendamento.Id)
            throw new ConflitoRegraNegocioException("O orçamento aprovado não pertence a este agendamento.");
        if (orcamento.ClienteId != agendamento.ClienteId || orcamento.VeiculoId != agendamento.VeiculoId)
            throw new ConflitoRegraNegocioException("Cliente e veículo do orçamento divergem do agendamento.");
        if (!orcamento.AprovadoEmUtc.HasValue || !orcamento.AprovadoPorUsuarioId.HasValue)
            throw new ConflitoRegraNegocioException("O orçamento aprovado não possui os dados de autorização. Revise a proposta.");
    }
}
