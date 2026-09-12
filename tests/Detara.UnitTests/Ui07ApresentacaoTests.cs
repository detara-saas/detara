using Detara.Web.Components.Agenda;

namespace Detara.UnitTests;

public sealed class Ui07ApresentacaoTests
{
    [Theory]
    [InlineData("1050", 10, 50)]
    [InlineData("0930", 9, 30)]
    [InlineData("1845", 18, 45)]
    [InlineData("0000", 0, 0)]
    [InlineData("2359", 23, 59)]
    [InlineData("00:00", 0, 0)]
    [InlineData("23:59", 23, 59)]
    public void Hora_AceitaDigitacaoRapidaEFormatoVinteEQuatroHoras(
        string texto,
        int hora,
        int minuto)
    {
        var valido = EntradaDataHoraAgenda.TentarInterpretarHora(texto, out var resultado);

        Assert.True(valido);
        Assert.Equal(new TimeSpan(hora, minuto, 0), resultado);
    }

    [Theory]
    [InlineData("2500")]
    [InlineData("1260")]
    [InlineData("9999")]
    [InlineData("105")]
    public void Hora_RejeitaValorInvalidoOuIncompleto(string texto)
    {
        Assert.False(EntradaDataHoraAgenda.TentarInterpretarHora(texto, out _));
    }

    [Theory]
    [InlineData("12092026", 2026, 9, 12)]
    [InlineData("12/09/2026", 2026, 9, 12)]
    [InlineData("29022028", 2028, 2, 29)]
    public void Data_AceitaDigitacaoRapidaNoPadraoBrasileiro(
        string texto,
        int ano,
        int mes,
        int dia)
    {
        var valido = EntradaDataHoraAgenda.TentarInterpretarData(texto, out var resultado);

        Assert.True(valido);
        Assert.Equal(new DateTime(ano, mes, dia), resultado);
    }

    [Theory]
    [InlineData("31022026")]
    [InlineData("29022027")]
    [InlineData("31132026")]
    [InlineData("1209202")]
    [InlineData("32132026")]
    public void Data_RejeitaValorInvalidoOuIncompleto(string texto)
    {
        Assert.False(EntradaDataHoraAgenda.TentarInterpretarData(texto, out _));
    }

    [Fact]
    public void Mascaras_InseremSeparadoresSemJavaScript()
    {
        var hora = EntradaDataHoraAgenda.CriarMascaraHora();
        var data = EntradaDataHoraAgenda.CriarMascaraData();

        hora.Insert("1050");
        data.Insert("12092026");

        Assert.Equal("10:50", hora.Text);
        Assert.Equal("12/09/2026", data.Text);
    }

    [Fact]
    public void NovoAgendamento_UsaAgoraLocalESemSobrescreverContextoExplicito()
    {
        var hoje = new DateOnly(2026, 9, 12);
        var agora = new DateTime(2026, 9, 12, 14, 37, 52);

        var padrao = EntradaDataHoraAgenda.ResolverPadrao(hoje, agora);
        var explicito = EntradaDataHoraAgenda.ResolverPadrao(
            hoje,
            agora,
            new DateTime(2026, 10, 20, 18, 0, 0),
            new TimeSpan(9, 15, 0));

        Assert.Equal(new DateTime(2026, 9, 12), padrao.Data);
        Assert.Equal(new TimeSpan(14, 37, 0), padrao.Hora);
        Assert.Equal(new DateTime(2026, 10, 20), explicito.Data);
        Assert.Equal(new TimeSpan(9, 15, 0), explicito.Hora);
    }

    [Fact]
    public void Agenda_RemoveAtendimentoAgoraEPreservaNovoAgendamento()
    {
        var agenda = LerArquivo("src", "Detara.Web", "Pages", "Agenda.razor");
        var novaOrdem = LerArquivo("src", "Detara.Web", "Pages", "OrdemServicoNova.razor");

        Assert.DoesNotContain("Atendimento agora", agenda);
        Assert.DoesNotContain("agora=true", agenda);
        Assert.DoesNotContain("agora=true", novaOrdem);
        Assert.Contains("Novo agendamento", agenda);
        Assert.Contains("Href=\"/agenda/novo\"", agenda);
    }

    [Fact]
    public void Formulario_OfereceCadastrosRapidosProtegidosESelecionaResultado()
    {
        var formulario = LerArquivo("src", "Detara.Web", "Components", "Agenda",
            "AgendamentoFormulario.razor");
        var cliente = LerArquivo("src", "Detara.Web", "Components", "Clientes",
            "ClienteFormulario.razor");
        var veiculo = LerArquivo("src", "Detara.Web", "Components", "Veiculos",
            "VeiculoFormulario.razor");

        Assert.Contains("Policy=\"@Permissoes.ClientesCriar\"", formulario);
        Assert.Contains("Policy=\"@Permissoes.VeiculosCriar\"", formulario);
        Assert.Contains("Selecione ou cadastre um cliente primeiro.", formulario);
        Assert.Contains("await SelecionarClienteAsync", formulario);
        Assert.Contains("_veiculoId = veiculo.Id", formulario);
        Assert.Contains("if (_salvando) return", formulario);
        Assert.Contains("EventCallback<ClienteDetalheResponse> Salvo", cliente);
        Assert.Contains("EventCallback<VeiculoDetalheResponse> Salvo", veiculo);
    }

    [Fact]
    public void CamposDeDataHora_SaoEditaveisMasMantemPickersOpcionais()
    {
        var formulario = LerArquivo("src", "Detara.Web", "Components", "Agenda",
            "AgendamentoFormulario.razor");

        Assert.Contains("DateFormat=\"dd/MM/yyyy\"", formulario);
        Assert.Contains("TimeFormat=\"HH:mm\"", formulario);
        Assert.Contains("Culture=\"@EntradaDataHoraAgenda.Cultura\"", formulario);
        Assert.Equal(2, ContarOcorrencias(formulario, "Editable=\"true\""));
        Assert.Contains("Mask=\"_mascaraData\"", formulario);
        Assert.Contains("Mask=\"_mascaraHora\"", formulario);
        Assert.Equal(2, ContarOcorrencias(formulario, "@attributes=\"AtributosEntradaNumerica\""));
        Assert.DoesNotContain("AtendimentoAgora", formulario);
    }

    private static int ContarOcorrencias(string texto, string trecho)
        => texto.Split(trecho, StringSplitOptions.None).Length - 1;

    private static string LerArquivo(params string[] partes)
        => File.ReadAllText(Path.Combine([EncontrarRaizRepositorio(), .. partes]));

    private static string EncontrarRaizRepositorio()
    {
        var atual = new DirectoryInfo(AppContext.BaseDirectory);
        while (atual is not null && !File.Exists(Path.Combine(atual.FullName, "Detara.sln")))
            atual = atual.Parent;

        return atual?.FullName
            ?? throw new DirectoryNotFoundException("Raiz do repositório não encontrada.");
    }
}
