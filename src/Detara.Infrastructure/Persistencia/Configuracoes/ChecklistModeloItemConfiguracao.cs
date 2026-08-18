using Detara.Domain.Atendimento;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class ChecklistModeloItemConfiguracao : IEntityTypeConfiguration<ChecklistModeloItem>
{
    public void Configure(EntityTypeBuilder<ChecklistModeloItem> builder)
    {
        builder.ToTable("ChecklistModeloItens");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Descricao).HasMaxLength(ChecklistModelo.LimiteDescricaoItem).IsRequired();
        builder.HasIndex(item => new { item.EmpresaId, item.ChecklistModeloId, item.Ordem }).IsUnique();
    }
}
