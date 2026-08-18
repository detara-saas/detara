using Detara.Domain.Atendimento;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class OrdemServicoFotoConfiguracao : IEntityTypeConfiguration<OrdemServicoFoto>
{
    public void Configure(EntityTypeBuilder<OrdemServicoFoto> builder)
    {
        builder.ToTable("OrdensServicoFotos");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Categoria).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(item => item.ChaveStorage).HasMaxLength(500).IsRequired();
        builder.Property(item => item.NomeOriginal).HasMaxLength(255).IsRequired();
        builder.Property(item => item.ContentType).HasMaxLength(100).IsRequired();
        builder.HasIndex(item => new { item.EmpresaId, item.OrdemServicoId, item.Categoria });
        builder.HasIndex(item => item.ChaveStorage).IsUnique();
    }
}

internal sealed class HistoricoStatusOrdemServicoConfiguracao : IEntityTypeConfiguration<HistoricoStatusOrdemServico>
{
    public void Configure(EntityTypeBuilder<HistoricoStatusOrdemServico> builder)
    {
        builder.ToTable("OrdensServicoHistoricosStatus");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(item => item.Observacao).HasMaxLength(1000);
        builder.HasIndex(item => new { item.EmpresaId, item.OrdemServicoId, item.DataUtc });
    }
}
