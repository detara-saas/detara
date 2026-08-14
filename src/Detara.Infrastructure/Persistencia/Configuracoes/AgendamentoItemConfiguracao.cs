using Detara.Domain.Agenda;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class AgendamentoItemConfiguracao : IEntityTypeConfiguration<AgendamentoItem>
{
    public void Configure(EntityTypeBuilder<AgendamentoItem> builder)
    {
        builder.ToTable("AgendamentosItens"); builder.HasKey(x => x.Id); builder.HasAlternateKey(x => new { x.EmpresaId, x.Id });
        builder.Property(x => x.TipoItem).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.NomeSnapshot).HasMaxLength(160).IsRequired(); builder.Property(x => x.DescricaoSnapshot).HasMaxLength(2000);
        builder.Property(x => x.TipoPrecificacaoSnapshot).HasConversion<string>().HasMaxLength(20).IsRequired(); builder.Property(x => x.PrecoReferenciaSnapshot).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.EmpresaId, x.AgendamentoId, x.Ordem }).IsUnique();
        builder.HasIndex(x => new { x.EmpresaId, x.TipoItem, x.ItemCatalogoId });
        builder.HasIndex(x => new { x.EmpresaId, x.AgendamentoId, x.TipoItem, x.ItemCatalogoId }).IsUnique();
    }
}
