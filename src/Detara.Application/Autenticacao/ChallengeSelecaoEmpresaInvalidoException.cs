namespace Detara.Application.Autenticacao;

public sealed class ChallengeSelecaoEmpresaInvalidoException : Exception
{
    public ChallengeSelecaoEmpresaInvalidoException()
        : base("Não foi possível concluir a seleção. Faça login novamente.")
    {
    }
}
