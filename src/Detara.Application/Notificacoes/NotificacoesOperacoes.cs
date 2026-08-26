using System.Net.Mail;
using Detara.Application.Abstracoes;
using Detara.Application.Comunicacao;
using Detara.Domain.Atendimento;
using Detara.Domain.Notificacoes;
using FluentValidation;
using MediatR;

namespace Detara.Application.Notificacoes;

public sealed record ConfiguracaoNotificacaoVisualizacao(CanalComunicacaoVeiculoPronto CanalAutomaticoVeiculoPronto,
    string? ResponderParaEmail, bool PermitirComunicacaoWhatsApp,
    DateTime? DataAtivacaoWhatsAppEmUtc, string? UsuarioAtivacaoWhatsApp,
    DateTime? AtualizadoEmUtc);
public sealed record TemplateComunicacaoVisualizacao(CanalComunicacaoCliente Canal,
    TipoTemplateComunicacao Tipo, string Nome, string? Assunto, string Conteudo,
    OrigemTemplateComunicacao Origem, DateTime? AtualizadoEmUtc);
public sealed record PreviewTemplateComunicacaoVisualizacao(CanalComunicacaoCliente Canal,
    string? Assunto, string Conteudo);
public sealed record NotificacaoEmailVisualizacao(Guid Id, Guid OrdemServicoId, StatusNotificacaoEmail Status,
    string? DestinatarioEmail, string DestinatarioNome, OrigemTemplateEmail OrigemTemplate,
    int QuantidadeTentativas, DateTime CriadoEmUtc, DateTime? EnviadaEmUtc, string? UltimoErroSeguro,
    IReadOnlyCollection<TentativaNotificacaoEmail> Tentativas);
public sealed record NotificacaoOrdemServicoVisualizacao(
    CanalComunicacaoVeiculoPronto CanalAutomaticoVeiculoPronto, string? EmailDestinoAtual,
    string? WhatsAppDestinoAtual, NotificacaoEmailVisualizacao? Notificacao,
    IReadOnlyCollection<ComunicacaoClienteVisualizacao> Comunicacoes);
public sealed record ComunicacaoClienteVisualizacao(Guid Id, Guid? OrdemServicoId,
    CanalComunicacaoCliente Canal, TipoComunicacaoCliente Tipo, StatusComunicacaoCliente Status,
    OrigemComunicacaoCliente Origem, string? Destinatario, string Mensagem,
    string? TemplateNome, string? SolicitadoPorUsuarioNome, DateTime CriadoEmUtc,
    DateTime? DataEnvioUtc, string? UltimoErroSeguro);
public sealed record SessaoWhatsAppVisualizacao(StatusSessaoWhatsApp Status,
    string? QrCodeDataUrl, DateTime? AtualizadoEmUtc,
    DateTime? UltimaConexaoEmUtc, string? NumeroConectado,
    string? UltimoErroSeguro);

public sealed record ObterConfiguracaoNotificacaoQuery : IRequest<ConfiguracaoNotificacaoVisualizacao>;
public sealed record AtualizarConfiguracaoNotificacaoCommand(CanalComunicacaoVeiculoPronto CanalAutomaticoVeiculoPronto,
    string? ResponderParaEmail, bool PermitirComunicacaoWhatsApp)
    : IRequest<ConfiguracaoNotificacaoVisualizacao>;
public sealed record ObterTemplateVeiculoProntoQuery(CanalComunicacaoCliente Canal)
    : IRequest<TemplateComunicacaoVisualizacao>;
public sealed record SalvarTemplateVeiculoProntoCommand(CanalComunicacaoCliente Canal,
    string? Assunto, string Conteudo) : IRequest<TemplateComunicacaoVisualizacao>;
public sealed record RestaurarTemplateVeiculoProntoCommand(CanalComunicacaoCliente Canal)
    : IRequest<TemplateComunicacaoVisualizacao>;
public sealed record VisualizarTemplateVeiculoProntoCommand(CanalComunicacaoCliente Canal,
    string? Assunto, string Conteudo) : IRequest<PreviewTemplateComunicacaoVisualizacao>;
public sealed record EnviarTesteVeiculoProntoCommand : IRequest;
public sealed record ObterNotificacaoOrdemServicoQuery(Guid OrdemServicoId) : IRequest<NotificacaoOrdemServicoVisualizacao>;
public sealed record EnviarAvisoVeiculoProntoCommand(Guid OrdemServicoId) : IRequest<NotificacaoEmailVisualizacao>;
public sealed record TentarNovamenteNotificacaoCommand(Guid OrdemServicoId) : IRequest<NotificacaoEmailVisualizacao>;
public sealed record ReenviarAvisoVeiculoProntoCommand(Guid OrdemServicoId, Guid SolicitacaoId)
    : IRequest<NotificacaoEmailVisualizacao>;
public sealed record ComunicarClienteVeiculoProntoCommand(Guid OrdemServicoId,
    CanalComunicacaoCliente Canal, Guid SolicitacaoId) : IRequest<ComunicacaoClienteVisualizacao>;
public sealed record ObterStatusSessaoWhatsAppQuery : IRequest<SessaoWhatsAppVisualizacao>;
public sealed record IniciarConexaoWhatsAppCommand : IRequest<SessaoWhatsAppVisualizacao>;
public sealed record DesconectarWhatsAppCommand : IRequest<SessaoWhatsAppVisualizacao>;
public sealed record EnviarTesteWhatsAppCommand(string Numero, bool Confirmado,
    Guid SolicitacaoId) : IRequest<ComunicacaoClienteVisualizacao>;
public sealed record ObterHistoricoTesteWhatsAppQuery
    : IRequest<IReadOnlyCollection<ComunicacaoClienteVisualizacao>>;

internal sealed class ComunicarClienteVeiculoProntoValidator : AbstractValidator<ComunicarClienteVeiculoProntoCommand>
{
    public ComunicarClienteVeiculoProntoValidator()
    {
        RuleFor(x => x.SolicitacaoId).NotEmpty();
        RuleFor(x => x.Canal).IsInEnum();
    }
}

internal sealed class EnviarTesteWhatsAppValidator : AbstractValidator<EnviarTesteWhatsAppCommand>
{
    public EnviarTesteWhatsAppValidator()
    {
        RuleFor(x => x.SolicitacaoId).NotEmpty();
        RuleFor(x => x.Numero).NotEmpty().MaximumLength(32)
            .Must(x => NotificacoesFluxo.NormalizarWhatsAppValido(x) is not null)
            .WithMessage("Informe um número de WhatsApp válido com DDD e código do país.");
        RuleFor(x => x.Confirmado).Equal(true)
            .WithMessage("Confirme o envio da mensagem de teste.");
    }
}

internal sealed class ReenviarAvisoVeiculoProntoValidator : AbstractValidator<ReenviarAvisoVeiculoProntoCommand>
{
    public ReenviarAvisoVeiculoProntoValidator() =>
        RuleFor(x => x.SolicitacaoId).NotEmpty();
}

internal sealed class AtualizarConfiguracaoNotificacaoValidator : AbstractValidator<AtualizarConfiguracaoNotificacaoCommand>
{
    public AtualizarConfiguracaoNotificacaoValidator()
    {
        RuleFor(x => x.CanalAutomaticoVeiculoPronto).IsInEnum();
        RuleFor(x => x.ResponderParaEmail).MaximumLength(200)
            .Must(NotificacoesFluxo.EmailOpcionalValido).WithMessage("Informe um e-mail de resposta válido.");
    }
}
internal sealed class SalvarTemplateVeiculoProntoValidator : AbstractValidator<SalvarTemplateVeiculoProntoCommand>
{
    public SalvarTemplateVeiculoProntoValidator()
    {
        RuleFor(x => x.Canal).IsInEnum();
        RuleFor(x => x.Assunto).NotEmpty().MaximumLength(200)
            .Must(x => x is not null && x.IndexOfAny(['\r', '\n']) < 0)
            .WithMessage("O assunto não pode conter quebras de linha.")
            .When(x => x.Canal == CanalComunicacaoCliente.Email);
        RuleFor(x => x.Assunto).Empty()
            .WithMessage("O template de WhatsApp não possui assunto.")
            .When(x => x.Canal == CanalComunicacaoCliente.WhatsApp);
        RuleFor(x => x.Conteudo).NotEmpty().Must((command, conteudo) =>
                conteudo is not null && conteudo.Length <= (command.Canal == CanalComunicacaoCliente.Email
                    ? 50 * 1024 : 4096))
            .WithMessage("O conteúdo do template excede o limite permitido para o canal.");
    }
}
internal sealed class VisualizarTemplateVeiculoProntoValidator : AbstractValidator<VisualizarTemplateVeiculoProntoCommand>
{
    public VisualizarTemplateVeiculoProntoValidator()
    {
        RuleFor(x => x.Canal).IsInEnum();
        RuleFor(x => x.Assunto).NotEmpty().MaximumLength(200)
            .Must(x => x is not null && x.IndexOfAny(['\r', '\n']) < 0)
            .When(x => x.Canal == CanalComunicacaoCliente.Email);
        RuleFor(x => x.Assunto).Empty()
            .When(x => x.Canal == CanalComunicacaoCliente.WhatsApp);
        RuleFor(x => x.Conteudo).NotEmpty().Must((command, conteudo) =>
            conteudo is not null && conteudo.Length <= (command.Canal == CanalComunicacaoCliente.Email
                ? 50 * 1024 : 4096));
    }
}

internal sealed class ObterConfiguracaoNotificacaoHandler(IUsuarioContexto usuario,
    INotificacoesRepositorio repositorio, IPlataformaNotificacoesConsulta plataforma)
    : IRequestHandler<ObterConfiguracaoNotificacaoQuery, ConfiguracaoNotificacaoVisualizacao>
{
    public async Task<ConfiguracaoNotificacaoVisualizacao> Handle(ObterConfiguracaoNotificacaoQuery request, CancellationToken ct)
    {
        var item = await repositorio.ObterConfiguracaoAsync(ct);
        if (item is null)
            return new(CanalComunicacaoVeiculoPronto.Nenhum, null, false,
                null, null, null);
        var nomeUsuario = item.UsuarioAtivacaoWhatsAppId.HasValue
            ? (await plataforma.ObterUsuarioAsync(usuario.EmpresaId,
                item.UsuarioAtivacaoWhatsAppId.Value, ct))?.Nome
            : null;
        return NotificacoesFluxo.Mapear(item, nomeUsuario);
    }
}

internal sealed class AtualizarConfiguracaoNotificacaoHandler(IUsuarioContexto usuario,
    INotificacoesRepositorio repositorio, IPlataformaNotificacoesConsulta plataforma)
    : IRequestHandler<AtualizarConfiguracaoNotificacaoCommand, ConfiguracaoNotificacaoVisualizacao>
{
    public async Task<ConfiguracaoNotificacaoVisualizacao> Handle(AtualizarConfiguracaoNotificacaoCommand request, CancellationToken ct)
    {
        var item = await repositorio.ObterConfiguracaoAsync(ct);
        if (item is null)
        {
            item = new(usuario.EmpresaId, request.CanalAutomaticoVeiculoPronto,
                request.PermitirComunicacaoWhatsApp, request.ResponderParaEmail,
                usuario.UsuarioId);
            repositorio.Adicionar(item);
        }
        else item.Atualizar(request.CanalAutomaticoVeiculoPronto,
            request.PermitirComunicacaoWhatsApp, request.ResponderParaEmail,
            usuario.UsuarioId);
        await repositorio.SalvarAsync(ct);
        var nomeUsuario = item.UsuarioAtivacaoWhatsAppId.HasValue
            ? (await plataforma.ObterUsuarioAsync(usuario.EmpresaId,
                item.UsuarioAtivacaoWhatsAppId.Value, ct))?.Nome
            : null;
        return NotificacoesFluxo.Mapear(item, nomeUsuario);
    }
}

internal sealed class ObterStatusSessaoWhatsAppHandler(IUsuarioContexto usuario,
    INotificacoesRepositorio repositorio, IWhatsAppClienteProvider provider)
    : IRequestHandler<ObterStatusSessaoWhatsAppQuery, SessaoWhatsAppVisualizacao>
{
    public async Task<SessaoWhatsAppVisualizacao> Handle(
        ObterStatusSessaoWhatsAppQuery request, CancellationToken ct)
    {
        var estado = await provider.ObterStatusAsync(usuario.EmpresaId, ct);
        var sessao = await repositorio.ObterSessaoWhatsAppAsync(true, ct);
        if (sessao is not null)
        {
            sessao.AtualizarStatus(estado.Status, estado.UltimaConexaoEmUtc,
                estado.ErroSeguro, estado.NumeroConectado);
            await repositorio.SalvarAsync(ct);
        }
        return Mapear(estado);
    }

    internal static SessaoWhatsAppVisualizacao Mapear(
        EstadoConexaoWhatsAppClienteProvider estado) =>
        new(estado.Status, estado.QrCodeDataUrl, estado.AtualizadoEmUtc,
            estado.UltimaConexaoEmUtc, estado.NumeroConectado,
            estado.ErroSeguro);
}

internal sealed class IniciarConexaoWhatsAppHandler(IUsuarioContexto usuario,
    INotificacoesRepositorio repositorio, IWhatsAppClienteProvider provider)
    : IRequestHandler<IniciarConexaoWhatsAppCommand, SessaoWhatsAppVisualizacao>
{
    public async Task<SessaoWhatsAppVisualizacao> Handle(
        IniciarConexaoWhatsAppCommand request, CancellationToken ct)
    {
        var sessao = await repositorio.ObterSessaoWhatsAppAsync(true, ct);
        if (sessao is null)
        {
            sessao = new SessaoWhatsAppEmpresa(usuario.EmpresaId,
                $"tenant-{usuario.EmpresaId:N}");
            repositorio.Adicionar(sessao);
        }
        var estado = await provider.IniciarConexaoAsync(usuario.EmpresaId, ct);
        sessao.AtualizarStatus(estado.Status, estado.UltimaConexaoEmUtc,
            estado.ErroSeguro, estado.NumeroConectado);
        await repositorio.SalvarAsync(ct);
        return ObterStatusSessaoWhatsAppHandler.Mapear(estado);
    }
}

internal sealed class DesconectarWhatsAppHandler(IUsuarioContexto usuario,
    INotificacoesRepositorio repositorio, IWhatsAppClienteProvider provider)
    : IRequestHandler<DesconectarWhatsAppCommand, SessaoWhatsAppVisualizacao>
{
    public async Task<SessaoWhatsAppVisualizacao> Handle(
        DesconectarWhatsAppCommand request, CancellationToken ct)
    {
        var estado = await provider.DesconectarAsync(usuario.EmpresaId, ct);
        var sessao = await repositorio.ObterSessaoWhatsAppAsync(true, ct);
        if (sessao is not null)
        {
            sessao.AtualizarStatus(StatusSessaoWhatsApp.Desconectada,
                estado.UltimaConexaoEmUtc, null, null);
            await repositorio.SalvarAsync(ct);
        }
        return ObterStatusSessaoWhatsAppHandler.Mapear(estado);
    }
}

internal sealed class ObterTemplateVeiculoProntoHandler(INotificacoesRepositorio repositorio,
    IRenderizadorTemplateEmail rendererEmail, IRenderizadorTemplateWhatsApp rendererWhatsApp)
    : IRequestHandler<ObterTemplateVeiculoProntoQuery, TemplateComunicacaoVisualizacao>
{
    public async Task<TemplateComunicacaoVisualizacao> Handle(
        ObterTemplateVeiculoProntoQuery request, CancellationToken ct) =>
        NotificacoesFluxo.MapearTemplate(request.Canal,
            await repositorio.ObterTemplateAsync(request.Canal,
                TipoTemplateComunicacao.VeiculoProntoRetirada, false, ct),
            rendererEmail, rendererWhatsApp);
}

internal sealed class SalvarTemplateVeiculoProntoHandler(IUsuarioContexto usuario, INotificacoesRepositorio repositorio,
    IRenderizadorTemplateEmail rendererEmail, IRenderizadorTemplateWhatsApp rendererWhatsApp)
    : IRequestHandler<SalvarTemplateVeiculoProntoCommand, TemplateComunicacaoVisualizacao>
{
    public async Task<TemplateComunicacaoVisualizacao> Handle(
        SalvarTemplateVeiculoProntoCommand request, CancellationToken ct)
    {
        string conteudo;
        if (request.Canal == CanalComunicacaoCliente.Email)
        {
            rendererEmail.ValidarTokens(request.Assunto!, request.Conteudo);
            conteudo = rendererEmail.SanitizarEValidarCorpo(request.Conteudo);
        }
        else
        {
            rendererWhatsApp.ValidarTokens(request.Conteudo);
            conteudo = rendererWhatsApp.SanitizarEValidarMensagem(request.Conteudo);
        }
        var item = await repositorio.ObterTemplateAsync(request.Canal,
            TipoTemplateComunicacao.VeiculoProntoRetirada, true, ct);
        if (item is null)
        {
            item = new(usuario.EmpresaId, request.Canal,
                TipoTemplateComunicacao.VeiculoProntoRetirada,
                "Veículo pronto para retirada", request.Assunto,
                conteudo, usuario.UsuarioId);
            repositorio.Adicionar(item);
        }
        else item.Atualizar(item.Nome, request.Assunto, conteudo, usuario.UsuarioId);
        await repositorio.SalvarAsync(ct);
        return NotificacoesFluxo.MapearTemplate(item);
    }
}

internal sealed class RestaurarTemplateVeiculoProntoHandler(INotificacoesRepositorio repositorio,
    IRenderizadorTemplateEmail rendererEmail, IRenderizadorTemplateWhatsApp rendererWhatsApp)
    : IRequestHandler<RestaurarTemplateVeiculoProntoCommand, TemplateComunicacaoVisualizacao>
{
    public async Task<TemplateComunicacaoVisualizacao> Handle(
        RestaurarTemplateVeiculoProntoCommand request, CancellationToken ct)
    {
        var item = await repositorio.ObterTemplateAsync(request.Canal,
            TipoTemplateComunicacao.VeiculoProntoRetirada, true, ct);
        if (item is not null) { repositorio.Remover(item); await repositorio.SalvarAsync(ct); }
        return NotificacoesFluxo.MapearTemplate(request.Canal, null,
            rendererEmail, rendererWhatsApp);
    }
}

internal sealed class VisualizarTemplateVeiculoProntoHandler(IRenderizadorTemplateEmail rendererEmail,
    IRenderizadorTemplateWhatsApp rendererWhatsApp)
    : IRequestHandler<VisualizarTemplateVeiculoProntoCommand, PreviewTemplateComunicacaoVisualizacao>
{
    public Task<PreviewTemplateComunicacaoVisualizacao> Handle(
        VisualizarTemplateVeiculoProntoCommand request, CancellationToken ct)
    {
        var dados = new DadosTemplateEmail("Estética Horizonte", "João Souza",
            "Honda Civic", "ABC1D23", "OS-2026-0042");
        if (request.Canal == CanalComunicacaoCliente.Email)
        {
            rendererEmail.ValidarTokens(request.Assunto!, request.Conteudo);
            var corpo = rendererEmail.SanitizarEValidarCorpo(request.Conteudo);
            var renderizado = rendererEmail.Renderizar(new(request.Assunto!, corpo,
                OrigemTemplateEmail.PersonalizadoEmpresa), dados);
            return Task.FromResult(new PreviewTemplateComunicacaoVisualizacao(
                request.Canal, renderizado.Assunto, renderizado.CorpoHtmlCompleto));
        }
        rendererWhatsApp.ValidarTokens(request.Conteudo);
        var mensagem = rendererWhatsApp.SanitizarEValidarMensagem(request.Conteudo);
        var preview = rendererWhatsApp.RenderizarVeiculoPronto(new(
            "Veículo pronto para retirada", mensagem,
            OrigemTemplateComunicacao.PersonalizadoEmpresa), dados);
        return Task.FromResult(new PreviewTemplateComunicacaoVisualizacao(
            request.Canal, null, preview));
    }
}

internal sealed class EnviarTesteVeiculoProntoHandler(IUsuarioContexto usuario, INotificacoesRepositorio repositorio,
    IRenderizadorTemplateEmail renderer, IPlataformaNotificacoesConsulta plataforma, IProvedorEmail provedor)
    : IRequestHandler<EnviarTesteVeiculoProntoCommand>
{
    public async Task Handle(EnviarTesteVeiculoProntoCommand request, CancellationToken ct)
    {
        var destinatario = await plataforma.ObterUsuarioAsync(usuario.EmpresaId, usuario.UsuarioId, ct)
            ?? throw new RecursoNaoEncontradoException("Usuário autenticado não encontrado.");
        var empresa = await plataforma.ObterEmpresaAsync(usuario.EmpresaId, ct)
            ?? throw new RecursoNaoEncontradoException("Empresa não encontrada.");
        var custom = await repositorio.ObterTemplateAsync(CanalComunicacaoCliente.Email,
            TipoTemplateComunicacao.VeiculoProntoRetirada, false, ct);
        var template = custom is null ? renderer.ObterPadraoVeiculoPronto() :
            new ConteudoTemplateEmail(custom.Assunto!, custom.Conteudo,
                OrigemTemplateEmail.PersonalizadoEmpresa);
        var renderizado = renderer.Renderizar(template, new(empresa.Nome, destinatario.Nome,
            "Veículo de demonstração", "ABC1D23", "OS-TESTE"));
        var config = await repositorio.ObterConfiguracaoAsync(ct);
        var resultado = await provedor.EnviarAsync(new(destinatario.Email, renderizado.Assunto,
            renderizado.CorpoHtmlCompleto, config?.ResponderParaEmail, $"teste-template/{usuario.EmpresaId:N}/{Guid.NewGuid():N}"), ct);
        if (!resultado.Sucesso) throw new ConflitoRegraNegocioException(resultado.ErroSeguro ?? "O provedor não aceitou o e-mail de teste.");
    }
}

internal sealed class EnviarTesteWhatsAppHandler(IUsuarioContexto usuario,
    INotificacoesRepositorio repositorio, IPlataformaNotificacoesConsulta plataforma,
    IRenderizadorTemplateWhatsApp renderer, IWhatsAppClienteProvider provider)
    : IRequestHandler<EnviarTesteWhatsAppCommand, ComunicacaoClienteVisualizacao>
{
    public async Task<ComunicacaoClienteVisualizacao> Handle(
        EnviarTesteWhatsAppCommand request, CancellationToken ct)
    {
        if (!request.Confirmado)
            throw new ConflitoRegraNegocioException(
                "Confirme o envio da mensagem de teste.");
        var repetida = await repositorio.ObterComunicacaoPorIdAsync(
            request.SolicitacaoId, false, ct);
        if (repetida is not null)
        {
            if (repetida.Tipo != TipoComunicacaoCliente.TesteWhatsApp)
                throw new ConflitoRegraNegocioException(
                    "A solicitação de teste é inválida.");
            return NotificacoesFluxo.Mapear(repetida, null);
        }

        var estado = await provider.ObterStatusAsync(usuario.EmpresaId, ct);
        if (estado.Status != StatusSessaoWhatsApp.Conectada)
            throw new ConflitoRegraNegocioException(
                "Conecte o WhatsApp da empresa antes de testar o envio.");
        var empresa = await plataforma.ObterEmpresaAsync(usuario.EmpresaId, ct)
            ?? throw new RecursoNaoEncontradoException("Empresa não encontrada.");
        var numero = NotificacoesFluxo.NormalizarWhatsAppValido(request.Numero)
            ?? throw new ConflitoRegraNegocioException(
                "Informe um número de WhatsApp válido com DDD e código do país.");
        var comunicacao = ComunicacaoCliente.CriarTesteWhatsApp(
            request.SolicitacaoId, usuario.EmpresaId,
            renderer.RenderizarTeste(empresa.Nome), numero, usuario.UsuarioId);
        if (!await repositorio.TentarAdicionarComunicacaoESalvarAsync(
            comunicacao, null, ct))
        {
            var concorrente = await repositorio.ObterComunicacaoPorIdAsync(
                request.SolicitacaoId, false, ct)
                ?? throw new ConflitoRegraNegocioException(
                    "O teste já foi solicitado.");
            return NotificacoesFluxo.Mapear(concorrente, null);
        }
        var solicitante = await plataforma.ObterUsuarioAsync(usuario.EmpresaId,
            usuario.UsuarioId, ct);
        return NotificacoesFluxo.Mapear(comunicacao, solicitante?.Nome);
    }
}

internal sealed class ObterHistoricoTesteWhatsAppHandler(IUsuarioContexto usuario,
    INotificacoesRepositorio repositorio, IPlataformaNotificacoesConsulta plataforma)
    : IRequestHandler<ObterHistoricoTesteWhatsAppQuery,
        IReadOnlyCollection<ComunicacaoClienteVisualizacao>>
{
    public async Task<IReadOnlyCollection<ComunicacaoClienteVisualizacao>> Handle(
        ObterHistoricoTesteWhatsAppQuery request, CancellationToken ct) =>
        await NotificacoesFluxo.MapearComUsuariosAsync(
            await repositorio.ObterTestesWhatsAppAsync(10, ct), plataforma,
            usuario.EmpresaId, ct);
}

internal sealed class ObterNotificacaoOrdemServicoHandler(IUsuarioContexto usuario,
    INotificacoesRepositorio repositorio, IClientesNotificacoesConsulta clientes,
    IAtendimentoNotificacoesConsulta atendimento,
    IPlataformaNotificacoesConsulta plataforma)
    : IRequestHandler<ObterNotificacaoOrdemServicoQuery, NotificacaoOrdemServicoVisualizacao>
{
    public async Task<NotificacaoOrdemServicoVisualizacao> Handle(
        ObterNotificacaoOrdemServicoQuery request, CancellationToken ct)
    {
        var ordem = await atendimento.ObterOrdemServicoAsync(usuario.EmpresaId,
            request.OrdemServicoId, ct)
            ?? throw new RecursoNaoEncontradoException("Ordem de serviço não encontrada.");
        var configuracao = await repositorio.ObterConfiguracaoAsync(ct);
        var cliente = await clientes.ObterClienteAsync(usuario.EmpresaId, ordem.ClienteId, ct);
        var item = await repositorio.ObterUltimaPorOrdemServicoAsync(request.OrdemServicoId, false, ct);
        var canalAutomatico = configuracao?.CanalAutomaticoVeiculoPronto ??
            CanalComunicacaoVeiculoPronto.Nenhum;
        var comunicacoes = await repositorio.ObterComunicacoesPorOrdemServicoAsync(
            request.OrdemServicoId, ct);
        return new(canalAutomatico,
            NotificacoesFluxo.NormalizarEmailValido(cliente?.Email),
            NotificacoesFluxo.NormalizarWhatsAppValido(cliente?.WhatsApp),
            item is null ? null : NotificacoesFluxo.Mapear(item),
            await NotificacoesFluxo.MapearComUsuariosAsync(comunicacoes,
                plataforma, usuario.EmpresaId, ct));
    }
}

internal sealed class ComunicarClienteVeiculoProntoHandler(IComunicacaoClienteService servico)
    : IRequestHandler<ComunicarClienteVeiculoProntoCommand, ComunicacaoClienteVisualizacao>
{
    public async Task<ComunicacaoClienteVisualizacao> Handle(
        ComunicarClienteVeiculoProntoCommand request, CancellationToken ct) =>
        NotificacoesFluxo.Mapear(await servico.PrepararManualAsync(
            request.OrdemServicoId, request.Canal, request.SolicitacaoId, ct));
}

internal sealed class EnviarAvisoVeiculoProntoHandler(IComunicacaoClienteService servico,
    INotificacoesRepositorio repositorio)
    : IRequestHandler<EnviarAvisoVeiculoProntoCommand, NotificacaoEmailVisualizacao>
{
    public async Task<NotificacaoEmailVisualizacao> Handle(
        EnviarAvisoVeiculoProntoCommand request, CancellationToken ct)
    {
        var existente = await repositorio.ObterUltimaPorOrdemServicoAsync(
            request.OrdemServicoId, false, ct);
        if (existente is not null)
            throw new ConflitoRegraNegocioException(
                NotificacoesFluxo.MensagemAcaoPara(existente.Status));
        var comunicacao = await servico.PrepararManualAsync(request.OrdemServicoId,
            CanalComunicacaoCliente.Email, request.OrdemServicoId, ct);
        var item = await repositorio.ObterPorIdAsync(comunicacao.Id, ct)
            ?? throw new InvalidOperationException("A intenção de e-mail não foi criada.");
        return NotificacoesFluxo.Mapear(item);
    }
}

internal sealed class TentarNovamenteNotificacaoHandler(IUsuarioContexto usuario,
    INotificacoesRepositorio repositorio, IClientesNotificacoesConsulta clientes,
    IAtendimentoNotificacoesConsulta atendimento)
    : IRequestHandler<TentarNovamenteNotificacaoCommand, NotificacaoEmailVisualizacao>
{
    public async Task<NotificacaoEmailVisualizacao> Handle(
        TentarNovamenteNotificacaoCommand request, CancellationToken ct)
    {
        await NotificacoesFluxo.ExigirOrdemAguardandoRetiradaAsync(
            atendimento, usuario.EmpresaId, request.OrdemServicoId, ct);
        var item = await repositorio.ObterUltimaPorOrdemServicoAsync(request.OrdemServicoId, true, ct)
            ?? throw new RecursoNaoEncontradoException("Notificação da ordem de serviço não encontrada.");
        string? email = item.DestinatarioEmailSnapshot;
        if (item.Status == StatusNotificacaoEmail.SemDestinatario)
        {
            var cliente = await clientes.ObterClienteAsync(usuario.EmpresaId, item.ClienteId, ct);
            email = NotificacoesFluxo.ExigirEmailCliente(cliente);
        }
        try { item.PrepararNovaTentativaManual(email, usuario.UsuarioId, DateTime.UtcNow); }
        catch (InvalidOperationException ex) { throw new ConflitoRegraNegocioException(ex.Message); }
        var comunicacao = await repositorio.ObterComunicacaoPorIdAsync(item.Id, true, ct);
        if (comunicacao is not null)
        {
            try { comunicacao.PrepararNovaTentativa(email!); }
            catch (InvalidOperationException ex) { throw new ConflitoRegraNegocioException(ex.Message); }
        }
        if (!await repositorio.TentarSalvarAlteracaoAsync(ct))
        {
            var concorrente = await repositorio.ObterUltimaPorOrdemServicoAsync(request.OrdemServicoId, false, ct)
                ?? throw new ConflitoRegraNegocioException("A nova tentativa já foi solicitada.");
            return NotificacoesFluxo.Mapear(concorrente);
        }
        return NotificacoesFluxo.Mapear(item);
    }
}

internal sealed class ReenviarAvisoVeiculoProntoHandler(IComunicacaoClienteService servico,
    INotificacoesRepositorio repositorio)
    : IRequestHandler<ReenviarAvisoVeiculoProntoCommand, NotificacaoEmailVisualizacao>
{
    public async Task<NotificacaoEmailVisualizacao> Handle(
        ReenviarAvisoVeiculoProntoCommand request, CancellationToken ct)
    {
        var repetida = await repositorio.ObterPorIdAsync(request.SolicitacaoId, ct);
        if (repetida is not null)
        {
            if (repetida.OrdemServicoId != request.OrdemServicoId)
                throw new ConflitoRegraNegocioException("A solicitação de reenvio é inválida.");
            return NotificacoesFluxo.Mapear(repetida);
        }
        var anterior = await repositorio.ObterUltimaPorOrdemServicoAsync(request.OrdemServicoId, false, ct)
            ?? throw new RecursoNaoEncontradoException("Notificação da ordem de serviço não encontrada.");
        if (anterior.Status != StatusNotificacaoEmail.Enviada)
            throw new ConflitoRegraNegocioException(NotificacoesFluxo.MensagemAcaoPara(anterior.Status));
        var comunicacao = await servico.PrepararManualAsync(request.OrdemServicoId,
            CanalComunicacaoCliente.Email, request.SolicitacaoId, ct);
        var item = await repositorio.ObterPorIdAsync(comunicacao.Id, ct)
            ?? throw new InvalidOperationException("A intenção de reenvio não foi criada.");
        return NotificacoesFluxo.Mapear(item);
    }
}

public sealed class ComunicacaoClienteService(IUsuarioContexto usuario,
    INotificacoesRepositorio repositorio, IClientesNotificacoesConsulta clientes,
    IPlataformaNotificacoesConsulta plataforma, IAtendimentoNotificacoesConsulta atendimento,
    IRenderizadorTemplateEmail rendererEmail, IRenderizadorTemplateWhatsApp rendererWhatsApp)
    : IComunicacaoClienteService, IIntegracaoNotificacoesOrdensServico
{
    public async Task PrepararNotificacaoAsync(OrdemServicoFinalizadaNotificacoes evento, CancellationToken ct)
    {
        if (evento.EmpresaId != usuario.EmpresaId)
            throw new ViolacaoIsolamentoTenantException();
        var config = await repositorio.ObterConfiguracaoAsync(ct);
        var canal = NotificacoesFluxo.MapearCanal(config?.CanalAutomaticoVeiculoPronto ??
            CanalComunicacaoVeiculoPronto.Nenhum);
        if (canal == CanalComunicacaoCliente.WhatsApp &&
            config?.PermitirComunicacaoWhatsApp != true) return;
        if (!canal.HasValue || await repositorio.ObterComunicacaoPorIdAsync(
            evento.OrdemServicoId, false, ct) is not null) return;

        var preparada = await CriarAsync(evento.OrdemServicoId,
            new(evento.OrdemServicoId, evento.OrdemServicoCodigo,
                StatusOrdemServico.AguardandoRetirada, evento.ClienteId, evento.ClienteNome,
                evento.VeiculoDescricao, evento.VeiculoPlaca), canal.Value,
            OrigemComunicacaoCliente.Automatica, null, config, exigirDestinatario: false, ct);
        repositorio.Adicionar(preparada.Comunicacao);
        if (preparada.NotificacaoEmail is not null)
            repositorio.Adicionar(preparada.NotificacaoEmail);
    }

    public async Task<ComunicacaoCliente> PrepararManualAsync(Guid ordemServicoId,
        CanalComunicacaoCliente canal, Guid solicitacaoId, CancellationToken ct)
    {
        if (!Enum.IsDefined(canal))
            throw new ConflitoRegraNegocioException("Selecione Email ou WhatsApp.");
        if (solicitacaoId == Guid.Empty)
            throw new ConflitoRegraNegocioException("A solicitação de comunicação é inválida.");

        var repetida = await repositorio.ObterComunicacaoPorIdAsync(solicitacaoId, false, ct);
        if (repetida is not null)
        {
            if (repetida.OrdemServicoId != ordemServicoId || repetida.Canal != canal)
                throw new ConflitoRegraNegocioException("A solicitação de comunicação é inválida.");
            return repetida;
        }

        var ordem = await NotificacoesFluxo.ExigirOrdemAguardandoRetiradaAsync(
            atendimento, usuario.EmpresaId, ordemServicoId, ct);
        if (await repositorio.ExisteComunicacaoPendenteAsync(ordemServicoId, ct))
            throw new ConflitoRegraNegocioException(
                "Já existe uma comunicação pendente para esta ordem de serviço.");

        var config = await repositorio.ObterConfiguracaoAsync(ct);
        var preparada = await CriarAsync(solicitacaoId, ordem, canal,
            OrigemComunicacaoCliente.Manual, usuario.UsuarioId, config,
            exigirDestinatario: true, ct);
        if (await repositorio.ExisteComunicacaoEnviadaRecenteAsync(
            ordemServicoId, canal, TipoComunicacaoCliente.VeiculoPronto,
            preparada.Comunicacao.Mensagem,
            preparada.Comunicacao.DestinatarioSnapshot!,
            DateTime.UtcNow.AddMinutes(-5), ct))
            throw new ConflitoRegraNegocioException(
                "Já existe uma comunicação enviada recentemente para este cliente.");
        if (!await repositorio.TentarAdicionarComunicacaoESalvarAsync(
            preparada.Comunicacao, preparada.NotificacaoEmail, ct))
        {
            var concorrente = await repositorio.ObterComunicacaoPorIdAsync(
                solicitacaoId, false, ct);
            if (concorrente?.OrdemServicoId != ordemServicoId || concorrente.Canal != canal)
                throw new ConflitoRegraNegocioException("O envio já foi solicitado.");
            return concorrente;
        }
        return preparada.Comunicacao;
    }

    private async Task<ComunicacaoPreparada> CriarAsync(Guid id,
        OrdemServicoNotificacoesInterna ordem, CanalComunicacaoCliente canal,
        OrigemComunicacaoCliente origem, Guid? solicitadoPorUsuarioId,
        ConfiguracaoNotificacaoEmpresa? configuracao, bool exigirDestinatario,
        CancellationToken ct)
    {
        var cliente = await clientes.ObterClienteAsync(usuario.EmpresaId, ordem.ClienteId, ct);
        var empresa = await plataforma.ObterEmpresaAsync(usuario.EmpresaId, ct)
            ?? throw new RecursoNaoEncontradoException("Empresa não encontrada.");
        var dados = new DadosTemplateEmail(empresa.Nome, ordem.ClienteNome,
            ordem.VeiculoDescricao, ordem.VeiculoPlaca, ordem.Codigo);

        if (canal == CanalComunicacaoCliente.WhatsApp)
        {
            var destinatario = NotificacoesFluxo.NormalizarWhatsAppValido(cliente?.WhatsApp);
            if (exigirDestinatario && destinatario is null)
                throw new ConflitoRegraNegocioException(
                    "O cliente não possui um WhatsApp válido cadastrado.");
            var templateWhatsAppEmpresa = await repositorio.ObterTemplateAsync(
                CanalComunicacaoCliente.WhatsApp,
                TipoTemplateComunicacao.VeiculoProntoRetirada, false, ct);
            var templateWhatsApp = templateWhatsAppEmpresa is null
                ? rendererWhatsApp.ObterPadraoVeiculoPronto()
                : new ConteudoTemplateWhatsApp(templateWhatsAppEmpresa.Nome,
                    templateWhatsAppEmpresa.Conteudo,
                    OrigemTemplateComunicacao.PersonalizadoEmpresa);
            var mensagem = rendererWhatsApp.RenderizarVeiculoPronto(templateWhatsApp, dados);
            return new(new ComunicacaoCliente(id, usuario.EmpresaId, ordem.ClienteId,
                ordem.Id, canal, TipoComunicacaoCliente.VeiculoPronto, mensagem,
                destinatario, origem, solicitadoPorUsuarioId, templateWhatsApp.Nome), null);
        }

        var email = NotificacoesFluxo.NormalizarEmailValido(cliente?.Email);
        if (exigirDestinatario && email is null)
            throw new ConflitoRegraNegocioException(
                "O cliente não possui um e-mail válido cadastrado.");
        var custom = await repositorio.ObterTemplateAsync(CanalComunicacaoCliente.Email,
            TipoTemplateComunicacao.VeiculoProntoRetirada, false, ct);
        var template = custom is null ? rendererEmail.ObterPadraoVeiculoPronto() :
            new ConteudoTemplateEmail(custom.Assunto!, custom.Conteudo,
                OrigemTemplateEmail.PersonalizadoEmpresa);
        var renderizado = rendererEmail.Renderizar(template, dados);
        var tipoTentativa = origem == OrigemComunicacaoCliente.Automatica
            ? TipoTentativaNotificacaoEmail.Automatica
            : TipoTentativaNotificacaoEmail.Manual;
        var notificacao = new NotificacaoEmail(id, usuario.EmpresaId, ordem.Id,
            ordem.ClienteId, TipoTemplateEmail.VeiculoProntoRetirada, email,
            ordem.ClienteNome, renderizado.Assunto, renderizado.CorpoHtmlCompleto,
            template.Origem, configuracao?.ResponderParaEmail, tipoTentativa,
            solicitadoPorUsuarioId);
        var comunicacao = new ComunicacaoCliente(id, usuario.EmpresaId,
            ordem.ClienteId, ordem.Id, canal, TipoComunicacaoCliente.VeiculoPronto,
            renderizado.CorpoHtmlCompleto, email, origem, solicitadoPorUsuarioId,
            custom?.Nome ?? "Veículo pronto para retirada");
        return new(comunicacao, notificacao);
    }

    private sealed record ComunicacaoPreparada(ComunicacaoCliente Comunicacao,
        NotificacaoEmail? NotificacaoEmail);
}

internal static class NotificacoesFluxo
{
    public static bool EmailOpcionalValido(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return true;
        try { return new MailAddress(email).Address.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase); }
        catch (FormatException) { return false; }
    }
    public static string? NormalizarEmailValido(string? email) =>
        string.IsNullOrWhiteSpace(email) || !EmailOpcionalValido(email)
            ? null
            : email.Trim().ToLowerInvariant();
    public static string? NormalizarWhatsAppValido(string? whatsApp)
    {
        if (string.IsNullOrWhiteSpace(whatsApp)) return null;
        var digitos = new string(whatsApp.Where(char.IsDigit).ToArray());
        return digitos.Length is >= 8 and <= 15 ? digitos : null;
    }
    public static string ExigirEmailCliente(ClienteNotificacoesInterno? cliente) =>
        NormalizarEmailValido(cliente?.Email)
        ?? throw new ConflitoRegraNegocioException("O cliente não possui um e-mail válido cadastrado.");
    public static async Task<OrdemServicoNotificacoesInterna> ExigirOrdemAguardandoRetiradaAsync(
        IAtendimentoNotificacoesConsulta atendimento, Guid empresaId, Guid ordemServicoId,
        CancellationToken ct)
    {
        var ordem = await atendimento.ObterOrdemServicoAsync(empresaId, ordemServicoId, ct)
            ?? throw new RecursoNaoEncontradoException("Ordem de serviço não encontrada.");
        if (ordem.Status != StatusOrdemServico.AguardandoRetirada)
            throw new ConflitoRegraNegocioException(
                "O aviso de veículo pronto só pode ser enviado enquanto a ordem de serviço aguarda retirada.");
        return ordem;
    }
    public static CanalComunicacaoCliente? MapearCanal(
        CanalComunicacaoVeiculoPronto canal) => canal switch
        {
            CanalComunicacaoVeiculoPronto.Email => CanalComunicacaoCliente.Email,
            CanalComunicacaoVeiculoPronto.WhatsApp => CanalComunicacaoCliente.WhatsApp,
            _ => null
        };
    public static string MensagemAcaoPara(StatusNotificacaoEmail status) => status switch
    {
        StatusNotificacaoEmail.Pendente => "Já existe um envio agendado para esta ordem de serviço.",
        StatusNotificacaoEmail.Processando => "O aviso já está sendo enviado.",
        StatusNotificacaoEmail.Enviada => "O aviso já foi enviado. Use a ação de reenvio para criar uma nova comunicação.",
        _ => "A notificação existente deve ser tentada novamente antes de criar outro envio."
    };
    public static ConfiguracaoNotificacaoVisualizacao Mapear(
        ConfiguracaoNotificacaoEmpresa item, string? usuarioAtivacaoWhatsApp) =>
        new(item.CanalAutomaticoVeiculoPronto, item.ResponderParaEmail,
            item.PermitirComunicacaoWhatsApp, item.DataAtivacaoWhatsAppEmUtc,
            usuarioAtivacaoWhatsApp, item.AtualizadoEmUtc ?? item.CriadoEmUtc);
    public static TemplateComunicacaoVisualizacao MapearTemplate(
        CanalComunicacaoCliente canal, TemplateComunicacaoEmpresa? item,
        IRenderizadorTemplateEmail rendererEmail,
        IRenderizadorTemplateWhatsApp rendererWhatsApp)
    {
        if (item is not null) return MapearTemplate(item);
        if (canal == CanalComunicacaoCliente.Email)
        {
            var padrao = rendererEmail.ObterPadraoVeiculoPronto();
            return new(canal, TipoTemplateComunicacao.VeiculoProntoRetirada,
                "Veículo pronto para retirada", padrao.Assunto, padrao.CorpoHtml,
                OrigemTemplateComunicacao.PadraoDetara, null);
        }
        var whatsapp = rendererWhatsApp.ObterPadraoVeiculoPronto();
        return new(canal, TipoTemplateComunicacao.VeiculoProntoRetirada,
            whatsapp.Nome, null, whatsapp.Mensagem, whatsapp.Origem, null);
    }

    public static TemplateComunicacaoVisualizacao MapearTemplate(
        TemplateComunicacaoEmpresa item) =>
        new(item.Canal, item.Tipo, item.Nome, item.Assunto, item.Conteudo,
            OrigemTemplateComunicacao.PersonalizadoEmpresa,
            item.AtualizadoEmUtc ?? item.CriadoEmUtc);
    public static NotificacaoEmailVisualizacao Mapear(NotificacaoEmail item) => new(item.Id, item.OrdemServicoId,
        item.Status, item.DestinatarioEmailSnapshot, item.DestinatarioNomeSnapshot, item.OrigemTemplate,
        item.QuantidadeTentativas, item.CriadoEmUtc, item.EnviadaEmUtc, item.UltimoErroSeguro,
        item.Tentativas.OrderByDescending(x => x.Numero).ToArray());
    public static ComunicacaoClienteVisualizacao Mapear(ComunicacaoCliente item,
        string? solicitadoPorUsuarioNome = null) =>
        new(item.Id, item.OrdemServicoId, item.Canal, item.Tipo, item.Status,
            item.Origem, item.DestinatarioSnapshot, item.Mensagem,
            item.TemplateNomeSnapshot, solicitadoPorUsuarioNome, item.CriadoEmUtc,
            item.DataEnvioUtc, item.UltimoErroSeguro);

    public static async Task<IReadOnlyCollection<ComunicacaoClienteVisualizacao>>
        MapearComUsuariosAsync(IReadOnlyCollection<ComunicacaoCliente> itens,
            IPlataformaNotificacoesConsulta plataforma, Guid empresaId,
            CancellationToken ct)
    {
        var nomes = new Dictionary<Guid, string>();
        foreach (var usuarioId in itens.Where(x => x.SolicitadoPorUsuarioId.HasValue)
                     .Select(x => x.SolicitadoPorUsuarioId!.Value).Distinct())
        {
            var usuario = await plataforma.ObterUsuarioAsync(empresaId, usuarioId, ct);
            if (usuario is not null) nomes[usuarioId] = usuario.Nome;
        }
        return itens.Select(x => Mapear(x,
            x.SolicitadoPorUsuarioId.HasValue &&
            nomes.TryGetValue(x.SolicitadoPorUsuarioId.Value, out var nome)
                ? nome
                : x.Origem == OrigemComunicacaoCliente.Automatica
                    ? "Automático Detara"
                    : null)).ToArray();
    }
}
