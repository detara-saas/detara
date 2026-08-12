using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using Microsoft.AspNetCore.Identity;

namespace Detara.Infrastructure.Autenticacao;

internal sealed class SenhaServico(IPasswordHasher<Usuario> passwordHasher) : ISenhaServico
{
    public string GerarHash(Usuario usuario, string senha) => passwordHasher.HashPassword(usuario, senha);

    public bool Verificar(Usuario usuario, string senhaHash, string senha) =>
        passwordHasher.VerifyHashedPassword(usuario, senhaHash, senha) is not PasswordVerificationResult.Failed;
}
