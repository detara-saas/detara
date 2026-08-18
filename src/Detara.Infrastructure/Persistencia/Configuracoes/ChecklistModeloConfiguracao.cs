using Detara.Domain.Atendimento;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class ChecklistModeloConfiguracao : IEntityTypeConfiguration<ChecklistModelo>
{
    public void Configure(EntityTypeBuilder<ChecklistModelo> builder)
    {
        builder.ToTable("ChecklistModelos");
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.EmpresaId, item.Id });
        builder.Property(item => item.Nome).HasMaxLength(120).IsRequired();
        builder.Property(item => item.Descricao).HasMaxLength(500);
        builder.HasIndex(item => item.EmpresaId).IsUnique();
        builder.HasMany(item => item.Itens)
            .WithOne(item => item.ChecklistModelo)
            .HasForeignKey(item => new { item.EmpresaId, item.ChecklistModeloId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(item => item.Itens).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
