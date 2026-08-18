using Detara.Application.Atendimento;
using Detara.Domain.Atendimento;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Atendimento;

internal sealed class ConfiguracoesOperacionaisRepositorio(DetaraDbContext dbContext)
    : IConfiguracoesOperacionaisRepositorio
{
    public Task<ConfiguracaoOperacionalAtendimento?> ObterConfiguracaoAsync(
        bool paraAlteracao,
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.ConfiguracoesOperacionaisAtendimento.AsQueryable();
        if (!paraAlteracao)
        {
            consulta = consulta.AsNoTracking();
        }

        return consulta.SingleOrDefaultAsync(cancellationToken);
    }

    public Task<ChecklistModelo?> ObterChecklistAsync(
        bool paraAlteracao,
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.ChecklistModelos
            .Include(item => item.Itens)
            .AsQueryable();
        if (!paraAlteracao)
        {
            consulta = consulta.AsNoTracking();
        }

        return consulta.SingleOrDefaultAsync(cancellationToken);
    }

    public void Adicionar(ConfiguracaoOperacionalAtendimento configuracao) =>
        dbContext.ConfiguracoesOperacionaisAtendimento.Add(configuracao);

    public void Adicionar(ChecklistModelo checklist) =>
        dbContext.ChecklistModelos.Add(checklist);

    public void RemoverItensAtuais(ChecklistModelo checklist) =>
        dbContext.ChecklistModeloItens.RemoveRange(checklist.Itens);

    public void AdicionarItensAtuais(ChecklistModelo checklist) =>
        dbContext.ChecklistModeloItens.AddRange(checklist.Itens);

    public Task SalvarAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
