namespace Detara.UnitTests;

public sealed class Ui06ApresentacaoTests
{
    [Fact]
    public void Despesa_RemoveAlertInformativoEPreservaRecorrenciaAcoesEHistorico()
    {
        var pagina = LerArquivo("src", "Detara.Web", "Pages", "DespesaDetalhe.razor");

        Assert.DoesNotContain("Severity=\"Severity.Info\"", pagina);
        Assert.Contains("expense-recurrence-note", pagina);
        Assert.Contains("Ver recorrências", pagina);
        Assert.Contains("Estornar pagamento", pagina);
        Assert.Contains("_dados.Pagamentos.OrderByDescending", pagina);
    }

    [Fact]
    public void Financeiro_DistingueCompetenciaCaixaEPosicaoAtual()
    {
        var pagina = LerArquivo("src", "Detara.Web", "Pages", "Financeiro.razor");
        var handler = LerArquivo("src", "Detara.Application", "Financeiro", "FinanceiroOperacoes.cs");

        Assert.Contains("DESPESAS PREVISTAS", pagina);
        Assert.Contains("Por competência", pagina);
        Assert.Contains("DESPESAS PAGAS", pagina);
        Assert.Contains("Por caixa no período", pagina);
        Assert.Contains("SALDO OPERACIONAL", pagina);
        Assert.Contains("não é lucro", pagina);
        Assert.Contains("resumo.RecebidoBruto - resumo.Taxas - resumo.DespesasPagasPeriodo", handler);
    }

    [Fact]
    public void HistoricosCatalogo_SaoReaisLimitadosNoBanco()
    {
        var consulta = LerArquivo("src", "Detara.Infrastructure", "Atendimento",
            "HistoricoExecucoesCatalogoConsulta.cs");
        var servico = LerArquivo("src", "Detara.Web", "Pages", "ServicoDetalhe.razor");
        var pacote = LerArquivo("src", "Detara.Web", "Pages", "PacoteDetalhe.razor");

        Assert.Contains("ExecucaoFinalizadaEmUtc != null", consulta);
        Assert.Contains("OrderByDescending", consulta);
        Assert.Contains(".Take(limite)", consulta);
        Assert.Contains("IgnoreQueryFilters().AsNoTracking()", consulta);
        Assert.Contains("item.EmpresaId == empresaId", consulta);
        Assert.Contains("@foreach (var execucao in _servico.Execucoes)", servico);
        Assert.Contains("@foreach (var execucao in _pacote.Execucoes)", pacote);
    }

    [Fact]
    public void Orcamento_CompactaCamposEPreservaAcoesSomenteQuandoDisponiveis()
    {
        var formulario = LerArquivo("src", "Detara.Web", "Components", "Atendimento",
            "OrcamentoFormulario.razor");
        var detalhe = LerArquivo("src", "Detara.Web", "Pages", "OrcamentoDetalhe.razor");

        Assert.Contains("quotation-catalog-tools", formulario);
        Assert.Contains("quotation-conditions-grid", formulario);
        Assert.Contains("Label=\"Quantidade\"", formulario);
        Assert.Contains("Label=\"Valor neste orçamento\"", formulario);
        Assert.Contains("Label=\"Observação do item (opcional)\"", formulario);
        Assert.Contains("private bool TemAcoesComerciais", detalhe);
        Assert.Contains("@if (TemAcoesComerciais)", detalhe);
    }

    [Fact]
    public void Cliente_UsaGrupoCompartilhadoDeAcoes()
    {
        var pagina = LerArquivo("src", "Detara.Web", "Pages", "ClienteDetalhe.razor");
        var estilos = LerArquivo("src", "Detara.Web", "wwwroot", "css", "app.css");

        Assert.Contains("customer-360-hero-actions page-actions", pagina);
        Assert.Contains(".page-actions {", estilos);
        Assert.Contains("gap: 10px", estilos);
    }

    private static string LerArquivo(params string[] partes) =>
        File.ReadAllText(Path.Combine([EncontrarRaizRepositorio(), .. partes]));

    private static string EncontrarRaizRepositorio()
    {
        var atual = new DirectoryInfo(AppContext.BaseDirectory);
        while (atual is not null && !File.Exists(Path.Combine(atual.FullName, "Detara.sln")))
            atual = atual.Parent;

        return atual?.FullName ?? throw new DirectoryNotFoundException("Raiz do repositório não encontrada.");
    }
}
