using Detara.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class CategoriaServicoConfiguracao : IEntityTypeConfiguration<CategoriaServico>
{
    public void Configure(EntityTypeBuilder<CategoriaServico> builder)
    {
        builder.ToTable("CategoriasServico"); builder.HasKey(x => x.Id); builder.HasAlternateKey(x => new { x.EmpresaId, x.Id });
        builder.Property(x => x.Nome).HasMaxLength(120).IsRequired(); builder.Property(x => x.Descricao).HasMaxLength(1000);
        builder.HasIndex(x => new { x.EmpresaId, x.Nome }).IsUnique(); builder.HasIndex(x => new { x.EmpresaId, x.EhAtivo });
        builder.HasOne<Empresa>().WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Servicos).WithOne(x => x.CategoriaServico).HasForeignKey(x => new { x.EmpresaId, x.CategoriaServicoId }).HasPrincipalKey(x => new { x.EmpresaId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
