using Detara.Domain.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class CodigoRecuperacaoAdministradorPlataformaConfiguracao
    : IEntityTypeConfiguration<CodigoRecuperacaoAdministradorPlataforma>
{
    public void Configure(EntityTypeBuilder<CodigoRecuperacaoAdministradorPlataforma> builder)
    {
        builder.ToTable("CodigosRecuperacaoAdministradoresPlataforma");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CodigoHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.AdministradorPlataformaId, x.CodigoHash }).IsUnique();
        builder.HasOne<AdministradorPlataforma>()
            .WithMany()
            .HasForeignKey(x => x.AdministradorPlataformaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
