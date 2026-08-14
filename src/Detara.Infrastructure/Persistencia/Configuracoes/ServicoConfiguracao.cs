using Detara.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class ServicoConfiguracao : IEntityTypeConfiguration<Servico>
{
    public void Configure(EntityTypeBuilder<Servico> builder)
    {
        builder.ToTable("Servicos"); builder.HasKey(x => x.Id); builder.HasAlternateKey(x => new { x.EmpresaId, x.Id });
        builder.Property(x => x.Nome).HasMaxLength(160).IsRequired(); builder.Property(x => x.Descricao).HasMaxLength(2000);
        builder.Property(x => x.TipoPrecificacao).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.PrecoBase).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.EmpresaId, x.CategoriaServicoId, x.Nome }).IsUnique();
        builder.HasIndex(x => new { x.EmpresaId, x.CategoriaServicoId }); builder.HasIndex(x => new { x.EmpresaId, x.EhAtivo });
        builder.HasOne<Empresa>().WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Pacotes).WithOne(x => x.Servico).HasForeignKey(x => new { x.EmpresaId, x.ServicoId }).HasPrincipalKey(x => new { x.EmpresaId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
