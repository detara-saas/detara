using Detara.Contracts.Atendimento;
using Detara.Contracts.Catalogo;
using Detara.Web.Components.Atendimento;
using Detara.Web.Components.Shared.DesignSystem;

namespace Detara.UnitTests;

public sealed class HotfixUxApresentacaoTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("development", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public void IndicadorAmbiente_ApareceSomenteEmDevelopment(string ambiente, bool esperado) =>
        Assert.Equal(esperado, AmbienteApresentacao.ExibirIndicadorDesenvolvimento(ambiente));

    [Theory]
    [InlineData(NivelExigenciaOperacionalContrato.Desabilitado, false)]
    [InlineData(NivelExigenciaOperacionalContrato.Opcional, true)]
    [InlineData(NivelExigenciaOperacionalContrato.Obrigatorio, true)]
    public void Checklist_RespeitaSnapshotDaOrdem(
        NivelExigenciaOperacionalContrato nivel,
        bool esperado)
    {
        var ordem = Ordem(checklist: nivel);

        Assert.Equal(esperado, OrdemServicoApresentacao.ChecklistHabilitado(ordem, null));
    }

    [Fact]
    public void Fotos_TodasDesabilitadas_NaoExibeSecao() =>
        Assert.Empty(OrdemServicoApresentacao.CategoriasFotoHabilitadas(Ordem(), null));

    [Theory]
    [InlineData(CategoriaFotoOrdemServicoContrato.Entrada, NivelExigenciaOperacionalContrato.Opcional)]
    [InlineData(CategoriaFotoOrdemServicoContrato.Durante, NivelExigenciaOperacionalContrato.Obrigatorio)]
    [InlineData(CategoriaFotoOrdemServicoContrato.Saida, NivelExigenciaOperacionalContrato.Opcional)]
    public void Fotos_UmaHabilitada_ExibeSomenteEla(
        CategoriaFotoOrdemServicoContrato categoria,
        NivelExigenciaOperacionalContrato nivel)
    {
        var ordem = categoria switch
        {
            CategoriaFotoOrdemServicoContrato.Entrada => Ordem(entrada: nivel),
            CategoriaFotoOrdemServicoContrato.Durante => Ordem(durante: nivel),
            _ => Ordem(saida: nivel)
        };

        Assert.Equal([categoria], OrdemServicoApresentacao.CategoriasFotoHabilitadas(ordem, null));
    }

    [Fact]
    public void Fotos_Combinacao_MantemSomenteCategoriasHabilitadas()
    {
        var ordem = Ordem(
            checklist: NivelExigenciaOperacionalContrato.Desabilitado,
            entrada: NivelExigenciaOperacionalContrato.Obrigatorio,
            durante: NivelExigenciaOperacionalContrato.Desabilitado,
            saida: NivelExigenciaOperacionalContrato.Opcional);

        Assert.Equal(
            [CategoriaFotoOrdemServicoContrato.Entrada, CategoriaFotoOrdemServicoContrato.Saida],
            OrdemServicoApresentacao.CategoriasFotoHabilitadas(ordem, null));
        Assert.False(OrdemServicoApresentacao.ChecklistHabilitado(ordem, null));
    }

    [Fact]
    public void Adicionais_EstaoDisponiveisComOrdemAberta()
    {
        Assert.True(OrdemServicoApresentacao.ExibirAdicionais(StatusOrdemServicoContrato.Aberta, 0));
        Assert.True(OrdemServicoApresentacao.PodeCriarAdicional(StatusOrdemServicoContrato.Aberta));
    }

    [Fact]
    public void Comunicacao_ApareceUmaVezDepoisDaProximaAcao()
    {
        var raiz = EncontrarRaizRepositorio();
        var pagina = File.ReadAllText(Path.Combine(raiz, "src", "Detara.Web", "Pages", "OrdemServicoDetalhe.razor"));
        var ocorrencias = pagina.Split("<OrdemServicoComunicacao ", StringSplitOptions.None).Length - 1;

        var proximaAcao = pagina.IndexOf("service-order-next-action-card", StringComparison.Ordinal);
        var comunicacao = pagina.IndexOf("<OrdemServicoComunicacao ", StringComparison.Ordinal);
        var financeiro = pagina.IndexOf("Resumo financeiro", StringComparison.Ordinal);

        Assert.Equal(1, ocorrencias);
        Assert.True(proximaAcao >= 0);
        Assert.True(comunicacao > proximaAcao);
        Assert.True(financeiro > comunicacao);
    }

    [Fact]
    public void Agenda_NaoContemAvisosComerciaisRemovidos()
    {
        var raiz = EncontrarRaizRepositorio();
        var formulario = File.ReadAllText(Path.Combine(raiz, "src", "Detara.Web", "Components", "Agenda", "AgendamentoFormulario.razor"));
        var detalhe = File.ReadAllText(Path.Combine(raiz, "src", "Detara.Web", "Pages", "AgendamentoDetalhe.razor"));

        Assert.DoesNotContain("Os valores exibidos são referências do catálogo", formulario);
        Assert.DoesNotContain("A Agenda não registra preço acordado", detalhe);
    }

    private static OrdemServicoDetalheResponse Ordem(
        NivelExigenciaOperacionalContrato checklist = NivelExigenciaOperacionalContrato.Desabilitado,
        NivelExigenciaOperacionalContrato entrada = NivelExigenciaOperacionalContrato.Desabilitado,
        NivelExigenciaOperacionalContrato durante = NivelExigenciaOperacionalContrato.Desabilitado,
        NivelExigenciaOperacionalContrato saida = NivelExigenciaOperacionalContrato.Desabilitado) =>
        new(
            Guid.NewGuid(), "OS-2026-TESTE", OrigemOrdemServicoContrato.Agendamento,
            null, Guid.NewGuid(), Guid.NewGuid(), "Cliente", null, null, Guid.NewGuid(),
            "Veículo", null, 60, StatusOrdemServicoContrato.Aberta, 100, 0, 0, 100,
            DateTime.UtcNow, Guid.NewGuid(), null, null, null, null, checklist, entrada, saida,
            null, null, null, null, null, DateTime.UtcNow,
            [new(Guid.NewGuid(), TipoItemOrcamentoContrato.Personalizado, null, null, null,
                "Serviço", null, 100, 1, 100, 1, OrigemComercialOrdemServicoContrato.AcordoDireto,
                DateTime.UtcNow, Guid.NewGuid(), "Usuário", null)],
            null, [], [], [])
        {
            FotosDuranteSnapshot = durante
        };

    private static string EncontrarRaizRepositorio()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null && !File.Exists(Path.Combine(diretorio.FullName, "Detara.sln")))
            diretorio = diretorio.Parent;
        return diretorio?.FullName ?? throw new DirectoryNotFoundException("Raiz do repositório não encontrada.");
    }
}
