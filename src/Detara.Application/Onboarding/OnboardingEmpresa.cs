using Detara.Application.Abstracoes;
using MediatR;

namespace Detara.Application.Onboarding;

public interface IPlataformaOnboardingConsulta
{
    Task<bool> PossuiEmpresaConfiguradaAsync(Guid empresaId, CancellationToken cancellationToken);
}

public interface IAtendimentoOnboardingConsulta
{
    Task<bool> PossuiConfiguracaoOperacionalAsync(Guid empresaId, CancellationToken cancellationToken);
}

public interface ICatalogoOnboardingConsulta
{
    Task<bool> PossuiServicoAtivoAsync(Guid empresaId, CancellationToken cancellationToken);
}

public sealed record EstadoClientesOnboarding(
    bool PossuiClienteAtivo,
    bool PossuiClienteComVeiculoAtivo);

public interface IClientesOnboardingConsulta
{
    Task<EstadoClientesOnboarding> ObterEstadoAsync(
        Guid empresaId,
        CancellationToken cancellationToken);
}

public interface IAgendaOnboardingConsulta
{
    Task<bool> PossuiAgendamentoValidoAsync(Guid empresaId, CancellationToken cancellationToken);
}

public sealed record PermissoesAcoesOnboarding(
    bool PodeConfigurarOperacao,
    bool PodeCriarServico,
    bool PodeCriarCliente,
    bool PodeCriarVeiculo,
    bool PodeCriarAgendamento);

public sealed record OnboardingEtapaResultado(
    string Codigo,
    string Titulo,
    string Descricao,
    bool Concluida,
    bool PodeExecutar,
    string? Destino);

public sealed record OnboardingEmpresaResultado(
    bool Concluido,
    int QuantidadeConcluida,
    int QuantidadeTotal,
    IReadOnlyCollection<OnboardingEtapaResultado> Etapas);

public sealed record ObterOnboardingEmpresaQuery(PermissoesAcoesOnboarding Permissoes)
    : IRequest<OnboardingEmpresaResultado>;

internal sealed class ObterOnboardingEmpresaHandler(
    IUsuarioContexto usuario,
    IPlataformaOnboardingConsulta plataforma,
    IAtendimentoOnboardingConsulta atendimento,
    ICatalogoOnboardingConsulta catalogo,
    IClientesOnboardingConsulta clientes,
    IAgendaOnboardingConsulta agenda)
    : IRequestHandler<ObterOnboardingEmpresaQuery, OnboardingEmpresaResultado>
{
    public async Task<OnboardingEmpresaResultado> Handle(
        ObterOnboardingEmpresaQuery request,
        CancellationToken cancellationToken)
    {
        var empresaId = usuario.EmpresaId;
        var empresaConfigurada = await plataforma.PossuiEmpresaConfiguradaAsync(
            empresaId,
            cancellationToken);
        var operacaoConfigurada = await atendimento.PossuiConfiguracaoOperacionalAsync(
            empresaId,
            cancellationToken);
        var possuiServico = await catalogo.PossuiServicoAtivoAsync(
            empresaId,
            cancellationToken);
        var estadoClientes = await clientes.ObterEstadoAsync(empresaId, cancellationToken);
        var possuiAgendamento = await agenda.PossuiAgendamentoValidoAsync(
            empresaId,
            cancellationToken);

        var podeCriarClienteOuVeiculo = estadoClientes.PossuiClienteAtivo
            ? request.Permissoes.PodeCriarVeiculo
            : request.Permissoes.PodeCriarCliente;
        var destinoClienteOuVeiculo = estadoClientes.PossuiClienteAtivo
            ? "/veiculos/novo"
            : "/clientes/novo";

        OnboardingEtapaResultado[] etapas =
        [
            new(
                "empresa",
                "Empresa provisionada",
                "Os dados essenciais da sua empresa estão prontos para iniciar.",
                empresaConfigurada,
                false,
                null),
            new(
                "operacao",
                "Configure sua operação",
                "Defina como checklist e fotos serão usados nos atendimentos.",
                operacaoConfigurada,
                request.Permissoes.PodeConfigurarOperacao,
                "/configuracoes"),
            new(
                "catalogo",
                "Cadastre seu primeiro serviço",
                "O catálogo organiza o que sua empresa oferece, com preço fixo, a partir de ou sob consulta.",
                possuiServico,
                request.Permissoes.PodeCriarServico,
                "/servicos/novo"),
            new(
                "cliente_veiculo",
                "Cadastre seu primeiro cliente e veículo",
                estadoClientes.PossuiClienteAtivo
                    ? "O cliente já está cadastrado. Agora vincule o veículo que será atendido."
                    : "Registre o cliente e depois vincule o veículo que será atendido.",
                estadoClientes.PossuiClienteComVeiculoAtivo,
                podeCriarClienteOuVeiculo,
                destinoClienteOuVeiculo),
            new(
                "agenda",
                "Faça seu primeiro agendamento",
                "Planeje um atendimento real. Agendamentos cancelados ou sem comparecimento não concluem esta etapa.",
                possuiAgendamento,
                request.Permissoes.PodeCriarAgendamento,
                "/agenda/novo")
        ];

        var quantidadeConcluida = etapas.Count(etapa => etapa.Concluida);
        return new(
            quantidadeConcluida == etapas.Length,
            quantidadeConcluida,
            etapas.Length,
            etapas);
    }
}
