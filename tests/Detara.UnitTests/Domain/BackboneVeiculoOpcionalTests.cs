using Detara.Domain.Agenda;
using Detara.Domain.Atendimento;
using Detara.Domain.Catalogo;
using Detara.Domain.Financeiro;

namespace Detara.UnitTests.Domain;

public sealed class BackboneVeiculoOpcionalTests
{
    [Fact]
    public void Agenda_AceitaAusenciaCompletaDeVeiculo()
    {
        var agendamento = new Agendamento(Guid.NewGuid(), Guid.NewGuid(), "Cliente", null, null, null,
            DateTime.UtcNow.AddDays(1), 90, null, null, [ItemAgenda()]);

        Assert.Null(agendamento.VeiculoId);
        Assert.Null(agendamento.VeiculoDescricaoSnapshot);
    }

    [Fact]
    public void Orcamento_AceitaAusenciaCompletaDeVeiculo()
    {
        var orcamento = new Orcamento(Guid.NewGuid(), PartesOrcamento(), null, null,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7), null, null, null, 0, 0,
            [ItemOrcamento()], Guid.NewGuid());

        Assert.Null(orcamento.VeiculoId);
        Assert.Null(orcamento.VeiculoDescricaoSnapshot);
    }

    [Fact]
    public void OrdemServico_AceitaAusenciaCompletaDeVeiculo()
    {
        var usuarioId = Guid.NewGuid();
        var ordem = new OrdemServico(Guid.NewGuid(), 2026,
            new(Guid.NewGuid(), "Cliente", null, null, null, null, null),
            OrigemOrdemServico.Agendamento, null, Guid.NewGuid(), 90, 0, 0,
            [ItemOrdem(usuarioId)], usuarioId, DateTime.UtcNow, "Autorização direta");

        Assert.Null(ordem.VeiculoId);
        Assert.Null(ordem.VeiculoDescricaoSnapshot);
    }

    [Fact]
    public void ContaReceber_AceitaAusenciaCompletaDeVeiculo()
    {
        var conta = new ContaReceber(Guid.NewGuid(), Guid.NewGuid(), "OS-2026-0001",
            Guid.NewGuid(), "Cliente", null, null, null, 120, 0, 0, 120,
            DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.Null(conta.VeiculoId);
        Assert.Null(conta.VeiculoDescricaoSnapshot);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Agenda_RejeitaVinculoDeVeiculoIncompleto(bool somenteId)
    {
        Assert.Throws<ArgumentException>(() => new Agendamento(Guid.NewGuid(), Guid.NewGuid(), "Cliente",
            somenteId ? Guid.NewGuid() : null, somenteId ? null : "Veículo", null,
            DateTime.UtcNow.AddDays(1), 90, null, null, [ItemAgenda()]));
    }

    [Fact]
    public void ContaReceber_RejeitaGuidEmptyComoVeiculo()
    {
        Assert.Throws<ArgumentException>(() => new ContaReceber(Guid.NewGuid(), Guid.NewGuid(), "OS-2026-0001",
            Guid.NewGuid(), "Cliente", Guid.Empty, "Veículo", null, 120, 0, 0, 120,
            DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    private static ItemAgendamentoSnapshot ItemAgenda() => new(TipoItemAgendamento.Servico,
        Guid.NewGuid(), "Serviço", null, TipoPrecificacao.Fixo, 120, 90);

    private static PartesOrcamentoSnapshot PartesOrcamento() =>
        new(Guid.NewGuid(), "Cliente", null, null, null, null, null);

    private static ItemOrcamentoSnapshot ItemOrcamento() => new(TipoItemOrcamento.Personalizado,
        null, "Serviço", null, null, null, 120, 1, 1, null);

    private static ItemOrdemServicoSnapshot ItemOrdem(Guid usuarioId) => new(
        TipoItemOrcamento.Personalizado, null, null, null, "Serviço", null, 120, 1, 1,
        OrigemComercialOrdemServico.AcordoDireto, DateTime.UtcNow, usuarioId, null);
}
