using Detara.Application.Onboarding;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Comum;
using Detara.Contracts.Onboarding;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/onboarding")]
public sealed class OnboardingController(
    ISender sender,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RespostaApi<OnboardingEmpresaResponse>>> Obter(
        CancellationToken cancellationToken)
    {
        var permissoes = new PermissoesAcoesOnboarding(
            await PodeAsync(Permissoes.ConfiguracoesEditar),
            await PodeAsync(Permissoes.ServicosCriar),
            await PodeAsync(Permissoes.ClientesCriar),
            await PodeAsync(Permissoes.VeiculosCriar),
            await PodeAsync(Permissoes.AgendaCriar));
        var resultado = await sender.Send(
            new ObterOnboardingEmpresaQuery(permissoes),
            cancellationToken);

        return Ok(RespostaApi<OnboardingEmpresaResponse>.Ok(new(
            resultado.Concluido,
            resultado.QuantidadeConcluida,
            resultado.QuantidadeTotal,
            resultado.Etapas.Select(etapa => new OnboardingEtapaResponse(
                etapa.Codigo,
                etapa.Titulo,
                etapa.Descricao,
                etapa.Concluida,
                etapa.PodeExecutar,
                etapa.Destino)).ToArray())));
    }

    private async Task<bool> PodeAsync(string permissao) =>
        (await authorizationService.AuthorizeAsync(User, permissao)).Succeeded;
}
