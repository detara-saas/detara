using Detara.Domain.Atendimento;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class OrcamentoConfiguracao : IEntityTypeConfiguration<Orcamento>
{
    public void Configure(EntityTypeBuilder<Orcamento> builder)
    {
        builder.ToTable("Orcamentos");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.EmpresaId, x.Id });
        builder.Property(x => x.Codigo).HasMaxLength(32);
        builder.Property(x => x.ClienteNomeSnapshot).HasMaxLength(160).IsRequired();
        builder.Property(x => x.ClienteDocumentoSnapshot).HasMaxLength(20);
        builder.Property(x => x.ClienteTelefoneSnapshot).HasMaxLength(20);
        builder.Property(x => x.VeiculoDescricaoSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(x => x.VeiculoPlacaSnapshot).HasMaxLength(10);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(x => x.ObservacaoCliente).HasMaxLength(2000);
        builder.Property(x => x.ObservacaoInterna).HasMaxLength(4000);
        builder.Property(x => x.Condicoes).HasMaxLength(2000);
        builder.Property(x => x.Desconto).HasPrecision(18, 2);
        builder.Property(x => x.Acrescimo).HasPrecision(18, 2);
        builder.Ignore(x => x.Subtotal);
        builder.Ignore(x => x.Total);
        builder.HasIndex(x => new { x.EmpresaId, x.Codigo }).IsUnique().HasFilter("[Codigo] IS NOT NULL");
        builder.HasIndex(x => new { x.EmpresaId, x.Status });
        builder.HasIndex(x => new { x.EmpresaId, x.CriadoEmUtc });
        builder.HasIndex(x => new { x.EmpresaId, x.ClienteId });
        builder.HasIndex(x => new { x.EmpresaId, x.VeiculoId });
        builder.HasIndex(x => new { x.EmpresaId, x.AgendamentoOrigemId }).HasFilter("[AgendamentoOrigemId] IS NOT NULL");
        builder.HasIndex(x => new { x.EmpresaId, x.AgendamentoId }).HasFilter("[AgendamentoId] IS NOT NULL");
        builder.Property(x => x.AgendamentoId).IsConcurrencyToken();
        builder.HasIndex(x => new { x.EmpresaId, x.OrcamentoOrigemId }).HasFilter("[OrcamentoOrigemId] IS NOT NULL");
        builder.HasIndex(x => new { x.EmpresaId, x.OrdemServicoOrigemId }).HasFilter("[OrdemServicoOrigemId] IS NOT NULL");
        builder.HasMany(x => x.Itens).WithOne(x => x.Orcamento).HasForeignKey(x => new { x.EmpresaId, x.OrcamentoId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Historico).WithOne(x => x.Orcamento).HasForeignKey(x => new { x.EmpresaId, x.OrcamentoId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id }).OnDelete(DeleteBehavior.Cascade);
    }
}
