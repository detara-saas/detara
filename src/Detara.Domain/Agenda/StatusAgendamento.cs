namespace Detara.Domain.Agenda;

public enum StatusAgendamento
{
    Agendado = 1,
    Confirmado = 2,
    Compareceu = 3,
    Concluido = 4,
    Cancelado = 5,
    NaoCompareceu = 6
}

public enum TipoItemAgendamento
{
    Servico = 1,
    Pacote = 2
}
