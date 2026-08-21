namespace Detara.Application.Autenticacao;

public sealed class CredenciaisInvalidasException : Exception
{
    public CredenciaisInvalidasException()
        : base("E-mail ou senha inválidos.")
    {
    }
}
