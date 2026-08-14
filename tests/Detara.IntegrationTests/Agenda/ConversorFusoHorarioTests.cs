using Detara.Application.Agenda;

namespace Detara.IntegrationTests.Agenda;

public sealed class ConversorFusoHorarioTests
{
    [Fact]
    public void AmericaSaoPaulo_ConverteLocalEUtc_SemDependerDoFusoDaMaquina()
    {
        var conversor = new ConversorFusoHorario();
        var local = new DateTime(2026, 8, 20, 9, 30, 0, DateTimeKind.Unspecified);

        var utc = conversor.ParaUtc(local, "America/Sao_Paulo");
        var retorno = conversor.ParaLocal(utc, "America/Sao_Paulo");

        Assert.Equal(new DateTime(2026, 8, 20, 12, 30, 0, DateTimeKind.Utc), utc);
        Assert.Equal(local, retorno);
        Assert.Equal(DateTimeKind.Unspecified, retorno.Kind);
    }
}
