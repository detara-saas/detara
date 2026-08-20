namespace Detara.Contracts.Autorizacao;

public static class Permissoes
{
    public const string ClientesVisualizar = "Clientes.Visualizar";
    public const string ClientesCriar = "Clientes.Criar";
    public const string ClientesEditar = "Clientes.Editar";
    public const string VeiculosVisualizar = "Veiculos.Visualizar";
    public const string VeiculosCriar = "Veiculos.Criar";
    public const string VeiculosEditar = "Veiculos.Editar";
    public const string ServicosVisualizar = "Servicos.Visualizar";
    public const string ServicosCriar = "Servicos.Criar";
    public const string ServicosEditar = "Servicos.Editar";
    public const string PacotesVisualizar = "Pacotes.Visualizar";
    public const string PacotesCriar = "Pacotes.Criar";
    public const string PacotesEditar = "Pacotes.Editar";
    public const string AgendaVisualizar = "Agenda.Visualizar";
    public const string AgendaCriar = "Agenda.Criar";
    public const string AgendaEditar = "Agenda.Editar";
    public const string OrcamentosVisualizar = "Orcamentos.Visualizar";
    public const string OrcamentosCriar = "Orcamentos.Criar";
    public const string OrcamentosEditar = "Orcamentos.Editar";
    public const string ConfiguracoesVisualizar = "Configuracoes.Visualizar";
    public const string ConfiguracoesEditar = "Configuracoes.Editar";
    public const string OrdemServicoVisualizar = "OrdemServico.Visualizar";
    public const string OrdemServicoCriar = "OrdemServico.Criar";
    public const string OrdemServicoEditar = "OrdemServico.Editar";
    public const string OrdemServicoFinalizar = "OrdemServico.Finalizar";
    public const string FinanceiroVisualizar = "Financeiro.Visualizar";
    public const string FinanceiroEditar = "Financeiro.Editar";
    public const string FinanceiroRegistrarPagamento = "Financeiro.RegistrarPagamento";
    public const string FinanceiroEstornarPagamento = "Financeiro.EstornarPagamento";

    public static readonly IReadOnlyCollection<string> Todas =
    [
        ClientesVisualizar,
        ClientesCriar,
        ClientesEditar,
        VeiculosVisualizar,
        VeiculosCriar,
        VeiculosEditar,
        ServicosVisualizar,
        ServicosCriar,
        ServicosEditar,
        PacotesVisualizar,
        PacotesCriar,
        PacotesEditar,
        AgendaVisualizar,
        AgendaCriar,
        AgendaEditar,
        OrcamentosVisualizar,
        OrcamentosCriar,
        OrcamentosEditar,
        ConfiguracoesVisualizar,
        ConfiguracoesEditar,
        OrdemServicoVisualizar,
        OrdemServicoCriar,
        OrdemServicoEditar,
        OrdemServicoFinalizar,
        FinanceiroVisualizar,
        FinanceiroEditar,
        FinanceiroRegistrarPagamento,
        FinanceiroEstornarPagamento
    ];
}
