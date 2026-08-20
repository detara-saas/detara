using Detara.Domain.Entidades;
using Detara.Domain.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class AuditoriaPlataformaConfiguracao : IEntityTypeConfiguration<AuditoriaPlataforma>
{
    public void Configure(EntityTypeBuilder<AuditoriaPlataforma> builder)
    {
        builder.ToTable("AuditoriasPlataforma");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TipoAcao).HasMaxLength(120).IsRequired();
        builder.Property(x => x.TraceId).HasMaxLength(160);
        builder.Property(x => x.DescricaoSegura).HasMaxLength(500);
        builder.HasIndex(x => x.CriadoEmUtc);
        builder.HasIndex(x => new { x.AdministradorPlataformaId, x.CriadoEmUtc });
        builder.HasIndex(x => new { x.EmpresaAlvoId, x.CriadoEmUtc });
        builder.HasOne<AdministradorPlataforma>()
            .WithMany()
            .HasForeignKey(x => x.AdministradorPlataformaId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Empresa>()
            .WithMany()
            .HasForeignKey(x => x.EmpresaAlvoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
