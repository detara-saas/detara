using Detara.Contracts.Agenda;
using Detara.Web.Components.Shared.DesignSystem;

namespace Detara.Web.Servicos;

public static class AgendamentoApresentacao
{
    public static string NomeStatus(StatusAgendamentoContrato status) => status switch
    {
        StatusAgendamentoContrato.NaoCompareceu => "Não compareceu",
        StatusAgendamentoContrato.Compareceu => "Em atendimento",
        StatusAgendamentoContrato.Concluido => "Concluído",
        _ => status.ToString()
    };

    public static DetaraStatusTone TomStatus(StatusAgendamentoContrato status) => status switch
    {
        StatusAgendamentoContrato.Agendado => DetaraStatusTone.Info,
        StatusAgendamentoContrato.Confirmado => DetaraStatusTone.Confirmed,
        StatusAgendamentoContrato.Compareceu => DetaraStatusTone.Warning,
        StatusAgendamentoContrato.Concluido => DetaraStatusTone.Positive,
        StatusAgendamentoContrato.Cancelado => DetaraStatusTone.Critical,
        _ => DetaraStatusTone.Neutral
    };

    public static bool PodeConfirmar(StatusAgendamentoContrato status) =>
        status == StatusAgendamentoContrato.Agendado;
}
