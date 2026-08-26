namespace Detara.Domain.Notificacoes;

public enum CanalComunicacaoVeiculoPronto
{
    Nenhum = 0,
    Email = 1,
    WhatsApp = 2
}

public enum CanalComunicacaoCliente
{
    Email = 1,
    WhatsApp = 2
}

public enum TipoComunicacaoCliente
{
    VeiculoPronto = 1
}

public enum StatusComunicacaoCliente
{
    Pendente = 1,
    Enviado = 2,
    Falhou = 3
}

public enum OrigemComunicacaoCliente
{
    Automatica = 1,
    Manual = 2
}

public enum StatusSessaoWhatsApp
{
    Desconectada = 0,
    AguardandoQrCode = 1,
    Conectada = 2,
    Erro = 3
}
