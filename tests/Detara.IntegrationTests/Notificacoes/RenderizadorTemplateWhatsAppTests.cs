using Detara.Application.Abstracoes;
using Detara.Application.Notificacoes;
using Detara.Domain.Notificacoes;
using Detara.Infrastructure.Notificacoes;

namespace Detara.IntegrationTests.Notificacoes;

public sealed class RenderizadorTemplateWhatsAppTests
{
    private readonly RenderizadorTemplateWhatsApp _renderer = new();
    private static readonly DadosTemplateEmail Dados = new(
        "Estética Horizonte", "João Souza", "Honda Civic", "ABC1D23", "OS-2026-0042");

    [Fact]
    public void Padrao_UsaTextoEVariaveisDefinidosParaWhatsApp()
    {
        var padrao = _renderer.ObterPadraoVeiculoPronto();

        Assert.Equal("Veículo pronto para retirada", padrao.Nome);
        Assert.Equal("Olá, {ClienteNome}! Tudo bem?\n\n" +
            "O seu veículo {VeiculoDescricao} ficou pronto e já está disponível para retirada na {EmpresaNome}.\n\n" +
            "Obrigado pela preferência!", padrao.Mensagem);
    }

    [Fact]
    public void Renderizar_SubstituiSomenteVariaveisDoCanal()
    {
        var renderizada = _renderer.RenderizarVeiculoPronto(
            _renderer.ObterPadraoVeiculoPronto(), Dados);

        Assert.Contains("Olá, João Souza!", renderizada);
        Assert.Contains("Honda Civic", renderizada);
        Assert.Contains("Estética Horizonte", renderizada);
        Assert.DoesNotContain('{', renderizada);
    }

    [Fact]
    public void ValidarTokens_RejeitaVariavelDesconhecida() =>
        Assert.Throws<ConflitoRegraNegocioException>(() =>
            _renderer.ValidarTokens("Olá, {Placa}."));

    [Fact]
    public void ValidarTokens_RejeitaVariavelIncompleta() =>
        Assert.Throws<ConflitoRegraNegocioException>(() =>
            _renderer.ValidarTokens("Olá, {ClienteNome."));
}
