using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Detara.Application.Abstracoes;
using Detara.Application.Agenda;
using Detara.Application.Atendimento;
using Detara.Application.Capacidades;
using Detara.Application.Financeiro;
using Detara.Domain.Atendimento;
using Detara.Domain.Financeiro;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Detara.Application.FluxoOperacional;

public sealed record RegistrarAtendimentoRapidoCommand(Guid SolicitacaoId, Guid ClienteId,
    Guid? VeiculoId, TipoItemOrcamento TipoItem, Guid ItemCatalogoId, decimal ValorPago,
    FormaPagamento FormaPagamento, DateTime RealizadoEmLocal, decimal Taxa, int? NumeroParcelas,
    string? ObservacaoPagamento) : IRequest<AtendimentoRapidoResultado>;

public sealed record AtendimentoRapidoResultado(Guid OrdemServicoId, string Codigo, Guid ContaReceberId);
public sealed record ObterContextoAtendimentoRapidoQuery : IRequest<DateTime>;

internal sealed class RegistrarAtendimentoRapidoValidator : AbstractValidator<RegistrarAtendimentoRapidoCommand>
{
    public RegistrarAtendimentoRapidoValidator()
    {
        RuleFor(x => x.SolicitacaoId).NotEmpty();
        RuleFor(x => x.ClienteId).NotEmpty();
        RuleFor(x => x.ItemCatalogoId).NotEmpty();
        RuleFor(x => x.TipoItem).Must(tipo => tipo is TipoItemOrcamento.Servico or TipoItemOrcamento.Pacote);
        RuleFor(x => x.ValorPago).GreaterThan(0).LessThanOrEqualTo(999999999.99m).PrecisionScale(11, 2, true);
        RuleFor(x => x.FormaPagamento).IsInEnum();
        RuleFor(x => x.Taxa).GreaterThanOrEqualTo(0).LessThanOrEqualTo(x => x.ValorPago).PrecisionScale(11, 2, true);
        RuleFor(x => x.RealizadoEmLocal).NotEmpty().Must(data => data.Kind == DateTimeKind.Unspecified)
            .WithMessage("Informe a data e hora locais do atendimento.");
        RuleFor(x => x.ObservacaoPagamento).MaximumLength(1000);
        RuleFor(x => x.ObservacaoPagamento).NotEmpty().When(x => x.FormaPagamento == FormaPagamento.Outro)
            .WithMessage("Descreva a forma de pagamento.");
        RuleFor(x => x.NumeroParcelas).NotNull().InclusiveBetween(1, 120)
            .When(x => x.FormaPagamento == FormaPagamento.CartaoCredito);
        RuleFor(x => x.NumeroParcelas).Null().When(x => x.FormaPagamento != FormaPagamento.CartaoCredito);
    }
}

internal sealed class ObterContextoAtendimentoRapidoHandler(IUsuarioContexto usuario,
    IPlataformaAtendimentoConsulta plataforma, IConversorFusoHorario conversor, TimeProvider relogio)
    : IRequestHandler<ObterContextoAtendimentoRapidoQuery, DateTime>
{
    public async Task<DateTime> Handle(ObterContextoAtendimentoRapidoQuery request, CancellationToken ct)
    {
        var empresa = await OrcamentoFluxo.ObterEmpresaAsync(plataforma, usuario.EmpresaId, ct);
        return conversor.ParaLocal(relogio.GetUtcNow().UtcDateTime, empresa.FusoHorario);
    }
}

internal sealed class RegistrarAtendimentoRapidoHandler(IUsuarioContexto usuario,
    IOrdensServicoRepositorio ordens, IClientesAtendimentoConsulta clientes,
    ICatalogoAtendimentoConsulta catalogo, IPlataformaAtendimentoConsulta plataforma,
    IEmpresaCapacidadesServico capacidades, IIntegracaoFinanceiroOrdensServico financeiro,
    IFinanceiroRepositorio contas, IConversorFusoHorario conversor, TimeProvider relogio)
    : IRequestHandler<RegistrarAtendimentoRapidoCommand, AtendimentoRapidoResultado>
{
    public async Task<AtendimentoRapidoResultado> Handle(RegistrarAtendimentoRapidoCommand request, CancellationToken ct)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
        var existente = await ordens.ObterPorSolicitacaoRapidaAsync(request.SolicitacaoId, ct);
        if (existente is not null)
        {
            if (existente.AtendimentoRapidoHash != hash)
                throw new ConflitoRegraNegocioException("Este lançamento já foi registrado com outros dados. Consulte o atendimento antes de continuar.");
            var contaId = await contas.ObterIdPorOrdemServicoAsync(existente.Id, ct)
                ?? throw new ConflitoRegraNegocioException("O lançamento existente não possui o vínculo financeiro esperado.");
            return new(existente.Id, existente.Codigo, contaId);
        }

        var empresa = await OrcamentoFluxo.ObterEmpresaAsync(plataforma, usuario.EmpresaId, ct);
        var realizadoEmUtc = conversor.ParaUtc(request.RealizadoEmLocal, empresa.FusoHorario);
        if (realizadoEmUtc > relogio.GetUtcNow().UtcDateTime)
            throw new ValidationException([new ValidationFailure(nameof(request.RealizadoEmLocal),
                "O atendimento realizado não pode ter data futura.")]);
        var usaVeiculos = await VeiculoTransacionalRegra.ValidarAsync(capacidades, request.VeiculoId, ct);
        var partes = await OrcamentoFluxo.PrepararPartesAsync(clientes, usuario.EmpresaId,
            request.ClienteId, request.VeiculoId, usaVeiculos, null, ct);
        var itens = await OrcamentoFluxo.PrepararItensAsync(catalogo, usuario.EmpresaId,
            [new(request.TipoItem, request.ItemCatalogoId, null, null, request.ValorPago, 1, null)], null, [], ct);
        var agora = relogio.GetUtcNow().UtcDateTime;
        var snapshots = itens.Select(item => new ItemOrdemServicoSnapshot(item.TipoItem, item.ItemCatalogoId,
            null, null, item.Nome, item.Descricao, item.ValorUnitario, 1, item.Ordem,
            OrigemComercialOrdemServico.AcordoDireto, agora, usuario.UsuarioId, null)).ToArray();
        var ordem = OrdemServico.RegistrarAtendimentoRealizado(usuario.EmpresaId, request.RealizadoEmLocal.Year,
            new(partes.ClienteId, partes.ClienteNome, partes.ClienteDocumento, partes.ClienteTelefone,
                partes.VeiculoId, partes.VeiculoDescricao, partes.VeiculoPlaca), snapshots,
            usuario.UsuarioId, realizadoEmUtc, request.SolicitacaoId, hash);
        var novaContaId = await financeiro.PrepararRecebimentoIntegralAsync(new(usuario.EmpresaId, ordem.Id,
            ordem.Codigo, ordem.ClienteId, ordem.ClienteNomeSnapshot, ordem.VeiculoId,
            ordem.VeiculoDescricaoSnapshot, ordem.VeiculoPlacaSnapshot, ordem.SubtotalAutorizado,
            0, 0, ordem.TotalAutorizado, realizadoEmUtc), request.FormaPagamento, request.Taxa,
            request.NumeroParcelas, request.ObservacaoPagamento, usuario.UsuarioId, ct);
        ordens.Adicionar(ordem);
        await ordens.SalvarAsync(ct);
        return new(ordem.Id, ordem.Codigo, novaContaId);
    }
}
