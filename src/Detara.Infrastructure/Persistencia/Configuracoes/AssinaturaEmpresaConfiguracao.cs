using Detara.Domain.Assinaturas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class AssinaturaEmpresaConfiguracao : IEntityTypeConfiguration<AssinaturaEmpresa>
{
    public void Configure(EntityTypeBuilder<AssinaturaEmpresa> builder)
    {
        builder.ToTable("AssinaturasEmpresas");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.EmpresaId, x.Id });
        builder.HasIndex(x => x.EmpresaId).IsUnique();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.ValorMensal).HasPrecision(18, 2);
        builder.Property(x => x.AsaasCustomerId).HasMaxLength(120);
        builder.Property(x => x.AsaasSubscriptionId).HasMaxLength(120);
        builder.Property(x => x.Versao).IsConcurrencyToken();
        builder.HasOne<Detara.Domain.Entidades.Empresa>().WithMany().HasForeignKey(x => x.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class HistoricoAssinaturaEmpresaConfiguracao : IEntityTypeConfiguration<HistoricoAssinaturaEmpresa>
{
    public void Configure(EntityTypeBuilder<HistoricoAssinaturaEmpresa> builder)
    {
        builder.ToTable("HistoricosAssinaturasEmpresas");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.EmpresaId, x.AssinaturaId, x.OcorridoEmUtc });
        builder.HasIndex(x => new { x.EmpresaId, x.ReferenciaPagamento }).IsUnique()
            .HasFilter("[ReferenciaPagamento] IS NOT NULL");
        builder.Property(x => x.TipoEvento).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.StatusAnterior).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.StatusNovo).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Motivo).HasMaxLength(500);
        builder.Property(x => x.ReferenciaPagamento).HasMaxLength(160);
        builder.HasOne<AssinaturaEmpresa>().WithMany()
            .HasForeignKey(x => new { x.EmpresaId, x.AssinaturaId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Detara.Domain.Entidades.Empresa>().WithMany().HasForeignKey(x => x.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Detara.Domain.Entidades.Usuario>().WithMany()
            .HasForeignKey(x => new { x.EmpresaId, x.UsuarioId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Detara.Domain.Plataforma.AdministradorPlataforma>().WithMany()
            .HasForeignKey(x => x.AdministradorPlataformaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AceiteTermoAssinaturaConfiguracao : IEntityTypeConfiguration<AceiteTermoAssinatura>
{
    public void Configure(EntityTypeBuilder<AceiteTermoAssinatura> builder)
    {
        builder.ToTable("AceitesTermosAssinaturas");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.EmpresaId, x.AssinaturaId, x.VersaoTermo }).IsUnique();
        builder.Property(x => x.VersaoTermo).HasMaxLength(30);
        builder.Property(x => x.DocumentoChave).HasMaxLength(500);
        builder.Property(x => x.HashSha256).HasMaxLength(64).IsFixedLength();
        builder.Property(x => x.ValorMensal).HasPrecision(18, 2);
        builder.Property(x => x.EmpresaNome).HasMaxLength(200);
        builder.Property(x => x.EmpresaDocumento).HasMaxLength(20);
        builder.Property(x => x.ResponsavelNome).HasMaxLength(160);
        builder.Property(x => x.ResponsavelEmail).HasMaxLength(200);
        builder.Property(x => x.IpAceite).HasMaxLength(64);
        builder.HasOne<AssinaturaEmpresa>().WithMany()
            .HasForeignKey(x => new { x.EmpresaId, x.AssinaturaId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Detara.Domain.Entidades.Empresa>().WithMany().HasForeignKey(x => x.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Detara.Domain.Entidades.Usuario>().WithMany()
            .HasForeignKey(x => new { x.EmpresaId, x.UsuarioId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
