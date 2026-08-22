using Detara.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class ContaReceberConfiguracao : IEntityTypeConfiguration<ContaReceber>
{
    public void Configure(EntityTypeBuilder<ContaReceber> builder)
    {
        builder.ToTable("ContasReceber");
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.EmpresaId, item.Id });
        builder.Property(item => item.OrdemServicoCodigoSnapshot).HasMaxLength(32).IsRequired();
        builder.Property(item => item.ClienteNomeSnapshot).HasMaxLength(160).IsRequired();
        builder.Property(item => item.VeiculoDescricaoSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(item => item.VeiculoPlacaSnapshot).HasMaxLength(10);
        builder.Property(item => item.SubtotalAutorizado).HasPrecision(18, 2);
        builder.Property(item => item.DescontoAutorizado).HasPrecision(18, 2);
        builder.Property(item => item.AcrescimoAutorizado).HasPrecision(18, 2);
        builder.Property(item => item.ValorOriginal).HasPrecision(18, 2);
        builder.Property(item => item.ValorRecebido).HasPrecision(18, 2);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(item => item.Versao).IsConcurrencyToken();
        builder.Ignore(item => item.ValorEmAberto);
        builder.HasIndex(item => new { item.EmpresaId, item.OrdemServicoId }).IsUnique();
        builder.HasIndex(item => new { item.EmpresaId, item.Status });
        builder.HasIndex(item => new { item.EmpresaId, item.DataCompetencia });
        builder.HasIndex(item => new { item.EmpresaId, item.DataVencimento });
        builder.HasIndex(item => new { item.EmpresaId, item.ClienteId });
        builder.HasIndex(item => new { item.EmpresaId, item.VeiculoId });
        builder.HasIndex(item => new { item.EmpresaId, item.CriadoEmUtc });
        builder.HasMany(item => item.Pagamentos).WithOne(item => item.ContaReceber)
            .HasForeignKey(item => new { item.EmpresaId, item.ContaReceberId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PagamentoConfiguracao : IEntityTypeConfiguration<Pagamento>
{
    public void Configure(EntityTypeBuilder<Pagamento> builder)
    {
        builder.ToTable("Pagamentos");
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.EmpresaId, item.Id });
        builder.Property(item => item.FormaPagamento).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(item => item.Valor).HasPrecision(18, 2);
        builder.Property(item => item.Taxa).HasPrecision(18, 2);
        builder.Property(item => item.Observacao).HasMaxLength(1000);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(item => item.MotivoEstorno).HasMaxLength(500);
        builder.Ignore(item => item.ValorLiquido);
        builder.HasIndex(item => new { item.EmpresaId, item.ContaReceberId, item.RecebidoEmUtc });
        builder.HasIndex(item => new { item.EmpresaId, item.Status, item.RecebidoEmUtc });
    }
}
