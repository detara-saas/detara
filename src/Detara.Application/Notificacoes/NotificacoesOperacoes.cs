using System.Net.Mail;
using Detara.Application.Abstracoes;
using Detara.Application.Comunicacao;
using Detara.Domain.Atendimento;
using Detara.Domain.Notificacoes;
using FluentValidation;
using MediatR;

namespace Detara.Application.Notificacoes;

public sealed record ConfiguracaoNotificacaoVisualizacao(bool EnviarVeiculoProntoAutomaticamente,
    string? ResponderParaEmail, DateTime? AtualizadoEmUtc);
public sealed record TemplateEmailVisualizacao(string Assunto, string CorpoHtml, OrigemTemplateEmail Origem,
    DateTime? AtualizadoEmUtc);
public sealed record NotificacaoEmailVisualizacao(Guid Id, Guid OrdemServicoId, StatusNotificacaoEmail Status,
    string? DestinatarioEmail, string DestinatarioNome, OrigemTemplateEmail OrigemTemplate,
    int QuantidadeTentativas, DateTime CriadoEmUtc, DateTime? EnviadaEmUtc, string? UltimoErroSeguro,
    IReadOnlyCollection<TentativaNotificacaoEmail> Tentativas);
public sealed record NotificacaoOrdemServicoVisualizacao(bool EnviarVeiculoProntoAutomaticamente,
    string? EmailDestinoAtual, NotificacaoEmailVisualizacao? Notificacao);

public sealed record ObterConfiguracaoNotificacaoQuery : IRequest<ConfiguracaoNotificacaoVisualizacao>;
public sealed record AtualizarConfiguracaoNotificacaoCommand(bool EnviarVeiculoProntoAutomaticamente,
    string? ResponderParaEmail) : IRequest<ConfiguracaoNotificacaoVisualizacao>;
public sealed record ObterTemplateVeiculoProntoQuery : IRequest<TemplateEmailVisualizacao>;
public sealed record SalvarTemplateVeiculoProntoCommand(string Assunto, string CorpoHtml) : IRequest<TemplateEmailVisualizacao>;
public sealed record RestaurarTemplateVeiculoProntoCommand : IRequest<TemplateEmailVisualizacao>;
public sealed record VisualizarTemplateVeiculoProntoCommand(string Assunto, string CorpoHtml) : IRequest<EmailRenderizado>;
public sealed record EnviarTesteVeiculoProntoCommand : IRequest;
public sealed record ObterNotificacaoOrdemServicoQuery(Guid OrdemServicoId) : IRequest<NotificacaoOrdemServicoVisualizacao>;
public sealed record EnviarAvisoVeiculoProntoCommand(Guid OrdemServicoId) : IRequest<NotificacaoEmailVisualizacao>;
public sealed record TentarNovamenteNotificacaoCommand(Guid OrdemServicoId) : IRequest<NotificacaoEmailVisualizacao>;
public sealed record ReenviarAvisoVeiculoProntoCommand(Guid OrdemServicoId, Guid SolicitacaoId)
    : IRequest<NotificacaoEmailVisualizacao>;

internal sealed class ReenviarAvisoVeiculoProntoValidator : AbstractValidator<ReenviarAvisoVeiculoProntoCommand>
{
    public ReenviarAvisoVeiculoProntoValidator() =>
        RuleFor(x => x.SolicitacaoId).NotEmpty();
}

internal sealed class AtualizarConfiguracaoNotificacaoValidator : AbstractValidator<AtualizarConfiguracaoNotificacaoCommand>
{
    public AtualizarConfiguracaoNotificacaoValidator()
    {
        RuleFor(x => x.ResponderParaEmail).MaximumLength(200)
            .Must(NotificacoesFluxo.EmailOpcionalValido).WithMessage("Informe um e-mail de resposta válido.");
    }
}
internal sealed class SalvarTemplateVeiculoProntoValidator : AbstractValidator<SalvarTemplateVeiculoProntoCommand>
{
    public SalvarTemplateVeiculoProntoValidator()
    {
        RuleFor(x => x.Assunto).NotEmpty().MaximumLength(200).Must(x => x.IndexOfAny(['\r', '\n']) < 0)
            .WithMessage("O assunto não pode conter quebras de linha.");
        RuleFor(x => x.CorpoHtml).NotEmpty().Must(x => x.Length <= 50 * 1024)
            .WithMessage("O corpo do e-mail deve possuir no máximo 50 KB.");
    }
}
internal sealed class VisualizarTemplateVeiculoProntoValidator : AbstractValidator<VisualizarTemplateVeiculoProntoCommand>
{
    public VisualizarTemplateVeiculoProntoValidator()
    {
        RuleFor(x => x.Assunto).NotEmpty().MaximumLength(200).Must(x => x.IndexOfAny(['\r', '\n']) < 0);
        RuleFor(x => x.CorpoHtml).NotEmpty().Must(x => x.Length <= 50 * 1024);
    }
}

internal sealed class ObterConfiguracaoNotificacaoHandler(INotificacoesRepositorio repositorio)
    : IRequestHandler<ObterConfiguracaoNotificacaoQuery, ConfiguracaoNotificacaoVisualizacao>
{
    public async Task<ConfiguracaoNotificacaoVisualizacao> Handle(ObterConfiguracaoNotificacaoQuery request, CancellationToken ct)
    {
        var item = await repositorio.ObterConfiguracaoAsync(ct);
        return item is null ? new(false, null, null) : NotificacoesFluxo.Mapear(item);
    }
}

internal sealed class AtualizarConfiguracaoNotificacaoHandler(IUsuarioContexto usuario, INotificacoesRepositorio repositorio)
    : IRequestHandler<AtualizarConfiguracaoNotificacaoCommand, ConfiguracaoNotificacaoVisualizacao>
{
    public async Task<ConfiguracaoNotificacaoVisualizacao> Handle(AtualizarConfiguracaoNotificacaoCommand request, CancellationToken ct)
    {
        var item = await repositorio.ObterConfiguracaoAsync(ct);
        if (item is null)
        {
            item = new(usuario.EmpresaId, request.EnviarVeiculoProntoAutomaticamente, request.ResponderParaEmail, usuario.UsuarioId);
            repositorio.Adicionar(item);
        }
        else item.Atualizar(request.EnviarVeiculoProntoAutomaticamente, request.ResponderParaEmail, usuario.UsuarioId);
        await repositorio.SalvarAsync(ct);
        return NotificacoesFluxo.Mapear(item);
    }
}

internal sealed class ObterTemplateVeiculoProntoHandler(INotificacoesRepositorio repositorio, IRenderizadorTemplateEmail renderer)
    : IRequestHandler<ObterTemplateVeiculoProntoQuery, TemplateEmailVisualizacao>
{
    public async Task<TemplateEmailVisualizacao> Handle(ObterTemplateVeiculoProntoQuery request, CancellationToken ct) =>
        NotificacoesFluxo.Mapear(await repositorio.ObterTemplateAsync(TipoTemplateEmail.VeiculoProntoRetirada, false, ct), renderer);
}

internal sealed class SalvarTemplateVeiculoProntoHandler(IUsuarioContexto usuario, INotificacoesRepositorio repositorio,
    IRenderizadorTemplateEmail renderer) : IRequestHandler<SalvarTemplateVeiculoProntoCommand, TemplateEmailVisualizacao>
{
    public async Task<TemplateEmailVisualizacao> Handle(SalvarTemplateVeiculoProntoCommand request, CancellationToken ct)
    {
        renderer.ValidarTokens(request.Assunto, request.CorpoHtml);
        var sanitizado = renderer.SanitizarEValidarCorpo(request.CorpoHtml);
        var item = await repositorio.ObterTemplateAsync(TipoTemplateEmail.VeiculoProntoRetirada, true, ct);
        if (item is null)
        {
            item = new(usuario.EmpresaId, TipoTemplateEmail.VeiculoProntoRetirada, request.Assunto, sanitizado, usuario.UsuarioId);
            repositorio.Adicionar(item);
        }
        else item.Atualizar(request.Assunto, sanitizado, usuario.UsuarioId);
        await repositorio.SalvarAsync(ct);
        return new(item.Assunto, item.CorpoHtmlSanitizado, OrigemTemplateEmail.PersonalizadoEmpresa, item.AtualizadoEmUtc ?? item.CriadoEmUtc);
    }
}

internal sealed class RestaurarTemplateVeiculoProntoHandler(INotificacoesRepositorio repositorio, IRenderizadorTemplateEmail renderer)
    : IRequestHandler<RestaurarTemplateVeiculoProntoCommand, TemplateEmailVisualizacao>
{
    public async Task<TemplateEmailVisualizacao> Handle(RestaurarTemplateVeiculoProntoCommand request, CancellationToken ct)
    {
        var item = await repositorio.ObterTemplateAsync(TipoTemplateEmail.VeiculoProntoRetirada, true, ct);
        if (item is not null) { repositorio.Remover(item); await repositorio.SalvarAsync(ct); }
        return NotificacoesFluxo.Mapear(null, renderer);
    }
}

internal sealed class VisualizarTemplateVeiculoProntoHandler(IRenderizadorTemplateEmail renderer)
    : IRequestHandler<VisualizarTemplateVeiculoProntoCommand, EmailRenderizado>
{
    public Task<EmailRenderizado> Handle(VisualizarTemplateVeiculoProntoCommand request, CancellationToken ct)
    {
        renderer.ValidarTokens(request.Assunto, request.CorpoHtml);
        var corpo = renderer.SanitizarEValidarCorpo(request.CorpoHtml);
        return Task.FromResult(renderer.Renderizar(new(request.Assunto, corpo, OrigemTemplateEmail.PersonalizadoEmpresa),
            new("Estética Horizonte", "Marina Souza", "Honda Civic Touring", "ABC1D23", "OS-2026-0042")));
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
        var custom = await repositorio.ObterTemplateAsync(TipoTemplateEmail.VeiculoProntoRetirada, false, ct);
        var template = custom is null ? renderer.ObterPadraoVeiculoPronto() :
            new ConteudoTemplateEmail(custom.Assunto, custom.CorpoHtmlSanitizado, OrigemTemplateEmail.PersonalizadoEmpresa);
        var renderizado = renderer.Renderizar(template, new(empresa.Nome, destinatario.Nome,
            "Veículo de demonstração", "ABC1D23", "OS-TESTE"));
        var config = await repositorio.ObterConfiguracaoAsync(ct);
        var resultado = await provedor.EnviarAsync(new(destinatario.Email, renderizado.Assunto,
            renderizado.CorpoHtmlCompleto, config?.ResponderParaEmail, $"teste-template/{usuario.EmpresaId:N}/{Guid.NewGuid():N}"), ct);
        if (!resultado.Sucesso) throw new ConflitoRegraNegocioException(resultado.ErroSeguro ?? "O provedor não aceitou o e-mail de teste.");
    }
}

internal sealed class ObterNotificacaoOrdemServicoHandler(IUsuarioContexto usuario,
    INotificacoesRepositorio repositorio, IClientesNotificacoesConsulta clientes,
    IAtendimentoNotificacoesConsulta atendimento)
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
        return new(configuracao?.EnviarVeiculoProntoAutomaticamente == true,
            NotificacoesFluxo.NormalizarEmailValido(cliente?.Email),
            item is null ? null : NotificacoesFluxo.Mapear(item));
    }
}

internal sealed class EnviarAvisoVeiculoProntoHandler(IUsuarioContexto usuario,
    INotificacoesRepositorio repositorio, IClientesNotificacoesConsulta clientes,
    IPlataformaNotificacoesConsulta plataforma, IAtendimentoNotificacoesConsulta atendimento,
    IRenderizadorTemplateEmail renderer)
    : IRequestHandler<EnviarAvisoVeiculoProntoCommand, NotificacaoEmailVisualizacao>
{
    public async Task<NotificacaoEmailVisualizacao> Handle(
        EnviarAvisoVeiculoProntoCommand request, CancellationToken ct)
    {
        var ordem = await NotificacoesFluxo.ExigirOrdemAguardandoRetiradaAsync(
            atendimento, usuario.EmpresaId, request.OrdemServicoId, ct);
        var existente = await repositorio.ObterUltimaPorOrdemServicoAsync(request.OrdemServicoId, false, ct);
        if (existente is not null)
            throw new ConflitoRegraNegocioException(NotificacoesFluxo.MensagemAcaoPara(existente.Status));
        var item = await NotificacoesFluxo.CriarNotificacaoAsync(ordem.Id, ordem, usuario,
            repositorio, clientes, plataforma, renderer, ct);
        var criada = await repositorio.TentarAdicionarESalvarAsync(item, ct);
        if (!criada)
        {
            var concorrente = await repositorio.ObterUltimaPorOrdemServicoAsync(ordem.Id, false, ct)
                ?? throw new ConflitoRegraNegocioException("O envio já foi solicitado.");
            return NotificacoesFluxo.Mapear(concorrente);
        }
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
        if (!await repositorio.TentarSalvarAlteracaoAsync(ct))
        {
            var concorrente = await repositorio.ObterUltimaPorOrdemServicoAsync(request.OrdemServicoId, false, ct)
                ?? throw new ConflitoRegraNegocioException("A nova tentativa já foi solicitada.");
            return NotificacoesFluxo.Mapear(concorrente);
        }
        return NotificacoesFluxo.Mapear(item);
    }
}

internal sealed class ReenviarAvisoVeiculoProntoHandler(IUsuarioContexto usuario,
    INotificacoesRepositorio repositorio, IClientesNotificacoesConsulta clientes,
    IPlataformaNotificacoesConsulta plataforma, IAtendimentoNotificacoesConsulta atendimento,
    IRenderizadorTemplateEmail renderer)
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
        var ordem = await NotificacoesFluxo.ExigirOrdemAguardandoRetiradaAsync(
            atendimento, usuario.EmpresaId, request.OrdemServicoId, ct);
        var anterior = await repositorio.ObterUltimaPorOrdemServicoAsync(request.OrdemServicoId, false, ct)
            ?? throw new RecursoNaoEncontradoException("Notificação da ordem de serviço não encontrada.");
        if (anterior.Status != StatusNotificacaoEmail.Enviada)
            throw new ConflitoRegraNegocioException(NotificacoesFluxo.MensagemAcaoPara(anterior.Status));
        var item = await NotificacoesFluxo.CriarNotificacaoAsync(request.SolicitacaoId, ordem,
            usuario, repositorio, clientes, plataforma, renderer, ct);
        if (!await repositorio.TentarAdicionarESalvarAsync(item, ct))
        {
            var concorrente = await repositorio.ObterPorIdAsync(request.SolicitacaoId, ct);
            if (concorrente?.OrdemServicoId != request.OrdemServicoId)
                throw new ConflitoRegraNegocioException("O reenvio já foi solicitado.");
            return NotificacoesFluxo.Mapear(concorrente);
        }
        return NotificacoesFluxo.Mapear(item);
    }
}

public sealed class IntegracaoNotificacoesOrdensServico(INotificacoesRepositorio repositorio,
    IClientesNotificacoesConsulta clientes, IPlataformaNotificacoesConsulta plataforma,
    IRenderizadorTemplateEmail renderer) : IIntegracaoNotificacoesOrdensServico
{
    public async Task PrepararNotificacaoAsync(OrdemServicoFinalizadaNotificacoes evento, CancellationToken ct)
    {
        var config = await repositorio.ObterConfiguracaoAsync(ct);
        if (config?.EnviarVeiculoProntoAutomaticamente != true ||
            await repositorio.ExistePorOrdemServicoAsync(evento.OrdemServicoId, TipoTemplateEmail.VeiculoProntoRetirada, ct)) return;
        var item = await NotificacoesFluxo.CriarNotificacaoAsync(evento.OrdemServicoId,
            new(evento.OrdemServicoId, evento.OrdemServicoCodigo,
                StatusOrdemServico.AguardandoRetirada, evento.ClienteId, evento.ClienteNome,
                evento.VeiculoDescricao, evento.VeiculoPlaca), null, repositorio, clientes,
            plataforma, renderer, ct, evento.EmpresaId, config);
        repositorio.Adicionar(item);
    }
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
    public static async Task<NotificacaoEmail> CriarNotificacaoAsync(Guid notificacaoId,
        OrdemServicoNotificacoesInterna ordem, IUsuarioContexto? usuario,
        INotificacoesRepositorio repositorio, IClientesNotificacoesConsulta clientes,
        IPlataformaNotificacoesConsulta plataforma, IRenderizadorTemplateEmail renderer,
        CancellationToken ct, Guid? empresaId = null,
        ConfiguracaoNotificacaoEmpresa? configuracao = null)
    {
        var tenantId = empresaId ?? usuario?.EmpresaId
            ?? throw new InvalidOperationException("A empresa da notificação não foi informada.");
        var cliente = await clientes.ObterClienteAsync(tenantId, ordem.ClienteId, ct);
        var email = usuario is null
            ? NormalizarEmailValido(cliente?.Email)
            : ExigirEmailCliente(cliente);
        var empresa = await plataforma.ObterEmpresaAsync(tenantId, ct)
            ?? throw new RecursoNaoEncontradoException("Empresa não encontrada.");
        configuracao ??= await repositorio.ObterConfiguracaoAsync(ct);
        var custom = await repositorio.ObterTemplateAsync(
            TipoTemplateEmail.VeiculoProntoRetirada, false, ct);
        var template = custom is null ? renderer.ObterPadraoVeiculoPronto() :
            new ConteudoTemplateEmail(custom.Assunto, custom.CorpoHtmlSanitizado,
                OrigemTemplateEmail.PersonalizadoEmpresa);
        var renderizado = renderer.Renderizar(template, new(empresa.Nome, ordem.ClienteNome,
            ordem.VeiculoDescricao, ordem.VeiculoPlaca, ordem.Codigo));
        return new(notificacaoId, tenantId, ordem.Id, ordem.ClienteId,
            TipoTemplateEmail.VeiculoProntoRetirada, email, ordem.ClienteNome,
            renderizado.Assunto, renderizado.CorpoHtmlCompleto, template.Origem,
            configuracao?.ResponderParaEmail,
            usuario is null ? TipoTentativaNotificacaoEmail.Automatica : TipoTentativaNotificacaoEmail.Manual,
            usuario?.UsuarioId);
    }
    public static string MensagemAcaoPara(StatusNotificacaoEmail status) => status switch
    {
        StatusNotificacaoEmail.Pendente => "Já existe um envio agendado para esta ordem de serviço.",
        StatusNotificacaoEmail.Processando => "O aviso já está sendo enviado.",
        StatusNotificacaoEmail.Enviada => "O aviso já foi enviado. Use a ação de reenvio para criar uma nova comunicação.",
        _ => "A notificação existente deve ser tentada novamente antes de criar outro envio."
    };
    public static ConfiguracaoNotificacaoVisualizacao Mapear(ConfiguracaoNotificacaoEmpresa item) =>
        new(item.EnviarVeiculoProntoAutomaticamente, item.ResponderParaEmail, item.AtualizadoEmUtc ?? item.CriadoEmUtc);
    public static TemplateEmailVisualizacao Mapear(TemplateEmailEmpresa? item, IRenderizadorTemplateEmail renderer)
    {
        if (item is not null) return new(item.Assunto, item.CorpoHtmlSanitizado,
            OrigemTemplateEmail.PersonalizadoEmpresa, item.AtualizadoEmUtc ?? item.CriadoEmUtc);
        var padrao = renderer.ObterPadraoVeiculoPronto();
        return new(padrao.Assunto, padrao.CorpoHtml, padrao.Origem, null);
    }
    public static NotificacaoEmailVisualizacao Mapear(NotificacaoEmail item) => new(item.Id, item.OrdemServicoId,
        item.Status, item.DestinatarioEmailSnapshot, item.DestinatarioNomeSnapshot, item.OrigemTemplate,
        item.QuantidadeTentativas, item.CriadoEmUtc, item.EnviadaEmUtc, item.UltimoErroSeguro,
        item.Tentativas.OrderByDescending(x => x.Numero).ToArray());
}
