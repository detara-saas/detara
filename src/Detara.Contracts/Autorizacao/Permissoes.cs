namespace Detara.Contracts.Autorizacao;

public static class Permissoes
{
    public sealed record Definicao(string Codigo, string Descricao);

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
    public const string NotificacoesReenviar = "Notificacoes.Reenviar";
    public const string AdministracaoUsuario = "Administracao.Usuario";

    public static readonly IReadOnlyCollection<Definicao> Definicoes =
    [
        new(ClientesVisualizar, "Visualizar clientes"),
        new(ClientesCriar, "Criar clientes"),
        new(ClientesEditar, "Editar clientes"),
        new(VeiculosVisualizar, "Visualizar veículos"),
        new(VeiculosCriar, "Criar veículos"),
        new(VeiculosEditar, "Editar veículos"),
        new(ServicosVisualizar, "Visualizar serviços"),
        new(ServicosCriar, "Criar serviços e categorias"),
        new(ServicosEditar, "Editar serviços e categorias"),
        new(PacotesVisualizar, "Visualizar pacotes"),
        new(PacotesCriar, "Criar pacotes"),
        new(PacotesEditar, "Editar pacotes"),
        new(AgendaVisualizar, "Visualizar agenda"),
        new(AgendaCriar, "Criar agendamentos"),
        new(AgendaEditar, "Editar agenda"),
        new(OrcamentosVisualizar, "Visualizar orçamentos"),
        new(OrcamentosCriar, "Criar orçamentos"),
        new(OrcamentosEditar, "Editar e registrar transições de orçamentos"),
        new(ConfiguracoesVisualizar, "Visualizar configurações operacionais"),
        new(ConfiguracoesEditar, "Editar configurações operacionais e checklist"),
        new(OrdemServicoVisualizar, "Visualizar ordens de serviço"),
        new(OrdemServicoCriar, "Criar ordens de serviço"),
        new(OrdemServicoEditar, "Editar check-in, evidências e adicionais da ordem de serviço"),
        new(OrdemServicoFinalizar, "Finalizar ordens de serviço"),
        new(FinanceiroVisualizar, "Visualizar financeiro"),
        new(FinanceiroEditar, "Editar vencimentos financeiros"),
        new(FinanceiroRegistrarPagamento, "Registrar pagamentos"),
        new(FinanceiroEstornarPagamento, "Estornar pagamentos"),
        new(NotificacoesReenviar, "Enviar e reenviar comunicações com clientes"),
        new(AdministracaoUsuario, "Administrar usuários")
    ];

    public static readonly IReadOnlyCollection<string> Todas =
        Definicoes.Select(x => x.Codigo).ToArray();
}
