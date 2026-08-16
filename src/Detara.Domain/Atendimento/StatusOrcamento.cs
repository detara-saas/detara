namespace Detara.Domain.Atendimento;

public enum StatusOrcamento
{
    Rascunho = 1,
    Emitido = 2,
    Aprovado = 3,
    Recusado = 4,
    Cancelado = 5,
    Substituido = 6
}

public enum StatusEfetivoOrcamento
{
    Rascunho = 1,
    Emitido = 2,
    Aprovado = 3,
    Recusado = 4,
    Cancelado = 5,
    Substituido = 6,
    Expirado = 7
}

public enum TipoItemOrcamento
{
    Servico = 1,
    Pacote = 2,
    Personalizado = 3
}
