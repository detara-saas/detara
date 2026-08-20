using Detara.Domain.Entidades;
using Detara.Domain.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class ConviteAdministradorEmpresaConfiguracao
    : IEntityTypeConfiguration<ConviteAdministradorEmpresa>
{
    public void Configure(EntityTypeBuilder<ConviteAdministradorEmpresa> builder)
    {
        builder.ToTable("ConvitesAdministradoresEmpresa");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EmailDestinoSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TokenHash).HasMaxLength(128);
        builder.Property(x => x.UltimoErroSeguro).HasMaxLength(500);
        builder.Property(x => x.ProviderMessageId).HasMaxLength(200);
        builder.Property(x => x.Versao).IsConcurrencyToken().HasDefaultValue(1L);
        builder.HasIndex(x => x.TokenHash).IsUnique().HasFilter("[TokenHash] IS NOT NULL");
        builder.HasIndex(x => new { x.Status, x.ProximaTentativaEnvioEmUtc });
        builder.HasIndex(x => new { x.EmpresaId, x.UsuarioId });
        builder.HasOne<Empresa>()
            .WithMany()
            .HasForeignKey(x => x.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(x => new { x.EmpresaId, x.UsuarioId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AdministradorPlataforma>()
            .WithMany()
            .HasForeignKey(x => x.CriadoPorAdministradorPlataformaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
