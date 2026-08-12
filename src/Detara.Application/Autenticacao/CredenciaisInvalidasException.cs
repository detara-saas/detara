namespace Detara.Application.Autenticacao;

public sealed class CredenciaisInvalidasException : Exception
{
    public CredenciaisInvalidasException()
        : base("Empresa, e-mail ou senha inválidos.")
    {
    }
}
