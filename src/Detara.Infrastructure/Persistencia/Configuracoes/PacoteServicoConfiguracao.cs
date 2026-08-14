using Detara.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detara.Infrastructure.Persistencia.Configuracoes;

internal sealed class PacoteServicoConfiguracao : IEntityTypeConfiguration<PacoteServico>
{
    public void Configure(EntityTypeBuilder<PacoteServico> builder)
    {
        builder.ToTable("PacotesServicos"); builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.EmpresaId, x.PacoteId, x.ServicoId }).IsUnique();
        builder.HasIndex(x => new { x.EmpresaId, x.PacoteId, x.Ordem });
        builder.HasOne<Empresa>().WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
    }
}
