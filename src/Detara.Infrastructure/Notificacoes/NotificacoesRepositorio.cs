using Detara.Application.Notificacoes;
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

    public Task<NotificacaoEmail?> ObterPorOrdemServicoAsync(Guid ordemServicoId, bool paraAlteracao, CancellationToken ct)
    {
        var query = db.NotificacoesEmail.Include(x => x.Tentativas).Where(x => x.OrdemServicoId == ordemServicoId);
        return (paraAlteracao ? query : query.AsNoTracking()).SingleOrDefaultAsync(ct);
    }

    public Task<bool> ExistePorOrdemServicoAsync(Guid ordemServicoId, TipoTemplateEmail tipo, CancellationToken ct)
    {
        if (db.NotificacoesEmail.Local.Any(x => x.OrdemServicoId == ordemServicoId && x.Tipo == tipo)) return Task.FromResult(true);
        return db.NotificacoesEmail.AnyAsync(x => x.OrdemServicoId == ordemServicoId && x.Tipo == tipo, ct);
    }
    public void Adicionar(ConfiguracaoNotificacaoEmpresa item) => db.Add(item);
    public void Adicionar(TemplateEmailEmpresa item) => db.Add(item);
    public void Adicionar(NotificacaoEmail item) => db.Add(item);
    public void Remover(TemplateEmailEmpresa item) => db.Remove(item);
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
