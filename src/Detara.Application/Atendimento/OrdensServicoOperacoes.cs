using Detara.Application.Abstracoes;
using Detara.Domain.Atendimento;
using FluentValidation;
using MediatR;

namespace Detara.Application.Atendimento;

public sealed record ItemOrdemServicoEntrada(TipoItemOrcamento TipoItem, Guid? ItemCatalogoId, string? Nome,
    string? Descricao, decimal ValorUnitarioAutorizado, int Quantidade, string? ObservacaoAutorizacao);
public sealed record CriarOrdemServicoCommand(Guid? OrcamentoOrigemId, Guid? AgendamentoOrigemId, Guid? ClienteId,
    Guid? VeiculoId, int? DuracaoPlanejadaMinutos, decimal Desconto, decimal Acrescimo,
    string? ObservacaoAutorizacaoDireta, IReadOnlyCollection<ItemOrdemServicoEntrada> Itens) : IRequest<OrdemServicoDetalheVisualizacao>;
public sealed record ListarOrdensServicoQuery(int Pagina, int TamanhoPagina, StatusOrdemServico? Status,
    DateOnly? DataInicial, DateOnly? DataFinal, string? Pesquisa) : IRequest<PaginacaoResultado<OrdemServicoListaResultado>>;
public sealed record ObterOrdemServicoQuery(Guid Id) : IRequest<OrdemServicoDetalheVisualizacao>;
public sealed record RealizarCheckInCommand(Guid Id, int? QuilometragemEntrada, string? ObservacaoEntrada)
    : IRequest<OrdemServicoDetalheVisualizacao>;
public sealed record AtualizarChecklistOrdemServicoCommand(Guid Id, IReadOnlyCollection<RespostaChecklistSnapshot> Respostas)
    : IRequest<OrdemServicoDetalheVisualizacao>;
public sealed record TransicaoOrdemServicoCommand(Guid Id, string? Observacao) : IRequest<OrdemServicoDetalheVisualizacao>;
public sealed record CancelarOrdemServicoCommand(Guid Id, string Motivo) : IRequest<OrdemServicoDetalheVisualizacao>;
public sealed record EnviarFotoOrdemServicoCommand(Guid Id, CategoriaFotoOrdemServico Categoria, string NomeOriginal,
    long TamanhoBytes, Stream Conteudo) : IRequest<OrdemServicoFoto>;
public sealed record ObterFotoOrdemServicoQuery(Guid Id, Guid FotoId) : IRequest<ConteudoFotoOrdemServico>;
public sealed record ExcluirFotoOrdemServicoCommand(Guid Id, Guid FotoId) : IRequest;
public sealed record CriarOrcamentoAdicionalCommand(Guid Id, DateOnly ValidoAte, string? ObservacaoCliente,
    string? ObservacaoInterna, string? Condicoes, decimal Desconto, decimal Acrescimo,
    IReadOnlyCollection<ItemOrcamentoEntrada> Itens) : IRequest<OrcamentoDetalheVisualizacao>;
public sealed record AdicionarCortesiaOrdemServicoCommand(Guid Id, ItemOrdemServicoEntrada Item)
    : IRequest<OrdemServicoDetalheVisualizacao>;

public sealed record ConteudoFotoOrdemServico(Stream Conteudo, string ContentType, string NomeOriginal);
public sealed record OrdemServicoDetalheVisualizacao(OrdemServico OrdemServico,
    IReadOnlyCollection<Orcamento> OrcamentosAdicionais, IReadOnlyDictionary<Guid, string> Usuarios);

internal sealed class CriarOrdemServicoValidator : AbstractValidator<CriarOrdemServicoCommand>
{
    public CriarOrdemServicoValidator()
    {
        RuleFor(item => item).Must(item => !(item.OrcamentoOrigemId.HasValue && item.AgendamentoOrigemId.HasValue))
            .WithMessage("Informe somente uma origem para a ordem de serviço.");
        RuleFor(item => item.Desconto).GreaterThanOrEqualTo(0);
        RuleFor(item => item.Acrescimo).GreaterThanOrEqualTo(0);
        RuleFor(item => item.ObservacaoAutorizacaoDireta).MaximumLength(1000);
        RuleFor(item => item.Itens).NotEmpty().When(item => !item.OrcamentoOrigemId.HasValue);
        RuleForEach(item => item.Itens).SetValidator(new ItemOrdemServicoEntradaValidator());
    }
}
internal sealed class ItemOrdemServicoEntradaValidator : AbstractValidator<ItemOrdemServicoEntrada>
{
    public ItemOrdemServicoEntradaValidator()
    {
        RuleFor(item => item.TipoItem).IsInEnum();
        RuleFor(item => item.ValorUnitarioAutorizado).GreaterThanOrEqualTo(0);
        RuleFor(item => item.Quantidade).GreaterThanOrEqualTo(1);
        RuleFor(item => item.Nome).MaximumLength(160);
        RuleFor(item => item.Descricao).MaximumLength(2000);
        RuleFor(item => item.ObservacaoAutorizacao).MaximumLength(1000);
        RuleFor(item => item).Must(item => item.TipoItem == TipoItemOrcamento.Personalizado
            ? !item.ItemCatalogoId.HasValue && !string.IsNullOrWhiteSpace(item.Nome)
            : item.ItemCatalogoId.HasValue).WithMessage("Informe um item de catálogo ou os dados do item personalizado.");
    }
}
internal sealed class ListarOrdensServicoValidator : AbstractValidator<ListarOrdensServicoQuery>
{
    public ListarOrdensServicoValidator()
    {
        RuleFor(item => item.Pagina).GreaterThanOrEqualTo(1);
        RuleFor(item => item.TamanhoPagina).Must(valor => valor is 10 or 25 or 50);
        RuleFor(item => item.Status).IsInEnum().When(item => item.Status.HasValue);
        RuleFor(item => item.Pesquisa).MaximumLength(160);
        RuleFor(item => item).Must(item => !item.DataInicial.HasValue || !item.DataFinal.HasValue || item.DataInicial <= item.DataFinal)
            .WithMessage("O período informado é inválido.");
    }
}

internal sealed class CriarOrdemServicoHandler(IUsuarioContexto usuario, IOrdensServicoRepositorio ordens,
    IOrcamentosRepositorio orcamentos, IClientesAtendimentoConsulta clientes, ICatalogoAtendimentoConsulta catalogo,
    IAgendaAtendimentoConsulta agenda, IPlataformaAtendimentoConsulta plataforma)
    : IRequestHandler<CriarOrdemServicoCommand, OrdemServicoDetalheVisualizacao>
{
    public async Task<OrdemServicoDetalheVisualizacao> Handle(CriarOrdemServicoCommand request, CancellationToken ct)
    {
        var empresa = await OrcamentoFluxo.ObterEmpresaAsync(plataforma, usuario.EmpresaId, ct);
        var ano = OrcamentoFluxo.HojeLocal(empresa.FusoHorario).Year;
        OrdemServico entidade;
        if (request.OrcamentoOrigemId.HasValue)
        {
            var orcamento = await orcamentos.ObterDetalheAsync(request.OrcamentoOrigemId.Value, ct)
                ?? throw new RecursoNaoEncontradoException("Orçamento não encontrado.");
            if (orcamento.Status != StatusOrcamento.Aprovado)
                throw new ConflitoRegraNegocioException("Somente um orçamento aprovado pode originar uma ordem de serviço.");
            if (orcamento.OrdemServicoOrigemId.HasValue)
                throw new ConflitoRegraNegocioException("Um orçamento adicional não pode originar outra ordem de serviço.");
            if (await ordens.ExistePorOrcamentoAsync(orcamento.Id, ct))
                throw new ConflitoRegraNegocioException("Este orçamento já possui uma ordem de serviço.");
            var partes = new PartesOrdemServicoSnapshot(orcamento.ClienteId, orcamento.ClienteNome,
                orcamento.ClienteDocumento, orcamento.ClienteTelefone, orcamento.VeiculoId,
                orcamento.VeiculoDescricao, orcamento.VeiculoPlaca);
            var autorizadoEm = orcamento.AprovadoEmUtc ?? throw new ConflitoRegraNegocioException("O orçamento aprovado não possui data de autorização.");
            var autorizadoPor = orcamento.AprovadoPorUsuarioId ?? throw new ConflitoRegraNegocioException("O orçamento aprovado não possui responsável pela autorização.");
            var itens = orcamento.Itens.Select(item => new ItemOrdemServicoSnapshot(item.TipoItem,
                item.ItemCatalogoId, orcamento.Id, item.Id, item.Nome, item.Descricao, item.ValorUnitario,
                item.Quantidade, item.Ordem, OrigemComercialOrdemServico.Orcamento, autorizadoEm,
                autorizadoPor, item.Observacao)).ToArray();
            entidade = new OrdemServico(usuario.EmpresaId, ano, partes, OrigemOrdemServico.Orcamento,
                orcamento.Id, null, null, orcamento.Desconto, orcamento.Acrescimo, itens, usuario.UsuarioId);
        }
        else
        {
            if (!request.ClienteId.HasValue || !request.VeiculoId.HasValue)
                throw new ConflitoRegraNegocioException("Cliente e veículo devem ser informados.");
            var agendamento = await OrcamentoFluxo.ObterOrigemAsync(agenda, usuario.EmpresaId, request.AgendamentoOrigemId, ct);
            var partesOrcamento = await OrcamentoFluxo.PrepararPartesAsync(clientes, usuario.EmpresaId,
                request.ClienteId.Value, request.VeiculoId.Value, agendamento, ct);
            var itensOrcamento = await OrcamentoFluxo.PrepararItensAsync(catalogo, usuario.EmpresaId,
                request.Itens.Select(item => new ItemOrcamentoEntrada(item.TipoItem, item.ItemCatalogoId, item.Nome,
                    item.Descricao, item.ValorUnitarioAutorizado, item.Quantidade, item.ObservacaoAutorizacao)).ToArray(),
                agendamento?.Itens, [], ct);
            var agora = DateTime.UtcNow;
            var itens = itensOrcamento.Select(item => new ItemOrdemServicoSnapshot(item.TipoItem, item.ItemCatalogoId,
                null, null, item.Nome, item.Descricao, item.ValorUnitario, item.Quantidade, item.Ordem,
                OrigemComercialOrdemServico.AcordoDireto, agora, usuario.UsuarioId, item.Observacao)).ToArray();
            var partes = new PartesOrdemServicoSnapshot(partesOrcamento.ClienteId, partesOrcamento.ClienteNome,
                partesOrcamento.ClienteDocumento, partesOrcamento.ClienteTelefone, partesOrcamento.VeiculoId,
                partesOrcamento.VeiculoDescricao, partesOrcamento.VeiculoPlaca);
            entidade = new OrdemServico(usuario.EmpresaId, ano, partes,
                request.AgendamentoOrigemId.HasValue ? OrigemOrdemServico.Agendamento : OrigemOrdemServico.AtendimentoDireto,
                null, request.AgendamentoOrigemId, request.DuracaoPlanejadaMinutos ?? agendamento?.DuracaoPlanejadaMinutos,
                request.Desconto, request.Acrescimo, itens, usuario.UsuarioId, agora,
                request.ObservacaoAutorizacaoDireta);
        }
        ordens.Adicionar(entidade);
        await ordens.SalvarAsync(ct);
        return await OrdemServicoFluxo.ObterDetalheAsync(entidade.Id, usuario.EmpresaId, ordens, plataforma, ct);
    }
}

internal sealed class ListarOrdensServicoHandler(IOrdensServicoRepositorio repositorio)
    : IRequestHandler<ListarOrdensServicoQuery, PaginacaoResultado<OrdemServicoListaResultado>>
{
    public Task<PaginacaoResultado<OrdemServicoListaResultado>> Handle(ListarOrdensServicoQuery request, CancellationToken ct) =>
        repositorio.ListarAsync(new(request.Pagina, request.TamanhoPagina, request.Status, request.DataInicial,
            request.DataFinal, request.Pesquisa), ct);
}
internal sealed class ObterOrdemServicoHandler(IUsuarioContexto usuario, IOrdensServicoRepositorio repositorio,
    IPlataformaAtendimentoConsulta plataforma) : IRequestHandler<ObterOrdemServicoQuery, OrdemServicoDetalheVisualizacao>
{
    public Task<OrdemServicoDetalheVisualizacao> Handle(ObterOrdemServicoQuery request, CancellationToken ct) =>
        OrdemServicoFluxo.ObterDetalheAsync(request.Id, usuario.EmpresaId, repositorio, plataforma, ct);
}

internal sealed class RealizarCheckInHandler(IUsuarioContexto usuario, IOrdensServicoRepositorio ordens,
    IConfiguracoesOperacionaisRepositorio configuracoes, IPlataformaAtendimentoConsulta plataforma)
    : IRequestHandler<RealizarCheckInCommand, OrdemServicoDetalheVisualizacao>
{
    public async Task<OrdemServicoDetalheVisualizacao> Handle(RealizarCheckInCommand request, CancellationToken ct)
    {
        var ordem = await OrdemServicoFluxo.ExigirAsync(ordens, request.Id, true, ct);
        var configuracao = await configuracoes.ObterConfiguracaoAsync(false, ct);
        var checklist = await configuracoes.ObterChecklistAsync(false, ct);
        var snapshot = new ConfiguracaoCheckInSnapshot(
            configuracao?.ChecklistEntrada ?? NivelExigenciaOperacional.Desabilitado,
            configuracao?.FotosEntrada ?? NivelExigenciaOperacional.Desabilitado,
            configuracao?.FotosSaida ?? NivelExigenciaOperacional.Desabilitado,
            checklist?.Nome,
            checklist?.Itens.OrderBy(item => item.Ordem).Select(item => item.Descricao).ToArray() ?? []);
        OrdemServicoFluxo.ExecutarRegra(() => ordem.RealizarCheckIn(snapshot, request.QuilometragemEntrada,
            request.ObservacaoEntrada, usuario.UsuarioId));
        if (ordem.Checklist is not null) ordens.AdicionarChecklist(ordem.Checklist);
        await ordens.SalvarAsync(ct);
        return await OrdemServicoFluxo.ObterDetalheAsync(ordem.Id, usuario.EmpresaId, ordens, plataforma, ct);
    }
}

internal sealed class AtualizarChecklistOrdemServicoHandler(IUsuarioContexto usuario, IOrdensServicoRepositorio ordens,
    IPlataformaAtendimentoConsulta plataforma)
    : IRequestHandler<AtualizarChecklistOrdemServicoCommand, OrdemServicoDetalheVisualizacao>
{
    public async Task<OrdemServicoDetalheVisualizacao> Handle(AtualizarChecklistOrdemServicoCommand request, CancellationToken ct)
    {
        var ordem = await OrdemServicoFluxo.ExigirAsync(ordens, request.Id, true, ct);
        OrdemServicoFluxo.ExecutarRegra(() => ordem.AtualizarChecklist(request.Respostas));
        await ordens.SalvarAsync(ct);
        return await OrdemServicoFluxo.ObterDetalheAsync(ordem.Id, usuario.EmpresaId, ordens, plataforma, ct);
    }
}

internal abstract class TransicaoOrdemServicoHandlerBase(IUsuarioContexto usuario, IOrdensServicoRepositorio ordens,
    IPlataformaAtendimentoConsulta plataforma)
{
    protected async Task<OrdemServicoDetalheVisualizacao> Executar(Guid id, string? observacao,
        Action<OrdemServico, Guid, string?> acao, CancellationToken ct)
    {
        var ordem = await OrdemServicoFluxo.ExigirAsync(ordens, id, true, ct);
        OrdemServicoFluxo.ExecutarRegra(() => acao(ordem, usuario.UsuarioId, observacao));
        ordens.AdicionarUltimoHistorico(ordem);
        await ordens.SalvarAsync(ct);
        return await OrdemServicoFluxo.ObterDetalheAsync(id, usuario.EmpresaId, ordens, plataforma, ct);
    }
}
internal sealed class IniciarExecucaoHandler(IUsuarioContexto usuario, IOrdensServicoRepositorio ordens,
    IPlataformaAtendimentoConsulta plataforma) : TransicaoOrdemServicoHandlerBase(usuario, ordens, plataforma),
    IRequestHandler<TransicaoOrdemServicoCommand, OrdemServicoDetalheVisualizacao>
{
    public Task<OrdemServicoDetalheVisualizacao> Handle(TransicaoOrdemServicoCommand request, CancellationToken ct) =>
        Executar(request.Id, request.Observacao, (ordem, usuarioId, obs) => ordem.IniciarExecucao(usuarioId, obs), ct);
}
internal sealed class FinalizarExecucaoHandler(IUsuarioContexto usuario, IOrdensServicoRepositorio ordens,
    IPlataformaAtendimentoConsulta plataforma) : TransicaoOrdemServicoHandlerBase(usuario, ordens, plataforma),
    IRequestHandler<FinalizarExecucaoOrdemServicoCommand, OrdemServicoDetalheVisualizacao>
{
    public Task<OrdemServicoDetalheVisualizacao> Handle(FinalizarExecucaoOrdemServicoCommand request, CancellationToken ct) =>
        Executar(request.Id, request.Observacao, (ordem, usuarioId, obs) => ordem.FinalizarExecucao(usuarioId, obs), ct);
}
public sealed record FinalizarExecucaoOrdemServicoCommand(Guid Id, string? Observacao) : IRequest<OrdemServicoDetalheVisualizacao>;
public sealed record ConcluirOrdemServicoCommand(Guid Id, string? Observacao) : IRequest<OrdemServicoDetalheVisualizacao>;
internal sealed class ConcluirOrdemServicoHandler(IUsuarioContexto usuario, IOrdensServicoRepositorio ordens,
    IPlataformaAtendimentoConsulta plataforma) : TransicaoOrdemServicoHandlerBase(usuario, ordens, plataforma),
    IRequestHandler<ConcluirOrdemServicoCommand, OrdemServicoDetalheVisualizacao>
{
    public Task<OrdemServicoDetalheVisualizacao> Handle(ConcluirOrdemServicoCommand request, CancellationToken ct) =>
        Executar(request.Id, request.Observacao, (ordem, usuarioId, obs) => ordem.Concluir(usuarioId, obs), ct);
}
internal sealed class CancelarOrdemServicoHandler(IUsuarioContexto usuario, IOrdensServicoRepositorio ordens,
    IPlataformaAtendimentoConsulta plataforma) : IRequestHandler<CancelarOrdemServicoCommand, OrdemServicoDetalheVisualizacao>
{
    public async Task<OrdemServicoDetalheVisualizacao> Handle(CancelarOrdemServicoCommand request, CancellationToken ct)
    {
        var ordem = await OrdemServicoFluxo.ExigirAsync(ordens, request.Id, true, ct);
        OrdemServicoFluxo.ExecutarRegra(() => ordem.Cancelar(usuario.UsuarioId, request.Motivo));
        ordens.AdicionarUltimoHistorico(ordem);
        await ordens.SalvarAsync(ct);
        return await OrdemServicoFluxo.ObterDetalheAsync(request.Id, usuario.EmpresaId, ordens, plataforma, ct);
    }
}

internal sealed class EnviarFotoOrdemServicoHandler(IUsuarioContexto usuario, IOrdensServicoRepositorio ordens,
    IArquivoStorage storage) : IRequestHandler<EnviarFotoOrdemServicoCommand, OrdemServicoFoto>
{
    public async Task<OrdemServicoFoto> Handle(EnviarFotoOrdemServicoCommand request, CancellationToken ct)
    {
        var ordem = await OrdemServicoFluxo.ExigirAsync(ordens, request.Id, true, ct);
        OrdemServicoFluxo.ExecutarRegra(() => ordem.ValidarInclusaoFoto(request.Categoria));
        var imagem = await ValidadorArquivoImagem.ValidarAsync(request.Conteudo, request.TamanhoBytes, ct);
        var categoria = request.Categoria.ToString().ToLowerInvariant();
        var chave = $"empresas/{usuario.EmpresaId:N}/atendimentos/ordens-servico/{ordem.Id:N}/{categoria}/{Guid.NewGuid():N}.{imagem.Extensao}";
        var nome = OrdemServicoFluxo.NormalizarNomeArquivo(request.NomeOriginal);
        await storage.SalvarAsync(chave, imagem.Conteudo, ct);
        try
        {
            var foto = new OrdemServicoFoto(usuario.EmpresaId, ordem.Id, request.Categoria, chave, nome,
                imagem.ContentType, request.TamanhoBytes, usuario.UsuarioId);
            ordem.AdicionarFoto(foto);
            ordens.AdicionarFoto(foto);
            await ordens.SalvarAsync(ct);
            return foto;
        }
        catch (Exception persistencia)
        {
            try { await storage.ExcluirAsync(chave, CancellationToken.None); }
            catch (Exception limpeza) { throw new AggregateException("Falha ao persistir a foto e remover o arquivo salvo.", persistencia, limpeza); }
            throw;
        }
    }
}
internal sealed class ObterFotoOrdemServicoHandler(IOrdensServicoRepositorio ordens, IArquivoStorage storage)
    : IRequestHandler<ObterFotoOrdemServicoQuery, ConteudoFotoOrdemServico>
{
    public async Task<ConteudoFotoOrdemServico> Handle(ObterFotoOrdemServicoQuery request, CancellationToken ct)
    {
        var ordem = await OrdemServicoFluxo.ExigirAsync(ordens, request.Id, false, ct);
        var foto = ordem.Fotos.SingleOrDefault(item => item.Id == request.FotoId)
            ?? throw new RecursoNaoEncontradoException("Foto não encontrada.");
        var conteudo = await storage.AbrirLeituraAsync(foto.ChaveStorage, ct)
            ?? throw new RecursoNaoEncontradoException("O conteúdo da foto não foi encontrado.");
        return new(conteudo, foto.ContentType, foto.NomeOriginal);
    }
}
internal sealed class ExcluirFotoOrdemServicoHandler(IOrdensServicoRepositorio ordens, IArquivoStorage storage)
    : IRequestHandler<ExcluirFotoOrdemServicoCommand>
{
    public async Task Handle(ExcluirFotoOrdemServicoCommand request, CancellationToken ct)
    {
        var ordem = await OrdemServicoFluxo.ExigirAsync(ordens, request.Id, true, ct);
        OrdemServicoFoto? foto = null;
        OrdemServicoFluxo.ExecutarRegra(() => foto = ordem.RemoverFoto(request.FotoId));
        ordens.RemoverFoto(foto!);
        await ordens.SalvarAsync(ct);
        if (!await storage.ExcluirAsync(foto!.ChaveStorage, CancellationToken.None))
            throw new IOException("Os metadados foram removidos, mas o arquivo físico não foi encontrado.");
    }
}

internal sealed class CriarOrcamentoAdicionalHandler(IUsuarioContexto usuario, IOrdensServicoRepositorio ordens,
    IOrcamentosRepositorio orcamentos, ICatalogoAtendimentoConsulta catalogo, IPlataformaAtendimentoConsulta plataforma)
    : IRequestHandler<CriarOrcamentoAdicionalCommand, OrcamentoDetalheVisualizacao>
{
    public async Task<OrcamentoDetalheVisualizacao> Handle(CriarOrcamentoAdicionalCommand request, CancellationToken ct)
    {
        var ordem = await OrdemServicoFluxo.ExigirAsync(ordens, request.Id, false, ct);
        if (ordem.Status != StatusOrdemServico.EmExecucao)
            throw new ConflitoRegraNegocioException("Orçamentos adicionais só podem ser criados durante a execução.");
        var itens = await OrcamentoFluxo.PrepararItensAsync(catalogo, usuario.EmpresaId, request.Itens, null, [], ct);
        var partes = new PartesOrcamentoSnapshot(ordem.ClienteId, ordem.ClienteNomeSnapshot,
            ordem.ClienteDocumentoSnapshot, ordem.ClienteTelefoneSnapshot, ordem.VeiculoId,
            ordem.VeiculoDescricaoSnapshot, ordem.VeiculoPlacaSnapshot);
        var adicional = new Orcamento(usuario.EmpresaId, partes, null, null, request.ValidoAte,
            request.ObservacaoCliente, request.ObservacaoInterna, request.Condicoes, request.Desconto,
            request.Acrescimo, itens, usuario.UsuarioId, ordem.Id);
        orcamentos.Adicionar(adicional);
        await orcamentos.SalvarAsync(ct);
        return await OrcamentoFluxo.ObterDetalheAsync(adicional.Id, usuario.EmpresaId, orcamentos, plataforma, ct);
    }
}

internal sealed class AdicionarCortesiaOrdemServicoHandler(IUsuarioContexto usuario, IOrdensServicoRepositorio ordens,
    ICatalogoAtendimentoConsulta catalogo, IPlataformaAtendimentoConsulta plataforma)
    : IRequestHandler<AdicionarCortesiaOrdemServicoCommand, OrdemServicoDetalheVisualizacao>
{
    public async Task<OrdemServicoDetalheVisualizacao> Handle(AdicionarCortesiaOrdemServicoCommand request, CancellationToken ct)
    {
        if (request.Item.ValorUnitarioAutorizado != 0)
            throw new ConflitoRegraNegocioException("Adicionais cobrados exigem um orçamento complementar.");
        var ordem = await OrdemServicoFluxo.ExigirAsync(ordens, request.Id, true, ct);
        var preparado = (await OrcamentoFluxo.PrepararItensAsync(catalogo, usuario.EmpresaId,
            [new ItemOrcamentoEntrada(request.Item.TipoItem, request.Item.ItemCatalogoId, request.Item.Nome,
                request.Item.Descricao, 0, request.Item.Quantidade, request.Item.ObservacaoAutorizacao)], null, [], ct)).Single();
        var item = new ItemOrdemServicoSnapshot(preparado.TipoItem, preparado.ItemCatalogoId, null, null,
            preparado.Nome, preparado.Descricao, 0, preparado.Quantidade, ordem.Itens.Count + 1,
            OrigemComercialOrdemServico.Cortesia, DateTime.UtcNow, usuario.UsuarioId,
            request.Item.ObservacaoAutorizacao);
        OrdemServicoFluxo.ExecutarRegra(() => ordem.AdicionarCortesia(item));
        ordens.AdicionarItens([ordem.Itens.OrderBy(item => item.Ordem).Last()]);
        await ordens.SalvarAsync(ct);
        return await OrdemServicoFluxo.ObterDetalheAsync(ordem.Id, usuario.EmpresaId, ordens, plataforma, ct);
    }
}

internal static class OrdemServicoFluxo
{
    public static async Task<OrdemServico> ExigirAsync(IOrdensServicoRepositorio repositorio, Guid id,
        bool paraAlteracao, CancellationToken ct) => await repositorio.ObterAsync(id, paraAlteracao, ct)
        ?? throw new RecursoNaoEncontradoException("Ordem de serviço não encontrada.");
    public static void ExecutarRegra(Action acao)
    {
        try { acao(); }
        catch (InvalidOperationException excecao) { throw new ConflitoRegraNegocioException(excecao.Message); }
    }
    public static async Task<OrdemServicoDetalheVisualizacao> ObterDetalheAsync(Guid id, Guid empresaId,
        IOrdensServicoRepositorio repositorio, IPlataformaAtendimentoConsulta plataforma, CancellationToken ct)
    {
        var ordem = await ExigirAsync(repositorio, id, false, ct);
        var adicionais = await repositorio.ListarOrcamentosAdicionaisAsync(id, ct);
        var usuariosIds = ordem.Historico.Select(item => item.UsuarioId)
            .Concat(ordem.Itens.Select(item => item.AutorizadoPorUsuarioId))
            .Concat(ordem.Fotos.Select(item => item.EnviadaPorUsuarioId)).Distinct().ToArray();
        var usuarios = await plataforma.ObterNomesUsuariosAsync(empresaId, usuariosIds, ct);
        return new(ordem, adicionais, usuarios);
    }
    public static string NormalizarNomeArquivo(string nome)
    {
        var segmento = nome.Replace('\\', '/').Split('/').LastOrDefault()?.Trim();
        var seguro = new string((segmento ?? string.Empty).Where(caractere => !char.IsControl(caractere)).ToArray());
        if (string.IsNullOrWhiteSpace(seguro)) return "foto";
        return seguro.Length <= 255 ? seguro : seguro[..255];
    }
}
