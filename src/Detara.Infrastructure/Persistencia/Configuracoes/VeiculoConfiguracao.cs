using Detara.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class VeiculoConfiguracao : IEntityTypeConfiguration<Veiculo>
{
    public void Configure(EntityTypeBuilder<Veiculo> builder)
    {
        builder.ToTable("Veiculos");
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.EmpresaId, item.Id });
        builder.Property(item => item.Placa).HasMaxLength(7).IsRequired();
        builder.Property(item => item.Marca).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Modelo).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Versao).HasMaxLength(80);
        builder.Property(item => item.Cor).HasMaxLength(50);
        builder.Property(item => item.Observacao).HasMaxLength(2000);
        builder.HasIndex(item => new { item.EmpresaId, item.Placa }).IsUnique();
        builder.HasIndex(item => new { item.EmpresaId, item.ClienteId });
        builder.HasOne<Empresa>()
            .WithMany()
            .HasForeignKey(item => item.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
