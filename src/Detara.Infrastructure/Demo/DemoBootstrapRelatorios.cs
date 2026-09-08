using Detara.Domain.Agenda;
using Detara.Domain.Atendimento;
using Detara.Domain.Entidades;
using Detara.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Demo;

public sealed partial class DemoBootstrapService
{
    // Somente presentation, após reconstruir explicitamente o tenant sintético. Nenhum fluxo HTTP usa este seed.
    private async Task ComplementarHistoricoRelatoriosAsync(Guid empresaId, CancellationToken ct)
    {
        await using var db = CriarContexto(new ContextoDemo(empresaId, Guid.NewGuid()));
        var empresa = await db.Empresas.SingleAsync(x => x.Id == empresaId && x.Slug == SlugEmpresa, ct);
        var usuario = await db.Usuarios.SingleAsync(x => x.Email == EmailAdministrador, ct);
        var clientes = await db.Clientes.OrderBy(x => x.Nome).Take(3).ToArrayAsync(ct);
        var veiculos = await db.Veiculos.ToArrayAsync(ct);
        var servicos = await db.Servicos.OrderBy(x => x.Nome).Take(3).ToArrayAsync(ct);
        var hoje = ObterHojeLocal();
        var fuso = TimeZoneInfo.FindSystemTimeZoneById(FusoHorario);
        DateTime Utc(DateOnly d, int hora) => TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(new TimeOnly(hora, 0)), fuso);
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        for (var i = 0; i < 5; i++)
        {
            var cliente = clientes[i == 0 ? 0 : (i - 1) % 3];
            var veiculo = veiculos.First(x => x.ClienteId == cliente.Id);
            var servico = servicos[i % servicos.Length];
            var data = i == 0 ? hoje.AddMonths(-1) : hoje.AddDays(-i);
            var inicio = Utc(data, 9); var concluida = Utc(data, 12);
            var valor = 150m + i * 100m;
            var agenda = new Agendamento(empresaId, cliente.Id, cliente.Nome, veiculo.Id, "Veículo de demonstração", veiculo.Placa,
                inicio, 180, "Histórico sintético para relatórios.", null,
                [new(TipoItemAgendamento.Servico, servico.Id, servico.Nome, null, Detara.Domain.Catalogo.TipoPrecificacao.Fixo, valor, 180)]);
            agenda.AlterarStatus(StatusAgendamento.Compareceu);
            agenda.AlterarStatus(StatusAgendamento.Concluido);
            var os = new OrdemServico(empresaId, data.Year, new(cliente.Id, cliente.Nome, null, null, veiculo.Id, "Veículo de demonstração", veiculo.Placa),
                OrigemOrdemServico.Agendamento, null, agenda.Id, 180, 0, 0,
                [new(TipoItemOrcamento.Servico, servico.Id, null, null, servico.Nome, null, valor, 1, 1, OrigemComercialOrdemServico.AcordoDireto, inicio, usuario.Id, "Autorização sintética")], usuario.Id, inicio);
            os.RealizarCheckIn(new(NivelExigenciaOperacional.Desabilitado, NivelExigenciaOperacional.Desabilitado, NivelExigenciaOperacional.Desabilitado, null, []), null, null, usuario.Id);
            os.IniciarExecucao(usuario.Id, null); os.FinalizarExecucao(usuario.Id, null); os.Concluir(usuario.Id, null);
            var conta = new ContaReceber(empresaId, os.Id, os.Codigo, cliente.Id, cliente.Nome, veiculo.Id, "Veículo de demonstração", veiculo.Placa, valor, 0, 0, valor, data);
            var pagamento = conta.RegistrarPagamento(FormaPagamento.Pix, valor, 0, null, "Pagamento sintético", concluida.AddMinutes(10), usuario.Id);
            db.AddRange(agenda, os, conta);
            // Datas sintéticas coerentes sem abrir setters no domínio de produção.
            db.Entry(os).Property(x => x.CheckInEmUtc).CurrentValue = inicio.AddMinutes(5);
            db.Entry(os).Property(x => x.IniciadaEmUtc).CurrentValue = inicio.AddMinutes(10);
            db.Entry(os).Property(x => x.ExecucaoFinalizadaEmUtc).CurrentValue = concluida.AddMinutes(-10);
            db.Entry(os).Property(x => x.ConcluidaEmUtc).CurrentValue = concluida;
            foreach (var evento in os.Historico)
            {
                var dataEvento = evento.Status == StatusOrdemServico.Concluida ? concluida : evento.Status == StatusOrdemServico.AguardandoRetirada ? concluida.AddMinutes(-10) : evento.Status == StatusOrdemServico.EmExecucao ? inicio.AddMinutes(10) : inicio;
                db.Entry(evento).Property(x => x.DataUtc).CurrentValue = dataEvento;
            }
        }
        // Os cadastros e despesas da apresentação precedem suas transações históricas.
        var fundacao = Utc(hoje.AddMonths(-2), 8);
        await db.SaveChangesAsync(ct);
        // ExecuteUpdate evita alterar a proteção de auditoria do SaveChanges de produção.
        // O tenant foi reconstruído por presentation e validado pelo slug exclusivo acima.
        await db.Empresas.Where(x => x.Id == empresaId && x.Slug == SlugEmpresa).ExecuteUpdateAsync(s => s.SetProperty(x => x.CriadoEmUtc, fundacao), ct);
        await db.Clientes.Where(x => x.EmpresaId == empresaId).ExecuteUpdateAsync(s => s.SetProperty(x => x.CriadoEmUtc, fundacao), ct);
        await db.Veiculos.Where(x => x.EmpresaId == empresaId).ExecuteUpdateAsync(s => s.SetProperty(x => x.CriadoEmUtc, fundacao), ct);
        await db.Agendamentos.Where(x => x.EmpresaId == empresaId).ExecuteUpdateAsync(s => s.SetProperty(x => x.CriadoEmUtc, fundacao), ct);
        await db.OrdensServico.Where(x => x.EmpresaId == empresaId).ExecuteUpdateAsync(s => s.SetProperty(x => x.CriadoEmUtc, fundacao), ct);
        await db.ContasReceber.Where(x => x.EmpresaId == empresaId).ExecuteUpdateAsync(s => s.SetProperty(x => x.CriadoEmUtc, fundacao), ct);
        await db.ContasPagar.Where(x => x.EmpresaId == empresaId).ExecuteUpdateAsync(s => s.SetProperty(x => x.CriadoEmUtc, fundacao), ct);
        await transacao.CommitAsync(ct);
    }
}
