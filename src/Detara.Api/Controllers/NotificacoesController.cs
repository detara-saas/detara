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
            new AtualizarConfiguracaoNotificacaoCommand(
                (CanalComunicacaoVeiculoPronto)(int)request.CanalAutomaticoVeiculoPronto,
                request.ResponderParaEmail,
                request.PermitirComunicacaoWhatsApp), ct)),
            "Configurações de comunicação atualizadas."));

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

    [HttpGet("whatsapp/status"), Authorize(Policy = Permissoes.ConfiguracoesVisualizar)]
    public async Task<ActionResult<RespostaApi<SessaoWhatsAppResponse>>> ObterStatusWhatsApp(
        CancellationToken ct) =>
        Ok(RespostaApi<SessaoWhatsAppResponse>.Ok(Mapear(await sender.Send(
            new ObterStatusSessaoWhatsAppQuery(), ct), incluirQrCode: false)));

    [HttpGet("whatsapp/conexao"), Authorize(Policy = Permissoes.ConfiguracoesEditar)]
    public async Task<ActionResult<RespostaApi<SessaoWhatsAppResponse>>> ObterConexaoWhatsApp(
        CancellationToken ct) =>
        Ok(RespostaApi<SessaoWhatsAppResponse>.Ok(Mapear(await sender.Send(
            new ObterStatusSessaoWhatsAppQuery(), ct), incluirQrCode: true)));

    [HttpGet("whatsapp/disponibilidade"), Authorize(Policy = Permissoes.OrdemServicoVisualizar)]
    public async Task<ActionResult<RespostaApi<SessaoWhatsAppResponse>>> ObterDisponibilidadeWhatsApp(
        CancellationToken ct) =>
        Ok(RespostaApi<SessaoWhatsAppResponse>.Ok(Mapear(await sender.Send(
            new ObterStatusSessaoWhatsAppQuery(), ct), incluirQrCode: false)));

    [HttpPost("whatsapp/conectar"), Authorize(Policy = Permissoes.ConfiguracoesEditar)]
    [EnableRateLimiting("whatsapp-conectar")]
    public async Task<ActionResult<RespostaApi<SessaoWhatsAppResponse>>> ConectarWhatsApp(
        CancellationToken ct) =>
        Ok(RespostaApi<SessaoWhatsAppResponse>.Ok(Mapear(await sender.Send(
            new IniciarConexaoWhatsAppCommand(), ct), incluirQrCode: true),
            "Conexão WhatsApp iniciada."));

    [HttpDelete("whatsapp/conexao"), Authorize(Policy = Permissoes.ConfiguracoesEditar)]
    [EnableRateLimiting("whatsapp-conectar")]
    public async Task<ActionResult<RespostaApi<SessaoWhatsAppResponse>>> DesconectarWhatsApp(
        CancellationToken ct) =>
        Ok(RespostaApi<SessaoWhatsAppResponse>.Ok(Mapear(await sender.Send(
            new DesconectarWhatsAppCommand(), ct), incluirQrCode: false),
            "WhatsApp desconectado desta empresa."));

    [HttpPost("whatsapp/teste"), Authorize(Policy = Permissoes.ConfiguracoesEditar)]
    [EnableRateLimiting("notificacao-teste")]
    public async Task<ActionResult<RespostaApi<ComunicacaoClienteResponse>>> TestarWhatsApp(
        EnviarTesteWhatsAppRequest request, CancellationToken ct) =>
        Ok(RespostaApi<ComunicacaoClienteResponse>.Ok(Mapear(await sender.Send(
            new EnviarTesteWhatsAppCommand(request.Numero, request.Confirmado,
                request.SolicitacaoId), ct)),
            "Mensagem de teste agendada."));

    [HttpGet("whatsapp/testes"), Authorize(Policy = Permissoes.ConfiguracoesVisualizar)]
    public async Task<ActionResult<RespostaApi<IReadOnlyCollection<ComunicacaoClienteResponse>>>>
        ObterTestesWhatsApp(CancellationToken ct) =>
        Ok(RespostaApi<IReadOnlyCollection<ComunicacaoClienteResponse>>.Ok(
            (await sender.Send(new ObterHistoricoTesteWhatsAppQuery(), ct))
                .Select(Mapear).ToArray()));

    [HttpGet("ordens-servico/{ordemServicoId:guid}"), Authorize(Policy = Permissoes.OrdemServicoVisualizar)]
    public async Task<ActionResult<RespostaApi<NotificacaoOrdemServicoResponse>>> ObterPorOrdemServico(Guid ordemServicoId,
        CancellationToken ct)
    {
        var item = await sender.Send(new ObterNotificacaoOrdemServicoQuery(ordemServicoId), ct);
        return Ok(RespostaApi<NotificacaoOrdemServicoResponse>.Ok(new(item.Notificacao is not null,
            item.Notificacao is null ? null : Mapear(item.Notificacao),
            (CanalComunicacaoVeiculoProntoContrato)(int)item.CanalAutomaticoVeiculoPronto,
            item.EmailDestinoAtual, item.WhatsAppDestinoAtual,
            item.Comunicacoes.Select(Mapear).ToArray())));
    }

    [HttpPost("ordens-servico/{ordemServicoId:guid}/comunicar"),
     Authorize(Policy = Permissoes.NotificacoesReenviar)]
    public async Task<ActionResult<RespostaApi<ComunicacaoClienteResponse>>> Comunicar(
        Guid ordemServicoId, ComunicarClienteVeiculoProntoRequest request,
        CancellationToken ct) =>
        Ok(RespostaApi<ComunicacaoClienteResponse>.Ok(Mapear(await sender.Send(
            new ComunicarClienteVeiculoProntoCommand(ordemServicoId,
                (CanalComunicacaoCliente)(int)request.Canal, request.SolicitacaoId), ct)),
            "Comunicação agendada."));

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
        new((CanalComunicacaoVeiculoProntoContrato)(int)x.CanalAutomaticoVeiculoPronto,
            x.ResponderParaEmail, x.PermitirComunicacaoWhatsApp,
            x.DataAtivacaoWhatsAppEmUtc, x.UsuarioAtivacaoWhatsApp,
            x.AtualizadoEmUtc);
    private static TemplateEmailResponse Mapear(TemplateEmailVisualizacao x) =>
        new(x.Assunto, x.CorpoHtml, (OrigemTemplateEmailContrato)(int)x.Origem, x.AtualizadoEmUtc);
    private static NotificacaoEmailResponse Mapear(NotificacaoEmailVisualizacao x) => new(x.Id, x.OrdemServicoId,
        (StatusNotificacaoEmailContrato)(int)x.Status, x.DestinatarioEmail, x.DestinatarioNome,
        (OrigemTemplateEmailContrato)(int)x.OrigemTemplate, x.QuantidadeTentativas, x.CriadoEmUtc,
        x.EnviadaEmUtc, x.UltimoErroSeguro, x.Tentativas.Select(t => new TentativaNotificacaoEmailResponse(t.Numero,
            (TipoTentativaNotificacaoEmailContrato)(int)t.Tipo, t.ConcluidaEmUtc,
            (ResultadoTentativaNotificacaoEmailContrato)(int)t.Resultado, t.ErroSeguro)).ToArray());
    private static ComunicacaoClienteResponse Mapear(ComunicacaoClienteVisualizacao x) =>
        new(x.Id, x.OrdemServicoId, (CanalComunicacaoClienteContrato)(int)x.Canal,
            (TipoComunicacaoClienteContrato)(int)x.Tipo,
            (StatusComunicacaoClienteContrato)(int)x.Status,
            (OrigemComunicacaoClienteContrato)(int)x.Origem, x.Destinatario,
            x.Mensagem, x.SolicitadoPorUsuarioNome, x.CriadoEmUtc,
            x.DataEnvioUtc, x.UltimoErroSeguro);
    private static SessaoWhatsAppResponse Mapear(SessaoWhatsAppVisualizacao x,
        bool incluirQrCode) =>
        new((StatusSessaoWhatsAppContrato)(int)x.Status,
            incluirQrCode ? x.QrCodeDataUrl : null,
            x.AtualizadoEmUtc, x.UltimaConexaoEmUtc, x.NumeroConectado,
            x.UltimoErroSeguro);
}
