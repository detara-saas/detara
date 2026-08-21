using Detara.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class EmpresaConfiguracao : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> builder)
    {
        builder.ToTable("Empresas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NomeFantasia).HasMaxLength(160).IsRequired();
        builder.Property(x => x.RazaoSocial).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CpfCnpj).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Telefone).HasMaxLength(30);
        builder.Property(x => x.Slug).HasMaxLength(100).IsRequired();
        builder.Property(x => x.FusoHorario).HasMaxLength(100).IsRequired().HasDefaultValue("America/Sao_Paulo");
        builder.Property(x => x.VersaoSeguranca).IsConcurrencyToken().HasDefaultValue(1L);
        builder.Property(x => x.VersaoCadastro).IsConcurrencyToken().HasDefaultValue(1L);
        builder.HasIndex(x => x.CpfCnpj).IsUnique();
        builder.HasIndex(x => x.Slug).IsUnique();
    }
}
