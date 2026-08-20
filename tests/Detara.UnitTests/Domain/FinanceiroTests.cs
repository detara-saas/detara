using Detara.Domain.Financeiro;

namespace Detara.UnitTests.Domain;

public sealed class FinanceiroTests
{
    private readonly Guid _empresaId = Guid.NewGuid();
    private readonly Guid _usuarioId = Guid.NewGuid();

    [Fact]
    public void ContaCriada_PreservaSnapshotValoresEVencimento()
    {
        var conta = CriarConta();

        Assert.Equal(240, conta.ValorOriginal);
        Assert.Equal(new DateOnly(2026, 8, 18), conta.DataVencimento);
        Assert.Equal("OS-2026-ABC", conta.OrdemServicoCodigoSnapshot);
        Assert.Equal(StatusContaReceber.EmAberto, conta.Status);
    }

    [Fact]
    public void PagamentoTotal_MarcaContaComoPaga()
    {
        var conta = CriarConta();
        conta.RegistrarPagamento(FormaPagamento.Pix, 240, 0, null, null, DateTime.UtcNow, _usuarioId);

        Assert.Equal(240, conta.ValorRecebido);
        Assert.Equal(0, conta.ValorEmAberto);
        Assert.Equal(StatusContaReceber.Pago, conta.Status);
    }

    [Fact]
    public void PagamentoParcialEMisto_RecalculaSaldo()
    {
        var conta = CriarConta();
        conta.RegistrarPagamento(FormaPagamento.Pix, 100, 0, null, null, DateTime.UtcNow, _usuarioId);
        conta.RegistrarPagamento(FormaPagamento.CartaoCredito, 140, 4, 2, null, DateTime.UtcNow, _usuarioId);

        Assert.Equal(StatusContaReceber.Pago, conta.Status);
        Assert.Equal(2, conta.Pagamentos.Count);
        Assert.Equal(136, conta.Pagamentos.Last().ValorLiquido);
    }

    [Fact]
    public void PagamentoMaiorQueSaldo_EhRejeitado()
    {
        var conta = CriarConta();
        Assert.Throws<InvalidOperationException>(() => conta.RegistrarPagamento(FormaPagamento.Pix,
            241, 0, null, null, DateTime.UtcNow, _usuarioId));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(240, 241)]
    public void TaxaForaDoIntervalo_EhRejeitada(decimal valor, decimal taxa)
    {
        var conta = CriarConta();
        Assert.Throws<InvalidOperationException>(() => conta.RegistrarPagamento(FormaPagamento.Pix,
            valor, taxa, null, null, DateTime.UtcNow, _usuarioId));
    }

    [Fact]
    public void ParcelasSomenteNoCredito()
    {
        var conta = CriarConta();
        Assert.Throws<InvalidOperationException>(() => conta.RegistrarPagamento(FormaPagamento.Pix,
            100, 0, 2, null, DateTime.UtcNow, _usuarioId));
    }

    [Fact]
    public void OutroExigeDescricao()
    {
        var conta = CriarConta();
        Assert.Throws<InvalidOperationException>(() => conta.RegistrarPagamento(FormaPagamento.Outro,
            100, 0, null, null, DateTime.UtcNow, _usuarioId));
    }

    [Fact]
    public void Estorno_PreservaPagamentoEReabreConta()
    {
        var conta = CriarConta();
        var pagamento = conta.RegistrarPagamento(FormaPagamento.Pix, 240, 0, null, null,
            DateTime.UtcNow, _usuarioId);

        conta.EstornarPagamento(pagamento.Id, _usuarioId, "Forma incorreta", DateTime.UtcNow);

        Assert.Equal(StatusPagamento.Estornado, pagamento.Status);
        Assert.Equal(StatusContaReceber.EmAberto, conta.Status);
        Assert.Equal(240, conta.ValorEmAberto);
        Assert.Single(conta.Pagamentos);
    }

    [Fact]
    public void SegundoEstorno_EhRejeitado()
    {
        var conta = CriarConta();
        var pagamento = conta.RegistrarPagamento(FormaPagamento.Pix, 100, 0, null, null,
            DateTime.UtcNow, _usuarioId);
        conta.EstornarPagamento(pagamento.Id, _usuarioId, "Correção", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => conta.EstornarPagamento(pagamento.Id,
            _usuarioId, "Outra correção", DateTime.UtcNow));
    }

    [Fact]
    public void Vencimento_EhCalculadoENaoPodeMudarQuandoPaga()
    {
        var conta = CriarConta();
        conta.AlterarVencimento(new DateOnly(2026, 8, 17));
        Assert.True(conta.EstaVencidaEm(new DateOnly(2026, 8, 18)));
        conta.RegistrarPagamento(FormaPagamento.Dinheiro, 240, 0, null, null, DateTime.UtcNow, _usuarioId);

        Assert.Throws<InvalidOperationException>(() => conta.AlterarVencimento(new DateOnly(2026, 8, 20)));
    }

    private ContaReceber CriarConta() => new(_empresaId, Guid.NewGuid(), "OS-2026-ABC",
        Guid.NewGuid(), "João Silva", Guid.NewGuid(), "BMW 323i", "ABC1D23",
        250, 10, 0, 240, new DateOnly(2026, 8, 18));
}
