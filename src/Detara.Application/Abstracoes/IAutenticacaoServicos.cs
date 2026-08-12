using Detara.Domain.Entidades;

namespace Detara.Application.Abstracoes;

public interface IUsuarioAutenticacaoRepositorio
{
    Task<Usuario?> ObterParaLoginAsync(string slugEmpresa, string email, CancellationToken cancellationToken);
}

public interface ISenhaServico
{
    string GerarHash(Usuario usuario, string senha);
    bool Verificar(Usuario usuario, string senhaHash, string senha);
}

public interface ITokenServico
{
    TokenGerado Gerar(Usuario usuario);
}

public sealed record TokenGerado(string Valor, DateTime ExpiraEmUtc);
