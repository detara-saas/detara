namespace Detara.Application.Abstracoes;

public sealed class ViolacaoIsolamentoTenantException : Exception
{
    public ViolacaoIsolamentoTenantException()
        : base("A operação solicitada não pertence à empresa autenticada.")
    {
    }
}
