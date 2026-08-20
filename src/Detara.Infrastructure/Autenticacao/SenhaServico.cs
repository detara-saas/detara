using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using Microsoft.AspNetCore.Identity;

namespace Detara.Infrastructure.Autenticacao;

internal sealed class SenhaServico : ISenhaServico
{
    private readonly IPasswordHasher<Usuario> _passwordHasher;
    private readonly Usuario _usuarioFicticio;
    private readonly string _hashFicticio;

    public SenhaServico(IPasswordHasher<Usuario> passwordHasher)
    {
        _passwordHasher = passwordHasher;
        _usuarioFicticio = new Usuario(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Usuário não encontrado",
            "nao-encontrado@invalid.local",
            "temporario");
        _hashFicticio = passwordHasher.HashPassword(
            _usuarioFicticio,
            Convert.ToBase64String(Guid.NewGuid().ToByteArray()));
    }

    public string GerarHash(Usuario usuario, string senha) => _passwordHasher.HashPassword(usuario, senha);

    public bool Verificar(Usuario usuario, string senhaHash, string senha) =>
        _passwordHasher.VerifyHashedPassword(usuario, senhaHash, senha) is not PasswordVerificationResult.Failed;

    public void VerificarContraHashFicticio(string senha) =>
        _ = _passwordHasher.VerifyHashedPassword(_usuarioFicticio, _hashFicticio, senha);
}
