using Detara.Domain.Notificacoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class ConfiguracaoNotificacaoEmpresaConfiguracao : IEntityTypeConfiguration<ConfiguracaoNotificacaoEmpresa>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoNotificacaoEmpresa> builder)
    {
        builder.ToTable("ConfiguracoesNotificacaoEmpresa");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ResponderParaEmail).HasMaxLength(200);
        builder.Property(x => x.Versao).IsConcurrencyToken();
        builder.HasIndex(x => x.EmpresaId).IsUnique();
    }
}

internal sealed class TemplateEmailEmpresaConfiguracao : IEntityTypeConfiguration<TemplateEmailEmpresa>
{
    public void Configure(EntityTypeBuilder<TemplateEmailEmpresa> builder)
    {
        builder.ToTable("TemplatesEmailEmpresa");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Assunto).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CorpoHtmlSanitizado).HasMaxLength(50 * 1024).IsRequired();
        builder.HasIndex(x => new { x.EmpresaId, x.Tipo }).IsUnique();
    }
}

internal sealed class NotificacaoEmailConfiguracao : IEntityTypeConfiguration<NotificacaoEmail>
{
    public void Configure(EntityTypeBuilder<NotificacaoEmail> builder)
    {
        builder.ToTable("NotificacoesEmail");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.EmpresaId, x.Id });
        builder.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(x => x.DestinatarioEmailSnapshot).HasMaxLength(200);
        builder.Property(x => x.DestinatarioNomeSnapshot).HasMaxLength(160).IsRequired();
        builder.Property(x => x.AssuntoSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CorpoHtmlSnapshot).HasMaxLength(100 * 1024).IsRequired();
        builder.Property(x => x.OrigemTemplate).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.TipoProximaTentativa).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.ResponderParaSnapshot).HasMaxLength(200);
        builder.Property(x => x.ProvedorMensagemId).HasMaxLength(200);
        builder.Property(x => x.UltimoErroSeguro).HasMaxLength(500);
        builder.Property(x => x.Versao).IsConcurrencyToken();
        builder.HasIndex(x => new { x.EmpresaId, x.Tipo, x.OrdemServicoId });
        builder.HasIndex(x => new { x.EmpresaId, x.Status, x.ProximaTentativaEmUtc });
        builder.HasMany(x => x.Tentativas).WithOne().HasForeignKey(x => new { x.EmpresaId, x.NotificacaoEmailId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TentativaNotificacaoEmailConfiguracao : IEntityTypeConfiguration<TentativaNotificacaoEmail>
{
    public void Configure(EntityTypeBuilder<TentativaNotificacaoEmail> builder)
    {
        builder.ToTable("TentativasNotificacaoEmail");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Resultado).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(x => x.ProvedorMensagemId).HasMaxLength(200);
        builder.Property(x => x.ErroSeguro).HasMaxLength(500);
        builder.HasIndex(x => new { x.EmpresaId, x.NotificacaoEmailId, x.Numero }).IsUnique();
    }
}
