using Detara.Domain.Atendimento;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class OrdemServicoChecklistConfiguracao : IEntityTypeConfiguration<OrdemServicoChecklist>
{
    public void Configure(EntityTypeBuilder<OrdemServicoChecklist> builder)
    {
        builder.ToTable("OrdensServicoChecklist");
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.EmpresaId, item.Id });
        builder.Property(item => item.NomeSnapshot).HasMaxLength(120).IsRequired();
        builder.Ignore(item => item.EstaCompleto);
        builder.HasIndex(item => new { item.EmpresaId, item.OrdemServicoId }).IsUnique();
        builder.HasMany(item => item.Itens).WithOne(item => item.Checklist)
            .HasForeignKey(item => new { item.EmpresaId, item.ChecklistId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id }).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OrdemServicoChecklistItemConfiguracao : IEntityTypeConfiguration<OrdemServicoChecklistItem>
{
    public void Configure(EntityTypeBuilder<OrdemServicoChecklistItem> builder)
    {
        builder.ToTable("OrdensServicoChecklistItens");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.DescricaoSnapshot).HasMaxLength(240).IsRequired();
        builder.Property(item => item.Resposta).HasConversion<string>().HasMaxLength(24);
        builder.Property(item => item.Observacao).HasMaxLength(1000);
        builder.HasIndex(item => new { item.EmpresaId, item.ChecklistId, item.Ordem }).IsUnique();
    }
}
