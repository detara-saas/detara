using Detara.Domain.Atendimento;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class OrdemServicoItemConfiguracao : IEntityTypeConfiguration<OrdemServicoItem>
{
    public void Configure(EntityTypeBuilder<OrdemServicoItem> builder)
    {
        builder.ToTable("OrdensServicoItens");
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.EmpresaId, item.Id });
        builder.Property(item => item.TipoItem).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(item => item.OrigemComercial).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(item => item.NomeSnapshot).HasMaxLength(160).IsRequired();
        builder.Property(item => item.DescricaoSnapshot).HasMaxLength(2000);
        builder.Property(item => item.ValorUnitarioAutorizado).HasPrecision(18, 2);
        builder.Property(item => item.ObservacaoAutorizacao).HasMaxLength(1000);
        builder.Ignore(item => item.Subtotal);
        builder.HasIndex(item => new { item.EmpresaId, item.OrdemServicoId, item.Ordem }).IsUnique();
        builder.HasIndex(item => new { item.EmpresaId, item.OrcamentoItemOrigemId }).IsUnique().HasFilter("[OrcamentoItemOrigemId] IS NOT NULL");
    }
}
