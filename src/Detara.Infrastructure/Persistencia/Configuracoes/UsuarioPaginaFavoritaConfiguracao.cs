using Detara.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class UsuarioPaginaFavoritaConfiguracao
    : IEntityTypeConfiguration<UsuarioPaginaFavorita>
{
    public void Configure(EntityTypeBuilder<UsuarioPaginaFavorita> builder)
    {
        builder.ToTable("UsuariosPaginasFavoritas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Pagina).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => new { x.EmpresaId, x.UsuarioPreferenciaId, x.Pagina }).IsUnique();
        builder.HasIndex(x => new { x.EmpresaId, x.UsuarioPreferenciaId, x.Ordem });
        builder.HasOne<UsuarioPreferencia>()
            .WithMany()
            .HasForeignKey(x => new { x.EmpresaId, x.UsuarioPreferenciaId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
