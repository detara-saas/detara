using Detara.Application.Notificacoes;
using Detara.Domain.Atendimento;
using Detara.Domain.Notificacoes;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Notificacoes;

internal sealed class NotificacoesRepositorio(DetaraDbContext db) : INotificacoesRepositorio
{
    public Task<ConfiguracaoNotificacaoEmpresa?> ObterConfiguracaoAsync(CancellationToken ct) =>
        db.ConfiguracoesNotificacaoEmpresa.SingleOrDefaultAsync(ct);

    public Task<TemplateEmailEmpresa?> ObterTemplateAsync(TipoTemplateEmail tipo, bool paraAlteracao, CancellationToken ct)
    {
        var query = db.TemplatesEmailEmpresa.Where(x => x.Tipo == tipo);
        return (paraAlteracao ? query : query.AsNoTracking()).SingleOrDefaultAsync(ct);
    }

    public Task<NotificacaoEmail?> ObterUltimaPorOrdemServicoAsync(Guid ordemServicoId, bool paraAlteracao, CancellationToken ct)
    {
        var query = db.NotificacoesEmail.Include(x => x.Tentativas).Where(x => x.OrdemServicoId == ordemServicoId);
        return (paraAlteracao ? query : query.AsNoTracking())
            .OrderByDescending(x => x.CriadoEmUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
    }

    public Task<NotificacaoEmail?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        db.NotificacoesEmail.AsNoTracking().Include(x => x.Tentativas)
            .SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> ExistePorOrdemServicoAsync(Guid ordemServicoId, TipoTemplateEmail tipo, CancellationToken ct)
    {
        if (db.NotificacoesEmail.Local.Any(x => x.OrdemServicoId == ordemServicoId && x.Tipo == tipo)) return Task.FromResult(true);
        return db.NotificacoesEmail.AnyAsync(x => x.OrdemServicoId == ordemServicoId && x.Tipo == tipo, ct);
    }
    public void Adicionar(ConfiguracaoNotificacaoEmpresa item) => db.Add(item);
    public void Adicionar(TemplateEmailEmpresa item) => db.Add(item);
    public void Adicionar(NotificacaoEmail item) => db.Add(item);
    public void Remover(TemplateEmailEmpresa item) => db.Remove(item);
    public async Task<bool> TentarAdicionarESalvarAsync(NotificacaoEmail item, CancellationToken ct)
    {
        db.Add(item);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (await db.NotificacoesEmail.AsNoTracking().AnyAsync(x => x.Id == item.Id, ct))
                return false;
            throw;
        }
    }
    public async Task<bool> TentarSalvarAlteracaoAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }
    public Task SalvarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

internal sealed class PlataformaNotificacoesConsulta(DetaraDbContext db) : IPlataformaNotificacoesConsulta
{
    public Task<EmpresaNotificacoesInterna?> ObterEmpresaAsync(Guid empresaId, CancellationToken ct) =>
        db.Empresas.AsNoTracking().Where(x => x.Id == empresaId)
            .Select(x => new EmpresaNotificacoesInterna(x.Id, x.NomeFantasia)).SingleOrDefaultAsync(ct);

    public Task<UsuarioNotificacoesInterno?> ObterUsuarioAsync(Guid empresaId, Guid usuarioId, CancellationToken ct) =>
        db.Usuarios.AsNoTracking().Where(x => x.Id == usuarioId)
            .Select(x => new UsuarioNotificacoesInterno(x.Id, x.Nome, x.Email)).SingleOrDefaultAsync(ct);
}

internal sealed class ClientesNotificacoesConsulta(DetaraDbContext db) : IClientesNotificacoesConsulta
{
    public Task<ClienteNotificacoesInterno?> ObterClienteAsync(Guid empresaId, Guid clienteId, CancellationToken ct) =>
        db.Clientes.AsNoTracking().Where(x => x.Id == clienteId)
            .Select(x => new ClienteNotificacoesInterno(x.Id, x.Nome, x.Email)).SingleOrDefaultAsync(ct);
}

internal sealed class AtendimentoNotificacoesConsulta(DetaraDbContext db) : IAtendimentoNotificacoesConsulta
{
    public Task<OrdemServicoNotificacoesInterna?> ObterOrdemServicoAsync(Guid empresaId,
        Guid ordemServicoId, CancellationToken ct) =>
        db.OrdensServico.AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && x.Id == ordemServicoId)
            .Select(x => new OrdemServicoNotificacoesInterna(x.Id, x.Codigo, x.Status,
                x.ClienteId, x.ClienteNomeSnapshot, x.VeiculoDescricaoSnapshot,
                x.VeiculoPlacaSnapshot))
            .SingleOrDefaultAsync(ct);
}
