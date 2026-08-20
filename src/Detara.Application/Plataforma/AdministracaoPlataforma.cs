using FluentValidation;
using MediatR;

namespace Detara.Application.Plataforma;

public sealed record ObterDashboardPlataformaQuery : IRequest<DashboardPlataformaResultado>;
public sealed record ListarEmpresasPlataformaQuery(
    int Pagina,
    int TamanhoPagina,
    string? Pesquisa,
    bool? Ativa) : IRequest<PaginaPlataforma<EmpresaPlataformaResumo>>;
public sealed record ObterEmpresaPlataformaQuery(Guid Id) : IRequest<EmpresaPlataformaDetalhe>;
public sealed record ProvisionarEmpresaCommand(
    string NomeFantasia,
    string RazaoSocial,
    string CpfCnpj,
    string? EmailContato,
    string? Telefone,
    string FusoHorario,
    string AdministradorNome,
    string AdministradorEmail,
    string? TraceId) : IRequest<EmpresaPlataformaDetalhe>;
public sealed record SuspenderEmpresaPlataformaCommand(Guid EmpresaId, string Motivo, string? TraceId) : IRequest;
public sealed record ReativarEmpresaPlataformaCommand(Guid EmpresaId, string Motivo, string? TraceId) : IRequest;
public sealed record ReenviarConviteAdministradorEmpresaCommand(Guid EmpresaId, string? TraceId) : IRequest;
public sealed record ListarAuditoriaPlataformaQuery(
    int Pagina,
    int TamanhoPagina,
    DateTime? InicioUtc,
    DateTime? FimUtc,
    string? Tipo,
    Guid? EmpresaId) : IRequest<PaginaPlataforma<AuditoriaPlataformaItemResultado>>;
public sealed record ValidarConviteAdministradorEmpresaQuery(string Token)
    : IRequest<ConviteAdministradorValidadoResultado>;
public sealed record AceitarConviteAdministradorEmpresaCommand(
    string Token,
    string Senha,
    string? TraceId) : IRequest;

internal sealed class ObterDashboardPlataformaHandler(IAdministracaoPlataformaServico servico)
    : IRequestHandler<ObterDashboardPlataformaQuery, DashboardPlataformaResultado>
{
    public Task<DashboardPlataformaResultado> Handle(
        ObterDashboardPlataformaQuery request,
        CancellationToken cancellationToken) => servico.ObterDashboardAsync(cancellationToken);
}

internal sealed class ListarEmpresasPlataformaHandler(IAdministracaoPlataformaServico servico)
    : IRequestHandler<ListarEmpresasPlataformaQuery, PaginaPlataforma<EmpresaPlataformaResumo>>
{
    public Task<PaginaPlataforma<EmpresaPlataformaResumo>> Handle(
        ListarEmpresasPlataformaQuery request,
        CancellationToken cancellationToken) => servico.ListarEmpresasAsync(
            request.Pagina,
            request.TamanhoPagina,
            request.Pesquisa,
            request.Ativa,
            cancellationToken);
}

internal sealed class ObterEmpresaPlataformaHandler(IAdministracaoPlataformaServico servico)
    : IRequestHandler<ObterEmpresaPlataformaQuery, EmpresaPlataformaDetalhe>
{
    public Task<EmpresaPlataformaDetalhe> Handle(
        ObterEmpresaPlataformaQuery request,
        CancellationToken cancellationToken) => servico.ObterEmpresaAsync(request.Id, cancellationToken);
}

internal sealed class ProvisionarEmpresaHandler(
    IAdministracaoPlataformaServico servico,
    IContextoAdministradorPlataforma contexto)
    : IRequestHandler<ProvisionarEmpresaCommand, EmpresaPlataformaDetalhe>
{
    public Task<EmpresaPlataformaDetalhe> Handle(
        ProvisionarEmpresaCommand request,
        CancellationToken cancellationToken) => servico.ProvisionarEmpresaAsync(
            contexto.AdministradorPlataformaId,
            new ProvisionarEmpresaEntrada(
                request.NomeFantasia,
                request.RazaoSocial,
                request.CpfCnpj,
                request.EmailContato,
                request.Telefone,
                request.FusoHorario,
                request.AdministradorNome,
                request.AdministradorEmail),
            request.TraceId,
            cancellationToken);
}

internal sealed class SuspenderEmpresaPlataformaHandler(
    IAdministracaoPlataformaServico servico,
    IContextoAdministradorPlataforma contexto)
    : IRequestHandler<SuspenderEmpresaPlataformaCommand>
{
    public async Task Handle(SuspenderEmpresaPlataformaCommand request, CancellationToken cancellationToken) =>
        await servico.SuspenderEmpresaAsync(
            contexto.AdministradorPlataformaId,
            request.EmpresaId,
            request.Motivo,
            request.TraceId,
            cancellationToken);
}

internal sealed class ReativarEmpresaPlataformaHandler(
    IAdministracaoPlataformaServico servico,
    IContextoAdministradorPlataforma contexto)
    : IRequestHandler<ReativarEmpresaPlataformaCommand>
{
    public async Task Handle(ReativarEmpresaPlataformaCommand request, CancellationToken cancellationToken) =>
        await servico.ReativarEmpresaAsync(
            contexto.AdministradorPlataformaId,
            request.EmpresaId,
            request.Motivo,
            request.TraceId,
            cancellationToken);
}

internal sealed class ReenviarConviteAdministradorEmpresaHandler(
    IAdministracaoPlataformaServico servico,
    IContextoAdministradorPlataforma contexto)
    : IRequestHandler<ReenviarConviteAdministradorEmpresaCommand>
{
    public async Task Handle(
        ReenviarConviteAdministradorEmpresaCommand request,
        CancellationToken cancellationToken) => await servico.ReenviarConviteAsync(
            contexto.AdministradorPlataformaId,
            request.EmpresaId,
            request.TraceId,
            cancellationToken);
}

internal sealed class ListarAuditoriaPlataformaHandler(IAdministracaoPlataformaServico servico)
    : IRequestHandler<ListarAuditoriaPlataformaQuery, PaginaPlataforma<AuditoriaPlataformaItemResultado>>
{
    public Task<PaginaPlataforma<AuditoriaPlataformaItemResultado>> Handle(
        ListarAuditoriaPlataformaQuery request,
        CancellationToken cancellationToken) => servico.ListarAuditoriaAsync(
            request.Pagina,
            request.TamanhoPagina,
            request.InicioUtc,
            request.FimUtc,
            request.Tipo,
            request.EmpresaId,
            cancellationToken);
}

internal sealed class ValidarConviteAdministradorEmpresaHandler(IConvitesAdministradoresEmpresaServico servico)
    : IRequestHandler<ValidarConviteAdministradorEmpresaQuery, ConviteAdministradorValidadoResultado>
{
    public Task<ConviteAdministradorValidadoResultado> Handle(
        ValidarConviteAdministradorEmpresaQuery request,
        CancellationToken cancellationToken) => servico.ValidarAsync(request.Token, cancellationToken);
}

internal sealed class AceitarConviteAdministradorEmpresaHandler(IConvitesAdministradoresEmpresaServico servico)
    : IRequestHandler<AceitarConviteAdministradorEmpresaCommand>
{
    public async Task Handle(
        AceitarConviteAdministradorEmpresaCommand request,
        CancellationToken cancellationToken) => await servico.AceitarAsync(
            request.Token,
            request.Senha,
            request.TraceId,
            cancellationToken);
}

internal sealed class ListarEmpresasPlataformaValidator : AbstractValidator<ListarEmpresasPlataformaQuery>
{
    public ListarEmpresasPlataformaValidator()
    {
        RuleFor(x => x.Pagina).GreaterThan(0);
        RuleFor(x => x.TamanhoPagina).Must(x => x is 10 or 25 or 50);
        RuleFor(x => x.Pesquisa).MaximumLength(160);
    }
}

internal sealed class ObterEmpresaPlataformaValidator : AbstractValidator<ObterEmpresaPlataformaQuery>
{
    public ObterEmpresaPlataformaValidator() => RuleFor(x => x.Id).NotEmpty();
}

internal sealed class ProvisionarEmpresaValidator : AbstractValidator<ProvisionarEmpresaCommand>
{
    public ProvisionarEmpresaValidator()
    {
        RuleFor(x => x.NomeFantasia).NotEmpty().MaximumLength(160);
        RuleFor(x => x.RazaoSocial).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CpfCnpj).NotEmpty().Matches("^[0-9.\\/\\-]{11,20}$");
        RuleFor(x => x.EmailContato).MaximumLength(200).EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.EmailContato));
        RuleFor(x => x.Telefone).MaximumLength(30);
        RuleFor(x => x.FusoHorario).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AdministradorNome).NotEmpty().MaximumLength(160);
        RuleFor(x => x.AdministradorEmail).NotEmpty().MaximumLength(200).EmailAddress();
    }
}

internal sealed class SuspenderEmpresaPlataformaValidator : AbstractValidator<SuspenderEmpresaPlataformaCommand>
{
    public SuspenderEmpresaPlataformaValidator()
    {
        RuleFor(x => x.EmpresaId).NotEmpty();
        RuleFor(x => x.Motivo).NotEmpty().MinimumLength(5).MaximumLength(500);
    }
}

internal sealed class ReativarEmpresaPlataformaValidator : AbstractValidator<ReativarEmpresaPlataformaCommand>
{
    public ReativarEmpresaPlataformaValidator()
    {
        RuleFor(x => x.EmpresaId).NotEmpty();
        RuleFor(x => x.Motivo).NotEmpty().MinimumLength(5).MaximumLength(500);
    }
}

internal sealed class ReenviarConviteAdministradorEmpresaValidator
    : AbstractValidator<ReenviarConviteAdministradorEmpresaCommand>
{
    public ReenviarConviteAdministradorEmpresaValidator() => RuleFor(x => x.EmpresaId).NotEmpty();
}

internal sealed class ListarAuditoriaPlataformaValidator : AbstractValidator<ListarAuditoriaPlataformaQuery>
{
    public ListarAuditoriaPlataformaValidator()
    {
        RuleFor(x => x.Pagina).GreaterThan(0);
        RuleFor(x => x.TamanhoPagina).Must(x => x is 10 or 25 or 50);
        RuleFor(x => x.Tipo).MaximumLength(120);
        RuleFor(x => x).Must(x =>
                x.InicioUtc is null || x.FimUtc is null || x.InicioUtc <= x.FimUtc)
            .WithMessage("O período informado é inválido.");
        RuleFor(x => x).Must(x =>
                x.InicioUtc is null || x.FimUtc is null || x.FimUtc - x.InicioUtc <= TimeSpan.FromDays(366))
            .WithMessage("O período máximo de auditoria é de 366 dias.");
    }
}

internal sealed class ValidarConviteAdministradorEmpresaValidator
    : AbstractValidator<ValidarConviteAdministradorEmpresaQuery>
{
    public ValidarConviteAdministradorEmpresaValidator() =>
        RuleFor(x => x.Token).NotEmpty().MaximumLength(500);
}

internal sealed class AceitarConviteAdministradorEmpresaValidator
    : AbstractValidator<AceitarConviteAdministradorEmpresaCommand>
{
    public AceitarConviteAdministradorEmpresaValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Senha).NotEmpty().MinimumLength(10).MaximumLength(256);
    }
}
