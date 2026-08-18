namespace Detara.Domain.Atendimento;

public enum StatusOrdemServico
{
    Aberta = 1,
    EmExecucao = 2,
    AguardandoRetirada = 3,
    Concluida = 4,
    Cancelada = 5
}

public enum OrigemOrdemServico
{
    Orcamento = 1,
    Agendamento = 2,
    AtendimentoDireto = 3
}

public enum OrigemComercialOrdemServico
{
    Orcamento = 1,
    AcordoDireto = 2,
    Cortesia = 3
}

public enum RespostaChecklistOrdemServico
{
    Conforme = 1,
    NaoConforme = 2,
    NaoAplicavel = 3
}

public enum CategoriaFotoOrdemServico
{
    Entrada = 1,
    Durante = 2,
    Saida = 3
}
