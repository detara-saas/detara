using FluentValidation;
using MediatR;
using System.Linq.Expressions;

namespace Detara.Application.AdministracaoTenant;

public sealed record ObterEmpresaTenantQuery : IRequest<EmpresaTenantResultado>;
public sealed record AtualizarEmpresaTenantCommand(
    string NomeFantasia,
    string RazaoSocial,
    string CpfCnpj,
    string? Email,
    string? Telefone,
    string FusoHorario,
    long Versao) : IRequest<EmpresaTenantResultado>;

public sealed record ListarUsuariosTenantQuery(
    int Pagina = 1,
    int TamanhoPagina = 25,
    string? Pesquisa = null,
    string? Status = null) : IRequest<PaginaTenant<UsuarioTenantResultado>>;
public sealed record ObterUsuarioTenantQuery(Guid Id) : IRequest<UsuarioTenantResultado>;
public sealed record ConvidarUsuarioTenantCommand(string Nome, string Email, Guid PerfilId)
    : IRequest<UsuarioTenantResultado>;
public sealed record AlterarPerfilUsuarioTenantCommand(Guid Id, Guid PerfilId, long Versao)
    : IRequest<UsuarioTenantResultado>;
public sealed record AlterarStatusUsuarioTenantCommand(Guid Id, bool Ativar, long Versao)
    : IRequest<UsuarioTenantResultado>;
public sealed record ReenviarConviteUsuarioTenantCommand(Guid Id) : IRequest<UsuarioTenantResultado>;

public sealed record ListarPerfisTenantQuery : IRequest<IReadOnlyCollection<PerfilTenantResumoResultado>>;
public sealed record ObterPerfilTenantQuery(Guid Id) : IRequest<PerfilTenantDetalheResultado>;
public sealed record ListarPermissoesTenantQuery : IRequest<IReadOnlyCollection<PermissaoTenantResultado>>;
public sealed record CriarPerfilTenantCommand(
    string Nome,
    string? Descricao,
    IReadOnlyCollection<string> Permissoes) : IRequest<PerfilTenantDetalheResultado>;
public sealed record AtualizarPerfilTenantCommand(
    Guid Id,
    string Nome,
    string? Descricao,
    IReadOnlyCollection<string> Permissoes,
    long Versao) : IRequest<PerfilTenantDetalheResultado>;
public sealed record AlterarStatusPerfilTenantCommand(Guid Id, bool Ativar, long Versao)
    : IRequest<PerfilTenantDetalheResultado>;

public sealed record ObterMinhaContaQuery : IRequest<MinhaContaResultado>;
public sealed record AtualizarNomeMinhaContaCommand(string Nome, long Versao) : IRequest<MinhaContaResultado>;
public sealed record AtualizarEmailMinhaContaCommand(string NovoEmail, string SenhaAtual, long Versao) : IRequest;
public sealed record AlterarSenhaMinhaContaCommand(
    string SenhaAtual,
    string NovaSenha,
    string ConfirmacaoNovaSenha,
    long Versao) : IRequest;

internal sealed class EmpresaTenantHandler(IAdministracaoEmpresaTenantServico servico) :
    IRequestHandler<ObterEmpresaTenantQuery, EmpresaTenantResultado>,
    IRequestHandler<AtualizarEmpresaTenantCommand, EmpresaTenantResultado>
{
    public Task<EmpresaTenantResultado> Handle(ObterEmpresaTenantQuery request, CancellationToken ct) =>
        servico.ObterAsync(ct);

    public Task<EmpresaTenantResultado> Handle(AtualizarEmpresaTenantCommand request, CancellationToken ct) =>
        servico.AtualizarAsync(request.NomeFantasia, request.RazaoSocial, request.CpfCnpj,
            request.Email, request.Telefone, request.FusoHorario, request.Versao, ct);
}

internal sealed class UsuariosTenantHandler(IAdministracaoUsuariosTenantServico servico) :
    IRequestHandler<ListarUsuariosTenantQuery, PaginaTenant<UsuarioTenantResultado>>,
    IRequestHandler<ObterUsuarioTenantQuery, UsuarioTenantResultado>,
    IRequestHandler<ConvidarUsuarioTenantCommand, UsuarioTenantResultado>,
    IRequestHandler<AlterarPerfilUsuarioTenantCommand, UsuarioTenantResultado>,
    IRequestHandler<AlterarStatusUsuarioTenantCommand, UsuarioTenantResultado>,
    IRequestHandler<ReenviarConviteUsuarioTenantCommand, UsuarioTenantResultado>
{
    public Task<PaginaTenant<UsuarioTenantResultado>> Handle(ListarUsuariosTenantQuery request, CancellationToken ct) =>
        servico.ListarAsync(request.Pagina, request.TamanhoPagina, request.Pesquisa, request.Status, ct);
    public Task<UsuarioTenantResultado> Handle(ObterUsuarioTenantQuery request, CancellationToken ct) =>
        servico.ObterAsync(request.Id, ct);
    public Task<UsuarioTenantResultado> Handle(ConvidarUsuarioTenantCommand request, CancellationToken ct) =>
        servico.ConvidarAsync(request.Nome, request.Email, request.PerfilId, ct);
    public Task<UsuarioTenantResultado> Handle(AlterarPerfilUsuarioTenantCommand request, CancellationToken ct) =>
        servico.AlterarPerfilAsync(request.Id, request.PerfilId, request.Versao, ct);
    public Task<UsuarioTenantResultado> Handle(AlterarStatusUsuarioTenantCommand request, CancellationToken ct) =>
        servico.AlterarStatusAsync(request.Id, request.Ativar, request.Versao, ct);
    public Task<UsuarioTenantResultado> Handle(ReenviarConviteUsuarioTenantCommand request, CancellationToken ct) =>
        servico.ReenviarConviteAsync(request.Id, ct);
}

internal sealed class PerfisTenantHandler(IAdministracaoPerfisTenantServico servico) :
    IRequestHandler<ListarPerfisTenantQuery, IReadOnlyCollection<PerfilTenantResumoResultado>>,
    IRequestHandler<ObterPerfilTenantQuery, PerfilTenantDetalheResultado>,
    IRequestHandler<ListarPermissoesTenantQuery, IReadOnlyCollection<PermissaoTenantResultado>>,
    IRequestHandler<CriarPerfilTenantCommand, PerfilTenantDetalheResultado>,
    IRequestHandler<AtualizarPerfilTenantCommand, PerfilTenantDetalheResultado>,
    IRequestHandler<AlterarStatusPerfilTenantCommand, PerfilTenantDetalheResultado>
{
    public Task<IReadOnlyCollection<PerfilTenantResumoResultado>> Handle(ListarPerfisTenantQuery request, CancellationToken ct) => servico.ListarAsync(ct);
    public Task<PerfilTenantDetalheResultado> Handle(ObterPerfilTenantQuery request, CancellationToken ct) => servico.ObterAsync(request.Id, ct);
    public Task<IReadOnlyCollection<PermissaoTenantResultado>> Handle(ListarPermissoesTenantQuery request, CancellationToken ct) => servico.ListarPermissoesAsync(ct);
    public Task<PerfilTenantDetalheResultado> Handle(CriarPerfilTenantCommand request, CancellationToken ct) => servico.CriarAsync(request.Nome, request.Descricao, request.Permissoes, ct);
    public Task<PerfilTenantDetalheResultado> Handle(AtualizarPerfilTenantCommand request, CancellationToken ct) => servico.AtualizarAsync(request.Id, request.Nome, request.Descricao, request.Permissoes, request.Versao, ct);
    public Task<PerfilTenantDetalheResultado> Handle(AlterarStatusPerfilTenantCommand request, CancellationToken ct) => servico.AlterarStatusAsync(request.Id, request.Ativar, request.Versao, ct);
}

internal sealed class MinhaContaTenantHandler(IMinhaContaTenantServico servico) :
    IRequestHandler<ObterMinhaContaQuery, MinhaContaResultado>,
    IRequestHandler<AtualizarNomeMinhaContaCommand, MinhaContaResultado>,
    IRequestHandler<AtualizarEmailMinhaContaCommand>,
    IRequestHandler<AlterarSenhaMinhaContaCommand>
{
    public Task<MinhaContaResultado> Handle(ObterMinhaContaQuery request, CancellationToken ct) => servico.ObterAsync(ct);
    public Task<MinhaContaResultado> Handle(AtualizarNomeMinhaContaCommand request, CancellationToken ct) => servico.AtualizarNomeAsync(request.Nome, request.Versao, ct);
    public async Task Handle(AtualizarEmailMinhaContaCommand request, CancellationToken ct) => await servico.AtualizarEmailAsync(request.NovoEmail, request.SenhaAtual, request.Versao, ct);
    public async Task Handle(AlterarSenhaMinhaContaCommand request, CancellationToken ct) => await servico.AlterarSenhaAsync(request.SenhaAtual, request.NovaSenha, request.Versao, ct);
}

internal sealed class AtualizarEmpresaTenantValidator : AbstractValidator<AtualizarEmpresaTenantCommand>
{
    public AtualizarEmpresaTenantValidator()
    {
        RuleFor(x => x.NomeFantasia).NotEmpty().MaximumLength(160);
        RuleFor(x => x.RazaoSocial).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CpfCnpj).NotEmpty().Matches("^[0-9.\\/\\-]{11,20}$");
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Telefone).MaximumLength(30);
        RuleFor(x => x.FusoHorario).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Versao).GreaterThan(0);
    }
}

internal sealed class ListarUsuariosTenantValidator : AbstractValidator<ListarUsuariosTenantQuery>
{
    public ListarUsuariosTenantValidator()
    {
        RuleFor(x => x.Pagina).GreaterThan(0);
        RuleFor(x => x.TamanhoPagina).Must(x => x is 10 or 25 or 50);
        RuleFor(x => x.Pesquisa).MaximumLength(160);
        RuleFor(x => x.Status).Must(x => x is null or "ativo" or "inativo" or "pendente" or "expirado");
    }
}

internal sealed class ConvidarUsuarioTenantValidator : AbstractValidator<ConvidarUsuarioTenantCommand>
{
    public ConvidarUsuarioTenantValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(200).EmailAddress();
        RuleFor(x => x.PerfilId).NotEmpty();
    }
}

internal sealed class AlterarPerfilUsuarioTenantValidator : AbstractValidator<AlterarPerfilUsuarioTenantCommand>
{
    public AlterarPerfilUsuarioTenantValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.PerfilId).NotEmpty();
        RuleFor(x => x.Versao).GreaterThan(0);
    }
}

internal sealed class AlterarStatusUsuarioTenantValidator : AbstractValidator<AlterarStatusUsuarioTenantCommand>
{
    public AlterarStatusUsuarioTenantValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Versao).GreaterThan(0);
    }
}

internal sealed class ReenviarConviteUsuarioTenantValidator : AbstractValidator<ReenviarConviteUsuarioTenantCommand>
{
    public ReenviarConviteUsuarioTenantValidator() => RuleFor(x => x.Id).NotEmpty();
}

internal sealed class PerfilTenantValidatorBase
{
    public static void Aplicar<T>(AbstractValidator<T> validator,
        Expression<Func<T, string>> nome,
        Expression<Func<T, string?>> descricao,
        Expression<Func<T, IReadOnlyCollection<string>>> permissoes)
    {
        validator.RuleFor(nome).NotEmpty().MaximumLength(100);
        validator.RuleFor(descricao).MaximumLength(240);
        validator.RuleFor(permissoes).NotNull().Must(x => x.Count <= 100)
            .WithMessage("A quantidade de permissões informada é inválida.");
    }
}

internal sealed class CriarPerfilTenantValidator : AbstractValidator<CriarPerfilTenantCommand>
{
    public CriarPerfilTenantValidator() => PerfilTenantValidatorBase.Aplicar(this, x => x.Nome, x => x.Descricao, x => x.Permissoes);
}

internal sealed class AtualizarPerfilTenantValidator : AbstractValidator<AtualizarPerfilTenantCommand>
{
    public AtualizarPerfilTenantValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Versao).GreaterThan(0);
        PerfilTenantValidatorBase.Aplicar(this, x => x.Nome, x => x.Descricao, x => x.Permissoes);
    }
}

internal sealed class AlterarStatusPerfilTenantValidator : AbstractValidator<AlterarStatusPerfilTenantCommand>
{
    public AlterarStatusPerfilTenantValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Versao).GreaterThan(0);
    }
}

internal sealed class AtualizarNomeMinhaContaValidator : AbstractValidator<AtualizarNomeMinhaContaCommand>
{
    public AtualizarNomeMinhaContaValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Versao).GreaterThan(0);
    }
}

internal sealed class AtualizarEmailMinhaContaValidator : AbstractValidator<AtualizarEmailMinhaContaCommand>
{
    public AtualizarEmailMinhaContaValidator()
    {
        RuleFor(x => x.NovoEmail).NotEmpty().MaximumLength(200).EmailAddress();
        RuleFor(x => x.SenhaAtual).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Versao).GreaterThan(0);
    }
}

internal sealed class AlterarSenhaMinhaContaValidator : AbstractValidator<AlterarSenhaMinhaContaCommand>
{
    public AlterarSenhaMinhaContaValidator()
    {
        RuleFor(x => x.SenhaAtual).NotEmpty().MaximumLength(256);
        RuleFor(x => x.NovaSenha).NotEmpty().MinimumLength(10).MaximumLength(256);
        RuleFor(x => x.ConfirmacaoNovaSenha).Equal(x => x.NovaSenha)
            .WithMessage("A confirmação da nova senha não confere.");
        RuleFor(x => x.Versao).GreaterThan(0);
    }
}
