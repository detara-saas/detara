using Detara.Application.Abstracoes;
using MediatR;

namespace Detara.Application.Autenticacao;

public sealed record AutenticarCommand(string SlugEmpresa, string Email, string Senha)
    : IRequest<ResultadoAutenticacao>;

public sealed record ResultadoAutenticacao(
    string Token,
    DateTime ExpiraEmUtc,
    Guid UsuarioId,
    Guid EmpresaId,
    string Nome,
    string Perfil,
    IReadOnlyCollection<string> Permissoes);

internal sealed class AutenticarCommandHandler(
    IUsuarioAutenticacaoRepositorio repositorio,
    ISenhaServico senhaServico,
    ITokenServico tokenServico)
    : IRequestHandler<AutenticarCommand, ResultadoAutenticacao>
{
    public async Task<ResultadoAutenticacao> Handle(
        AutenticarCommand request,
        CancellationToken cancellationToken)
    {
        var usuario = await repositorio.ObterParaLoginAsync(
            request.SlugEmpresa.Trim().ToLowerInvariant(),
            request.Email.Trim().ToLowerInvariant(),
            cancellationToken);

        if (usuario is null || !usuario.EhAtivo || !usuario.Perfil.EhAtivo ||
            !senhaServico.Verificar(usuario, usuario.SenhaHash, request.Senha))
        {
            throw new CredenciaisInvalidasException();
        }

        var token = tokenServico.Gerar(usuario);
        var permissoesAtivas = usuario.Perfil.Permissoes
            .Where(permissao => permissao.EhAtivo)
            .Select(permissao => permissao.Codigo)
            .ToArray();

        return new ResultadoAutenticacao(
            token.Valor,
            token.ExpiraEmUtc,
            usuario.Id,
            usuario.EmpresaId,
            usuario.Nome,
            usuario.Perfil.Nome,
            permissoesAtivas);
    }
}
