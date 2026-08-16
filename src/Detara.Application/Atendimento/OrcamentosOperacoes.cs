using Detara.Application.Abstracoes;
using Detara.Domain.Atendimento;
using FluentValidation;
using MediatR;

namespace Detara.Application.Atendimento;

public sealed record ItemOrcamentoEntrada(TipoItemOrcamento TipoItem, Guid? ItemCatalogoId, string? Nome, string? Descricao,
    decimal ValorUnitario, int Quantidade, string? Observacao);
public sealed record CriarOrcamentoCommand(Guid ClienteId, Guid VeiculoId, Guid? AgendamentoOrigemId, DateOnly ValidoAte,
    string? ObservacaoCliente, string? ObservacaoInterna, string? Condicoes, decimal Desconto, decimal Acrescimo,
    IReadOnlyCollection<ItemOrcamentoEntrada> Itens) : IRequest<OrcamentoDetalheVisualizacao>;
public sealed record AtualizarOrcamentoCommand(Guid Id, Guid ClienteId, Guid VeiculoId, Guid? AgendamentoOrigemId, DateOnly ValidoAte,
    string? ObservacaoCliente, string? ObservacaoInterna, string? Condicoes, decimal Desconto, decimal Acrescimo,
    IReadOnlyCollection<ItemOrcamentoEntrada> Itens) : IRequest<OrcamentoDetalheVisualizacao>;
public sealed record EmitirOrcamentoCommand(Guid Id, string? Observacao) : IRequest<OrcamentoDetalheVisualizacao>;
public sealed record AprovarOrcamentoCommand(Guid Id, string? Observacao) : IRequest<OrcamentoDetalheVisualizacao>;
public sealed record RecusarOrcamentoCommand(Guid Id, string? Observacao) : IRequest<OrcamentoDetalheVisualizacao>;
public sealed record CancelarOrcamentoCommand(Guid Id, string? Observacao) : IRequest<OrcamentoDetalheVisualizacao>;
public sealed record CriarNovaPropostaCommand(Guid Id) : IRequest<OrcamentoDetalheVisualizacao>;
public sealed record ObterOrcamentoQuery(Guid Id) : IRequest<OrcamentoDetalheVisualizacao>;
public sealed record ListarOrcamentosQuery(int Pagina, int TamanhoPagina, StatusEfetivoOrcamento? Status, string? Pesquisa) : IRequest<PaginacaoResultado<OrcamentoListaVisualizacao>>;
public sealed record BuscarClientesOrcamentoQuery(string Pesquisa, int Limite = 15) : IRequest<IReadOnlyCollection<ClienteAtendimentoInterno>>;
public sealed record ListarVeiculosOrcamentoQuery(Guid ClienteId) : IRequest<IReadOnlyCollection<VeiculoAtendimentoInterno>>;
public sealed record BuscarCatalogoOrcamentoQuery(string? Pesquisa, int Limite = 30) : IRequest<IReadOnlyCollection<ItemCatalogoAtendimentoInterno>>;
public sealed record ObterOrigemAgendamentoOrcamentoQuery(Guid AgendamentoId) : IRequest<AgendamentoAtendimentoInterno>;
public sealed record ObterContextoOrcamentoQuery : IRequest<ContextoOrcamentoVisualizacao>;
public sealed record GerarPdfOrcamentoQuery(Guid Id) : IRequest<PdfOrcamentoResultado>;

public sealed record OrcamentoListaVisualizacao(OrcamentoListaResultado Orcamento, StatusEfetivoOrcamento StatusEfetivo);
public sealed record HistoricoStatusOrcamentoVisualizacao(HistoricoStatusOrcamentoResultado Historico, string UsuarioNome);
public sealed record OrcamentoDetalheVisualizacao(OrcamentoDetalheResultado Orcamento, StatusEfetivoOrcamento StatusEfetivo,
    IReadOnlyCollection<HistoricoStatusOrcamentoVisualizacao> Historico);
public sealed record ContextoOrcamentoVisualizacao(DateOnly HojeLocal, DateOnly ValidadeSugerida);
public sealed record PdfOrcamentoResultado(string NomeArquivo, byte[] Conteudo);

internal abstract class SalvarOrcamentoValidatorBase<T> : AbstractValidator<T>
{
    protected void Regras(Func<T, Guid> cliente, Func<T, Guid> veiculo, Func<T, DateOnly> validade, Func<T, decimal> desconto,
        Func<T, decimal> acrescimo, Func<T, string?> observacaoCliente, Func<T, string?> observacaoInterna,
        Func<T, string?> condicoes, Func<T, IReadOnlyCollection<ItemOrcamentoEntrada>> itens)
    {
        RuleFor(x => cliente(x)).NotEmpty().WithName("ClienteId");
        RuleFor(x => veiculo(x)).NotEmpty().WithName("VeiculoId");
        RuleFor(x => validade(x)).NotEmpty().WithName("ValidoAte");
        RuleFor(x => desconto(x)).GreaterThanOrEqualTo(0).WithName("Desconto");
        RuleFor(x => acrescimo(x)).GreaterThanOrEqualTo(0).WithName("Acrescimo");
        RuleFor(x => observacaoCliente(x)).MaximumLength(2000).WithName("ObservacaoCliente");
        RuleFor(x => observacaoInterna(x)).MaximumLength(4000).WithName("ObservacaoInterna");
        RuleFor(x => condicoes(x)).MaximumLength(2000).WithName("Condicoes");
        RuleFor(x => itens(x)).NotEmpty().WithName("Itens");
        RuleForEach(x => itens(x)).ChildRules(item =>
        {
            item.RuleFor(x => x.TipoItem).IsInEnum();
            item.RuleFor(x => x.ValorUnitario).GreaterThanOrEqualTo(0);
            item.RuleFor(x => x.Quantidade).GreaterThanOrEqualTo(1);
            item.RuleFor(x => x.Nome).MaximumLength(160);
            item.RuleFor(x => x.Descricao).MaximumLength(2000);
            item.RuleFor(x => x.Observacao).MaximumLength(1000);
            item.RuleFor(x => x).Must(x => x.TipoItem == TipoItemOrcamento.Personalizado
                ? !x.ItemCatalogoId.HasValue && !string.IsNullOrWhiteSpace(x.Nome)
                : x.ItemCatalogoId.HasValue).WithMessage("Informe um item de catálogo ou os dados do item personalizado.");
        });
    }
}

internal sealed class CriarOrcamentoValidator : SalvarOrcamentoValidatorBase<CriarOrcamentoCommand>
{ public CriarOrcamentoValidator() => Regras(x => x.ClienteId, x => x.VeiculoId, x => x.ValidoAte, x => x.Desconto, x => x.Acrescimo, x => x.ObservacaoCliente, x => x.ObservacaoInterna, x => x.Condicoes, x => x.Itens); }
internal sealed class AtualizarOrcamentoValidator : SalvarOrcamentoValidatorBase<AtualizarOrcamentoCommand>
{ public AtualizarOrcamentoValidator() { RuleFor(x => x.Id).NotEmpty(); Regras(x => x.ClienteId, x => x.VeiculoId, x => x.ValidoAte, x => x.Desconto, x => x.Acrescimo, x => x.ObservacaoCliente, x => x.ObservacaoInterna, x => x.Condicoes, x => x.Itens); } }
internal sealed class ListarOrcamentosValidator : AbstractValidator<ListarOrcamentosQuery>
{ public ListarOrcamentosValidator() { RuleFor(x => x.Pagina).GreaterThanOrEqualTo(1); RuleFor(x => x.TamanhoPagina).Must(x => x is 10 or 25 or 50); RuleFor(x => x.Pesquisa).MaximumLength(160); RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue); } }

internal sealed class CriarOrcamentoHandler(IUsuarioContexto usuario, IClientesAtendimentoConsulta clientes, ICatalogoAtendimentoConsulta catalogo,
    IAgendaAtendimentoConsulta agenda, IOrcamentosRepositorio repositorio) : IRequestHandler<CriarOrcamentoCommand, OrcamentoDetalheVisualizacao>
{
    public async Task<OrcamentoDetalheVisualizacao> Handle(CriarOrcamentoCommand request, CancellationToken ct)
    {
        var origem = await OrcamentoFluxo.ObterOrigemAsync(agenda, usuario.EmpresaId, request.AgendamentoOrigemId, ct);
        var partes = await OrcamentoFluxo.PrepararPartesAsync(clientes, usuario.EmpresaId, request.ClienteId, request.VeiculoId, origem, ct);
        var itens = await OrcamentoFluxo.PrepararItensAsync(catalogo, usuario.EmpresaId, request.Itens, origem?.Itens, [], ct);
        var entidade = new Orcamento(usuario.EmpresaId, partes, request.AgendamentoOrigemId, null, request.ValidoAte,
            request.ObservacaoCliente, request.ObservacaoInterna, request.Condicoes, request.Desconto, request.Acrescimo, itens, usuario.UsuarioId);
        repositorio.Adicionar(entidade);
        await repositorio.SalvarAsync(ct);
        return await OrcamentoFluxo.ObterDetalheAsync(entidade.Id, usuario.EmpresaId, repositorio, null, ct);
    }
}

internal sealed class AtualizarOrcamentoHandler(IUsuarioContexto usuario, IClientesAtendimentoConsulta clientes, ICatalogoAtendimentoConsulta catalogo,
    IAgendaAtendimentoConsulta agenda, IOrcamentosRepositorio repositorio) : IRequestHandler<AtualizarOrcamentoCommand, OrcamentoDetalheVisualizacao>
{
    public async Task<OrcamentoDetalheVisualizacao> Handle(AtualizarOrcamentoCommand request, CancellationToken ct)
    {
        var entidade = await repositorio.ObterParaAlteracaoAsync(request.Id, ct) ?? throw new RecursoNaoEncontradoException("Orçamento não encontrado.");
        var origem = await OrcamentoFluxo.ObterOrigemAsync(agenda, usuario.EmpresaId, request.AgendamentoOrigemId, ct);
        if (entidade.AgendamentoOrigemId != request.AgendamentoOrigemId) throw new ConflitoRegraNegocioException("A origem por agendamento não pode ser trocada. Crie outro orçamento.");
        var partes = await OrcamentoFluxo.PrepararPartesAsync(clientes, usuario.EmpresaId, request.ClienteId, request.VeiculoId, origem, ct);
        var antigos = entidade.CopiarItens();
        var itens = await OrcamentoFluxo.PrepararItensAsync(catalogo, usuario.EmpresaId, request.Itens, origem?.Itens, antigos, ct);
        repositorio.RemoverItensAtuais(entidade);
        OrcamentoFluxo.ExecutarRegra(() => entidade.AtualizarRascunho(partes, request.ValidoAte, request.ObservacaoCliente,
            request.ObservacaoInterna, request.Condicoes, request.Desconto, request.Acrescimo, itens));
        repositorio.AdicionarItensAtuais(entidade);
        await repositorio.SalvarAsync(ct);
        return await OrcamentoFluxo.ObterDetalheAsync(entidade.Id, usuario.EmpresaId, repositorio, null, ct);
    }
}

internal sealed class EmitirOrcamentoHandler(IUsuarioContexto usuario, IOrcamentosRepositorio repositorio, IPlataformaAtendimentoConsulta plataforma)
    : IRequestHandler<EmitirOrcamentoCommand, OrcamentoDetalheVisualizacao>
{
    public async Task<OrcamentoDetalheVisualizacao> Handle(EmitirOrcamentoCommand request, CancellationToken ct)
    {
        var entidade = await repositorio.ObterParaAlteracaoAsync(request.Id, ct) ?? throw new RecursoNaoEncontradoException("Orçamento não encontrado.");
        var empresa = await OrcamentoFluxo.ObterEmpresaAsync(plataforma, usuario.EmpresaId, ct);
        var hoje = OrcamentoFluxo.HojeLocal(empresa.FusoHorario);
        if (entidade.ValidoAte < hoje) throw new ConflitoRegraNegocioException("A validade precisa ser hoje ou uma data futura para emitir o orçamento.");
        OrcamentoFluxo.ExecutarRegra(() => entidade.Emitir(hoje.Year, usuario.UsuarioId, request.Observacao));
        repositorio.AdicionarUltimoHistorico(entidade);
        if (entidade.OrcamentoOrigemId.HasValue)
        {
            var anterior = await repositorio.ObterParaAlteracaoAsync(entidade.OrcamentoOrigemId.Value, ct);
            if (anterior?.Status is StatusOrcamento.Emitido or StatusOrcamento.Aprovado)
            {
                OrcamentoFluxo.ExecutarRegra(() => anterior.MarcarSubstituido(usuario.UsuarioId, $"Substituído por {entidade.Codigo}."));
                repositorio.AdicionarUltimoHistorico(anterior);
            }
        }
        await repositorio.SalvarAsync(ct);
        return await OrcamentoFluxo.ObterDetalheAsync(entidade.Id, usuario.EmpresaId, repositorio, plataforma, ct);
    }
}

internal sealed class AprovarOrcamentoHandler(IUsuarioContexto usuario, IOrcamentosRepositorio repositorio, IPlataformaAtendimentoConsulta plataforma)
    : IRequestHandler<AprovarOrcamentoCommand, OrcamentoDetalheVisualizacao>
{
    public async Task<OrcamentoDetalheVisualizacao> Handle(AprovarOrcamentoCommand request, CancellationToken ct)
    {
        var entidade = await repositorio.ObterParaAlteracaoAsync(request.Id, ct) ?? throw new RecursoNaoEncontradoException("Orçamento não encontrado.");
        var empresa = await OrcamentoFluxo.ObterEmpresaAsync(plataforma, usuario.EmpresaId, ct);
        OrcamentoFluxo.ExecutarRegra(() => entidade.Aprovar(OrcamentoFluxo.HojeLocal(empresa.FusoHorario), usuario.UsuarioId, request.Observacao));
        repositorio.AdicionarUltimoHistorico(entidade);
        await repositorio.SalvarAsync(ct);
        return await OrcamentoFluxo.ObterDetalheAsync(entidade.Id, usuario.EmpresaId, repositorio, plataforma, ct);
    }
}

internal sealed class RecusarOrcamentoHandler(IUsuarioContexto usuario, IOrcamentosRepositorio repositorio) : IRequestHandler<RecusarOrcamentoCommand, OrcamentoDetalheVisualizacao>
{ public async Task<OrcamentoDetalheVisualizacao> Handle(RecusarOrcamentoCommand request, CancellationToken ct) { var entidade = await OrcamentoFluxo.ObterEntidadeAsync(repositorio, request.Id, ct); OrcamentoFluxo.ExecutarRegra(() => entidade.Recusar(usuario.UsuarioId, request.Observacao)); repositorio.AdicionarUltimoHistorico(entidade); await repositorio.SalvarAsync(ct); return await OrcamentoFluxo.ObterDetalheAsync(request.Id, usuario.EmpresaId, repositorio, null, ct); } }
internal sealed class CancelarOrcamentoHandler(IUsuarioContexto usuario, IOrcamentosRepositorio repositorio) : IRequestHandler<CancelarOrcamentoCommand, OrcamentoDetalheVisualizacao>
{ public async Task<OrcamentoDetalheVisualizacao> Handle(CancelarOrcamentoCommand request, CancellationToken ct) { var entidade = await OrcamentoFluxo.ObterEntidadeAsync(repositorio, request.Id, ct); OrcamentoFluxo.ExecutarRegra(() => entidade.Cancelar(usuario.UsuarioId, request.Observacao)); repositorio.AdicionarUltimoHistorico(entidade); await repositorio.SalvarAsync(ct); return await OrcamentoFluxo.ObterDetalheAsync(request.Id, usuario.EmpresaId, repositorio, null, ct); } }

internal sealed class CriarNovaPropostaHandler(IUsuarioContexto usuario, IOrcamentosRepositorio repositorio, IPlataformaAtendimentoConsulta plataforma)
    : IRequestHandler<CriarNovaPropostaCommand, OrcamentoDetalheVisualizacao>
{
    public async Task<OrcamentoDetalheVisualizacao> Handle(CriarNovaPropostaCommand request, CancellationToken ct)
    {
        var anterior = await OrcamentoFluxo.ObterEntidadeAsync(repositorio, request.Id, ct);
        if (anterior.Status == StatusOrcamento.Rascunho) throw new ConflitoRegraNegocioException("Edite o rascunho atual em vez de criar uma nova proposta.");
        var empresa = await OrcamentoFluxo.ObterEmpresaAsync(plataforma, usuario.EmpresaId, ct);
        var hoje = OrcamentoFluxo.HojeLocal(empresa.FusoHorario);
        var partes = new PartesOrcamentoSnapshot(anterior.ClienteId, anterior.ClienteNomeSnapshot, anterior.ClienteDocumentoSnapshot,
            anterior.ClienteTelefoneSnapshot, anterior.VeiculoId, anterior.VeiculoDescricaoSnapshot, anterior.VeiculoPlacaSnapshot);
        var validade = anterior.ValidoAte < hoje ? hoje.AddDays(7) : anterior.ValidoAte;
        var nova = new Orcamento(usuario.EmpresaId, partes, anterior.AgendamentoOrigemId, anterior.Id, validade,
            anterior.ObservacaoCliente, anterior.ObservacaoInterna, anterior.Condicoes, anterior.Desconto, anterior.Acrescimo,
            anterior.CopiarItens(), usuario.UsuarioId);
        repositorio.Adicionar(nova);
        await repositorio.SalvarAsync(ct);
        return await OrcamentoFluxo.ObterDetalheAsync(nova.Id, usuario.EmpresaId, repositorio, plataforma, ct);
    }
}

internal sealed class ObterOrcamentoHandler(IUsuarioContexto usuario, IOrcamentosRepositorio repositorio, IPlataformaAtendimentoConsulta plataforma)
    : IRequestHandler<ObterOrcamentoQuery, OrcamentoDetalheVisualizacao>
{ public Task<OrcamentoDetalheVisualizacao> Handle(ObterOrcamentoQuery request, CancellationToken ct) => OrcamentoFluxo.ObterDetalheAsync(request.Id, usuario.EmpresaId, repositorio, plataforma, ct); }

internal sealed class ListarOrcamentosHandler(IUsuarioContexto usuario, IOrcamentosRepositorio repositorio, IPlataformaAtendimentoConsulta plataforma)
    : IRequestHandler<ListarOrcamentosQuery, PaginacaoResultado<OrcamentoListaVisualizacao>>
{
    public async Task<PaginacaoResultado<OrcamentoListaVisualizacao>> Handle(ListarOrcamentosQuery request, CancellationToken ct)
    {
        var empresa = await OrcamentoFluxo.ObterEmpresaAsync(plataforma, usuario.EmpresaId, ct);
        var hoje = OrcamentoFluxo.HojeLocal(empresa.FusoHorario);
        var pagina = await repositorio.ListarAsync(new(request.Pagina, request.TamanhoPagina, request.Status, request.Pesquisa, hoje), ct);
        return new(pagina.Itens.Select(x => new OrcamentoListaVisualizacao(x, x.Status == StatusOrcamento.Emitido && x.ValidoAte < hoje ? StatusEfetivoOrcamento.Expirado : (StatusEfetivoOrcamento)(int)x.Status)).ToArray(), pagina.Pagina, pagina.TamanhoPagina, pagina.TotalItens);
    }
}

internal sealed class BuscarClientesOrcamentoHandler(IUsuarioContexto usuario, IClientesAtendimentoConsulta consulta) : IRequestHandler<BuscarClientesOrcamentoQuery, IReadOnlyCollection<ClienteAtendimentoInterno>>
{ public Task<IReadOnlyCollection<ClienteAtendimentoInterno>> Handle(BuscarClientesOrcamentoQuery request, CancellationToken ct) => consulta.BuscarClientesAsync(usuario.EmpresaId, request.Pesquisa, request.Limite, ct); }
internal sealed class ListarVeiculosOrcamentoHandler(IUsuarioContexto usuario, IClientesAtendimentoConsulta consulta) : IRequestHandler<ListarVeiculosOrcamentoQuery, IReadOnlyCollection<VeiculoAtendimentoInterno>>
{ public Task<IReadOnlyCollection<VeiculoAtendimentoInterno>> Handle(ListarVeiculosOrcamentoQuery request, CancellationToken ct) => consulta.ListarVeiculosAsync(usuario.EmpresaId, request.ClienteId, ct); }
internal sealed class BuscarCatalogoOrcamentoHandler(IUsuarioContexto usuario, ICatalogoAtendimentoConsulta consulta) : IRequestHandler<BuscarCatalogoOrcamentoQuery, IReadOnlyCollection<ItemCatalogoAtendimentoInterno>>
{ public Task<IReadOnlyCollection<ItemCatalogoAtendimentoInterno>> Handle(BuscarCatalogoOrcamentoQuery request, CancellationToken ct) => consulta.BuscarItensAsync(usuario.EmpresaId, request.Pesquisa, request.Limite, ct); }
internal sealed class ObterOrigemAgendamentoOrcamentoHandler(IUsuarioContexto usuario, IAgendaAtendimentoConsulta agenda) : IRequestHandler<ObterOrigemAgendamentoOrcamentoQuery, AgendamentoAtendimentoInterno>
{ public async Task<AgendamentoAtendimentoInterno> Handle(ObterOrigemAgendamentoOrcamentoQuery request, CancellationToken ct) => await agenda.ObterAsync(usuario.EmpresaId, request.AgendamentoId, ct) ?? throw new RecursoNaoEncontradoException("Agendamento não encontrado."); }
internal sealed class ObterContextoOrcamentoHandler(IUsuarioContexto usuario, IPlataformaAtendimentoConsulta plataforma) : IRequestHandler<ObterContextoOrcamentoQuery, ContextoOrcamentoVisualizacao>
{ public async Task<ContextoOrcamentoVisualizacao> Handle(ObterContextoOrcamentoQuery request, CancellationToken ct) { var empresa = await OrcamentoFluxo.ObterEmpresaAsync(plataforma, usuario.EmpresaId, ct); var hoje = OrcamentoFluxo.HojeLocal(empresa.FusoHorario); return new(hoje, hoje.AddDays(7)); } }

internal sealed class GerarPdfOrcamentoHandler(IUsuarioContexto usuario, IOrcamentosRepositorio repositorio, IPlataformaAtendimentoConsulta plataforma,
    IOrcamentoPdfGenerator gerador) : IRequestHandler<GerarPdfOrcamentoQuery, PdfOrcamentoResultado>
{
    public async Task<PdfOrcamentoResultado> Handle(GerarPdfOrcamentoQuery request, CancellationToken ct)
    {
        var detalhe = await OrcamentoFluxo.ObterDetalheAsync(request.Id, usuario.EmpresaId, repositorio, plataforma, ct);
        if (detalhe.Orcamento.Status == StatusOrcamento.Rascunho || !detalhe.Orcamento.EmitidoEmUtc.HasValue || string.IsNullOrWhiteSpace(detalhe.Orcamento.Codigo))
            throw new ConflitoRegraNegocioException("O PDF oficial só está disponível para documentos que foram emitidos.");
        var empresa = await OrcamentoFluxo.ObterEmpresaAsync(plataforma, usuario.EmpresaId, ct);
        return new($"{detalhe.Orcamento.Codigo ?? "orcamento"}.pdf", gerador.Gerar(new(empresa, detalhe)));
    }
}

internal static class OrcamentoFluxo
{
    public static async Task<PartesOrcamentoSnapshot> PrepararPartesAsync(IClientesAtendimentoConsulta clientes, Guid empresaId, Guid clienteId,
        Guid veiculoId, AgendamentoAtendimentoInterno? origem, CancellationToken ct)
    {
        var atual = await clientes.ObterClienteVeiculoAsync(empresaId, clienteId, veiculoId, ct)
            ?? throw new RecursoNaoEncontradoException("Cliente ou veículo não encontrado.");
        if (atual.Veiculo.ClienteId != clienteId) throw new ConflitoRegraNegocioException("O veículo não pertence ao cliente informado.");
        if (!atual.Cliente.EhAtivo || !atual.Veiculo.EhAtivo) throw new ConflitoRegraNegocioException("Cliente e veículo precisam estar ativos.");
        if (origem is not null && (origem.ClienteId != clienteId || origem.VeiculoId != veiculoId))
            throw new ConflitoRegraNegocioException("O agendamento de origem não corresponde ao cliente e veículo informados.");
        return new(clienteId, origem?.ClienteNome ?? atual.Cliente.Nome, atual.Cliente.Documento, atual.Cliente.Telefone,
            veiculoId, origem?.VeiculoDescricao ?? atual.Veiculo.Descricao, origem?.VeiculoPlaca ?? atual.Veiculo.Placa);
    }

    public static async Task<IReadOnlyCollection<ItemOrcamentoSnapshot>> PrepararItensAsync(ICatalogoAtendimentoConsulta catalogo, Guid empresaId,
        IReadOnlyCollection<ItemOrcamentoEntrada> entradas, IReadOnlyCollection<ItemAgendamentoAtendimentoInterno>? origem,
        IReadOnlyCollection<ItemOrcamentoSnapshot> antigos, CancellationToken ct)
    {
        var chaves = entradas.Where(x => x.TipoItem != TipoItemOrcamento.Personalizado).Select(x => (x.TipoItem, x.ItemCatalogoId!.Value)).ToArray();
        if (chaves.Distinct().Count() != chaves.Length) throw new ConflitoRegraNegocioException("Serviços e pacotes não podem se repetir; utilize a quantidade.");
        var snapshotsAntigos = antigos.Where(x => x.ItemCatalogoId.HasValue).ToDictionary(x => (x.TipoItem, x.ItemCatalogoId!.Value));
        var snapshotsAgenda = (origem ?? []).ToDictionary(x => (x.TipoItem, x.ItemCatalogoId));
        var novas = chaves.Where(x => !snapshotsAntigos.ContainsKey(x) && !snapshotsAgenda.ContainsKey(x)).ToArray();
        var atuais = novas.Length == 0 ? [] : await catalogo.ObterItensAsync(empresaId, novas, ct);
        var atuaisPorChave = atuais.ToDictionary(x => (x.TipoItem, x.Id));
        if (atuais.Count != novas.Length || atuais.Any(x => !x.EhAtivo)) throw new RecursoNaoEncontradoException("Um ou mais itens do catálogo não existem, estão inativos ou pertencem a outra empresa.");
        return entradas.Select((entrada, indice) =>
        {
            if (entrada.TipoItem == TipoItemOrcamento.Personalizado)
                return new ItemOrcamentoSnapshot(entrada.TipoItem, null, entrada.Nome!, entrada.Descricao, null, null,
                    entrada.ValorUnitario, entrada.Quantidade, indice + 1, entrada.Observacao);
            var chave = (entrada.TipoItem, entrada.ItemCatalogoId!.Value);
            if (snapshotsAntigos.TryGetValue(chave, out var antigo))
                return antigo with { ValorUnitario = entrada.ValorUnitario, Quantidade = entrada.Quantidade, Ordem = indice + 1, Observacao = entrada.Observacao };
            if (snapshotsAgenda.TryGetValue(chave, out var agendado))
                return new(entrada.TipoItem, entrada.ItemCatalogoId, agendado.Nome, agendado.Descricao, agendado.TipoPrecificacao,
                    agendado.PrecoReferencia, entrada.ValorUnitario, entrada.Quantidade, indice + 1, entrada.Observacao);
            var atual = atuaisPorChave[chave];
            return new(entrada.TipoItem, entrada.ItemCatalogoId, atual.Nome, atual.Descricao, atual.TipoPrecificacao,
                atual.PrecoReferencia, entrada.ValorUnitario, entrada.Quantidade, indice + 1, entrada.Observacao);
        }).ToArray();
    }

    public static async Task<AgendamentoAtendimentoInterno?> ObterOrigemAsync(IAgendaAtendimentoConsulta agenda, Guid empresaId, Guid? id, CancellationToken ct) =>
        id.HasValue ? await agenda.ObterAsync(empresaId, id.Value, ct) ?? throw new RecursoNaoEncontradoException("Agendamento de origem não encontrado.") : null;
    public static async Task<Orcamento> ObterEntidadeAsync(IOrcamentosRepositorio repositorio, Guid id, CancellationToken ct) => await repositorio.ObterParaAlteracaoAsync(id, ct) ?? throw new RecursoNaoEncontradoException("Orçamento não encontrado.");
    public static async Task<EmpresaAtendimentoInterno> ObterEmpresaAsync(IPlataformaAtendimentoConsulta plataforma, Guid empresaId, CancellationToken ct) => await plataforma.ObterEmpresaAsync(empresaId, ct) ?? throw new RecursoNaoEncontradoException("Empresa não encontrada.");
    public static DateOnly HojeLocal(string fusoHorario) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(fusoHorario)));
    public static void ExecutarRegra(Action acao) { try { acao(); } catch (InvalidOperationException ex) { throw new ConflitoRegraNegocioException(ex.Message); } }

    public static async Task<OrcamentoDetalheVisualizacao> ObterDetalheAsync(Guid id, Guid empresaId, IOrcamentosRepositorio repositorio,
        IPlataformaAtendimentoConsulta? plataforma, CancellationToken ct)
    {
        var detalhe = await repositorio.ObterDetalheAsync(id, ct) ?? throw new RecursoNaoEncontradoException("Orçamento não encontrado.");
        DateOnly hoje;
        IReadOnlyDictionary<Guid, string> nomes = new Dictionary<Guid, string>();
        if (plataforma is not null)
        {
            var empresa = await ObterEmpresaAsync(plataforma, empresaId, ct);
            hoje = HojeLocal(empresa.FusoHorario);
            nomes = await plataforma.ObterNomesUsuariosAsync(empresaId, detalhe.Historico.Select(x => x.UsuarioId).Distinct().ToArray(), ct);
        }
        else hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var historico = detalhe.Historico.Select(x => new HistoricoStatusOrcamentoVisualizacao(x,
            nomes.TryGetValue(x.UsuarioId, out var nome) ? nome : "Usuário Detara")).ToArray();
        var efetivo = detalhe.Status == StatusOrcamento.Emitido && detalhe.ValidoAte < hoje ? StatusEfetivoOrcamento.Expirado : (StatusEfetivoOrcamento)(int)detalhe.Status;
        return new(detalhe, efetivo, historico);
    }
}
