using Detara.Domain.Atendimento;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class OrcamentoItemConfiguracao : IEntityTypeConfiguration<OrcamentoItem>
{
    public void Configure(EntityTypeBuilder<OrcamentoItem> builder)
    {
        builder.ToTable("OrcamentosItens");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.EmpresaId, x.Id });
        builder.Property(x => x.TipoItem).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(x => x.NomeSnapshot).HasMaxLength(160).IsRequired();
        builder.Property(x => x.DescricaoSnapshot).HasMaxLength(2000);
        builder.Property(x => x.TipoPrecificacaoReferenciaSnapshot).HasConversion<string>().HasMaxLength(24);
        builder.Property(x => x.PrecoReferenciaSnapshot).HasPrecision(18, 2);
        builder.Property(x => x.ValorUnitario).HasPrecision(18, 2);
        builder.Property(x => x.Observacao).HasMaxLength(1000);
        builder.Ignore(x => x.Subtotal);
        builder.HasIndex(x => new { x.EmpresaId, x.OrcamentoId, x.Ordem }).IsUnique();
    }
}
