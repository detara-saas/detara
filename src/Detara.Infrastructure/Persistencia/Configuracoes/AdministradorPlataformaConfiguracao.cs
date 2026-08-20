using Detara.Domain.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class AdministradorPlataformaConfiguracao
    : IEntityTypeConfiguration<AdministradorPlataforma>
{
    public void Configure(EntityTypeBuilder<AdministradorPlataforma> builder)
    {
        builder.ToTable("AdministradoresPlataforma");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Nome).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EmailNormalizado).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SenhaHash).HasMaxLength(500).IsRequired();
        builder.Property(x => x.SegredoTotpProtegido).HasMaxLength(2000);
        builder.Property(x => x.UltimoTimestepTotpAceito).IsConcurrencyToken();
        builder.Property(x => x.VersaoSeguranca).IsConcurrencyToken().HasDefaultValue(1L);
        builder.HasIndex(x => x.EmailNormalizado).IsUnique();
    }
}
