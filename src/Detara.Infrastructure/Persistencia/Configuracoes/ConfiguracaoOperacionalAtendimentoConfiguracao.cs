using Detara.Domain.Atendimento;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class ConfiguracaoOperacionalAtendimentoConfiguracao
    : IEntityTypeConfiguration<ConfiguracaoOperacionalAtendimento>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoOperacionalAtendimento> builder)
    {
        builder.ToTable("ConfiguracoesOperacionaisAtendimento");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ChecklistEntrada).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.FotosEntrada).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.FotosSaida).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(item => item.EmpresaId).IsUnique();
    }
}
