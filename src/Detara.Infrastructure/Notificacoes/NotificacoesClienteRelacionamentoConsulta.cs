using Detara.Application.Clientes;
using Detara.Domain.Notificacoes;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Notificacoes;

internal sealed class NotificacoesClienteRelacionamentoConsulta(DetaraDbContext db)
    : INotificacoesClienteRelacionamentoConsulta
{
    public Task<ComunicacaoRelacionamentoClienteResultado?> ObterUltimaAsync(
        Guid clienteId,
        CancellationToken cancellationToken) =>
        db.ComunicacoesCliente
            .AsNoTracking()
            .Where(item => item.ClienteId == clienteId &&
                item.Tipo == TipoComunicacaoCliente.VeiculoPronto)
            .OrderByDescending(item => item.DataEnvioUtc ?? item.CriadoEmUtc)
            .Select(item => new ComunicacaoRelacionamentoClienteResultado(
                item.Id,
                item.OrdemServicoId,
                item.Canal,
                item.Tipo,
                item.Status,
                item.Origem,
                item.DataEnvioUtc ?? item.CriadoEmUtc))
            .FirstOrDefaultAsync(cancellationToken);
}
