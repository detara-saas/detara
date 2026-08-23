using Detara.Contracts.Veiculos;

namespace Detara.Web.Servicos;

public static class FormatacaoVeiculo
{
    public static string Exibir(string descricao, string? placa)
    {
        if (string.IsNullOrWhiteSpace(placa) ||
            descricao.Contains(placa, StringComparison.OrdinalIgnoreCase))
        {
            return descricao;
        }

        return $"{descricao} · {placa}";
    }

    public static string Identificacao(string? placa, string? alternativa) =>
        !string.IsNullOrWhiteSpace(placa)
            ? placa
            : string.IsNullOrWhiteSpace(alternativa) ? "—" : alternativa;

    public static string NomeTipo(TipoVeiculoContrato tipo) => tipo switch
    {
        TipoVeiculoContrato.MotoAquatica => "Moto aquática",
        TipoVeiculoContrato.QuadricicloUtv => "Quadriciclo / UTV",
        TipoVeiculoContrato.Caminhao => "Caminhão",
        TipoVeiculoContrato.Embarcacao => "Embarcação",
        _ => tipo.ToString()
    };
}
