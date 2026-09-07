using Detara.Domain.Financeiro;

namespace Detara.UnitTests.Domain;

public sealed class DespesasTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 7);
    private readonly CategoriaDespesa _categoria = new(Guid.NewGuid(), "Aluguel");
    private DespesaRecorrente Regra(int dia = 31, DateOnly? inicio = null, DateOnly? fim = null) =>
        new(_categoria.EmpresaId, "Aluguel", _categoria, 4500, dia, inicio ?? new(2026, 9, 1), fim, Hoje);
    private ContaPagar Conta(DateOnly? vencimento = null) => new(_categoria.EmpresaId, "Insumos", _categoria, 200,
        new(2026, 9, 1), vencimento ?? Hoje);

    [Theory]
    [InlineData(2026, 2, 28)]
    [InlineData(2028, 2, 29)]
    [InlineData(2026, 4, 30)]
    [InlineData(2026, 12, 31)]
    public void Vencimento_AjustaFimDoMes(int ano, int mes, int dia) =>
        Assert.Equal(new DateOnly(ano, mes, dia), Regra().CalcularVencimento(new(ano, mes, 1)));

    [Fact]
    public void Criacao_NaoRetroage_EFuturoAguardaInicio()
    {
        Assert.Equal(new DateOnly(2026, 9, 1), Regra(inicio: new(2025, 1, 1)).ProximaCompetencia);
        Assert.False(Regra(inicio: new(2026, 10, 1)).PodeMaterializar(Hoje));
    }
    [Fact]
    public void CompetenciaFinal_EInclusiva()
    {
        var regra = Regra(fim: new(2026, 9, 1));
        Assert.True(regra.PodeMaterializar(Hoje));
        regra.Materializar(_categoria, Hoje);
        Assert.False(regra.PodeMaterializar(Hoje.AddMonths(5)));
    }
    [Fact]
    public void Catchup_AvancaUmaCompetenciaPorConta()
    {
        var regra = Regra(); var meses = new List<DateOnly>();
        while (regra.PodeMaterializar(new(2026, 12, 15))) meses.Add(regra.Materializar(_categoria, new(2026, 12, 15)).Competencia);
        Assert.Equal(new[] { new DateOnly(2026, 9, 1), new(2026, 10, 1), new(2026, 11, 1), new(2026, 12, 1) }, meses);
    }
    [Fact]
    public void Reativacao_PulaMesesInativos_EPreservaInicioFuturo()
    {
        var regra = Regra();
        Assert.Equal(new DateOnly(2026, 9, 1), regra.Materializar(_categoria, Hoje).Competencia);
        regra.DefinirAtividade(false, Hoje);
        Assert.False(regra.PodeMaterializar(Hoje.AddMonths(3)));
        regra.DefinirAtividade(true, Hoje.AddMonths(3));
        Assert.Equal(new DateOnly(2026, 12, 1), regra.ProximaCompetencia);
        Assert.Equal(new DateOnly(2026, 12, 1), regra.Materializar(_categoria, Hoje.AddMonths(3)).Competencia);
        var futura = Regra(inicio: new(2027, 1, 1)); futura.DefinirAtividade(false, Hoje); futura.DefinirAtividade(true, Hoje);
        Assert.Equal(new DateOnly(2027, 1, 1), futura.ProximaCompetencia);
    }
    [Fact]
    public void EditarRegra_NaoAlteraSnapshotNemCompetenciaDaConta()
    {
        var regra = Regra(); var conta = regra.Materializar(_categoria, Hoje);
        regra.Editar("Novo aluguel", _categoria, 5000, 10, null, "Outro", "Novo");
        Assert.Equal(4500, conta.Valor); Assert.Equal("Aluguel", conta.Descricao); Assert.Equal(new DateOnly(2026, 9, 30), conta.DataVencimento);
        Assert.Equal(regra.Id, conta.DespesaRecorrenteId); Assert.Equal(OrigemContaPagar.Recorrente, conta.Origem);
        Assert.Throws<InvalidOperationException>(() => conta.Editar("Teste", _categoria, 1, new(2026, 10, 1), Hoje, null, null));
        var proxima = regra.Materializar(_categoria, Hoje.AddMonths(1));
        Assert.Equal(5000, proxima.Valor);
        Assert.Equal("Novo aluguel", proxima.Descricao);
        Assert.Equal(new DateOnly(2026, 10, 10), proxima.DataVencimento);
    }
    [Fact]
    public void PagamentoIntegral_PreservaPrevisto_EEstornoAuditaPermiteNovoPagamento()
    {
        var conta = Conta(); var usuario = Guid.NewGuid(); var agora = DateTime.UtcNow;
        conta.RegistrarPagamento(Hoje, 195, usuario, agora);
        Assert.Equal(200, conta.Valor); Assert.Equal(195, conta.ValorPago); Assert.Equal(StatusContaPagar.Pago, conta.Status);
        Assert.Throws<InvalidOperationException>(() => conta.RegistrarPagamento(Hoje, 5, usuario, agora));
        Assert.Throws<InvalidOperationException>(() => conta.Cancelar(usuario, agora));
        Assert.Throws<InvalidOperationException>(() => conta.Editar("X", _categoria, 1, new(2026, 9, 1), Hoje, null, null));
        conta.EstornarPagamento(usuario, "Pagamento lançado em duplicidade", agora);
        Assert.Null(conta.ValorPago); Assert.Null(conta.DataPagamento); Assert.Equal(StatusContaPagar.Pendente, conta.Status);
        Assert.Equal(usuario, conta.Pagamentos.Single().EstornadoPorUsuarioId);
        Assert.Equal(StatusPagamento.Estornado, conta.Pagamentos.Single().Status);
        conta.RegistrarPagamento(Hoje, 200, usuario, agora); Assert.Equal(2, conta.Pagamentos.Count);
    }
    [Fact]
    public void Vencido_EhDerivado_ECancelarPreservaConta()
    {
        var conta = Conta(Hoje.AddDays(-1)); Assert.True(conta.EstaVencidaEm(Hoje));
        Assert.False(Conta(Hoje).EstaVencidaEm(Hoje));
        conta.Cancelar(Guid.NewGuid(), DateTime.UtcNow);
        Assert.False(conta.EstaVencidaEm(Hoje)); Assert.Equal(StatusContaPagar.Cancelado, conta.Status);
        Assert.NotNull(conta.CanceladoPorUsuarioId);
        Assert.Throws<InvalidOperationException>(() => conta.RegistrarPagamento(Hoje, 200, Guid.NewGuid(), DateTime.UtcNow));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.001)]
    public void ValorInvalido_Rejeitado(decimal valor) => Assert.Throws<ArgumentException>(() =>
        new ContaPagar(_categoria.EmpresaId, "Teste", _categoria, valor, new(2026, 9, 1), Hoje));
    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void DiaInvalido_Rejeitado(int dia) => Assert.Throws<ArgumentException>(() => Regra(dia));
    [Fact]
    public void Categoria_InativaOuDeOutroTenant_NaoPermiteLancamento()
    {
        Assert.Throws<InvalidOperationException>(() => new ContaPagar(Guid.NewGuid(), "X", _categoria, 1, new(2026, 9, 1), Hoje));
        _categoria.DefinirAtividade(false); Assert.Throws<InvalidOperationException>(() => Conta());
    }
}
