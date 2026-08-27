using Detara.Application.Abstracoes;
using Detara.Domain.Atendimento;
using Detara.Domain.Notificacoes;
using MediatR;

namespace Detara.Application.Clientes;

public sealed record ResumoRelacionamentoClienteResultado(
    int QuantidadeAtendimentos,
    decimal TotalInvestido,
    decimal? TicketMedio,
    DateTime? UltimaVisitaEmUtc,
    string? ServicoMaisRealizado,
    int? FrequenciaRetornoDias);

public sealed record VeiculoRelacionamentoAtendimentoResultado(
    Guid VeiculoId,
    int QuantidadeAtendimentos,
    int QuantidadeServicos,
    string? UltimoServico,
    DateTime? UltimaVisitaEmUtc);

public sealed record AtendimentoRelacionamentoClienteResultado(
    Guid Id,
    string Codigo,
    Guid VeiculoId,
    string VeiculoDescricao,
    string? VeiculoPlaca,
    StatusOrdemServico Status,
    decimal TotalAutorizado,
    DateTime DataEmUtc,
    IReadOnlyCollection<string> Servicos);

public sealed record OrcamentoRelacionamentoClienteResultado(
    Guid Id,
    string? Codigo,
    Guid VeiculoId,
    string VeiculoDescricao,
    string? VeiculoPlaca,
    StatusEfetivoOrcamento Status,
    decimal Total,
    DateTime DataEmUtc,
    IReadOnlyCollection<string> Itens);

public sealed record ComunicacaoRelacionamentoClienteResultado(
    Guid Id,
    Guid? OrdemServicoId,
    CanalComunicacaoCliente Canal,
    TipoComunicacaoCliente Tipo,
    StatusComunicacaoCliente Status,
    OrigemComunicacaoCliente Origem,
    DateTime DataEmUtc);

public sealed record AtendimentoClienteRelacionamentoResultado(
    ResumoRelacionamentoClienteResultado Resumo,
    IReadOnlyCollection<VeiculoRelacionamentoAtendimentoResultado> Veiculos,
    IReadOnlyCollection<AtendimentoRelacionamentoClienteResultado> Atendimentos,
    IReadOnlyCollection<OrcamentoRelacionamentoClienteResultado> Orcamentos);

public interface IAtendimentoClienteRelacionamentoConsulta
{
    Task<AtendimentoClienteRelacionamentoResultado> ObterAsync(
        Guid clienteId,
        bool incluirAtendimentos,
        bool incluirOrcamentos,
        CancellationToken cancellationToken);
}

public interface INotificacoesClienteRelacionamentoConsulta
{
    Task<ComunicacaoRelacionamentoClienteResultado?> ObterUltimaAsync(
        Guid clienteId,
        CancellationToken cancellationToken);
}

public sealed record VeiculoRelacionamentoClienteResultado(
    VeiculoResumoClienteResultado Veiculo,
    int QuantidadeAtendimentos,
    int QuantidadeServicos,
    string? UltimoServico,
    DateTime? UltimaVisitaEmUtc);

public sealed record ClienteRelacionamentoResultado(
    ClienteDetalheResultado Cliente,
    ResumoRelacionamentoClienteResultado? Resumo,
    IReadOnlyCollection<VeiculoRelacionamentoClienteResultado> Veiculos,
    IReadOnlyCollection<AtendimentoRelacionamentoClienteResultado> Atendimentos,
    IReadOnlyCollection<OrcamentoRelacionamentoClienteResultado> Orcamentos,
    ComunicacaoRelacionamentoClienteResultado? UltimaComunicacao,
    bool PodeVisualizarAtendimentos,
    bool PodeVisualizarOrcamentos,
    bool PodeVisualizarComunicacoes);

public sealed record ObterClienteRelacionamentoQuery(
    Guid Id,
    bool IncluirAtendimentos,
    bool IncluirOrcamentos,
    bool IncluirComunicacoes) : IRequest<ClienteRelacionamentoResultado>;

internal sealed class ObterClienteRelacionamentoQueryHandler(
    IClientesRepositorio clientes,
    IAtendimentoClienteRelacionamentoConsulta atendimento,
    INotificacoesClienteRelacionamentoConsulta notificacoes)
    : IRequestHandler<ObterClienteRelacionamentoQuery, ClienteRelacionamentoResultado>
{
    public async Task<ClienteRelacionamentoResultado> Handle(
        ObterClienteRelacionamentoQuery request,
        CancellationToken cancellationToken)
    {
        var cliente = await clientes.ObterDetalheAsync(request.Id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Cliente não encontrado.");
        var dadosAtendimento = await atendimento.ObterAsync(
            request.Id,
            request.IncluirAtendimentos,
            request.IncluirOrcamentos,
            cancellationToken);
        var comunicacao = request.IncluirComunicacoes
            ? await notificacoes.ObterUltimaAsync(request.Id, cancellationToken)
            : null;
        var indicadoresPorVeiculo = dadosAtendimento.Veiculos.ToDictionary(item => item.VeiculoId);
        var veiculos = cliente.Veiculos.Select(veiculo =>
        {
            indicadoresPorVeiculo.TryGetValue(veiculo.Id, out var indicador);
            return new VeiculoRelacionamentoClienteResultado(
                veiculo,
                indicador?.QuantidadeAtendimentos ?? 0,
                indicador?.QuantidadeServicos ?? 0,
                indicador?.UltimoServico,
                indicador?.UltimaVisitaEmUtc);
        }).ToArray();

        return new ClienteRelacionamentoResultado(
            cliente,
            request.IncluirAtendimentos ? dadosAtendimento.Resumo : null,
            veiculos,
            dadosAtendimento.Atendimentos,
            dadosAtendimento.Orcamentos,
            comunicacao,
            request.IncluirAtendimentos,
            request.IncluirOrcamentos,
            request.IncluirComunicacoes);
    }
}
