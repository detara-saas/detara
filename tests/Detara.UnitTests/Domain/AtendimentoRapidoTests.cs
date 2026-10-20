using Detara.Domain.Atendimento;

namespace Detara.UnitTests.Domain;

public sealed class AtendimentoRapidoTests
{
    [Fact]
    public void RegistroRealizado_ConcluiSemForjarEtapasOperacionais()
    {
        var data = DateTime.UtcNow.AddDays(-2);
        var usuarioId = Guid.NewGuid();
        var ordem = Criar(data, usuarioId);

        Assert.Equal(OrigemOrdemServico.AtendimentoRapido, ordem.Origem);
        Assert.Equal(StatusOrdemServico.Concluida, ordem.Status);
        Assert.Equal(150m, ordem.TotalAutorizado);
        Assert.Equal(data, ordem.ExecucaoFinalizadaEmUtc);
        Assert.Equal(data, ordem.ConcluidaEmUtc);
        Assert.Equal(usuarioId, ordem.ConcluidaPorUsuarioId);
        Assert.Null(ordem.AgendamentoOrigemId);
        Assert.Null(ordem.OrcamentoOrigemId);
        Assert.Null(ordem.CheckInEmUtc);
        Assert.Null(ordem.IniciadaEmUtc);
        Assert.Null(ordem.Checklist);
        Assert.Empty(ordem.Fotos);
        var historico = Assert.Single(ordem.Historico);
        Assert.Equal(StatusOrdemServico.Concluida, historico.Status);
        Assert.Equal(usuarioId, historico.UsuarioId);
        Assert.True(historico.DataUtc > data);
        Assert.Throws<InvalidOperationException>(() => ordem.IniciarExecucao(usuarioId, null));
        Assert.Throws<InvalidOperationException>(() => ordem.Cancelar(usuarioId, "Correção"));
    }

    [Fact]
    public void RegistroRealizado_NaoAceitaDataFutura() =>
        Assert.Throws<ArgumentException>(() => Criar(DateTime.UtcNow.AddDays(1), Guid.NewGuid()));

    [Fact]
    public void RegistroRealizado_NaoAceitaValorZero() =>
        Assert.Throws<ArgumentException>(() => Criar(DateTime.UtcNow.AddMinutes(-1), Guid.NewGuid(), 0));

    private static OrdemServico Criar(DateTime data, Guid usuarioId, decimal valor = 150m) =>
        OrdemServico.RegistrarAtendimentoRealizado(Guid.NewGuid(), data.Year,
            new(Guid.NewGuid(), "Cliente", null, null, Guid.NewGuid(), "Honda Civic", "ABC1D23"),
            [new(TipoItemOrcamento.Servico, Guid.NewGuid(), null, null, "Lavagem", null, valor, 1, 1,
                OrigemComercialOrdemServico.AcordoDireto, DateTime.UtcNow, usuarioId, null)],
            usuarioId, data, Guid.NewGuid(), new string('A', 64));
}
