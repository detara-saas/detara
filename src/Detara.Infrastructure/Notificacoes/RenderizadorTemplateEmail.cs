using System.Net;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Detara.Application.Abstracoes;
using Detara.Application.Notificacoes;
using Detara.Domain.Notificacoes;
using Ganss.Xss;

namespace Detara.Infrastructure.Notificacoes;

internal sealed partial class RenderizadorTemplateEmail : IRenderizadorTemplateEmail
{
    private static readonly HashSet<string> TokensPermitidos = new(StringComparer.Ordinal)
    { "EmpresaNome", "ClienteNome", "ClientePrimeiroNome", "VeiculoDescricao", "Placa", "OrdemServicoCodigo" };
    private readonly HtmlSanitizer _sanitizer = CriarSanitizer();

    public ConteudoTemplateEmail ObterPadraoVeiculoPronto() => new(
        "Seu veículo está pronto para retirada 🚗",
        "<p>Olá, <strong>{{ClientePrimeiroNome}}</strong>!</p><p>Seu <strong>{{VeiculoDescricao}}</strong> está pronto para retirada.</p><p>Ordem de serviço: <strong>{{OrdemServicoCodigo}}</strong>.</p><p>Atenciosamente,<br><strong>{{EmpresaNome}}</strong></p>",
        OrigemTemplateEmail.PadraoDetara);

    public string SanitizarEValidarCorpo(string corpoHtml)
    {
        var sanitizado = _sanitizer.Sanitize(corpoHtml, "https://detara.invalid/");
        if (string.IsNullOrWhiteSpace(WebUtility.HtmlDecode(RemoverTagsRegex().Replace(sanitizado, string.Empty))))
            throw new ConflitoRegraNegocioException("O corpo do e-mail ficou vazio após a sanitização.");
        if (sanitizado.Length > 50 * 1024) throw new ConflitoRegraNegocioException("O corpo sanitizado excede 50 KB.");
        return sanitizado;
    }

    public void ValidarTokens(string assunto, string corpoHtml)
    {
        var desconhecidos = TokenRegex().Matches(assunto + corpoHtml).Select(x => x.Groups[1].Value)
            .Where(x => !TokensPermitidos.Contains(x)).Distinct(StringComparer.Ordinal).ToArray();
        if (desconhecidos.Length > 0)
            throw new ConflitoRegraNegocioException($"Variável desconhecida: {string.Join(", ", desconhecidos.Select(x => "{{" + x + "}}"))}.");
        var semTokensValidos = TokenRegex().Replace(assunto + corpoHtml, string.Empty);
        if (semTokensValidos.Contains("{{", StringComparison.Ordinal) || semTokensValidos.Contains("}}", StringComparison.Ordinal))
            throw new ConflitoRegraNegocioException("Existe uma variável inválida ou incompleta no template.");
        var maximos = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["EmpresaNome"] = new('E', 160),
            ["ClienteNome"] = new('C', 160),
            ["ClientePrimeiroNome"] = new('N', 160),
            ["VeiculoDescricao"] = new('V', 200),
            ["Placa"] = new('P', 10),
            ["OrdemServicoCodigo"] = new('O', 32)
        };
        if (Substituir(assunto, maximos).Length > 200)
            throw new ConflitoRegraNegocioException("O assunto pode exceder 200 caracteres após substituir as variáveis.");
        if (Substituir(corpoHtml, maximos).Length > 50 * 1024)
            throw new ConflitoRegraNegocioException("O corpo pode exceder 50 KB após substituir as variáveis.");
    }

    public EmailRenderizado Renderizar(ConteudoTemplateEmail template, DadosTemplateEmail dados)
    {
        ValidarTokens(template.Assunto, template.CorpoHtml);
        var primeiroNome = dados.ClienteNome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? dados.ClienteNome;
        var valoresTexto = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["EmpresaNome"] = LimparCabecalho(dados.EmpresaNome),
            ["ClienteNome"] = LimparCabecalho(dados.ClienteNome),
            ["ClientePrimeiroNome"] = LimparCabecalho(primeiroNome),
            ["VeiculoDescricao"] = LimparCabecalho(dados.VeiculoDescricao),
            ["Placa"] = LimparCabecalho(dados.Placa),
            ["OrdemServicoCodigo"] = LimparCabecalho(dados.OrdemServicoCodigo)
        };
        var assunto = Substituir(template.Assunto, valoresTexto);
        if (assunto.Length is < 1 or > 200) throw new ConflitoRegraNegocioException("O assunto renderizado deve possuir entre 1 e 200 caracteres.");
        var valoresHtml = valoresTexto.ToDictionary(x => x.Key, x => HtmlEncoder.Default.Encode(x.Value), StringComparer.Ordinal);
        var corpo = SanitizarEValidarCorpo(Substituir(template.CorpoHtml, valoresHtml));
        return new(assunto, MontarShell(corpo));
    }

    private static string Substituir(string conteudo, IReadOnlyDictionary<string, string> valores) =>
        TokenRegex().Replace(conteudo, match => valores[match.Groups[1].Value]);
    private static string LimparCabecalho(string? valor) =>
        (valor ?? string.Empty).Replace("\r", string.Empty).Replace("\n", " ").Trim();
    private static string MontarShell(string corpo) => $"""
        <!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
        <body style="margin:0;background:#f5f6f8;color:#172033;font-family:Arial,sans-serif"><div style="display:none;max-height:0;overflow:hidden">Atualização sobre seu veículo</div>
        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f5f6f8"><tr><td align="center" style="padding:28px 12px"><table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:620px;background:#ffffff;border:1px solid #e3e7ee;border-radius:16px"><tr><td style="padding:28px"><div style="font-size:20px;font-weight:700;color:#1f6b55;margin-bottom:22px">DETARA</div><div style="font-size:16px;line-height:1.65">{corpo}</div><div style="border-top:1px solid #e3e7ee;margin-top:26px;padding-top:16px;color:#657086;font-size:12px">Mensagem operacional enviada pelo Detara.</div></td></tr></table></td></tr></table></body></html>
        """;

    private static HtmlSanitizer CriarSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear(); foreach (var tag in new[] { "p", "br", "strong", "b", "em", "i", "u", "ul", "ol", "li", "a", "span", "div" }) sanitizer.AllowedTags.Add(tag);
        sanitizer.AllowedAttributes.Clear(); foreach (var attr in new[] { "href", "title", "target", "rel", "style" }) sanitizer.AllowedAttributes.Add(attr);
        sanitizer.AllowedCssProperties.Clear(); foreach (var css in new[] { "text-align", "color" }) sanitizer.AllowedCssProperties.Add(css);
        sanitizer.AllowedAtRules.Clear(); sanitizer.AllowedSchemes.Clear(); foreach (var scheme in new[] { "http", "https", "mailto" }) sanitizer.AllowedSchemes.Add(scheme);
        return sanitizer;
    }

    [GeneratedRegex(@"\{\{([A-Za-z][A-Za-z0-9]*)\}\}", RegexOptions.CultureInvariant)] private static partial Regex TokenRegex();
    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)] private static partial Regex RemoverTagsRegex();
}
