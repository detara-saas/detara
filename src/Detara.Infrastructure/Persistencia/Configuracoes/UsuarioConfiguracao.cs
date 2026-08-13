using Detara.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class UsuarioConfiguracao : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.EmpresaId, x.Id });
        builder.Property(x => x.Nome).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SenhaHash).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.EmpresaId, x.Email }).IsUnique();
        builder.HasIndex(x => new { x.EmpresaId, x.PerfilId });
        builder.HasOne(x => x.Perfil)
            .WithMany()
            .HasForeignKey(x => new { x.EmpresaId, x.PerfilId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
