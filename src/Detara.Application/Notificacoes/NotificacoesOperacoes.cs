using System.Net.Mail;
using Detara.Application.Abstracoes;
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

public sealed record ObterConfiguracaoNotificacaoQuery : IRequest<ConfiguracaoNotificacaoVisualizacao>;
public sealed record AtualizarConfiguracaoNotificacaoCommand(bool EnviarVeiculoProntoAutomaticamente,
    string? ResponderParaEmail) : IRequest<ConfiguracaoNotificacaoVisualizacao>;
public sealed record ObterTemplateVeiculoProntoQuery : IRequest<TemplateEmailVisualizacao>;
public sealed record SalvarTemplateVeiculoProntoCommand(string Assunto, string CorpoHtml) : IRequest<TemplateEmailVisualizacao>;
public sealed record RestaurarTemplateVeiculoProntoCommand : IRequest<TemplateEmailVisualizacao>;
public sealed record VisualizarTemplateVeiculoProntoCommand(string Assunto, string CorpoHtml) : IRequest<EmailRenderizado>;
public sealed record EnviarTesteVeiculoProntoCommand : IRequest;
public sealed record ObterNotificacaoOrdemServicoQuery(Guid OrdemServicoId) : IRequest<NotificacaoEmailVisualizacao?>;
public sealed record ReenviarNotificacaoCommand(Guid OrdemServicoId) : IRequest<NotificacaoEmailVisualizacao>;

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

internal sealed class ObterNotificacaoOrdemServicoHandler(INotificacoesRepositorio repositorio)
    : IRequestHandler<ObterNotificacaoOrdemServicoQuery, NotificacaoEmailVisualizacao?>
{
    public async Task<NotificacaoEmailVisualizacao?> Handle(ObterNotificacaoOrdemServicoQuery request, CancellationToken ct)
    {
        var item = await repositorio.ObterPorOrdemServicoAsync(request.OrdemServicoId, false, ct);
        return item is null ? null : NotificacoesFluxo.Mapear(item);
    }
}

internal sealed class ReenviarNotificacaoHandler(IUsuarioContexto usuario, INotificacoesRepositorio repositorio,
    IClientesNotificacoesConsulta clientes) : IRequestHandler<ReenviarNotificacaoCommand, NotificacaoEmailVisualizacao>
{
    public async Task<NotificacaoEmailVisualizacao> Handle(ReenviarNotificacaoCommand request, CancellationToken ct)
    {
        var item = await repositorio.ObterPorOrdemServicoAsync(request.OrdemServicoId, true, ct)
            ?? throw new RecursoNaoEncontradoException("Notificação da ordem de serviço não encontrada.");
        string? email = item.DestinatarioEmailSnapshot;
        if (item.Status == StatusNotificacaoEmail.SemDestinatario)
            email = (await clientes.ObterClienteAsync(usuario.EmpresaId, item.ClienteId, ct))?.Email;
        try { item.PrepararReenvioManual(email, usuario.UsuarioId, DateTime.UtcNow); }
        catch (InvalidOperationException ex) { throw new ConflitoRegraNegocioException(ex.Message); }
        await repositorio.SalvarAsync(ct);
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
        var cliente = await clientes.ObterClienteAsync(evento.EmpresaId, evento.ClienteId, ct);
        var empresa = await plataforma.ObterEmpresaAsync(evento.EmpresaId, ct)
            ?? throw new RecursoNaoEncontradoException("Empresa não encontrada.");
        var custom = await repositorio.ObterTemplateAsync(TipoTemplateEmail.VeiculoProntoRetirada, false, ct);
        var template = custom is null ? renderer.ObterPadraoVeiculoPronto() :
            new ConteudoTemplateEmail(custom.Assunto, custom.CorpoHtmlSanitizado, OrigemTemplateEmail.PersonalizadoEmpresa);
        var renderizado = renderer.Renderizar(template, new(empresa.Nome, evento.ClienteNome,
            evento.VeiculoDescricao, evento.VeiculoPlaca, evento.OrdemServicoCodigo));
        repositorio.Adicionar(new NotificacaoEmail(evento.EmpresaId, evento.OrdemServicoId, evento.ClienteId,
            TipoTemplateEmail.VeiculoProntoRetirada, cliente?.Email, evento.ClienteNome, renderizado.Assunto,
            renderizado.CorpoHtmlCompleto, template.Origem, config.ResponderParaEmail));
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
