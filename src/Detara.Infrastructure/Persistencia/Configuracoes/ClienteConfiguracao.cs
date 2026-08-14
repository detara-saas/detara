using Detara.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class ClienteConfiguracao : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("Clientes");
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.EmpresaId, item.Id });
        builder.Property(item => item.Nome).HasMaxLength(160).IsRequired();
        builder.Property(item => item.TipoPessoa).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.CpfCnpj).HasMaxLength(14);
        builder.Property(item => item.Telefone).HasMaxLength(15);
        builder.Property(item => item.WhatsApp).HasMaxLength(15);
        builder.Property(item => item.Email).HasMaxLength(200);
        builder.Property(item => item.Observacao).HasMaxLength(2000);
        builder.HasIndex(item => new { item.EmpresaId, item.CpfCnpj })
            .IsUnique()
            .HasFilter("[CpfCnpj] IS NOT NULL");
        builder.HasIndex(item => new { item.EmpresaId, item.Nome });
        builder.HasIndex(item => new { item.EmpresaId, item.Telefone });
        builder.HasOne<Empresa>()
            .WithMany()
            .HasForeignKey(item => item.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Veiculos)
            .WithOne(item => item.Cliente)
            .HasForeignKey(item => new { item.EmpresaId, item.ClienteId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
