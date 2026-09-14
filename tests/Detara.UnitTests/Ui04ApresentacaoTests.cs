using Detara.Contracts.Agenda;
using Detara.Contracts.Catalogo;
using Detara.Web.Servicos;

namespace Detara.UnitTests;

public sealed class Ui04ApresentacaoTests
{
    [Theory]
    [InlineData(150, "R$ 150,00")]
    [InlineData(200, "R$ 200,00")]
    [InlineData(1250.5, "R$ 1.250,50")]
    public void MoedaBrl_UsaCulturaExplicita(decimal valor, string esperado)
    {
        Assert.Equal(esperado, FormatacaoMoeda.Brl(valor));
        Assert.DoesNotContain("¤", FormatacaoMoeda.Brl(valor));
    }

    [Fact]
    public void ReferenciaAgenda_FormataDadosNumericosSemDependerDoTextoDaApi()
    {
        var referencia = new ResumoReferenciaAgendamentoResponse(1250.5m, true, false, "¤1,250.50");

        Assert.Equal("A partir de R$ 1.250,50", FormatacaoCatalogo.Referencia(referencia));
    }

    [Fact]
    public void CriacaoOs_RemoveObservacaoVisualEPreservaContratoOpcional()
    {
        var pagina = LerArquivo("src", "Detara.Web", "Pages", "OrdemServicoNova.razor");

        Assert.DoesNotContain("Registro da autorização direta", pagina);
        Assert.DoesNotContain("_observacaoAutorizacao", pagina);
        Assert.Contains("ClienteOrcamentoAutocomplete", pagina);
        Assert.Contains("Label=\"Veículo *\"", pagina);
        Assert.Contains("<CampoDuracao", pagina);
        Assert.Contains("Label=\"Duração planejada (opcional)\"", pagina);
        Assert.Contains("service-order-intake-grid", pagina);
        Assert.Contains("_duracao, 0, 0,\n            null,", pagina);
    }

    [Fact]
    public void DetalheOs_RemoveContextoRedundante()
    {
        var pagina = LerArquivo("src", "Detara.Web", "Pages", "OrdemServicoDetalhe.razor");
        var estilos = LerArquivo("src", "Detara.Web", "wwwroot", "css", "app.css");

        Assert.DoesNotContain("Cliente e atendimento", pagina);
        Assert.DoesNotContain("service-order-context-card", pagina);
        Assert.DoesNotContain("service-order-context-list", estilos);
        Assert.Contains("service-order-aside-sticky", pagina);
        Assert.Contains("OrdemServicoComunicacao", pagina);
    }

    [Fact]
    public void SistemaBotoes_CentralizaVariantesETamanhos()
    {
        var estilos = LerArquivo("src", "Detara.Web", "wwwroot", "css", "app.css");
        var designSystem = LerArquivo("docs", "design-system.md");

        Assert.Contains("--detara-action-height: 42px", estilos);
        Assert.Contains(".mud-button-filled-primary", estilos);
        Assert.Contains(".mud-button-outlined-default", estilos);
        Assert.Contains(".mud-button-text-default", estilos);
        Assert.Contains(".mud-button-root:focus-visible", estilos);
        Assert.Contains(".mud-button-text-error, .mud-button-outlined-error", estilos);
        Assert.Contains(".mud-icon-button", estilos);
        Assert.Contains("Primary", designSystem);
        Assert.Contains("Secondary", designSystem);
        Assert.Contains("Tertiary / Ghost", designSystem);
        Assert.Contains("Destructive", designSystem);
    }

    [Fact]
    public void HierarquiaCritica_UsaUmaAcaoPrincipalEDestrutivasSemanticas()
    {
        var agendamento = LerArquivo("src", "Detara.Web", "Pages", "AgendamentoDetalhe.razor");
        var orcamento = LerArquivo("src", "Detara.Web", "Pages", "OrcamentoDetalhe.razor");
        var ordem = LerArquivo("src", "Detara.Web", "Pages", "OrdemServicoDetalhe.razor");
        var despesa = LerArquivo("src", "Detara.Web", "Components", "Financeiro", "DespesaAcaoDialog.razor");

        Assert.Contains("Variant=\"Variant.Outlined\" StartIcon=\"@Icons.Material.Outlined.EventRepeat\">Reagendar", agendamento);
        Assert.Contains("Variant=\"Variant.Text\" StartIcon=\"@Icons.Material.Outlined.Edit\">Editar", agendamento);
        Assert.Contains("Variant=\"Variant.Filled\" Color=\"Color.Primary\" OnClick=\"@(() => SolicitarAcaoAsync(\"aprovar\"))\">Registrar aprovação", orcamento);
        Assert.Contains("Color=\"Color.Primary\" StartIcon=\"@Icons.Material.Outlined.TaskAlt\"", ordem);
        Assert.Contains("Color=\"Color.Error\" OnClick=\"CancelarAsync\"", ordem);
        Assert.Contains("Acao == \"pagar\" ? Color.Primary : Color.Error", despesa);
    }

    private static string LerArquivo(params string[] partes) =>
        File.ReadAllText(Path.Combine([EncontrarRaizRepositorio(), .. partes]));

    private static string EncontrarRaizRepositorio()
    {
        var atual = new DirectoryInfo(AppContext.BaseDirectory);
        while (atual is not null && !File.Exists(Path.Combine(atual.FullName, "Detara.sln")))
        {
            atual = atual.Parent;
        }

        return atual?.FullName ?? throw new DirectoryNotFoundException("Raiz do repositório não encontrada.");
    }
}
