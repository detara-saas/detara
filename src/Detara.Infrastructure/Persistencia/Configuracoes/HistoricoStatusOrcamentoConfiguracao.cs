using Detara.Domain.Atendimento;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class HistoricoStatusOrcamentoConfiguracao : IEntityTypeConfiguration<HistoricoStatusOrcamento>
{
    public void Configure(EntityTypeBuilder<HistoricoStatusOrcamento> builder)
    {
        builder.ToTable("OrcamentosHistoricosStatus");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(x => x.Observacao).HasMaxLength(1000);
        builder.HasIndex(x => new { x.EmpresaId, x.OrcamentoId, x.DataUtc });
    }
}
