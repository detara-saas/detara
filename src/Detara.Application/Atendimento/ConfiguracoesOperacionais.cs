using Detara.Application.Abstracoes;
using Detara.Domain.Atendimento;
using FluentValidation;
using MediatR;

namespace Detara.Application.Atendimento;

public sealed record ChecklistModeloItemVisualizacao(
    Guid Id,
    string Descricao,
    int Ordem);

public sealed record ChecklistModeloVisualizacao(
    Guid? Id,
    string Nome,
    string? Descricao,
    IReadOnlyCollection<ChecklistModeloItemVisualizacao> Itens,
    DateTime? CriadoEmUtc,
    DateTime? AtualizadoEmUtc);

public sealed record ConfiguracaoOperacionalVisualizacao(
    Guid? Id,
    NivelExigenciaOperacional ChecklistEntrada,
    NivelExigenciaOperacional FotosEntrada,
    NivelExigenciaOperacional FotosSaida,
    DateTime? CriadoEmUtc,
    DateTime? AtualizadoEmUtc,
    ChecklistModeloVisualizacao Checklist);

public sealed record ObterConfiguracaoOperacionalQuery
    : IRequest<ConfiguracaoOperacionalVisualizacao>;

public sealed record AtualizarConfiguracaoOperacionalCommand(
    NivelExigenciaOperacional ChecklistEntrada,
    NivelExigenciaOperacional FotosEntrada,
    NivelExigenciaOperacional FotosSaida)
    : IRequest<ConfiguracaoOperacionalVisualizacao>;

public sealed record AtualizarChecklistModeloCommand(
    string Nome,
    string? Descricao,
    IReadOnlyCollection<string> Itens)
    : IRequest<ConfiguracaoOperacionalVisualizacao>;

internal sealed class AtualizarConfiguracaoOperacionalValidator
    : AbstractValidator<AtualizarConfiguracaoOperacionalCommand>
{
    public AtualizarConfiguracaoOperacionalValidator()
    {
        RuleFor(item => item.ChecklistEntrada).IsInEnum();
        RuleFor(item => item.FotosEntrada).IsInEnum();
        RuleFor(item => item.FotosSaida).IsInEnum();
    }
}

internal sealed class AtualizarChecklistModeloValidator
    : AbstractValidator<AtualizarChecklistModeloCommand>
{
    public AtualizarChecklistModeloValidator()
    {
        RuleFor(item => item.Nome).NotEmpty().MaximumLength(120);
        RuleFor(item => item.Descricao).MaximumLength(500);
        RuleFor(item => item.Itens).NotNull().Must(itens => itens.Count <= ChecklistModelo.LimiteItens)
            .WithMessage($"O checklist pode possuir no máximo {ChecklistModelo.LimiteItens} itens.");
        RuleForEach(item => item.Itens).NotEmpty().MaximumLength(ChecklistModelo.LimiteDescricaoItem);
    }
}

internal sealed class ObterConfiguracaoOperacionalHandler(
    IConfiguracoesOperacionaisRepositorio repositorio)
    : IRequestHandler<ObterConfiguracaoOperacionalQuery, ConfiguracaoOperacionalVisualizacao>
{
    public async Task<ConfiguracaoOperacionalVisualizacao> Handle(
        ObterConfiguracaoOperacionalQuery request,
        CancellationToken cancellationToken) =>
        await ConfiguracaoOperacionalFluxo.ObterVisualizacaoAsync(repositorio, cancellationToken);
}

internal sealed class AtualizarConfiguracaoOperacionalHandler(
    IUsuarioContexto usuarioContexto,
    IConfiguracoesOperacionaisRepositorio repositorio)
    : IRequestHandler<AtualizarConfiguracaoOperacionalCommand, ConfiguracaoOperacionalVisualizacao>
{
    public async Task<ConfiguracaoOperacionalVisualizacao> Handle(
        AtualizarConfiguracaoOperacionalCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ChecklistEntrada != NivelExigenciaOperacional.Desabilitado)
        {
            var checklist = await repositorio.ObterChecklistAsync(false, cancellationToken);
            if (checklist is null || checklist.Itens.Count == 0)
            {
                throw new ConflitoRegraNegocioException(
                    "Cadastre ao menos um item válido antes de habilitar o checklist de entrada.");
            }
        }

        var configuracao = await repositorio.ObterConfiguracaoAsync(true, cancellationToken);
        if (configuracao is null)
        {
            configuracao = new ConfiguracaoOperacionalAtendimento(
                usuarioContexto.EmpresaId,
                request.ChecklistEntrada,
                request.FotosEntrada,
                request.FotosSaida);
            repositorio.Adicionar(configuracao);
        }
        else
        {
            configuracao.Atualizar(
                request.ChecklistEntrada,
                request.FotosEntrada,
                request.FotosSaida);
        }

        await repositorio.SalvarAsync(cancellationToken);
        return await ConfiguracaoOperacionalFluxo.ObterVisualizacaoAsync(repositorio, cancellationToken);
    }
}

internal sealed class AtualizarChecklistModeloHandler(
    IUsuarioContexto usuarioContexto,
    IConfiguracoesOperacionaisRepositorio repositorio)
    : IRequestHandler<AtualizarChecklistModeloCommand, ConfiguracaoOperacionalVisualizacao>
{
    public async Task<ConfiguracaoOperacionalVisualizacao> Handle(
        AtualizarChecklistModeloCommand request,
        CancellationToken cancellationToken)
    {
        var configuracao = await repositorio.ObterConfiguracaoAsync(false, cancellationToken);
        if (configuracao is { ChecklistEntrada: not NivelExigenciaOperacional.Desabilitado } &&
            request.Itens.Count == 0)
        {
            throw new ConflitoRegraNegocioException(
                "O checklist habilitado deve possuir ao menos um item válido.");
        }

        var checklist = await repositorio.ObterChecklistAsync(true, cancellationToken);
        if (checklist is null)
        {
            checklist = new ChecklistModelo(
                usuarioContexto.EmpresaId,
                request.Nome,
                request.Descricao,
                request.Itens);
            repositorio.Adicionar(checklist);
        }
        else
        {
            repositorio.RemoverItensAtuais(checklist);
            checklist.Atualizar(request.Nome, request.Descricao, request.Itens);
            repositorio.AdicionarItensAtuais(checklist);
        }

        await repositorio.SalvarAsync(cancellationToken);
        return await ConfiguracaoOperacionalFluxo.ObterVisualizacaoAsync(repositorio, cancellationToken);
    }
}

internal static class ConfiguracaoOperacionalFluxo
{
    public static async Task<ConfiguracaoOperacionalVisualizacao> ObterVisualizacaoAsync(
        IConfiguracoesOperacionaisRepositorio repositorio,
        CancellationToken cancellationToken)
    {
        var configuracao = await repositorio.ObterConfiguracaoAsync(false, cancellationToken);
        var checklist = await repositorio.ObterChecklistAsync(false, cancellationToken);
        return new ConfiguracaoOperacionalVisualizacao(
            configuracao?.Id,
            configuracao?.ChecklistEntrada ?? NivelExigenciaOperacional.Desabilitado,
            configuracao?.FotosEntrada ?? NivelExigenciaOperacional.Desabilitado,
            configuracao?.FotosSaida ?? NivelExigenciaOperacional.Desabilitado,
            configuracao?.CriadoEmUtc,
            configuracao?.AtualizadoEmUtc,
            checklist is null
                ? new ChecklistModeloVisualizacao(
                    null,
                    ChecklistModelo.NomePadrao,
                    null,
                    [],
                    null,
                    null)
                : new ChecklistModeloVisualizacao(
                    checklist.Id,
                    checklist.Nome,
                    checklist.Descricao,
                    checklist.Itens
                        .OrderBy(item => item.Ordem)
                        .Select(item => new ChecklistModeloItemVisualizacao(
                            item.Id,
                            item.Descricao,
                            item.Ordem))
                        .ToArray(),
                    checklist.CriadoEmUtc,
                    checklist.AtualizadoEmUtc));
    }
}
