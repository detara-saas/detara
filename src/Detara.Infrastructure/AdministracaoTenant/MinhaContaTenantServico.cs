using Detara.Application.Abstracoes;
using Detara.Application.AdministracaoTenant;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.AdministracaoTenant;

internal sealed class MinhaContaTenantServico(
    DetaraDbContext db,
    IUsuarioContexto usuarioContexto,
    ISenhaServico senhaServico) : IMinhaContaTenantServico
{
    public async Task<MinhaContaResultado> ObterAsync(CancellationToken cancellationToken) =>
        Mapear(await ObterUsuarioAsync(asNoTracking: true, cancellationToken));

    public async Task<MinhaContaResultado> AtualizarNomeAsync(
        string nome,
        long versao,
        CancellationToken cancellationToken)
    {
        var usuario = await ObterUsuarioAsync(asNoTracking: false, cancellationToken);
        try
        {
            usuario.AlterarNome(nome, versao);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflitoRegraNegocioException(exception.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Mapear(usuario);
    }

    public async Task AtualizarEmailAsync(
        string novoEmail,
        string senhaAtual,
        long versao,
        CancellationToken cancellationToken)
    {
        var usuario = await ObterUsuarioAsync(asNoTracking: false, cancellationToken);
        ConfirmarSenha(usuario, senhaAtual);
        var normalizado = novoEmail.Trim().ToLowerInvariant();
        if (await db.Usuarios.AsNoTracking().AnyAsync(
                x => x.Id != usuario.Id && x.Email == normalizado,
                cancellationToken))
        {
            throw new ConflitoRegraNegocioException(
                "O e-mail informado já está vinculado a outro usuário desta empresa.");
        }

        try
        {
            usuario.AlterarEmail(normalizado, versao);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflitoRegraNegocioException(exception.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AlterarSenhaAsync(
        string senhaAtual,
        string novaSenha,
        long versao,
        CancellationToken cancellationToken)
    {
        var usuario = await ObterUsuarioAsync(asNoTracking: false, cancellationToken);
        ConfirmarSenha(usuario, senhaAtual);
        if (senhaServico.Verificar(usuario, usuario.SenhaHash, novaSenha))
        {
            throw new ConflitoRegraNegocioException("A nova senha deve ser diferente da senha atual.");
        }

        try
        {
            usuario.AlterarSenhaHash(senhaServico.GerarHash(usuario, novaSenha), versao);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflitoRegraNegocioException(exception.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Usuario> ObterUsuarioAsync(
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        var query = db.Usuarios
            .Include(x => x.Perfil)
            .Include(x => x.Empresa)
            .AsQueryable();
        if (asNoTracking) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(x => x.Id == usuarioContexto.UsuarioId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Conta não encontrada.");
    }

    private void ConfirmarSenha(Usuario usuario, string senhaAtual)
    {
        if (!senhaServico.Verificar(usuario, usuario.SenhaHash, senhaAtual))
        {
            throw new ConflitoRegraNegocioException("Não foi possível confirmar a senha atual.");
        }
    }

    private static MinhaContaResultado Mapear(Usuario usuario) => new(
        usuario.Nome,
        usuario.Email,
        usuario.Empresa.NomeFantasia,
        usuario.Perfil.Nome,
        usuario.Versao);
}
