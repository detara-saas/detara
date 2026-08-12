using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Persistencia;

public sealed class DetaraDbContext(
    DbContextOptions<DetaraDbContext> options,
    IUsuarioContexto usuarioContexto)
    : DbContext(options)
{
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Perfil> Perfis => Set<Perfil>();
    public DbSet<Permissao> Permissoes => Set<Permissao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DetaraDbContext).Assembly);

        modelBuilder.Entity<Usuario>()
            .HasQueryFilter(usuario => usuario.EmpresaId == usuarioContexto.EmpresaId);
        modelBuilder.Entity<Perfil>()
            .HasQueryFilter(perfil => perfil.EmpresaId == usuarioContexto.EmpresaId);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidarIsolamentoDeEscrita();
        AtualizarAuditoria();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ValidarIsolamentoDeEscrita();
        AtualizarAuditoria();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidarIsolamentoDeEscrita()
    {
        var alteracoesTenant = ChangeTracker.Entries<EntidadeEmpresaBase>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

        foreach (var entry in alteracoesTenant)
        {
            if (!usuarioContexto.EstaAutenticado)
            {
                throw new ViolacaoIsolamentoTenantException();
            }

            var empresaOriginal = entry.State == EntityState.Added
                ? entry.Entity.EmpresaId
                : entry.Property<Guid>(nameof(EntidadeEmpresaBase.EmpresaId)).OriginalValue;

            if (empresaOriginal != usuarioContexto.EmpresaId ||
                entry.Entity.EmpresaId != usuarioContexto.EmpresaId)
            {
                throw new ViolacaoIsolamentoTenantException();
            }

            entry.Property(nameof(EntidadeEmpresaBase.EmpresaId)).IsModified = false;
        }
    }

    private void AtualizarAuditoria()
    {
        var agora = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<EntidadeBase>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(EntidadeBase.CriadoEmUtc)).CurrentValue = agora;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(EntidadeBase.AtualizadoEmUtc)).CurrentValue = agora;
                entry.Property(nameof(EntidadeBase.CriadoEmUtc)).IsModified = false;
            }
        }
    }
}
