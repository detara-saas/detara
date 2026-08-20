using Detara.Application.Abstracoes;
using Detara.Application.Agenda;
using Detara.Domain.Financeiro;
using FluentValidation;
using MediatR;

namespace Detara.Application.Financeiro;

public sealed record ListarContasReceberQuery(int Pagina, int TamanhoPagina, StatusContaReceber? Status,
    bool? Vencida, DateOnly? CompetenciaInicial, DateOnly? CompetenciaFinal, string? Pesquisa)
    : IRequest<PaginacaoResultado<ContaReceberListaResultado>>;
public sealed record ObterContaReceberQuery(Guid Id) : IRequest<ContaReceberDetalheVisualizacao>;
public sealed record ObterContaReceberPorOrdemServicoQuery(Guid OrdemServicoId) : IRequest<Guid?>;
public sealed record ObterResumoFinanceiroQuery(DateOnly? Inicio, DateOnly? Fim) : IRequest<ResumoFinanceiroVisualizacao>;
public sealed record RegistrarPagamentoCommand(Guid ContaReceberId, FormaPagamento FormaPagamento,
    decimal Valor, decimal Taxa, int? NumeroParcelas, string? Observacao, DateTime RecebidoEmLocal)
    : IRequest<ContaReceberDetalheVisualizacao>;
public sealed record EstornarPagamentoCommand(Guid ContaReceberId, Guid PagamentoId, string Motivo)
    : IRequest<ContaReceberDetalheVisualizacao>;
public sealed record AlterarVencimentoContaReceberCommand(Guid ContaReceberId, DateOnly DataVencimento)
    : IRequest<ContaReceberDetalheVisualizacao>;

public sealed record ContaReceberDetalheVisualizacao(ContaReceber Conta, DateOnly HojeLocal,
    string FusoHorario, IReadOnlyDictionary<Guid, string> Usuarios);
public sealed record ResumoFinanceiroVisualizacao(DateOnly Inicio, DateOnly Fim, decimal Faturado,
    decimal RecebidoBruto, decimal Taxas, decimal ReceitaLiquidaRecebida, decimal EmAbertoAtual,
    decimal VencidoAtual, decimal TicketMedio, IReadOnlyCollection<FormaPagamentoResumo> FormasPagamento);

internal sealed class ListarContasReceberValidator : AbstractValidator<ListarContasReceberQuery>
{
    public ListarContasReceberValidator()
    {
        RuleFor(x => x.Pagina).GreaterThanOrEqualTo(1);
        RuleFor(x => x.TamanhoPagina).Must(x => x is 10 or 25 or 50);
        RuleFor(x => x.Pesquisa).MaximumLength(160);
        RuleFor(x => x).Must(x => !x.CompetenciaInicial.HasValue || !x.CompetenciaFinal.HasValue ||
            x.CompetenciaFinal >= x.CompetenciaInicial).WithMessage("O período de competência é inválido.");
    }
}

internal sealed class ObterContaReceberValidator : AbstractValidator<ObterContaReceberQuery>
{
    public ObterContaReceberValidator() => RuleFor(x => x.Id).NotEmpty();
}

internal sealed class ObterResumoFinanceiroValidator : AbstractValidator<ObterResumoFinanceiroQuery>
{
    public ObterResumoFinanceiroValidator() => RuleFor(x => x).Must(x => !x.Inicio.HasValue || !x.Fim.HasValue || x.Fim >= x.Inicio)
        .WithMessage("O período financeiro é inválido.");
}

internal sealed class RegistrarPagamentoValidator : AbstractValidator<RegistrarPagamentoCommand>
{
    public RegistrarPagamentoValidator()
    {
        RuleFor(x => x.ContaReceberId).NotEmpty();
        RuleFor(x => x.FormaPagamento).IsInEnum();
        RuleFor(x => x.Valor).GreaterThan(0);
        RuleFor(x => x.Taxa).GreaterThanOrEqualTo(0).LessThanOrEqualTo(x => x.Valor);
        RuleFor(x => x.NumeroParcelas).InclusiveBetween(1, 120).When(x => x.NumeroParcelas.HasValue);
        RuleFor(x => x.Observacao).MaximumLength(1000);
        RuleFor(x => x.RecebidoEmLocal).NotEmpty();
    }
}

internal sealed class EstornarPagamentoValidator : AbstractValidator<EstornarPagamentoCommand>
{
    public EstornarPagamentoValidator()
    {
        RuleFor(x => x.ContaReceberId).NotEmpty();
        RuleFor(x => x.PagamentoId).NotEmpty();
        RuleFor(x => x.Motivo).NotEmpty().MaximumLength(500);
    }
}

internal sealed class AlterarVencimentoValidator : AbstractValidator<AlterarVencimentoContaReceberCommand>
{
    public AlterarVencimentoValidator()
    {
        RuleFor(x => x.ContaReceberId).NotEmpty();
        RuleFor(x => x.DataVencimento).NotEmpty();
    }
}

internal sealed class ListarContasReceberHandler(IUsuarioContexto usuario, IFinanceiroRepositorio repositorio,
    IPlataformaFinanceiroConsulta plataforma, IConversorFusoHorario conversor)
    : IRequestHandler<ListarContasReceberQuery, PaginacaoResultado<ContaReceberListaResultado>>
{
    public async Task<PaginacaoResultado<ContaReceberListaResultado>> Handle(ListarContasReceberQuery request, CancellationToken ct)
    {
        var fuso = await FinanceiroFluxo.ObterFusoAsync(plataforma, usuario.EmpresaId, ct);
        var hoje = DateOnly.FromDateTime(conversor.ParaLocal(DateTime.UtcNow, fuso));
        return await repositorio.ListarAsync(new(request.Pagina, request.TamanhoPagina, request.Status,
            request.Vencida, request.CompetenciaInicial, request.CompetenciaFinal, request.Pesquisa, hoje), ct);
    }
}

internal sealed class ObterContaReceberHandler(IUsuarioContexto usuario, IFinanceiroRepositorio repositorio,
    IPlataformaFinanceiroConsulta plataforma, IConversorFusoHorario conversor)
    : IRequestHandler<ObterContaReceberQuery, ContaReceberDetalheVisualizacao>
{
    public Task<ContaReceberDetalheVisualizacao> Handle(ObterContaReceberQuery request, CancellationToken ct) =>
        FinanceiroFluxo.ObterDetalheAsync(request.Id, usuario.EmpresaId, repositorio, plataforma, conversor, ct);
}

internal sealed class ObterContaReceberPorOrdemServicoHandler(IFinanceiroRepositorio repositorio)
    : IRequestHandler<ObterContaReceberPorOrdemServicoQuery, Guid?>
{
    public Task<Guid?> Handle(ObterContaReceberPorOrdemServicoQuery request, CancellationToken ct) =>
        repositorio.ObterIdPorOrdemServicoAsync(request.OrdemServicoId, ct);
}

internal sealed class ObterResumoFinanceiroHandler(IUsuarioContexto usuario, IFinanceiroRepositorio repositorio,
    IPlataformaFinanceiroConsulta plataforma, IConversorFusoHorario conversor)
    : IRequestHandler<ObterResumoFinanceiroQuery, ResumoFinanceiroVisualizacao>
{
    public async Task<ResumoFinanceiroVisualizacao> Handle(ObterResumoFinanceiroQuery request, CancellationToken ct)
    {
        var fuso = await FinanceiroFluxo.ObterFusoAsync(plataforma, usuario.EmpresaId, ct);
        var hoje = DateOnly.FromDateTime(conversor.ParaLocal(DateTime.UtcNow, fuso));
        var inicio = request.Inicio ?? new DateOnly(hoje.Year, hoje.Month, 1);
        var fim = request.Fim ?? inicio.AddMonths(1).AddDays(-1);
        if (fim < inicio) throw new ArgumentException("O período financeiro é inválido.");
        var inicioUtc = conversor.ParaUtc(inicio.ToDateTime(TimeOnly.MinValue), fuso);
        var fimExclusivoUtc = conversor.ParaUtc(fim.AddDays(1).ToDateTime(TimeOnly.MinValue), fuso);
        var resumo = await repositorio.ObterResumoAsync(inicio, fim, inicioUtc, fimExclusivoUtc, hoje, ct);
        var ticket = resumo.QuantidadeContas == 0 ? 0 : resumo.Faturado / resumo.QuantidadeContas;
        return new(inicio, fim, resumo.Faturado, resumo.RecebidoBruto, resumo.Taxas,
            resumo.RecebidoBruto - resumo.Taxas, resumo.EmAbertoAtual, resumo.VencidoAtual,
            ticket, resumo.FormasPagamento);
    }
}

internal sealed class RegistrarPagamentoHandler(IUsuarioContexto usuario, IFinanceiroRepositorio repositorio,
    IPlataformaFinanceiroConsulta plataforma, IConversorFusoHorario conversor)
    : IRequestHandler<RegistrarPagamentoCommand, ContaReceberDetalheVisualizacao>
{
    public async Task<ContaReceberDetalheVisualizacao> Handle(RegistrarPagamentoCommand request, CancellationToken ct)
    {
        var conta = await FinanceiroFluxo.ExigirAsync(repositorio, request.ContaReceberId, true, ct);
        var fuso = await FinanceiroFluxo.ObterFusoAsync(plataforma, usuario.EmpresaId, ct);
        Pagamento? pagamento = null;
        FinanceiroFluxo.ExecutarRegra(() => pagamento = conta.RegistrarPagamento(request.FormaPagamento,
            request.Valor, request.Taxa, request.NumeroParcelas, request.Observacao,
            conversor.ParaUtc(request.RecebidoEmLocal, fuso), usuario.UsuarioId));
        repositorio.AdicionarPagamento(pagamento!);
        await repositorio.SalvarAsync(ct);
        return await FinanceiroFluxo.ObterDetalheAsync(conta.Id, usuario.EmpresaId, repositorio, plataforma, conversor, ct);
    }
}

internal sealed class EstornarPagamentoHandler(IUsuarioContexto usuario, IFinanceiroRepositorio repositorio,
    IPlataformaFinanceiroConsulta plataforma, IConversorFusoHorario conversor)
    : IRequestHandler<EstornarPagamentoCommand, ContaReceberDetalheVisualizacao>
{
    public async Task<ContaReceberDetalheVisualizacao> Handle(EstornarPagamentoCommand request, CancellationToken ct)
    {
        var conta = await FinanceiroFluxo.ExigirAsync(repositorio, request.ContaReceberId, true, ct);
        FinanceiroFluxo.ExecutarRegra(() => conta.EstornarPagamento(request.PagamentoId, usuario.UsuarioId,
            request.Motivo, DateTime.UtcNow));
        await repositorio.SalvarAsync(ct);
        return await FinanceiroFluxo.ObterDetalheAsync(conta.Id, usuario.EmpresaId, repositorio, plataforma, conversor, ct);
    }
}

internal sealed class AlterarVencimentoContaReceberHandler(IUsuarioContexto usuario, IFinanceiroRepositorio repositorio,
    IPlataformaFinanceiroConsulta plataforma, IConversorFusoHorario conversor)
    : IRequestHandler<AlterarVencimentoContaReceberCommand, ContaReceberDetalheVisualizacao>
{
    public async Task<ContaReceberDetalheVisualizacao> Handle(AlterarVencimentoContaReceberCommand request, CancellationToken ct)
    {
        var conta = await FinanceiroFluxo.ExigirAsync(repositorio, request.ContaReceberId, true, ct);
        FinanceiroFluxo.ExecutarRegra(() => conta.AlterarVencimento(request.DataVencimento));
        await repositorio.SalvarAsync(ct);
        return await FinanceiroFluxo.ObterDetalheAsync(conta.Id, usuario.EmpresaId, repositorio, plataforma, conversor, ct);
    }
}

public sealed class IntegracaoFinanceiroOrdensServico(IFinanceiroRepositorio repositorio,
    IPlataformaFinanceiroConsulta plataforma, IConversorFusoHorario conversor) : IIntegracaoFinanceiroOrdensServico
{
    public async Task PrepararContaReceberAsync(OrdemServicoFinalizadaFinanceiro evento, CancellationToken ct)
    {
        if (evento.TotalAutorizado <= 0 || await repositorio.ExistePorOrdemServicoAsync(evento.OrdemServicoId, ct)) return;
        var fuso = await FinanceiroFluxo.ObterFusoAsync(plataforma, evento.EmpresaId, ct);
        var competencia = DateOnly.FromDateTime(conversor.ParaLocal(evento.FinalizadaEmUtc, fuso));
        repositorio.Adicionar(new ContaReceber(evento.EmpresaId, evento.OrdemServicoId,
            evento.OrdemServicoCodigo, evento.ClienteId, evento.ClienteNome, evento.VeiculoId,
            evento.VeiculoDescricao, evento.VeiculoPlaca, evento.SubtotalAutorizado,
            evento.DescontoAutorizado, evento.AcrescimoAutorizado, evento.TotalAutorizado, competencia));
    }
}

internal static class FinanceiroFluxo
{
    public static async Task<ContaReceber> ExigirAsync(IFinanceiroRepositorio repositorio, Guid id,
        bool paraAlteracao, CancellationToken ct) => await repositorio.ObterAsync(id, paraAlteracao, ct)
        ?? throw new RecursoNaoEncontradoException("Conta a receber não encontrada.");

    public static async Task<string> ObterFusoAsync(IPlataformaFinanceiroConsulta plataforma, Guid empresaId,
        CancellationToken ct) => await plataforma.ObterFusoHorarioAsync(empresaId, ct)
        ?? throw new RecursoNaoEncontradoException("Empresa não encontrada.");

    public static async Task<ContaReceberDetalheVisualizacao> ObterDetalheAsync(Guid id, Guid empresaId,
        IFinanceiroRepositorio repositorio, IPlataformaFinanceiroConsulta plataforma,
        IConversorFusoHorario conversor, CancellationToken ct)
    {
        var conta = await ExigirAsync(repositorio, id, false, ct);
        var fuso = await ObterFusoAsync(plataforma, empresaId, ct);
        var hoje = DateOnly.FromDateTime(conversor.ParaLocal(DateTime.UtcNow, fuso));
        var usuariosIds = conta.Pagamentos.Select(x => x.RegistradoPorUsuarioId)
            .Concat(conta.Pagamentos.Where(x => x.EstornadoPorUsuarioId.HasValue)
                .Select(x => x.EstornadoPorUsuarioId!.Value)).Distinct().ToArray();
        var nomes = await plataforma.ObterNomesUsuariosAsync(empresaId, usuariosIds, ct);
        return new(conta, hoje, fuso, nomes);
    }

    public static void ExecutarRegra(Action acao)
    {
        try { acao(); }
        catch (InvalidOperationException exception) { throw new ConflitoRegraNegocioException(exception.Message); }
    }
}
