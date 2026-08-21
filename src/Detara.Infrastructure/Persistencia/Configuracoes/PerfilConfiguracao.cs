using Detara.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class PerfilConfiguracao : IEntityTypeConfiguration<Perfil>
{
    public void Configure(EntityTypeBuilder<Perfil> builder)
    {
        builder.ToTable("Perfis");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.EmpresaId, x.Id });
        builder.Property(x => x.Nome).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NomeNormalizado).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Descricao).HasMaxLength(240);
        builder.Property(x => x.EhSistema).HasDefaultValue(false);
        builder.Property(x => x.Versao).IsConcurrencyToken().HasDefaultValue(1L);
        builder.HasIndex(x => new { x.EmpresaId, x.NomeNormalizado }).IsUnique();
        builder.HasOne<Empresa>()
            .WithMany()
            .HasForeignKey(x => x.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Permissoes)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "PerfilPermissao",
                right => right.HasOne<Permissao>().WithMany().HasForeignKey("PermissaoId"),
                left => left.HasOne<Perfil>().WithMany().HasForeignKey("PerfilId"),
                join =>
                {
                    join.ToTable("PerfisPermissoes");
                    join.HasKey("PerfilId", "PermissaoId");
                });
    }
}
