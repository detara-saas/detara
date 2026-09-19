using Detara.Domain.Capacidades;
using Detara.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class EmpresaCapacidadeConfiguracao : IEntityTypeConfiguration<EmpresaCapacidade>
{
    public void Configure(EntityTypeBuilder<EmpresaCapacidade> builder)
    {
        builder.ToTable("EmpresasCapacidades");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.EmpresaId, x.Codigo }).IsUnique();
        builder.Property(x => x.Codigo).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Versao).IsConcurrencyToken().HasDefaultValue(1L);
        builder.HasOne<Empresa>().WithMany().HasForeignKey(x => x.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
