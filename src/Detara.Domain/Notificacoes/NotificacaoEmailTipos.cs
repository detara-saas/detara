namespace Detara.Domain.Notificacoes;

public enum TipoTemplateEmail
{
    VeiculoProntoRetirada = 1
}

public enum StatusNotificacaoEmail
{
    Pendente = 1,
    Processando = 2,
    Enviada = 3,
    Falhou = 4,
    SemDestinatario = 5
}

public enum OrigemTemplateEmail
{
    PadraoDetara = 1,
    PersonalizadoEmpresa = 2
}

public enum TipoTentativaNotificacaoEmail
{
    Automatica = 1,
    Manual = 2
}

public enum ResultadoTentativaNotificacaoEmail
{
    Enviada = 1,
    FalhaTemporaria = 2,
    FalhaTerminal = 3
}
