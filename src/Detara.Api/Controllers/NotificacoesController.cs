using Detara.Application.Notificacoes;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Comum;
using Detara.Contracts.Notificacoes;
using Detara.Domain.Notificacoes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Detara.Api.Controllers;

[ApiController]
[Route("api/notificacoes")]
public sealed class NotificacoesController(ISender sender) : ControllerBase
{
    [HttpGet("configuracao"), Authorize(Policy = Permissoes.ConfiguracoesVisualizar)]
    public async Task<ActionResult<RespostaApi<ConfiguracaoNotificacaoResponse>>> ObterConfiguracao(CancellationToken ct) =>
        Ok(RespostaApi<ConfiguracaoNotificacaoResponse>.Ok(Mapear(await sender.Send(new ObterConfiguracaoNotificacaoQuery(), ct))));

    [HttpPut("configuracao"), Authorize(Policy = Permissoes.ConfiguracoesEditar)]
    public async Task<ActionResult<RespostaApi<ConfiguracaoNotificacaoResponse>>> AtualizarConfiguracao(
        AtualizarConfiguracaoNotificacaoRequest request, CancellationToken ct) =>
        Ok(RespostaApi<ConfiguracaoNotificacaoResponse>.Ok(Mapear(await sender.Send(
            new AtualizarConfiguracaoNotificacaoCommand(request.EnviarVeiculoProntoAutomaticamente, request.ResponderParaEmail), ct)),
            "Configurações de e-mail atualizadas."));

    [HttpGet("templates/veiculo-pronto"), Authorize(Policy = Permissoes.ConfiguracoesVisualizar)]
    public async Task<ActionResult<RespostaApi<TemplateEmailResponse>>> ObterTemplate(CancellationToken ct) =>
        Ok(RespostaApi<TemplateEmailResponse>.Ok(Mapear(await sender.Send(new ObterTemplateVeiculoProntoQuery(), ct))));

    [HttpPut("templates/veiculo-pronto"), Authorize(Policy = Permissoes.ConfiguracoesEditar)]
    public async Task<ActionResult<RespostaApi<TemplateEmailResponse>>> SalvarTemplate(SalvarTemplateEmailRequest request,
        CancellationToken ct) => Ok(RespostaApi<TemplateEmailResponse>.Ok(Mapear(await sender.Send(
            new SalvarTemplateVeiculoProntoCommand(request.Assunto, request.CorpoHtml), ct)), "Template de e-mail salvo."));

    [HttpDelete("templates/veiculo-pronto"), Authorize(Policy = Permissoes.ConfiguracoesEditar)]
    public async Task<ActionResult<RespostaApi<TemplateEmailResponse>>> RestaurarTemplate(CancellationToken ct) =>
        Ok(RespostaApi<TemplateEmailResponse>.Ok(Mapear(await sender.Send(new RestaurarTemplateVeiculoProntoCommand(), ct)),
            "Template padrão restaurado."));

    [HttpPost("templates/veiculo-pronto/preview"), Authorize(Policy = Permissoes.ConfiguracoesVisualizar)]
    public async Task<ActionResult<RespostaApi<PreviewTemplateEmailResponse>>> Preview(PreviewTemplateEmailRequest request,
        CancellationToken ct)
    {
        var resultado = await sender.Send(new VisualizarTemplateVeiculoProntoCommand(request.Assunto, request.CorpoHtml), ct);
        return Ok(RespostaApi<PreviewTemplateEmailResponse>.Ok(new(resultado.Assunto, resultado.CorpoHtmlCompleto)));
    }

    [HttpPost("templates/veiculo-pronto/teste"), Authorize(Policy = Permissoes.ConfiguracoesEditar)]
    [EnableRateLimiting("notificacao-teste")]
    public async Task<ActionResult<RespostaApi<object>>> EnviarTeste(CancellationToken ct)
    {
        await sender.Send(new EnviarTesteVeiculoProntoCommand(), ct);
        return Ok(RespostaApi<object>.Ok(new { }, "E-mail de teste aceito pelo provedor."));
    }

    [HttpGet("ordens-servico/{ordemServicoId:guid}"), Authorize(Policy = Permissoes.OrdemServicoVisualizar)]
    public async Task<ActionResult<RespostaApi<NotificacaoOrdemServicoResponse>>> ObterPorOrdemServico(Guid ordemServicoId,
        CancellationToken ct)
    {
        var item = await sender.Send(new ObterNotificacaoOrdemServicoQuery(ordemServicoId), ct);
        return Ok(RespostaApi<NotificacaoOrdemServicoResponse>.Ok(new(item.Notificacao is not null,
            item.Notificacao is null ? null : Mapear(item.Notificacao),
            item.EnviarVeiculoProntoAutomaticamente, item.EmailDestinoAtual)));
    }

    [HttpPost("ordens-servico/{ordemServicoId:guid}/enviar"), Authorize(Policy = Permissoes.NotificacoesReenviar)]
    public async Task<ActionResult<RespostaApi<NotificacaoEmailResponse>>> Enviar(
        Guid ordemServicoId, CancellationToken ct) =>
        Ok(RespostaApi<NotificacaoEmailResponse>.Ok(Mapear(await sender.Send(
            new EnviarAvisoVeiculoProntoCommand(ordemServicoId), ct)), "Envio agendado."));

    [HttpPost("ordens-servico/{ordemServicoId:guid}/tentar-novamente"), Authorize(Policy = Permissoes.NotificacoesReenviar)]
    public async Task<ActionResult<RespostaApi<NotificacaoEmailResponse>>> TentarNovamente(
        Guid ordemServicoId, CancellationToken ct) =>
        Ok(RespostaApi<NotificacaoEmailResponse>.Ok(Mapear(await sender.Send(
            new TentarNovamenteNotificacaoCommand(ordemServicoId), ct)), "Nova tentativa agendada."));

    [HttpPost("ordens-servico/{ordemServicoId:guid}/reenviar"), Authorize(Policy = Permissoes.NotificacoesReenviar)]
    public async Task<ActionResult<RespostaApi<NotificacaoEmailResponse>>> Reenviar(Guid ordemServicoId,
        ReenviarAvisoVeiculoProntoRequest request, CancellationToken ct) =>
        Ok(RespostaApi<NotificacaoEmailResponse>.Ok(Mapear(await sender.Send(
            new ReenviarAvisoVeiculoProntoCommand(ordemServicoId, request.SolicitacaoId), ct)),
            "Reenvio agendado."));

    private static ConfiguracaoNotificacaoResponse Mapear(ConfiguracaoNotificacaoVisualizacao x) =>
        new(x.EnviarVeiculoProntoAutomaticamente, x.ResponderParaEmail, x.AtualizadoEmUtc);
    private static TemplateEmailResponse Mapear(TemplateEmailVisualizacao x) =>
        new(x.Assunto, x.CorpoHtml, (OrigemTemplateEmailContrato)(int)x.Origem, x.AtualizadoEmUtc);
    private static NotificacaoEmailResponse Mapear(NotificacaoEmailVisualizacao x) => new(x.Id, x.OrdemServicoId,
        (StatusNotificacaoEmailContrato)(int)x.Status, x.DestinatarioEmail, x.DestinatarioNome,
        (OrigemTemplateEmailContrato)(int)x.OrigemTemplate, x.QuantidadeTentativas, x.CriadoEmUtc,
        x.EnviadaEmUtc, x.UltimoErroSeguro, x.Tentativas.Select(t => new TentativaNotificacaoEmailResponse(t.Numero,
            (TipoTentativaNotificacaoEmailContrato)(int)t.Tipo, t.ConcluidaEmUtc,
            (ResultadoTentativaNotificacaoEmailContrato)(int)t.Resultado, t.ErroSeguro)).ToArray());
}
