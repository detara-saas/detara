using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Detara.Application.Abstracoes;
using Detara.Application.Plataforma;
using Detara.Contracts.Autorizacao;
using Detara.Domain.Entidades;
using Detara.Domain.Plataforma;
using Detara.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Plataforma;

internal sealed class AdministracaoPlataformaServico(
    DetaraDbContext db,
    DbContextOptions<DetaraDbContext> dbOptions,
    IPasswordHasher<Usuario> passwordHasher)
    : IAdministracaoPlataformaServico
{
    public async Task<DashboardPlataformaResultado> ObterDashboardAsync(CancellationToken cancellationToken)
    {
        var empresasAtivas = await db.Empresas.CountAsync(x => x.EhAtivo, cancellationToken);
        var empresasSuspensas = await db.Empresas.CountAsync(x => !x.EhAtivo, cancellationToken);
        var convitesPendentes = await db.ConvitesAdministradoresEmpresa.CountAsync(
            x => x.Origem == OrigemConviteAcessoEmpresa.AdministradorInicialPlataforma &&
                (x.Status == StatusConviteAdministradorEmpresa.Pendente ||
                x.Status == StatusConviteAdministradorEmpresa.Processando ||
                x.Status == StatusConviteAdministradorEmpresa.Enviado),
            cancellationToken);
        var convitesComFalha = await db.ConvitesAdministradoresEmpresa.CountAsync(
            x => x.Origem == OrigemConviteAcessoEmpresa.AdministradorInicialPlataforma &&
                x.Status == StatusConviteAdministradorEmpresa.FalhaEnvio,
            cancellationToken);
        return new(empresasAtivas, empresasSuspensas, convitesPendentes, convitesComFalha);
    }

    public async Task<PaginaPlataforma<EmpresaPlataformaResumo>> ListarEmpresasAsync(
        int pagina,
        int tamanhoPagina,
        string? pesquisa,
        bool? ativa,
        CancellationToken cancellationToken)
    {
        var usuarios = db.Usuarios.IgnoreQueryFilters().AsNoTracking();
        var query =
            from empresa in db.Empresas.AsNoTracking()
            join convite in db.ConvitesAdministradoresEmpresa.AsNoTracking()
                    .Where(x => x.Origem == OrigemConviteAcessoEmpresa.AdministradorInicialPlataforma)
                on empresa.Id equals convite.EmpresaId
            join usuario in usuarios
                on new { EmpresaId = empresa.Id, Id = convite.UsuarioId }
                equals new { usuario.EmpresaId, Id = usuario.Id }
            select new { empresa, convite, usuario };

        if (ativa is not null)
        {
            query = query.Where(x => x.empresa.EhAtivo == ativa);
        }

        if (!string.IsNullOrWhiteSpace(pesquisa))
        {
            var termo = pesquisa.Trim().ToLower();
            var digitos = SomenteDigitos(termo);
            query = query.Where(x =>
                x.empresa.NomeFantasia.ToLower().Contains(termo) ||
                x.empresa.RazaoSocial.ToLower().Contains(termo) ||
                x.empresa.Slug.ToLower().Contains(termo) ||
                x.usuario.Email.ToLower().Contains(termo) ||
                digitos.Length > 0 && x.empresa.CpfCnpj.Contains(digitos));
        }

        var total = await query.CountAsync(cancellationToken);
        var itens = await query
            .OrderBy(x => x.empresa.NomeFantasia)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .Select(x => new EmpresaPlataformaResumo(
                x.empresa.Id,
                x.empresa.NomeFantasia,
                x.empresa.RazaoSocial,
                x.empresa.CpfCnpj,
                x.empresa.Slug,
                x.empresa.EhAtivo,
                x.usuario.Nome,
                x.usuario.Email,
                x.convite.Status.ToString(),
                x.empresa.CriadoEmUtc))
            .ToArrayAsync(cancellationToken);
        return new(
            itens,
            pagina,
            tamanhoPagina,
            total,
            (int)Math.Ceiling(total / (double)tamanhoPagina));
    }

    public async Task<EmpresaPlataformaDetalhe> ObterEmpresaAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var empresa = await db.Empresas.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Empresa não encontrada.");
        var convite = await db.ConvitesAdministradoresEmpresa.AsNoTracking()
            .SingleOrDefaultAsync(x =>
                x.EmpresaId == id &&
                x.Origem == OrigemConviteAcessoEmpresa.AdministradorInicialPlataforma,
                cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Convite administrativo não encontrado.");
        var usuario = await db.Usuarios.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.EmpresaId == id && x.Id == convite.UsuarioId,
                cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Administrador inicial não encontrado.");
        return MapearDetalhe(empresa, usuario, convite);
    }

    public async Task<EmpresaPlataformaDetalhe> ProvisionarEmpresaAsync(
        Guid administradorPlataformaId,
        ProvisionarEmpresaEntrada entrada,
        string? traceId,
        CancellationToken cancellationToken)
    {
        var documento = SomenteDigitos(entrada.CpfCnpj);
        if (documento.Length is not (11 or 14))
        {
            throw new ArgumentException("O CPF/CNPJ deve possuir 11 ou 14 dígitos.", nameof(entrada.CpfCnpj));
        }

        if (!TimeZoneValido(entrada.FusoHorario))
        {
            throw new ArgumentException("O fuso horário informado é inválido.", nameof(entrada.FusoHorario));
        }

        if (await db.Empresas.AnyAsync(x => x.CpfCnpj == documento, cancellationToken))
        {
            throw new ConflitoRegraNegocioException("Já existe uma empresa com este documento.");
        }

        var slug = await GerarSlugUnicoAsync(entrada.NomeFantasia, cancellationToken);
        var empresa = new Empresa(
            entrada.NomeFantasia,
            entrada.RazaoSocial,
            documento,
            slug,
            entrada.EmailContato,
            entrada.Telefone,
            entrada.FusoHorario);
        await using var contexto = new DetaraDbContext(dbOptions, new ContextoProvisionamento(empresa.Id));
        await using var transacao = await contexto.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (!await contexto.AdministradoresPlataforma.AsNoTracking()
                .AnyAsync(x => x.Id == administradorPlataformaId && x.EhAtivo && x.MfaHabilitado, cancellationToken))
            {
                throw new CredenciaisPlataformaInvalidasException();
            }

            contexto.Empresas.Add(empresa);
            var perfil = new Perfil(
                empresa.Id,
                "Administrador",
                "Perfil administrativo protegido com acesso integral ao tenant.",
                ehSistema: true);
            contexto.Perfis.Add(perfil);
            foreach (var definicao in Permissoes.Definicoes)
            {
                var permissao = await contexto.Permissoes.SingleOrDefaultAsync(
                    x => x.Codigo == definicao.Codigo,
                    cancellationToken);
                if (permissao is null)
                {
                    permissao = new Permissao(definicao.Codigo, definicao.Descricao);
                    contexto.Permissoes.Add(permissao);
                }

                perfil.ConcederPermissao(permissao);
            }

            var usuario = new Usuario(
                empresa.Id,
                perfil.Id,
                entrada.AdministradorNome,
                entrada.AdministradorEmail,
                "pendente");
            usuario.AlterarSenhaHash(passwordHasher.HashPassword(
                usuario,
                Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))));
            usuario.Desativar();
            contexto.Usuarios.Add(usuario);
            var convite = new ConviteAdministradorEmpresa(
                empresa.Id,
                usuario.Id,
                usuario.Email,
                administradorPlataformaId);
            contexto.ConvitesAdministradoresEmpresa.Add(convite);
            contexto.AuditoriasPlataforma.Add(new AuditoriaPlataforma(
                administradorPlataformaId,
                AcoesAuditoriaPlataforma.EmpresaProvisionada,
                empresa.Id,
                usuario.Id,
                traceId,
                "Empresa, perfil administrador, permissões, usuário pendente e convite criados atomicamente."));
            await contexto.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            return MapearDetalhe(empresa, usuario, convite);
        }
        catch (DbUpdateException)
        {
            await transacao.RollbackAsync(cancellationToken);
            throw new ConflitoRegraNegocioException(
                "Os dados informados conflitam com uma empresa ou usuário já provisionado.");
        }
    }

    public async Task SuspenderEmpresaAsync(
        Guid administradorPlataformaId,
        Guid empresaId,
        string motivo,
        string? traceId,
        CancellationToken cancellationToken)
    {
        var empresa = await db.Empresas.SingleOrDefaultAsync(x => x.Id == empresaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Empresa não encontrada.");
        if (!empresa.EhAtivo)
        {
            throw new ConflitoRegraNegocioException("A empresa já está suspensa.");
        }

        empresa.Suspender();
        db.AuditoriasPlataforma.Add(new AuditoriaPlataforma(
            administradorPlataformaId,
            AcoesAuditoriaPlataforma.EmpresaSuspensa,
            empresaId,
            empresaId,
            traceId,
            motivo));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReativarEmpresaAsync(
        Guid administradorPlataformaId,
        Guid empresaId,
        string motivo,
        string? traceId,
        CancellationToken cancellationToken)
    {
        var empresa = await db.Empresas.SingleOrDefaultAsync(x => x.Id == empresaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Empresa não encontrada.");
        if (empresa.EhAtivo)
        {
            throw new ConflitoRegraNegocioException("A empresa já está ativa.");
        }

        empresa.Reativar();
        db.AuditoriasPlataforma.Add(new AuditoriaPlataforma(
            administradorPlataformaId,
            AcoesAuditoriaPlataforma.EmpresaReativada,
            empresaId,
            empresaId,
            traceId,
            motivo));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReenviarConviteAsync(
        Guid administradorPlataformaId,
        Guid empresaId,
        string? traceId,
        CancellationToken cancellationToken)
    {
        var convite = await db.ConvitesAdministradoresEmpresa
            .SingleOrDefaultAsync(x =>
                x.EmpresaId == empresaId &&
                x.Origem == OrigemConviteAcessoEmpresa.AdministradorInicialPlataforma,
                cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Convite administrativo não encontrado.");
        convite.PrepararReenvio(DateTime.UtcNow, administradorPlataformaId);
        db.AuditoriasPlataforma.Add(new AuditoriaPlataforma(
            administradorPlataformaId,
            AcoesAuditoriaPlataforma.ConviteReenviado,
            empresaId,
            convite.Id,
            traceId,
            "Convite anterior invalidado e novo envio solicitado."));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaginaPlataforma<AuditoriaPlataformaItemResultado>> ListarAuditoriaAsync(
        int pagina,
        int tamanhoPagina,
        DateTime? inicioUtc,
        DateTime? fimUtc,
        string? tipo,
        Guid? empresaId,
        CancellationToken cancellationToken)
    {
        var query = db.AuditoriasPlataforma.AsNoTracking().AsQueryable();
        if (inicioUtc is not null) query = query.Where(x => x.CriadoEmUtc >= inicioUtc);
        if (fimUtc is not null) query = query.Where(x => x.CriadoEmUtc <= fimUtc);
        if (!string.IsNullOrWhiteSpace(tipo)) query = query.Where(x => x.TipoAcao == tipo.Trim());
        if (empresaId is not null) query = query.Where(x => x.EmpresaAlvoId == empresaId);
        var total = await query.CountAsync(cancellationToken);
        var itens = await (
                from auditoria in query
                join empresa in db.Empresas.AsNoTracking()
                    on auditoria.EmpresaAlvoId equals empresa.Id into empresas
                from empresa in empresas.DefaultIfEmpty()
                join administrador in db.AdministradoresPlataforma.AsNoTracking()
                    on auditoria.AdministradorPlataformaId equals administrador.Id into administradores
                from administrador in administradores.DefaultIfEmpty()
                orderby auditoria.CriadoEmUtc descending
                select new AuditoriaPlataformaItemResultado(
                    auditoria.Id,
                    auditoria.TipoAcao,
                    auditoria.EmpresaAlvoId,
                    empresa == null ? null : empresa.NomeFantasia,
                    administrador == null ? null : administrador.Nome,
                    auditoria.CriadoEmUtc,
                    auditoria.TraceId,
                    auditoria.DescricaoSegura))
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToArrayAsync(cancellationToken);
        return new(itens, pagina, tamanhoPagina, total, (int)Math.Ceiling(total / (double)tamanhoPagina));
    }

    private async Task<string> GerarSlugUnicoAsync(string nomeFantasia, CancellationToken cancellationToken)
    {
        var normalizado = nomeFantasia.Normalize(NormalizationForm.FormD);
        var slug = new string(normalizado
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsAsciiLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-')
            .ToArray());
        while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-", StringComparison.Ordinal);
        slug = slug.Trim('-');
        if (string.IsNullOrWhiteSpace(slug)) slug = "empresa";
        if (slug.Length > 55) slug = slug[..55].TrimEnd('-');
        if (!await db.Empresas.AnyAsync(x => x.Slug == slug, cancellationToken)) return slug;
        var sufixo = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        return $"{slug}-{sufixo}";
    }

    private static EmpresaPlataformaDetalhe MapearDetalhe(
        Empresa empresa,
        Usuario usuario,
        ConviteAdministradorEmpresa convite) => new(
            empresa.Id,
            empresa.NomeFantasia,
            empresa.RazaoSocial,
            empresa.CpfCnpj,
            empresa.Email,
            empresa.Telefone,
            empresa.Slug,
            empresa.FusoHorario,
            empresa.EhAtivo,
            empresa.CriadoEmUtc,
            usuario.Id,
            usuario.Nome,
            usuario.Email,
            usuario.EhAtivo,
            convite.Id,
            convite.Status.ToString(),
            convite.ExpiraEmUtc,
            convite.QuantidadeTentativasEnvio,
            convite.UltimoErroSeguro);

    private static bool TimeZoneValido(string fuso)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(fuso);
            return true;
        }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }

    private static string SomenteDigitos(string valor) => new(valor.Where(char.IsAsciiDigit).ToArray());

    private sealed class ContextoProvisionamento(Guid empresaId) : IUsuarioContexto
    {
        public Guid UsuarioId { get; } = Guid.Parse("00000000-0000-0000-0000-000000000011");
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado => true;
    }
}
