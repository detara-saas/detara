using Detara.Application.Dashboard;
using Detara.Domain.Notificacoes;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Notificacoes;

internal sealed class NotificacoesDashboardConsulta(DetaraDbContext db)
    : INotificacoesDashboardConsulta
{
    public async Task<IReadOnlyCollection<DashboardAtividadeItemDto>> ObterAtividadesAsync(
        Guid empresaId,
        DashboardPeriodoDto periodo,
        int limite,
        CancellationToken cancellationToken)
    {
        return await db.ComunicacoesCliente
            .AsNoTracking()
            .Where(item =>
                item.EmpresaId == empresaId &&
                item.Tipo == TipoComunicacaoCliente.VeiculoPronto &&
                item.Status == StatusComunicacaoCliente.Enviado &&
                item.OrdemServicoId.HasValue &&
                item.DataEnvioUtc >= periodo.InicioUtc &&
                item.DataEnvioUtc < periodo.FimExclusivoUtc)
            .OrderByDescending(item => item.DataEnvioUtc)
            .Take(limite)
            .Select(item => new DashboardAtividadeItemDto(
                TipoAtividadeDashboard.ComunicacaoEnviada,
                item.OrdemServicoId!.Value,
                item.DataEnvioUtc!.Value,
                item.Canal == CanalComunicacaoCliente.Email
                    ? "Aviso de veículo pronto enviado por e-mail"
                    : "Aviso de veículo pronto enviado por WhatsApp"))
            .ToArrayAsync(cancellationToken);
    }
}
