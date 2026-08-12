using Detara.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class PermissaoConfiguracao : IEntityTypeConfiguration<Permissao>
{
    public void Configure(EntityTypeBuilder<Permissao> builder)
    {
        builder.ToTable("Permissoes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Codigo).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Descricao).HasMaxLength(240).IsRequired();
        builder.HasIndex(x => x.Codigo).IsUnique();
    }
}
