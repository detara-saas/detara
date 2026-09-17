using Detara.Domain.Entidades;
using Detara.Domain.Identidade;
using Detara.Domain.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class SessaoAutenticacaoConfiguracao : IEntityTypeConfiguration<SessaoAutenticacao>
{
    public void Configure(EntityTypeBuilder<SessaoAutenticacao> builder)
    {
        builder.ToTable("SessoesAutenticacao", tabela => tabela.HasCheckConstraint(
            "CK_SessoesAutenticacao_Identidade",
            "([TipoIdentidade] = 'Tenant' AND [UsuarioId] IS NOT NULL AND [EmpresaId] IS NOT NULL AND [AdministradorPlataformaId] IS NULL AND [VersaoSegurancaEmpresa] IS NOT NULL) OR " +
            "([TipoIdentidade] = 'AdministradorPlataforma' AND [UsuarioId] IS NULL AND [EmpresaId] IS NULL AND [AdministradorPlataformaId] IS NOT NULL AND [VersaoSegurancaEmpresa] IS NULL)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TipoIdentidade).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.TokenHash).HasMaxLength(44).IsRequired();
        builder.Property(x => x.MotivoRevogacao).HasMaxLength(80);
        builder.Property(x => x.Versao).IsConcurrencyToken().HasDefaultValue(1L);
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.FamiliaId, x.RevogadoEmUtc });
        builder.HasIndex(x => x.ExpiraEmUtc);
        builder.HasIndex(x => new { x.UsuarioId, x.EmpresaId });
        builder.HasIndex(x => x.AdministradorPlataformaId);
        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(
                nameof(SessaoAutenticacao.EmpresaId),
                nameof(SessaoAutenticacao.UsuarioId))
            .HasPrincipalKey(
                nameof(Usuario.EmpresaId),
                nameof(Usuario.Id))
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AdministradorPlataforma>()
            .WithMany()
            .HasForeignKey(x => x.AdministradorPlataformaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
