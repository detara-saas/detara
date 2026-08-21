using System.Data;
using Detara.Application.Abstracoes;
using Detara.Application.AdministracaoTenant;
using Detara.Contracts.Autorizacao;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.AdministracaoTenant;

internal sealed class AdministracaoPerfisTenantServico(
    DetaraDbContext db,
    IUsuarioContexto usuarioContexto) : IAdministracaoPerfisTenantServico
{
    public async Task<IReadOnlyCollection<PerfilTenantResumoResultado>> ListarAsync(
        CancellationToken cancellationToken) => await db.Perfis.AsNoTracking()
        .OrderByDescending(x => x.EhSistema)
        .ThenBy(x => x.Nome)
        .Select(x => new PerfilTenantResumoResultado(
            x.Id,
            x.Nome,
            x.Descricao,
            x.EhAtivo,
            x.EhSistema,
            db.Usuarios.Count(usuario => usuario.PerfilId == x.Id),
            x.Permissoes.Count(permissao => permissao.EhAtivo),
            x.Versao))
        .ToArrayAsync(cancellationToken);

    public async Task<PerfilTenantDetalheResultado> ObterAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var perfil = await ObterPerfilAsync(id, false, cancellationToken);
        return Mapear(perfil, await ContarUsuariosAsync(id, cancellationToken));
    }

    public async Task<IReadOnlyCollection<PermissaoTenantResultado>> ListarPermissoesAsync(
        CancellationToken cancellationToken)
    {
        var concediveis = await ObterPermissoesDoUsuarioAtualAsync(cancellationToken);
        var permissoes = await db.Permissoes.AsNoTracking()
            .Where(x => x.EhAtivo && Permissoes.Todas.Contains(x.Codigo))
            .OrderBy(x => x.Codigo)
            .Select(x => new { x.Codigo, x.Descricao })
            .ToArrayAsync(cancellationToken);
        return permissoes.Select(x => new PermissaoTenantResultado(
            x.Codigo,
            x.Descricao,
            Grupo(x.Codigo),
            concediveis.Contains(x.Codigo))).ToArray();
    }

    public async Task<PerfilTenantDetalheResultado> CriarAsync(
        string nome,
        string? descricao,
        IReadOnlyCollection<string> permissoes,
        CancellationToken cancellationToken)
    {
        var nomeNormalizado = nome.Trim().ToUpperInvariant();
        if (await db.Perfis.AsNoTracking().AnyAsync(
                x => x.NomeNormalizado == nomeNormalizado,
                cancellationToken))
        {
            throw new ConflitoRegraNegocioException("Já existe um perfil com este nome.");
        }

        var concedidas = await ValidarPermissoesAsync(permissoes, cancellationToken);
        var perfil = new Perfil(usuarioContexto.EmpresaId, nome, descricao);
        foreach (var permissao in concedidas)
        {
            perfil.ConcederPermissao(permissao);
        }

        db.Perfis.Add(perfil);
        await db.SaveChangesAsync(cancellationToken);
        return Mapear(perfil, 0);
    }

    public async Task<PerfilTenantDetalheResultado> AtualizarAsync(
        Guid id,
        string nome,
        string? descricao,
        IReadOnlyCollection<string> permissoes,
        long versao,
        CancellationToken cancellationToken)
    {
        await using var transacao = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var perfil = await ObterPerfilAsync(id, true, cancellationToken);
        if (perfil.EhSistema)
        {
            throw new ConflitoRegraNegocioException(
                "O perfil administrativo protegido não pode ser alterado.");
        }

        var nomeNormalizado = nome.Trim().ToUpperInvariant();
        if (await db.Perfis.AsNoTracking().AnyAsync(
                x => x.Id != perfil.Id && x.NomeNormalizado == nomeNormalizado,
                cancellationToken))
        {
            throw new ConflitoRegraNegocioException("Já existe um perfil com este nome.");
        }

        var concedidas = await ValidarPermissoesAsync(permissoes, cancellationToken);
        var eraAdministrativo = EhAdministrativo(perfil.Permissoes);
        var seraAdministrativo = EhAdministrativo(concedidas);
        if (eraAdministrativo && !seraAdministrativo)
        {
            await ProtegerUltimoAdministradorDoPerfilAsync(perfil.Id, cancellationToken);
        }

        try
        {
            perfil.Atualizar(nome, descricao, concedidas, versao);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflitoRegraNegocioException(exception.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        return Mapear(perfil, await ContarUsuariosAsync(perfil.Id, cancellationToken));
    }

    public async Task<PerfilTenantDetalheResultado> AlterarStatusAsync(
        Guid id,
        bool ativar,
        long versao,
        CancellationToken cancellationToken)
    {
        await using var transacao = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var perfil = await ObterPerfilAsync(id, true, cancellationToken);
        if (perfil.EhSistema)
        {
            throw new ConflitoRegraNegocioException(
                "O perfil administrativo protegido não pode ser inativado.");
        }

        if (!ativar && perfil.EhAtivo && EhAdministrativo(perfil.Permissoes))
        {
            await ProtegerUltimoAdministradorDoPerfilAsync(perfil.Id, cancellationToken);
        }

        try
        {
            perfil.AlterarStatus(ativar, versao);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflitoRegraNegocioException(exception.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        return Mapear(perfil, await ContarUsuariosAsync(perfil.Id, cancellationToken));
    }

    private async Task<IReadOnlyCollection<Permissao>> ValidarPermissoesAsync(
        IReadOnlyCollection<string> codigos,
        CancellationToken cancellationToken)
    {
        var solicitadas = codigos
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (solicitadas.Length != codigos.Count ||
            solicitadas.Any(x => !Permissoes.Todas.Contains(x, StringComparer.Ordinal)))
        {
            throw new ArgumentException("Uma ou mais permissões informadas são desconhecidas.", nameof(codigos));
        }

        var entidades = await db.Permissoes
            .Where(x => x.EhAtivo && solicitadas.Contains(x.Codigo))
            .ToArrayAsync(cancellationToken);
        if (entidades.Length != solicitadas.Length)
        {
            throw new ArgumentException("Uma ou mais permissões informadas são desconhecidas.", nameof(codigos));
        }

        var concediveis = await ObterPermissoesDoUsuarioAtualAsync(cancellationToken);
        if (solicitadas.Any(x => !concediveis.Contains(x)))
        {
            throw new ConflitoRegraNegocioException(
                "Você não pode conceder uma permissão que não possui.");
        }

        return entidades;
    }

    private async Task<HashSet<string>> ObterPermissoesDoUsuarioAtualAsync(
        CancellationToken cancellationToken) => (await db.Usuarios.AsNoTracking()
        .Where(x => x.Id == usuarioContexto.UsuarioId && x.EhAtivo && x.Perfil.EhAtivo)
        .SelectMany(x => x.Perfil.Permissoes)
        .Where(x => x.EhAtivo)
        .Select(x => x.Codigo)
        .ToArrayAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);

    private async Task ProtegerUltimoAdministradorDoPerfilAsync(
        Guid perfilId,
        CancellationToken cancellationToken)
    {
        var usuariosAdministrativosNoPerfil = await db.Usuarios.CountAsync(
            x => x.EhAtivo && x.PerfilId == perfilId,
            cancellationToken);
        if (usuariosAdministrativosNoPerfil == 0)
        {
            return;
        }

        var administradoresFora = await db.Usuarios.CountAsync(
            x => x.EhAtivo &&
                x.PerfilId != perfilId &&
                x.Perfil.EhAtivo &&
                x.Perfil.Permissoes.Any(permissao =>
                    permissao.EhAtivo && permissao.Codigo == Permissoes.AdministracaoUsuario),
            cancellationToken);
        if (administradoresFora == 0)
        {
            throw new ConflitoRegraNegocioException(
                "Não é possível remover o último acesso administrativo ativo da empresa.");
        }
    }

    private async Task<Perfil> ObterPerfilAsync(
        Guid id,
        bool rastrear,
        CancellationToken cancellationToken)
    {
        var query = db.Perfis.Include(x => x.Permissoes).AsQueryable();
        if (!rastrear) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Perfil não encontrado.");
    }

    private Task<int> ContarUsuariosAsync(Guid perfilId, CancellationToken cancellationToken) =>
        db.Usuarios.CountAsync(x => x.PerfilId == perfilId, cancellationToken);

    private static PerfilTenantDetalheResultado Mapear(Perfil perfil, int quantidadeUsuarios = 0) => new(
        perfil.Id,
        perfil.Nome,
        perfil.Descricao,
        perfil.EhAtivo,
        perfil.EhSistema,
        quantidadeUsuarios,
        perfil.Permissoes.Where(x => x.EhAtivo).OrderBy(x => x.Codigo).Select(x => x.Codigo).ToArray(),
        perfil.Versao);

    private static bool EhAdministrativo(IEnumerable<Permissao> permissoes) =>
        permissoes.Any(x => x.EhAtivo && x.Codigo == Permissoes.AdministracaoUsuario);

    private static string Grupo(string codigo) => codigo.Split('.', 2)[0] switch
    {
        "OrdemServico" => "Ordens de Serviço",
        "Administracao" => "Administração",
        "Configuracoes" => "Configurações",
        "Servicos" or "Pacotes" => "Catálogo",
        var grupo => grupo
    };
}
