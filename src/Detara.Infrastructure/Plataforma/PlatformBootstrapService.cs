using Detara.Application.Abstracoes;
using Detara.Domain.Plataforma;
using Detara.Infrastructure.Persistencia;
using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Plataforma;

public sealed class PlatformBootstrapService(
    DbContextOptions<DetaraDbContext> dbOptions,
    IPasswordHasher<AdministradorPlataforma> passwordHasher)
{
    public async Task<Guid> CriarPrimeiroAdministradorAsync(
        string nome,
        string email,
        string senha,
        CancellationToken cancellationToken = default)
    {
        ValidarSenha(senha);
        await using var db = CriarContexto();
        await using var transacao = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        if (await db.AdministradoresPlataforma.AnyAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "O bootstrap cria somente o primeiro Platform Admin e foi recusado porque já existe um administrador.");
        }

        var administrador = new AdministradorPlataforma(nome, email, "pendente");
        administrador.AlterarSenhaHash(passwordHasher.HashPassword(administrador, senha));
        db.AdministradoresPlataforma.Add(administrador);
        db.AuditoriasPlataforma.Add(new AuditoriaPlataforma(
            null,
            AcoesAuditoriaPlataforma.PlatformAdminBootstrapCriado,
            null,
            administrador.Id,
            null,
            "Primeiro Platform Admin criado pela ferramenta de bootstrap."));
        await db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        return administrador.Id;
    }

    public async Task ResetarSenhaAsync(
        string email,
        string novaSenha,
        CancellationToken cancellationToken = default)
    {
        ValidarSenha(novaSenha);
        await using var db = CriarContexto();
        var normalizado = email.Trim().ToUpperInvariant();
        var administrador = await db.AdministradoresPlataforma.SingleOrDefaultAsync(
            x => x.EmailNormalizado == normalizado,
            cancellationToken)
            ?? throw new InvalidOperationException("Platform Admin não encontrado.");
        administrador.AlterarSenhaHash(passwordHasher.HashPassword(administrador, novaSenha));
        db.AuditoriasPlataforma.Add(new AuditoriaPlataforma(
            null,
            AcoesAuditoriaPlataforma.PlatformAdminSenhaResetada,
            null,
            administrador.Id,
            null,
            "Senha administrativa redefinida pela ferramenta break-glass; sessões anteriores revogadas."));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetarMfaAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        await using var db = CriarContexto();
        var normalizado = email.Trim().ToUpperInvariant();
        var administrador = await db.AdministradoresPlataforma.SingleOrDefaultAsync(
            x => x.EmailNormalizado == normalizado,
            cancellationToken)
            ?? throw new InvalidOperationException("Platform Admin não encontrado.");
        administrador.ResetarMfa();
        var codigos = db.CodigosRecuperacaoAdministradoresPlataforma
            .Where(x => x.AdministradorPlataformaId == administrador.Id);
        db.CodigosRecuperacaoAdministradoresPlataforma.RemoveRange(codigos);
        db.AuditoriasPlataforma.Add(new AuditoriaPlataforma(
            null,
            AcoesAuditoriaPlataforma.PlatformAdminMfaResetado,
            null,
            administrador.Id,
            null,
            "MFA e recovery codes resetados pela ferramenta break-glass; sessões anteriores revogadas."));
        await db.SaveChangesAsync(cancellationToken);
    }

    private DetaraDbContext CriarContexto() => new(dbOptions, ContextoBootstrap.Anonimo);

    private static void ValidarSenha(string senha)
    {
        if (string.IsNullOrWhiteSpace(senha) || senha.Length is < 12 or > 256)
        {
            throw new ArgumentException(
                "A senha deve possuir entre 12 e 256 caracteres e pode usar uma passphrase.",
                nameof(senha));
        }
    }

    private sealed class ContextoBootstrap : IUsuarioContexto
    {
        public static ContextoBootstrap Anonimo { get; } = new();
        public Guid UsuarioId => Guid.Empty;
        public Guid EmpresaId => Guid.Empty;
        public bool EstaAutenticado => false;
    }
}
