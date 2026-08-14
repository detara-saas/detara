using Detara.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class PacoteConfiguracao : IEntityTypeConfiguration<Pacote>
{
    public void Configure(EntityTypeBuilder<Pacote> builder)
    {
        builder.ToTable("Pacotes"); builder.HasKey(x => x.Id); builder.HasAlternateKey(x => new { x.EmpresaId, x.Id });
        builder.Property(x => x.Nome).HasMaxLength(160).IsRequired(); builder.Property(x => x.Descricao).HasMaxLength(2000); builder.Property(x => x.Preco).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.EmpresaId, x.Nome }).IsUnique(); builder.HasIndex(x => new { x.EmpresaId, x.EhAtivo });
        builder.HasOne<Empresa>().WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Servicos).WithOne(x => x.Pacote).HasForeignKey(x => new { x.EmpresaId, x.PacoteId }).HasPrincipalKey(x => new { x.EmpresaId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
