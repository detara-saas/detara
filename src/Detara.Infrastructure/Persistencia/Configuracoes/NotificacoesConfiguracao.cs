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
        builder.Property(x => x.PermitirComunicacaoWhatsApp).IsRequired();
        builder.Property(x => x.CanalAutomaticoVeiculoPronto).HasConversion<string>()
            .HasMaxLength(16).IsRequired();
        builder.Property(x => x.Versao).IsConcurrencyToken();
        builder.HasIndex(x => x.EmpresaId).IsUnique();
    }
}

internal sealed class ComunicacaoClienteConfiguracao : IEntityTypeConfiguration<ComunicacaoCliente>
{
    public void Configure(EntityTypeBuilder<ComunicacaoCliente> builder)
    {
        builder.ToTable("ComunicacoesCliente");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Canal).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Origem).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Mensagem).HasMaxLength(100 * 1024).IsRequired();
        builder.Property(x => x.TemplateNomeSnapshot).HasMaxLength(160);
        builder.Property(x => x.DestinatarioSnapshot).HasMaxLength(200);
        builder.Property(x => x.ProvedorMensagemId).HasMaxLength(200);
        builder.Property(x => x.UltimoErroSeguro).HasMaxLength(500);
        builder.Property(x => x.Versao).IsConcurrencyToken();
        builder.HasIndex(x => new { x.EmpresaId, x.OrdemServicoId, x.CriadoEmUtc });
        builder.HasIndex(x => new { x.EmpresaId, x.Canal, x.Status, x.ProcessamentoIniciadoEmUtc });
    }
}

internal sealed class SessaoWhatsAppEmpresaConfiguracao : IEntityTypeConfiguration<SessaoWhatsAppEmpresa>
{
    public void Configure(EntityTypeBuilder<SessaoWhatsAppEmpresa> builder)
    {
        builder.ToTable("SessoesWhatsAppEmpresa");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SessionKey).HasMaxLength(80).IsRequired();
        builder.Property(x => x.NumeroConectado).HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(x => x.UltimoErroSeguro).HasMaxLength(500);
        builder.Property(x => x.Versao).IsConcurrencyToken();
        builder.HasIndex(x => x.EmpresaId).IsUnique();
        builder.HasIndex(x => x.SessionKey).IsUnique();
    }
}

internal sealed class TemplateComunicacaoEmpresaConfiguracao : IEntityTypeConfiguration<TemplateComunicacaoEmpresa>
{
    public void Configure(EntityTypeBuilder<TemplateComunicacaoEmpresa> builder)
    {
        builder.ToTable("TemplatesComunicacaoEmpresa");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Canal).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Nome).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Assunto).HasMaxLength(200);
        builder.Property(x => x.Conteudo).HasMaxLength(50 * 1024).IsRequired();
        builder.HasIndex(x => new { x.EmpresaId, x.Canal, x.Tipo }).IsUnique();
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
