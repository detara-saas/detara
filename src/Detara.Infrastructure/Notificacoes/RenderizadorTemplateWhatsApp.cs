using Detara.Application.Notificacoes;
using Detara.Application.Abstracoes;
using Detara.Domain.Notificacoes;
using System.Text.RegularExpressions;

namespace Detara.Infrastructure.Notificacoes;

internal sealed partial class RenderizadorTemplateWhatsApp : IRenderizadorTemplateWhatsApp
{
    private static readonly HashSet<string> TokensPermitidos = new(StringComparer.Ordinal)
    { "ClienteNome", "VeiculoDescricao", "EmpresaNome" };

    public ConteudoTemplateWhatsApp ObterPadraoVeiculoPronto() => new(
        "Veículo pronto para retirada",
        "Olá, {ClienteNome}! Tudo bem?\n\n" +
        "O seu veículo {VeiculoDescricao} ficou pronto e já está disponível para retirada na {EmpresaNome}.\n\n" +
        "Obrigado pela preferência!",
        OrigemTemplateComunicacao.PadraoDetara);

    public string SanitizarEValidarMensagem(string mensagem)
    {
        var normalizada = (mensagem ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n').Trim();
        if (normalizada.Length is < 1 or > 4096)
            throw new ConflitoRegraNegocioException(
                "A mensagem de WhatsApp deve possuir entre 1 e 4096 caracteres.");
        if (normalizada.Any(c => char.IsControl(c) && c is not '\n' and not '\t'))
            throw new ConflitoRegraNegocioException(
                "A mensagem de WhatsApp contém caracteres de controle inválidos.");
        return normalizada;
    }

    public void ValidarTokens(string mensagem)
    {
        var desconhecidos = TokenRegex().Matches(mensagem ?? string.Empty)
            .Select(x => x.Groups[1].Value)
            .Where(x => !TokensPermitidos.Contains(x))
            .Distinct(StringComparer.Ordinal).ToArray();
        if (desconhecidos.Length > 0)
            throw new ConflitoRegraNegocioException(
                $"Variável desconhecida: {string.Join(", ", desconhecidos.Select(x => "{" + x + "}"))}.");
        var semTokensValidos = TokenRegex().Replace(mensagem ?? string.Empty, string.Empty);
        if (semTokensValidos.Contains('{') || semTokensValidos.Contains('}'))
            throw new ConflitoRegraNegocioException(
                "Existe uma variável inválida ou incompleta no template de WhatsApp.");
    }

    public string RenderizarVeiculoPronto(ConteudoTemplateWhatsApp template,
        DadosTemplateEmail dados)
    {
        ValidarTokens(template.Mensagem);
        var mensagem = SanitizarEValidarMensagem(template.Mensagem);
        var valores = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ClienteNome"] = Limpar(dados.ClienteNome),
            ["VeiculoDescricao"] = Limpar(dados.VeiculoDescricao),
            ["EmpresaNome"] = Limpar(dados.EmpresaNome)
        };
        var renderizada = TokenRegex().Replace(mensagem,
            match => valores[match.Groups[1].Value]);
        if (renderizada.Length > 4096)
            throw new ConflitoRegraNegocioException(
                "A mensagem pode exceder 4096 caracteres após substituir as variáveis.");
        return renderizada;
    }

    public string RenderizarTeste(string empresaNome)
    {
        var empresa = Limpar(empresaNome);
        return "Olá!\n\nEsta é uma mensagem de teste do Detara.\n\n" +
            "Sua conexão WhatsApp está funcionando corretamente.\n\n" +
            empresa;
    }

    private static string Limpar(string valor) =>
        valor.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal).Trim();

    [GeneratedRegex(@"\{([A-Za-z][A-Za-z0-9]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
