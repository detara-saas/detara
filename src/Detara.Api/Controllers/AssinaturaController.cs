using Detara.Api.Autenticacao;
using Detara.Application.Assinaturas;
using Detara.Contracts.Assinaturas;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Comum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[ApiController]
[Route("api/assinatura")]
[Authorize(AuthenticationSchemes = EsquemasAutenticacao.Tenant)]
public sealed class AssinaturaController(IAssinaturasTenantServico servico) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RespostaApi<AssinaturaEmpresaResponse>>> Obter(CancellationToken cancellationToken) =>
        Ok(RespostaApi<AssinaturaEmpresaResponse>.Ok(Mapear(await servico.ObterAsync(cancellationToken))));

    [HttpGet("termo/previa")]
    [Authorize(Policy = Permissoes.ConfiguracoesEditar)]
    public async Task<IActionResult> Previa(CancellationToken cancellationToken)
    {
        var documento = await servico.GerarPreviaAsync(cancellationToken);
        return File(documento.Conteudo, documento.ContentType, documento.NomeArquivo, enableRangeProcessing: false);
    }

    [HttpPost("termo/aceite")]
    [Authorize(Policy = Permissoes.ConfiguracoesEditar)]
    public async Task<ActionResult<RespostaApi<AssinaturaEmpresaResponse>>> Aceitar(
        AceitarTermoAssinaturaRequest request, CancellationToken cancellationToken)
    {
        if (!request.Aceito) return ValidationProblem("É necessário confirmar expressamente o aceite do termo.");
        var resultado = await servico.AceitarAsync(HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Ok(RespostaApi<AssinaturaEmpresaResponse>.Ok(Mapear(resultado), "Termo aceito com sucesso."));
    }

    [HttpGet("termo/aceito")]
    public async Task<IActionResult> TermoAceito(CancellationToken cancellationToken)
    {
        var documento = await servico.AbrirTermoAceitoAsync(cancellationToken);
        return File(documento.Conteudo, documento.ContentType, documento.NomeArquivo, enableRangeProcessing: false);
    }

    private static AssinaturaEmpresaResponse Mapear(AssinaturaEmpresaResultado resultado) => new(
        resultado.PossuiAssinatura, resultado.Id, resultado.Status, resultado.ValorMensal,
        resultado.InicioTeste, resultado.FimTeste, resultado.DataConfirmacaoComercial,
        resultado.ConfirmacaoComercialRegistradaEmUtc, resultado.DiaVencimento,
        resultado.PrimeiroVencimento, resultado.ProximoVencimento, resultado.Versao,
        resultado.AsaasCustomerId, resultado.AsaasSubscriptionId,
        resultado.TermoAceito is null ? null : new(resultado.TermoAceito.Id,
            resultado.TermoAceito.VersaoTermo, resultado.TermoAceito.AceitoEmUtc,
            resultado.TermoAceito.HashSha256, resultado.TermoAceito.ResponsavelNome,
            resultado.TermoAceito.ResponsavelEmail));
}
