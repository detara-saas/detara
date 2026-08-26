using Detara.Application.Notificacoes;

namespace Detara.Infrastructure.Notificacoes;

internal sealed class RenderizadorTemplateWhatsApp : IRenderizadorTemplateWhatsApp
{
    public string RenderizarVeiculoPronto(DadosTemplateEmail dados)
    {
        var cliente = Limpar(dados.ClienteNome);
        var veiculo = Limpar(dados.VeiculoDescricao);
        var empresa = Limpar(dados.EmpresaNome);
        return $"Olá, {cliente}! Tudo bem?\n\n" +
            $"O serviço do seu veículo {veiculo} foi finalizado e ele já está disponível para retirada na {empresa}.\n\n" +
            $"Ficamos à disposição.\n\n{empresa}";
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
}
