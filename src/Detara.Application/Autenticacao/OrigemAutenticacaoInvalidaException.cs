namespace Detara.Application.Autenticacao;

public sealed class OrigemAutenticacaoInvalidaException : Exception
{
    public OrigemAutenticacaoInvalidaException()
        : base("A origem da solicitação não é permitida.")
    {
    }
}
