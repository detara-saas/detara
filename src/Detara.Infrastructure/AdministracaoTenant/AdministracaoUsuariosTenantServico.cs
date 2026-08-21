using System.Data;
using System.Security.Cryptography;
using Detara.Application.Abstracoes;
using Detara.Application.AdministracaoTenant;
using Detara.Contracts.Autorizacao;
using Detara.Domain.Entidades;
using Detara.Domain.Plataforma;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.AdministracaoTenant;

internal sealed class AdministracaoUsuariosTenantServico(
    DetaraDbContext db,
    IUsuarioContexto usuarioContexto,
    ISenhaServico senhaServico) : IAdministracaoUsuariosTenantServico
{
    public async Task<PaginaTenant<UsuarioTenantResultado>> ListarAsync(
        int pagina,
        int tamanhoPagina,
        string? pesquisa,
        string? status,
        CancellationToken cancellationToken)
    {
        var agora = DateTime.UtcNow;
        var convites = db.ConvitesAdministradoresEmpresa.AsNoTracking().Where(x =>
            x.EmpresaId == usuarioContexto.EmpresaId &&
            x.Origem == OrigemConviteAcessoEmpresa.UsuarioTenant);
        var query =
            from usuario in db.Usuarios.AsNoTracking()
            join convite in convites on usuario.Id equals convite.UsuarioId into convitesUsuario
            from convite in convitesUsuario.DefaultIfEmpty()
            select new { usuario, convite };

        if (!string.IsNullOrWhiteSpace(pesquisa))
        {
            var termo = pesquisa.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.usuario.Nome.ToLower().Contains(termo) ||
                x.usuario.Email.Contains(termo));
        }

        query = status switch
        {
            "ativo" => query.Where(x => x.usuario.EhAtivo),
            "inativo" => query.Where(x => !x.usuario.EhAtivo &&
                (x.convite == null ||
                    x.convite.Status == StatusConviteAdministradorEmpresa.Aceito ||
                    x.convite.Status == StatusConviteAdministradorEmpresa.Invalidado)),
            "pendente" => query.Where(x => !x.usuario.EhAtivo && x.convite != null &&
                x.convite.Status != StatusConviteAdministradorEmpresa.Aceito &&
                x.convite.Status != StatusConviteAdministradorEmpresa.Invalidado &&
                x.convite.Status != StatusConviteAdministradorEmpresa.Expirado &&
                (x.convite.ExpiraEmUtc == null || x.convite.ExpiraEmUtc > agora)),
            "expirado" => query.Where(x => !x.usuario.EhAtivo && x.convite != null &&
                (x.convite.Status == StatusConviteAdministradorEmpresa.Expirado ||
                    x.convite.ExpiraEmUtc <= agora)),
            _ => query
        };

        var total = await query.CountAsync(cancellationToken);
        var itens = await query
            .OrderBy(x => x.usuario.Nome)
            .ThenBy(x => x.usuario.Email)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .Select(x => new
            {
                x.usuario.Id,
                x.usuario.Nome,
                x.usuario.Email,
                x.usuario.PerfilId,
                PerfilNome = x.usuario.Perfil.Nome,
                x.usuario.EhAtivo,
                ConviteStatus = x.convite == null ? (StatusConviteAdministradorEmpresa?)null : x.convite.Status,
                ConviteExpiraEmUtc = x.convite == null ? null : x.convite.ExpiraEmUtc,
                x.usuario.Versao
            })
            .ToArrayAsync(cancellationToken);
        var resultados = itens.Select(x => Mapear(
            x.Id,
            x.Nome,
            x.Email,
            x.PerfilId,
            x.PerfilNome,
            x.EhAtivo,
            x.ConviteStatus,
            x.ConviteExpiraEmUtc,
            x.Versao,
            agora)).ToArray();
        return new(resultados, pagina, tamanhoPagina, total,
            (int)Math.Ceiling(total / (double)tamanhoPagina));
    }

    public async Task<UsuarioTenantResultado> ObterAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var usuario = await db.Usuarios.AsNoTracking().Include(x => x.Perfil)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Usuário não encontrado.");
        var convite = await ObterConviteTenantAsync(id, true, cancellationToken);
        return Mapear(usuario, convite, DateTime.UtcNow);
    }

    public async Task<UsuarioTenantResultado> ConvidarAsync(
        string nome,
        string email,
        Guid perfilId,
        CancellationToken cancellationToken)
    {
        var normalizado = email.Trim().ToLowerInvariant();
        if (await db.Usuarios.AsNoTracking().AnyAsync(x => x.Email == normalizado, cancellationToken))
        {
            throw new ConflitoRegraNegocioException(
                "O e-mail informado já está vinculado a outro usuário desta empresa.");
        }

        var perfil = await ObterPerfilConcedivelAsync(perfilId, cancellationToken);
        var usuario = new Usuario(
            usuarioContexto.EmpresaId,
            perfil.Id,
            nome,
            normalizado,
            "pendente");
        usuario.AlterarSenhaHash(senhaServico.GerarHash(
            usuario,
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))));
        usuario.DesativarAcesso(usuario.Versao);
        var convite = ConviteAdministradorEmpresa.CriarParaUsuarioTenant(
            usuarioContexto.EmpresaId,
            usuario.Id,
            usuario.Email,
            usuarioContexto.UsuarioId);
        db.Usuarios.Add(usuario);
        db.ConvitesAdministradoresEmpresa.Add(convite);
        await db.SaveChangesAsync(cancellationToken);
        return Mapear(usuario, perfil, convite, DateTime.UtcNow);
    }

    public async Task<UsuarioTenantResultado> AlterarPerfilAsync(
        Guid id,
        Guid perfilId,
        long versao,
        CancellationToken cancellationToken)
    {
        if (id == usuarioContexto.UsuarioId)
        {
            throw new ConflitoRegraNegocioException(
                "Você não pode alterar o próprio perfil pela administração de usuários.");
        }

        await using var transacao = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var usuario = await ObterUsuarioRastreadoAsync(id, cancellationToken);
        var perfil = await ObterPerfilConcedivelAsync(perfilId, cancellationToken);
        if (EhAdministrativo(usuario.Perfil) && !EhAdministrativo(perfil))
        {
            await ProtegerUltimoAdministradorAsync(usuario.Id, cancellationToken);
        }

        try
        {
            usuario.AlterarPerfil(perfil.Id, versao);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflitoRegraNegocioException(exception.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        var convite = await ObterConviteTenantAsync(id, true, cancellationToken);
        return Mapear(usuario, perfil, convite, DateTime.UtcNow);
    }

    public async Task<UsuarioTenantResultado> AlterarStatusAsync(
        Guid id,
        bool ativar,
        long versao,
        CancellationToken cancellationToken)
    {
        if (!ativar && id == usuarioContexto.UsuarioId)
        {
            throw new ConflitoRegraNegocioException("Você não pode inativar a própria conta.");
        }

        await using var transacao = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var usuario = await ObterUsuarioRastreadoAsync(id, cancellationToken);
        if (!ativar && usuario.EhAtivo && EhAdministrativo(usuario.Perfil))
        {
            await ProtegerUltimoAdministradorAsync(usuario.Id, cancellationToken);
        }

        if (ativar && !usuario.EhAtivo)
        {
            var convitePendente = await db.ConvitesAdministradoresEmpresa.AsNoTracking().AnyAsync(
                x => x.EmpresaId == usuarioContexto.EmpresaId &&
                    x.UsuarioId == usuario.Id &&
                    x.Status != StatusConviteAdministradorEmpresa.Aceito &&
                    x.Status != StatusConviteAdministradorEmpresa.Invalidado,
                cancellationToken);
            if (convitePendente)
            {
                throw new ConflitoRegraNegocioException(
                    "O usuário ainda precisa aceitar ou receber novamente o convite.");
            }
        }

        try
        {
            if (ativar) usuario.ReativarAcesso(versao);
            else usuario.DesativarAcesso(versao);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflitoRegraNegocioException(exception.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        var convite = await ObterConviteTenantAsync(id, true, cancellationToken);
        return Mapear(usuario, convite, DateTime.UtcNow);
    }

    public async Task<UsuarioTenantResultado> ReenviarConviteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var usuario = await db.Usuarios.AsNoTracking().Include(x => x.Perfil)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Usuário não encontrado.");
        if (usuario.EhAtivo)
        {
            throw new ConflitoRegraNegocioException("Usuários ativos não possuem convite pendente.");
        }

        var convite = await ObterConviteTenantAsync(id, false, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Convite do usuário não encontrado.");
        try
        {
            convite.PrepararReenvioTenant(DateTime.UtcNow, usuarioContexto.UsuarioId);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflitoRegraNegocioException(exception.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Mapear(usuario, convite, DateTime.UtcNow);
    }

    private async Task<Perfil> ObterPerfilConcedivelAsync(
        Guid perfilId,
        CancellationToken cancellationToken)
    {
        var perfil = await db.Perfis.Include(x => x.Permissoes)
            .SingleOrDefaultAsync(x => x.Id == perfilId && x.EhAtivo, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Perfil ativo não encontrado.");
        var permissoesAtuais = await db.Usuarios.AsNoTracking()
            .Where(x => x.Id == usuarioContexto.UsuarioId && x.EhAtivo && x.Perfil.EhAtivo)
            .SelectMany(x => x.Perfil.Permissoes)
            .Where(x => x.EhAtivo)
            .Select(x => x.Codigo)
            .ToArrayAsync(cancellationToken);
        var concediveis = permissoesAtuais.ToHashSet(StringComparer.Ordinal);
        if (perfil.Permissoes.Any(x => x.EhAtivo && !concediveis.Contains(x.Codigo)))
        {
            throw new ConflitoRegraNegocioException(
                "Você não pode atribuir um perfil com permissões que não possui.");
        }

        return perfil;
    }

    private async Task<Usuario> ObterUsuarioRastreadoAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Usuarios.Include(x => x.Perfil).ThenInclude(x => x.Permissoes)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Usuário não encontrado.");

    private Task<ConviteAdministradorEmpresa?> ObterConviteTenantAsync(
        Guid usuarioId,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        var query = db.ConvitesAdministradoresEmpresa.Where(x =>
            x.EmpresaId == usuarioContexto.EmpresaId &&
            x.UsuarioId == usuarioId &&
            x.Origem == OrigemConviteAcessoEmpresa.UsuarioTenant);
        if (asNoTracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(cancellationToken);
    }

    private async Task ProtegerUltimoAdministradorAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var outros = await db.Usuarios.CountAsync(x =>
            x.Id != usuarioId &&
            x.EhAtivo &&
            x.Perfil.EhAtivo &&
            x.Perfil.Permissoes.Any(permissao =>
                permissao.EhAtivo && permissao.Codigo == Permissoes.AdministracaoUsuario),
            cancellationToken);
        if (outros == 0)
        {
            throw new ConflitoRegraNegocioException(
                "Não é possível inativar ou remover o perfil do último administrador ativo da empresa.");
        }
    }

    private UsuarioTenantResultado Mapear(
        Usuario usuario,
        ConviteAdministradorEmpresa? convite,
        DateTime agora) => Mapear(usuario, usuario.Perfil, convite, agora);

    private UsuarioTenantResultado Mapear(
        Usuario usuario,
        Perfil perfil,
        ConviteAdministradorEmpresa? convite,
        DateTime agora) => Mapear(
            usuario.Id,
            usuario.Nome,
            usuario.Email,
            perfil.Id,
            perfil.Nome,
            usuario.EhAtivo,
            convite?.Status,
            convite?.ExpiraEmUtc,
            usuario.Versao,
            agora);

    private UsuarioTenantResultado Mapear(
        Guid id,
        string nome,
        string email,
        Guid perfilId,
        string perfilNome,
        bool usuarioAtivo,
        StatusConviteAdministradorEmpresa? conviteStatus,
        DateTime? conviteExpiraEmUtc,
        long versao,
        DateTime agora)
    {
        var conviteExpirado = conviteStatus == StatusConviteAdministradorEmpresa.Expirado ||
            conviteExpiraEmUtc <= agora;
        var status = usuarioAtivo
            ? "Ativo"
            : conviteExpirado
                ? "Convite expirado"
                : conviteStatus is not null and not StatusConviteAdministradorEmpresa.Aceito
                    and not StatusConviteAdministradorEmpresa.Invalidado
                    ? "Convite pendente"
                    : "Inativo";
        var podeReenviar = !usuarioAtivo && conviteStatus is not null
            and not StatusConviteAdministradorEmpresa.Aceito
            and not StatusConviteAdministradorEmpresa.Invalidado;
        return new(
            id,
            nome,
            email,
            perfilId,
            perfilNome,
            status,
            conviteExpiraEmUtc,
            podeReenviar,
            id == usuarioContexto.UsuarioId,
            versao);
    }

    private static bool EhAdministrativo(Perfil perfil) => perfil.EhAtivo &&
        perfil.Permissoes.Any(x => x.EhAtivo && x.Codigo == Permissoes.AdministracaoUsuario);
}
