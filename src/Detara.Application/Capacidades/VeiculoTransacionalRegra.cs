using Detara.Domain.Capacidades;
using FluentValidation;
using FluentValidation.Results;

namespace Detara.Application.Capacidades;

public static class VeiculoTransacionalRegra
{
    public static async Task<bool> ValidarAsync(
        IEmpresaCapacidadesServico capacidades,
        Guid? veiculoId,
        CancellationToken cancellationToken)
    {
        var utilizaVeiculos = await capacidades.PossuiAsync(
            CodigosCapacidadeEmpresa.Veiculos,
            cancellationToken);

        var possuiVeiculoValido = veiculoId is { } id && id != Guid.Empty;

        if (utilizaVeiculos && !possuiVeiculoValido)
            throw Falha("Veículo é obrigatório para empresas que utilizam o módulo de veículos.");
        if (!utilizaVeiculos && veiculoId.HasValue)
            throw Falha("Não informe veículo quando o módulo de veículos estiver desabilitado.");
        return utilizaVeiculos;
    }

    private static ValidationException Falha(string mensagem) =>
        new([new ValidationFailure("VeiculoId", mensagem)]);
}
