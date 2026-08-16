using Detara.Domain.Agenda;
using Detara.Domain.Atendimento;
using Detara.Domain.Catalogo;

namespace Detara.Application.Atendimento;

public sealed record ClienteAtendimentoInterno(Guid Id, string Nome, string? Documento, string? Telefone, bool EhAtivo);
public sealed record VeiculoAtendimentoInterno(Guid Id, Guid ClienteId, string Descricao, string Placa, bool EhAtivo);
public sealed record ClienteVeiculoAtendimentoInterno(ClienteAtendimentoInterno Cliente, VeiculoAtendimentoInterno Veiculo);

public interface IClientesAtendimentoConsulta
{
    Task<ClienteVeiculoAtendimentoInterno?> ObterClienteVeiculoAsync(Guid empresaId, Guid clienteId, Guid veiculoId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ClienteAtendimentoInterno>> BuscarClientesAsync(Guid empresaId, string pesquisa, int limite, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<VeiculoAtendimentoInterno>> ListarVeiculosAsync(Guid empresaId, Guid clienteId, CancellationToken cancellationToken);
}

public sealed record ItemCatalogoAtendimentoInterno(TipoItemOrcamento TipoItem, Guid Id, string Nome, string? Descricao,
    TipoPrecificacao TipoPrecificacao, decimal? PrecoReferencia, bool EhAtivo);

public interface ICatalogoAtendimentoConsulta
{
    Task<IReadOnlyCollection<ItemCatalogoAtendimentoInterno>> ObterItensAsync(Guid empresaId, IReadOnlyCollection<(TipoItemOrcamento Tipo, Guid Id)> itens, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ItemCatalogoAtendimentoInterno>> BuscarItensAsync(Guid empresaId, string? pesquisa, int limite, CancellationToken cancellationToken);
}

public sealed record ItemAgendamentoAtendimentoInterno(TipoItemOrcamento TipoItem, Guid ItemCatalogoId, string Nome, string? Descricao,
    TipoPrecificacao TipoPrecificacao, decimal? PrecoReferencia);
public sealed record AgendamentoAtendimentoInterno(Guid Id, Guid ClienteId, string ClienteNome, Guid VeiculoId, string VeiculoDescricao,
    string VeiculoPlaca, IReadOnlyCollection<ItemAgendamentoAtendimentoInterno> Itens);

public interface IAgendaAtendimentoConsulta
{
    Task<AgendamentoAtendimentoInterno?> ObterAsync(Guid empresaId, Guid agendamentoId, CancellationToken cancellationToken);
}

public sealed record EmpresaAtendimentoInterno(Guid Id, string NomeFantasia, string RazaoSocial, string CpfCnpj,
    string? Email, string? Telefone, string FusoHorario);

public interface IPlataformaAtendimentoConsulta
{
    Task<EmpresaAtendimentoInterno?> ObterEmpresaAsync(Guid empresaId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, string>> ObterNomesUsuariosAsync(Guid empresaId, IReadOnlyCollection<Guid> usuarioIds, CancellationToken cancellationToken);
}

public interface IOrcamentoPdfGenerator
{
    byte[] Gerar(DocumentoPdfOrcamento documento);
}

public sealed record DocumentoPdfOrcamento(EmpresaAtendimentoInterno Empresa, OrcamentoDetalheVisualizacao Orcamento);
