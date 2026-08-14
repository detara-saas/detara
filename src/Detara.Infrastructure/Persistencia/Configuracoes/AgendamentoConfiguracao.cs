using Detara.Domain.Agenda;
using Detara.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class AgendamentoConfiguracao : IEntityTypeConfiguration<Agendamento>
{
    public void Configure(EntityTypeBuilder<Agendamento> builder)
    {
        builder.ToTable("Agendamentos"); builder.HasKey(x => x.Id); builder.HasAlternateKey(x => new { x.EmpresaId, x.Id });
        builder.Property(x => x.ClienteNomeSnapshot).HasMaxLength(160).IsRequired();
        builder.Property(x => x.VeiculoDescricaoSnapshot).HasMaxLength(200).IsRequired(); builder.Property(x => x.VeiculoPlacaSnapshot).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(x => x.ObservacaoSolicitante).HasMaxLength(2000); builder.Property(x => x.ObservacaoInterna).HasMaxLength(4000); builder.Property(x => x.MotivoCancelamento).HasMaxLength(1000);
        builder.Ignore(x => x.FimUtc);
        builder.HasIndex(x => new { x.EmpresaId, x.InicioUtc }); builder.HasIndex(x => new { x.EmpresaId, x.Status, x.InicioUtc });
        builder.HasIndex(x => new { x.EmpresaId, x.ClienteId }); builder.HasIndex(x => new { x.EmpresaId, x.VeiculoId });
        builder.HasOne<Empresa>().WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Itens).WithOne(x => x.Agendamento).HasForeignKey(x => new { x.EmpresaId, x.AgendamentoId }).HasPrincipalKey(x => new { x.EmpresaId, x.Id }).OnDelete(DeleteBehavior.Cascade);
    }
}
