using Detara.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class UsuarioPreferenciaConfiguracao : IEntityTypeConfiguration<UsuarioPreferencia>
{
    public void Configure(EntityTypeBuilder<UsuarioPreferencia> builder)
    {
        builder.ToTable("UsuariosPreferencias");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.EmpresaId, x.Id });
        builder.Property(x => x.Tema).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Idioma).HasMaxLength(10).IsRequired();
        builder.Property(x => x.PaginaInicial).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => new { x.EmpresaId, x.UsuarioId }).IsUnique();
        builder.HasOne<Usuario>()
            .WithOne()
            .HasForeignKey<UsuarioPreferencia>(x => new { x.EmpresaId, x.UsuarioId })
            .HasPrincipalKey<Usuario>(x => new { x.EmpresaId, x.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
