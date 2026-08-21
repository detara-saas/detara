using Detara.Api.Autenticacao;
using Detara.Application.AdministracaoTenant;
using Detara.Contracts.AdministracaoTenant;
using Detara.Contracts.Comum;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[ApiController]
[Route("api/minha-conta")]
[Authorize(AuthenticationSchemes = EsquemasAutenticacao.Tenant)]
public sealed class MinhaContaController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RespostaApi<MinhaContaResponse>>> Obter(
        CancellationToken cancellationToken) =>
        Ok(RespostaApi<MinhaContaResponse>.Ok(Mapear(
            await sender.Send(new ObterMinhaContaQuery(), cancellationToken))));

    [HttpPut("nome")]
    public async Task<ActionResult<RespostaApi<MinhaContaResponse>>> AtualizarNome(
        AtualizarNomeMinhaContaRequest request,
        CancellationToken cancellationToken) =>
        Ok(RespostaApi<MinhaContaResponse>.Ok(Mapear(await sender.Send(
            new AtualizarNomeMinhaContaCommand(request.Nome, request.Versao),
            cancellationToken)), "Nome atualizado."));

    [HttpPut("email")]
    public async Task<ActionResult<RespostaApi<object>>> AtualizarEmail(
        AtualizarEmailMinhaContaRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(new AtualizarEmailMinhaContaCommand(
            request.NovoEmail, request.SenhaAtual, request.Versao), cancellationToken);
        return Ok(RespostaApi<object>.Ok(new { },
            "E-mail atualizado. Entre novamente para continuar."));
    }

    [HttpPut("senha")]
    public async Task<ActionResult<RespostaApi<object>>> AlterarSenha(
        AlterarSenhaMinhaContaRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(new AlterarSenhaMinhaContaCommand(
            request.SenhaAtual,
            request.NovaSenha,
            request.ConfirmacaoNovaSenha,
            request.Versao), cancellationToken);
        return Ok(RespostaApi<object>.Ok(new { },
            "Senha alterada. Entre novamente para continuar."));
    }

    private static MinhaContaResponse Mapear(MinhaContaResultado resultado) => new(
        resultado.Nome,
        resultado.Email,
        resultado.EmpresaNome,
        resultado.PerfilNome,
        resultado.Versao);
}
