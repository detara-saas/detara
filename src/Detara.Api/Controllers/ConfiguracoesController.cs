using Detara.Application.Atendimento;
using Detara.Contracts.Atendimento;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Comum;
using Detara.Domain.Atendimento;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[ApiController]
[Route("api/configuracoes/operacao")]
public sealed class ConfiguracoesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissoes.ConfiguracoesVisualizar)]
    public async Task<ActionResult<RespostaApi<ConfiguracaoOperacionalResponse>>> Obter(
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(new ObterConfiguracaoOperacionalQuery(), cancellationToken);
        return Ok(RespostaApi<ConfiguracaoOperacionalResponse>.Ok(Mapear(resultado)));
    }

    [HttpPut]
    [Authorize(Policy = Permissoes.ConfiguracoesEditar)]
    public async Task<ActionResult<RespostaApi<ConfiguracaoOperacionalResponse>>> Atualizar(
        AtualizarConfiguracaoOperacionalRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(
            new AtualizarConfiguracaoOperacionalCommand(
                (NivelExigenciaOperacional)request.ChecklistEntrada,
                (NivelExigenciaOperacional)request.FotosEntrada,
                (NivelExigenciaOperacional)request.FotosSaida),
            cancellationToken);
        return Ok(RespostaApi<ConfiguracaoOperacionalResponse>.Ok(
            Mapear(resultado),
            "Configurações operacionais atualizadas."));
    }

    [HttpPut("checklist")]
    [Authorize(Policy = Permissoes.ConfiguracoesEditar)]
    public async Task<ActionResult<RespostaApi<ConfiguracaoOperacionalResponse>>> AtualizarChecklist(
        AtualizarChecklistModeloRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(
            new AtualizarChecklistModeloCommand(
                request.Nome,
                request.Descricao,
                request.Itens.Select(item => item.Descricao).ToArray()),
            cancellationToken);
        return Ok(RespostaApi<ConfiguracaoOperacionalResponse>.Ok(
            Mapear(resultado),
            "Checklist de entrada atualizado."));
    }

    private static ConfiguracaoOperacionalResponse Mapear(
        ConfiguracaoOperacionalVisualizacao resultado) =>
        new(
            resultado.Id,
            (NivelExigenciaOperacionalContrato)resultado.ChecklistEntrada,
            (NivelExigenciaOperacionalContrato)resultado.FotosEntrada,
            (NivelExigenciaOperacionalContrato)resultado.FotosSaida,
            resultado.CriadoEmUtc,
            resultado.AtualizadoEmUtc,
            new ChecklistModeloResponse(
                resultado.Checklist.Id,
                resultado.Checklist.Nome,
                resultado.Checklist.Descricao,
                resultado.Checklist.Itens
                    .OrderBy(item => item.Ordem)
                    .Select(item => new ChecklistModeloItemResponse(
                        item.Id,
                        item.Descricao,
                        item.Ordem))
                    .ToArray(),
                resultado.Checklist.CriadoEmUtc,
                resultado.Checklist.AtualizadoEmUtc));
}
