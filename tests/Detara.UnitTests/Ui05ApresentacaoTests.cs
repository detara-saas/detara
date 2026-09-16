using Detara.Contracts.Agenda;
using Detara.Web.Components.Shared.DesignSystem;
using Detara.Web.Servicos;

namespace Detara.UnitTests;

public sealed class Ui05ApresentacaoTests
{
    [Fact]
    public void StatusConfirmado_EhDistintoDeConcluido()
    {
        Assert.Equal(DetaraStatusTone.Confirmed,
            AgendamentoApresentacao.TomStatus(StatusAgendamentoContrato.Confirmado));
        Assert.Equal(DetaraStatusTone.Positive,
            AgendamentoApresentacao.TomStatus(StatusAgendamentoContrato.Concluido));
        Assert.NotEqual(
            AgendamentoApresentacao.TomStatus(StatusAgendamentoContrato.Confirmado),
            AgendamentoApresentacao.TomStatus(StatusAgendamentoContrato.Concluido));
    }

    [Fact]
    public void ConfirmacaoManual_FicaDisponivelSomenteParaEstadoPendenteLegado()
    {
        Assert.True(AgendamentoApresentacao.PodeConfirmar(StatusAgendamentoContrato.Agendado));
        Assert.False(AgendamentoApresentacao.PodeConfirmar(StatusAgendamentoContrato.Confirmado));
    }

    [Fact]
    public void DetalheAgendamento_RemoveChegadaDuplicadaEPreservaDemaisAcoes()
    {
        var pagina = LerArquivo("src", "Detara.Web", "Pages", "AgendamentoDetalhe.razor");

        Assert.DoesNotContain("Registrar chegada", pagina);
        Assert.Contains("Não compareceu", pagina);
        Assert.Contains("Cancelar agendamento", pagina);
        Assert.Contains("Criar ordem de serviço", pagina);
    }

    [Fact]
    public void AcaoDeFotoDaOs_UsaHierarquiaSecundaria()
    {
        var pagina = LerArquivo("src", "Detara.Web", "Pages", "OrdemServicoDetalhe.razor");
        var estilos = LerArquivo("src", "Detara.Web", "wwwroot", "css", "app.css");

        Assert.Contains("vehicle-photo-upload detara-action-secondary", pagina);
        Assert.Contains("Color=\"Color.Primary\" StartIcon=\"@Icons.Material.Outlined.TaskAlt\"", pagina);
        Assert.Contains(".vehicle-photo-upload.detara-action-secondary", estilos);
    }

    [Fact]
    public void BotoesTertiarios_PreservamAffordanceEFoco()
    {
        var estilos = LerArquivo("src", "Detara.Web", "wwwroot", "css", "app.css");

        Assert.Contains(".mud-button-text {", estilos);
        Assert.Contains("background: color-mix", estilos);
        Assert.Contains("border: 1px solid", estilos);
        Assert.Contains(".mud-button-root:focus-visible", estilos);
    }

    [Fact]
    public void Conectividade_ExplicaEstadosESemReloadAutomatico()
    {
        var componente = LerArquivo("src", "Detara.Web", "Shared", "PwaStatus.razor");
        var servico = LerArquivo("src", "Detara.Web", "Servicos", "PwaServico.cs");
        var paginaInicial = LerArquivo("src", "Detara.Web", "wwwroot", "index.html");

        Assert.Contains("Conexão perdida", componente);
        Assert.Contains("Tentando reconectar...", componente);
        Assert.Contains("Conexão restabelecida", componente);
        Assert.Contains("Não foi possível reconectar", componente);
        Assert.Contains("Tentar novamente", componente);
        Assert.Contains("Recarregar agora", componente);
        Assert.DoesNotContain("forceLoad: true", servico);
        Assert.Contains("blazor-error-card", paginaInicial);
        Assert.Contains("blazor-error-content", paginaInicial);
        Assert.Contains("Permanecer aqui", paginaInicial);
        Assert.DoesNotContain("🗙", paginaInicial);
    }

    [Fact]
    public void FalhaGlobal_FicaIsoladaEmCardResponsivoSemEstilosLegados()
    {
        var estilos = LerArquivo("src", "Detara.Web", "wwwroot", "css", "app.css");

        Assert.Contains("#blazor-error-ui { display: none; position: fixed;", estilos);
        Assert.Contains("background: transparent; border: 0; box-shadow: none;", estilos);
        Assert.Contains("#blazor-error-ui .reload, #blazor-error-ui .dismiss { position: static; inset: auto;", estilos);
        Assert.Contains(".blazor-error-actions { display: grid; grid-template-columns: 1fr 1fr; }", estilos);
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
