using Detara.Domain.Assinaturas;

namespace Detara.UnitTests.Domain;

public sealed class AssinaturaEmpresaTests
{
    [Theory]
    [InlineData(2026, 9, 16, 2026, 9, 23, 2026, 10, 10)]
    [InlineData(2026, 10, 1, 2026, 10, 8, 2026, 10, 10)]
    [InlineData(2026, 10, 5, 2026, 10, 12, 2026, 11, 10)]
    [InlineData(2026, 10, 8, 2026, 10, 15, 2026, 11, 10)]
    [InlineData(2026, 12, 25, 2027, 1, 1, 2027, 1, 10)]
    public void Calendario_CalculaFimDoTesteEPrimeiroVencimentoPosterior(
        int ano, int mes, int dia, int anoFim, int mesFim, int diaFim,
        int anoVencimento, int mesVencimento, int diaVencimento)
    {
        var inicio = new DateOnly(ano, mes, dia);
        var fim = CalendarioAssinatura.CalcularFimTeste(inicio);
        var vencimento = CalendarioAssinatura.CalcularPrimeiroVencimento(fim);

        Assert.Equal(new DateOnly(anoFim, mesFim, diaFim), fim);
        Assert.Equal(new DateOnly(anoVencimento, mesVencimento, diaVencimento), vencimento);
    }

    [Fact]
    public void Calendario_SuspendeAoCompletarSeteDiasCorridosDeAtraso()
    {
        var vencimento = new DateOnly(2026, 10, 10);
        Assert.True(CalendarioAssinatura.DeveMarcarAtraso(vencimento, new(2026, 10, 11)));
        Assert.False(CalendarioAssinatura.DeveSuspender(vencimento, new(2026, 10, 16)));
        Assert.True(CalendarioAssinatura.DeveSuspender(vencimento, new(2026, 10, 17)));
    }

    [Theory]
    [InlineData(2026, 9, 23, 2026, 9, 20, 2026, 10, 10)]
    [InlineData(2026, 9, 23, 2026, 9, 23, 2026, 10, 10)]
    [InlineData(2026, 10, 12, 2026, 10, 8, 2026, 11, 10)]
    [InlineData(2026, 10, 8, 2026, 10, 12, 2026, 11, 10)]
    [InlineData(2026, 10, 8, 2026, 10, 10, 2026, 11, 10)]
    [InlineData(2026, 12, 20, 2026, 12, 22, 2027, 1, 10)]
    public void Calendario_UsaMaiorDataEPrimeiroDiaDezEstritamentePosterior(
        int anoFim, int mesFim, int diaFim, int anoConfirmacao, int mesConfirmacao,
        int diaConfirmacao, int anoVencimento, int mesVencimento, int diaVencimento)
    {
        var vencimento = CalendarioAssinatura.CalcularPrimeiroVencimento(
            new DateOnly(anoFim, mesFim, diaFim),
            new DateOnly(anoConfirmacao, mesConfirmacao, diaConfirmacao));

        Assert.Equal(new DateOnly(anoVencimento, mesVencimento, diaVencimento), vencimento);
    }

    [Fact]
    public void ConfirmacaoComercial_PreservaTrialCalculaVencimentoEMantemStatus()
    {
        var assinatura = new AssinaturaEmpresa(Guid.NewGuid(), 120m, new(2026, 9, 16));
        var fimTesteOriginal = assinatura.FimTeste;
        var registradaEmUtc = new DateTime(2026, 9, 20, 15, 0, 0, DateTimeKind.Utc);

        var alterou = assinatura.ConfirmarComercialmente(
            new DateOnly(2026, 9, 20), registradaEmUtc, assinatura.Versao);
        var versaoConfirmada = assinatura.Versao;
        var repetida = assinatura.ConfirmarComercialmente(
            new DateOnly(2026, 9, 21), registradaEmUtc.AddMinutes(1), versaoConfirmada - 1);

        Assert.True(alterou);
        Assert.False(repetida);
        Assert.Equal(new DateOnly(2026, 9, 23), fimTesteOriginal);
        Assert.Equal(fimTesteOriginal, assinatura.FimTeste);
        Assert.Equal(new DateOnly(2026, 9, 20), assinatura.DataConfirmacaoComercial);
        Assert.Equal(registradaEmUtc, assinatura.ConfirmacaoComercialRegistradaEmUtc);
        Assert.Equal(new DateOnly(2026, 10, 10), assinatura.PrimeiroVencimento);
        Assert.Equal(assinatura.PrimeiroVencimento, assinatura.ProximoVencimento);
        Assert.Equal(StatusAssinaturaEmpresa.EmTeste, assinatura.Status);
        Assert.Equal(versaoConfirmada, assinatura.Versao);
    }

    [Fact]
    public void Assinatura_PercorreAtrasoSuspensaoEReativacaoPorPagamento()
    {
        var assinatura = new AssinaturaEmpresa(Guid.NewGuid(), 120m, new(2026, 9, 16));
        Assert.Equal(StatusAssinaturaEmpresa.EmTeste, assinatura.Status);

        assinatura.MarcarEmAtraso(assinatura.Versao);
        Assert.Equal(StatusAssinaturaEmpresa.EmAtraso, assinatura.Status);

        assinatura.Suspender(new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc), assinatura.Versao);
        Assert.Equal(StatusAssinaturaEmpresa.Suspensa, assinatura.Status);

        assinatura.ConfirmarPagamento(new DateTime(2026, 10, 17, 13, 0, 0, DateTimeKind.Utc),
            new DateOnly(2026, 10, 17), null, null, assinatura.Versao);
        Assert.Equal(StatusAssinaturaEmpresa.Ativa, assinatura.Status);
        Assert.Null(assinatura.SuspensaEmUtc);
        Assert.Equal(new DateOnly(2026, 11, 10), assinatura.ProximoVencimento);
    }

    [Fact]
    public void Assinatura_CanceladaNaoPodeSerReativada()
    {
        var assinatura = new AssinaturaEmpresa(Guid.NewGuid(), 120m, new(2026, 9, 16));
        assinatura.Cancelar(DateTime.UtcNow, assinatura.Versao);

        Assert.Equal(StatusAssinaturaEmpresa.Cancelada, assinatura.Status);
        Assert.Throws<InvalidOperationException>(() => assinatura.Reativar(DateTime.UtcNow, assinatura.Versao));
    }

    [Fact]
    public void Assinatura_RejeitaValorNaoPositivoEConcorrenciaObsoleta()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AssinaturaEmpresa(Guid.NewGuid(), 0m, new(2026, 9, 16)));
        var assinatura = new AssinaturaEmpresa(Guid.NewGuid(), 120m, new(2026, 9, 16));
        var versao = assinatura.Versao;
        assinatura.MarcarEmAtraso(versao);
        Assert.Throws<InvalidOperationException>(() => assinatura.Suspender(DateTime.UtcNow, versao));
    }
}
