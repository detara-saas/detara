using Detara.Application.Abstracoes;
using Detara.Application.Notificacoes;
using Detara.Domain.Notificacoes;
using Detara.Infrastructure.Notificacoes;

namespace Detara.IntegrationTests.Notificacoes;

public sealed class RenderizadorTemplateEmailTests
{
    private readonly RenderizadorTemplateEmail _renderer = new();
    private static readonly DadosTemplateEmail Dados = new("Estética Norte", "Marina Souza", "Honda Civic", "ABC1D23", "OS-2026-0042");

    [Fact]
    public void Padrao_TemAssuntoEmojiETokensEsperados()
    { var padrao = _renderer.ObterPadraoVeiculoPronto(); Assert.Equal("Seu veículo está pronto para retirada 🚗", padrao.Assunto); Assert.Contains("{{ClientePrimeiroNome}}", padrao.CorpoHtml); }

    [Fact]
    public void Renderizar_SubstituiTodosOsTokens()
    { var r = _renderer.Renderizar(new("{{EmpresaNome}} · {{OrdemServicoCodigo}}", "<p>{{ClienteNome}} {{ClientePrimeiroNome}} {{VeiculoDescricao}} {{Placa}}</p>", OrigemTemplateEmail.PersonalizadoEmpresa), Dados); Assert.Equal("Estética Norte · OS-2026-0042", r.Assunto); Assert.Contains("Marina Souza Marina Honda Civic ABC1D23", r.CorpoHtmlCompleto); }

    [Fact]
    public void Renderizar_EncodaValorMalicioso()
    { var r = _renderer.Renderizar(new("Aviso", "<p>{{ClienteNome}}</p>", OrigemTemplateEmail.PadraoDetara), Dados with { ClienteNome = "<img src=x onerror=alert(1)>" }); Assert.DoesNotContain("<img", r.CorpoHtmlCompleto); Assert.Contains("&lt;img", r.CorpoHtmlCompleto); }

    [Theory]
    [InlineData("<script>alert(1)</script><p>Seguro</p>", "script")]
    [InlineData("<p onclick=\"alert(1)\">Seguro</p>", "onclick")]
    [InlineData("<iframe src=\"https://evil.test\"></iframe><p>Seguro</p>", "iframe")]
    [InlineData("<img src=\"x\"><p>Seguro</p>", "img")]
    [InlineData("<style>body{display:none}</style><p>Seguro</p>", "style")]
    public void Sanitizar_RemoveElementosPerigosos(string html, string proibido)
    { var r = _renderer.SanitizarEValidarCorpo(html); Assert.DoesNotContain(proibido, r, StringComparison.OrdinalIgnoreCase); Assert.Contains("Seguro", r); }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("vbscript:msgbox(1)")]
    public void Sanitizar_RemoveProtocolosPerigosos(string url)
    { var r = _renderer.SanitizarEValidarCorpo($"<p><a href=\"{url}\">Link</a></p>"); Assert.DoesNotContain(url.Split(':')[0] + ":", r, StringComparison.OrdinalIgnoreCase); }

    [Fact]
    public void Sanitizar_PreservaFormatacaoPermitida()
    { var r = _renderer.SanitizarEValidarCorpo("<p style=\"text-align:center;color:#336699\"><strong>Negrito</strong> <em>itálico</em> <u>sublinhado</u></p><ul><li>Item</li></ul>"); Assert.Contains("text-align", r); Assert.Contains("color", r); Assert.Contains("<strong>", r); Assert.Contains("<ul>", r); }

    [Fact]
    public void Sanitizar_PreservaLinkHttpsSeguro()
    { var r = _renderer.SanitizarEValidarCorpo("<p><a href=\"https://detara.com.br/ajuda\">Ajuda</a></p>"); Assert.Contains("https://detara.com.br/ajuda", r); }

    [Fact]
    public void ValidarTokens_RejeitaDesconhecido()
    { var ex = Assert.Throws<ConflitoRegraNegocioException>(() => _renderer.ValidarTokens("{{NaoExiste}}", "<p>Oi</p>")); Assert.Contains("Variável desconhecida", ex.Message); }

    [Fact]
    public void ValidarTokens_RejeitaIncompleto()
    { var ex = Assert.Throws<ConflitoRegraNegocioException>(() => _renderer.ValidarTokens("Aviso", "<p>{{ClienteNome}</p>")); Assert.Contains("inválida ou incompleta", ex.Message); }

    [Fact]
    public void ValidarTokens_RejeitaAssuntoQuePodeEstourarAposSubstituicao()
    { var ex = Assert.Throws<ConflitoRegraNegocioException>(() => _renderer.ValidarTokens(new string('A', 50) + "{{ClienteNome}}", "<p>Oi</p>")); Assert.Contains("exceder 200", ex.Message); }

    [Fact]
    public void Renderizar_RemoveQuebraDeLinhaDosValoresDoAssunto()
    { var r = _renderer.Renderizar(new("Olá {{ClienteNome}}", "<p>Oi</p>", OrigemTemplateEmail.PadraoDetara), Dados with { ClienteNome = "Marina\r\nBcc: atacante" }); Assert.DoesNotContain('\r', r.Assunto); Assert.DoesNotContain('\n', r.Assunto); }

    [Fact]
    public void Renderizar_UsaMesmoShellResponsivo()
    { var r = _renderer.Renderizar(_renderer.ObterPadraoVeiculoPronto(), Dados); Assert.Contains("viewport", r.CorpoHtmlCompleto); Assert.Contains("max-width:620px", r.CorpoHtmlCompleto); Assert.Contains("Mensagem operacional", r.CorpoHtmlCompleto); }
}
