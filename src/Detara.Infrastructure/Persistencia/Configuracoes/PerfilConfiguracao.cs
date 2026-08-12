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
        builder.HasIndex(x => new { x.EmpresaId, x.Nome }).IsUnique();

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
