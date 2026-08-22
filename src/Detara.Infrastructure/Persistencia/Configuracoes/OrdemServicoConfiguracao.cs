using Detara.Domain.Atendimento;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class OrdemServicoConfiguracao : IEntityTypeConfiguration<OrdemServico>
{
    public void Configure(EntityTypeBuilder<OrdemServico> builder)
    {
        builder.ToTable("OrdensServico");
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.EmpresaId, item.Id });
        builder.Property(item => item.Codigo).HasMaxLength(32).IsRequired();
        builder.Property(item => item.Origem).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(item => item.ClienteNomeSnapshot).HasMaxLength(160).IsRequired();
        builder.Property(item => item.ClienteDocumentoSnapshot).HasMaxLength(20);
        builder.Property(item => item.ClienteTelefoneSnapshot).HasMaxLength(20);
        builder.Property(item => item.VeiculoDescricaoSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(item => item.VeiculoPlacaSnapshot).HasMaxLength(10);
        builder.Property(item => item.DescontoAutorizado).HasPrecision(18, 2);
        builder.Property(item => item.AcrescimoAutorizado).HasPrecision(18, 2);
        builder.Property(item => item.ObservacaoAutorizacaoDireta).HasMaxLength(1000);
        builder.Property(item => item.ChecklistEntradaSnapshot).HasConversion<string>().HasMaxLength(16);
        builder.Property(item => item.FotosEntradaSnapshot).HasConversion<string>().HasMaxLength(16);
        builder.Property(item => item.FotosSaidaSnapshot).HasConversion<string>().HasMaxLength(16);
        builder.Property(item => item.ObservacaoEntrada).HasMaxLength(2000);
        builder.Property(item => item.MotivoCancelamento).HasMaxLength(1000);
        builder.Ignore(item => item.SubtotalAutorizado);
        builder.Ignore(item => item.TotalAutorizado);
        builder.HasIndex(item => new { item.EmpresaId, item.Codigo }).IsUnique();
        builder.HasIndex(item => new { item.EmpresaId, item.Status });
        builder.HasIndex(item => new { item.EmpresaId, item.CriadoEmUtc });
        builder.HasIndex(item => new { item.EmpresaId, item.ClienteId });
        builder.HasIndex(item => new { item.EmpresaId, item.VeiculoId });
        builder.HasIndex(item => new { item.EmpresaId, item.OrcamentoOrigemId }).IsUnique().HasFilter("[OrcamentoOrigemId] IS NOT NULL");
        builder.HasIndex(item => new { item.EmpresaId, item.AgendamentoOrigemId }).IsUnique().HasFilter("[AgendamentoOrigemId] IS NOT NULL");
        builder.HasMany(item => item.Itens).WithOne(item => item.OrdemServico)
            .HasForeignKey(item => new { item.EmpresaId, item.OrdemServicoId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(item => item.Fotos).WithOne(item => item.OrdemServico)
            .HasForeignKey(item => new { item.EmpresaId, item.OrdemServicoId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(item => item.Historico).WithOne(item => item.OrdemServico)
            .HasForeignKey(item => new { item.EmpresaId, item.OrdemServicoId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Checklist).WithOne(item => item.OrdemServico)
            .HasForeignKey<OrdemServicoChecklist>(item => new { item.EmpresaId, item.OrdemServicoId })
            .HasPrincipalKey<OrdemServico>(item => new { item.EmpresaId, item.Id }).OnDelete(DeleteBehavior.Cascade);
    }
}
