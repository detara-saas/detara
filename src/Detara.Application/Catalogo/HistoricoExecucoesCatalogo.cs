using Detara.Domain.Atendimento;
using Detara.Domain.Catalogo;

namespace Detara.Application.Catalogo;

public sealed record ExecucaoItemCatalogoResultado(Guid OrdemServicoId, string OrdemServicoCodigo,
    DateTime ExecutadaEmUtc, string ClienteNome, string VeiculoDescricao, string? VeiculoPlaca,
    decimal ValorUnitario, int Quantidade, StatusOrdemServico Status);

public interface IHistoricoExecucoesCatalogoConsulta
{
    Task<IReadOnlyCollection<ExecucaoItemCatalogoResultado>> ListarAsync(Guid empresaId,
        TipoItemOrcamento tipoItem, Guid itemCatalogoId, int limite, CancellationToken cancellationToken);
}

public sealed record ServicoDetalheVisualizacao(ServicoDetalheResultado Servico,
    IReadOnlyCollection<ExecucaoItemCatalogoResultado> Execucoes);

public sealed record PacoteDetalheVisualizacao(PacoteDetalheResultado Pacote,
    IReadOnlyCollection<ExecucaoItemCatalogoResultado> Execucoes);
