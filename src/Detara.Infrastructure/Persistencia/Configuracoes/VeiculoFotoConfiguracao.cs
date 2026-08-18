using Detara.Domain.Clientes;
using Detara.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class VeiculoFotoConfiguracao : IEntityTypeConfiguration<VeiculoFoto>
{
    public void Configure(EntityTypeBuilder<VeiculoFoto> builder)
    {
        builder.ToTable("VeiculosFotos");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ChaveStorage).HasMaxLength(500).IsRequired();
        builder.Property(item => item.NomeOriginal).HasMaxLength(255).IsRequired();
        builder.Property(item => item.ContentType).HasMaxLength(30).IsRequired();
        builder.HasIndex(item => new { item.EmpresaId, item.ChaveStorage }).IsUnique();
        builder.HasIndex(item => new { item.EmpresaId, item.VeiculoId, item.CriadoEmUtc });
        builder.HasIndex(item => new { item.EmpresaId, item.VeiculoId, item.EhPrincipal })
            .IsUnique()
            .HasFilter("[EhPrincipal] = 1");
        builder.HasOne<Veiculo>()
            .WithMany()
            .HasForeignKey(item => new { item.EmpresaId, item.VeiculoId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
