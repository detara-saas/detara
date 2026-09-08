using Detara.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class CategoriaDespesaConfiguracao : IEntityTypeConfiguration<CategoriaDespesa>
{
    public void Configure(EntityTypeBuilder<CategoriaDespesa> b)
    {
        b.ToTable("CategoriasDespesa");
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.EmpresaId, x.Id });
        b.Property(x => x.Nome).HasMaxLength(100).IsRequired();
        b.Property(x => x.NomeNormalizado).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.EmpresaId, x.NomeNormalizado }).IsUnique();
        b.Property(x => x.Versao).IsConcurrencyToken();
    }
}

internal sealed class ContaPagarConfiguracao : IEntityTypeConfiguration<ContaPagar>
{
    public void Configure(EntityTypeBuilder<ContaPagar> b)
    {
        b.ToTable("ContasPagar");
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.EmpresaId, x.Id });
        b.Property(x => x.Descricao).HasMaxLength(200).IsRequired();
        b.Property(x => x.CategoriaNomeSnapshot).HasMaxLength(100).IsRequired();
        b.Property(x => x.Fornecedor).HasMaxLength(160);
        b.Property(x => x.Observacao).HasMaxLength(2000);
        b.Property(x => x.Valor).HasPrecision(18, 2);
        b.Property(x => x.ValorPago).HasPrecision(18, 2);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Origem).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Versao).IsConcurrencyToken();
        b.HasOne<CategoriaDespesa>().WithMany().HasForeignKey(x => new { x.EmpresaId, x.CategoriaDespesaId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DespesaRecorrente>().WithMany().HasForeignKey(x => new { x.EmpresaId, x.DespesaRecorrenteId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.DespesaRecorrenteId, x.Competencia })
            .IsUnique().HasFilter("[DespesaRecorrenteId] IS NOT NULL");
        b.HasIndex(x => new { x.EmpresaId, x.Competencia, x.Status, x.DataVencimento });
        b.HasIndex(x => new { x.EmpresaId, x.CategoriaDespesaId, x.Competencia });
        b.HasMany(x => x.Pagamentos).WithOne().HasForeignKey(x => new { x.EmpresaId, x.ContaPagarId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DespesaRecorrenteConfiguracao : IEntityTypeConfiguration<DespesaRecorrente>
{
    public void Configure(EntityTypeBuilder<DespesaRecorrente> b)
    {
        b.ToTable("DespesasRecorrentes");
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.EmpresaId, x.Id });
        b.Property(x => x.Descricao).HasMaxLength(200).IsRequired();
        b.Property(x => x.Fornecedor).HasMaxLength(160);
        b.Property(x => x.Observacao).HasMaxLength(2000);
        b.Property(x => x.Valor).HasPrecision(18, 2);
        b.Property(x => x.Versao).IsConcurrencyToken();
        b.HasOne<CategoriaDespesa>().WithMany().HasForeignKey(x => new { x.EmpresaId, x.CategoriaDespesaId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EhAtivo, x.ProximaCompetencia, x.EmpresaId });
    }
}

internal sealed class PagamentoContaPagarConfiguracao : IEntityTypeConfiguration<PagamentoContaPagar>
{
    public void Configure(EntityTypeBuilder<PagamentoContaPagar> b)
    {
        b.ToTable("PagamentosContasPagar");
        b.HasKey(x => x.Id);
        b.Property(x => x.Valor).HasPrecision(18, 2);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.MotivoEstorno).HasMaxLength(500);
        b.HasIndex(x => new { x.EmpresaId, x.ContaPagarId }).IsUnique().HasFilter("[Status] = 'Confirmado'");
        b.HasIndex(x => new { x.EmpresaId, x.DataPagamento, x.Status });
    }
}
