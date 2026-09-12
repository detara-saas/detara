using Detara.Application.Atendimento;
using Detara.Application.Abstracoes;
using Detara.Domain.Agenda;
using Detara.Domain.Atendimento;
using Detara.Domain.Catalogo;
using Detara.Infrastructure.Atendimento;
using Detara.Infrastructure.Agenda;
using Detara.Infrastructure.Persistencia;
using Detara.Infrastructure.Plataforma;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.Atendimento;

public sealed partial class OrcamentosPersistenciaTests
{
    [Fact]
    public async Task Flow02_CaminhosEquivalentes_PreservamSnapshotCompletoEAjustesUmaVez()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var pacote = await c.Pacotes.SingleAsync();
        var comando = Comando(_clienteA, _veiculoA, _servicoA, 120m) with
        {
            Desconto = 30m,
            Acrescimo = 10m,
            Itens = [new(TipoItemOrcamento.Servico, _servicoA, null, null, 120m, 2, "Lavagem negociada"),
                new(TipoItemOrcamento.Pacote, pacote.Id, null, null, 90m, 3, "Pacote negociado"),
                new(TipoItemOrcamento.Personalizado, null, "Tratamento especial", "Descrição aprovada", 20m, 2, "Condição do item")]
        };
        var ordens = new List<OrdemServico>();
        foreach (var direto in new[] { true, false })
        {
            var criado = await CriarHandler(c).Handle(comando, default);
            await AprovarFlow02Async(c, criado.Orcamento.Id);
            var agendamento = await AgendarHandler(c).Handle(new(criado.Orcamento.Id,
                new DateTime(2026, 10, 1, 9, 0, 0), 90, null, null), default);
            c.ChangeTracker.Clear();
            Assert.Equal(criado.Orcamento.Id, await OrigemFlow02(c, agendamento.Id));
            // Sem ID nem itens: backend resolve a fonte aprovada, não depende do formulário.
            var request = new CriarOrdemServicoCommand(direto ? criado.Orcamento.Id : null,
                agendamento.Id, null, null, null, 999m, 888m, "Não substituir aprovação", []);
            Assert.True((await new CriarOrdemServicoValidator().ValidateAsync(request)).IsValid);
            var ordem = (await CriarOrdemServicoHandler(c).Handle(request, default)).OrdemServico;
            Assert.Equal(530m, ordem.TotalAutorizado);
            Assert.Equal(30m, ordem.DescontoAutorizado);
            Assert.Equal(10m, ordem.AcrescimoAutorizado);
            Assert.Null(ordem.AutorizacaoDiretaEmUtc);
            var aprovado = (await new OrcamentosRepositorio(c).ObterDetalheAsync(criado.Orcamento.Id, default))!;
            foreach (var item in ordem.Itens)
            {
                var original = aprovado.Itens.Single(x => x.Id == item.OrcamentoItemOrigemId);
                Assert.Equal((original.TipoItem, original.ItemCatalogoId, original.Nome, original.Descricao,
                    original.ValorUnitario, original.Quantidade, original.Ordem, original.Observacao),
                    (item.TipoItem, item.ItemCatalogoId, item.NomeSnapshot, item.DescricaoSnapshot,
                    item.ValorUnitarioAutorizado, item.Quantidade, item.Ordem, item.ObservacaoAutorizacao));
                Assert.Equal(aprovado.AprovadoEmUtc, item.AutorizadoEmUtc);
                Assert.Equal(aprovado.AprovadoPorUsuarioId, item.AutorizadoPorUsuarioId);
            }
            ordens.Add(ordem);
            c.ChangeTracker.Clear();
            await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => CriarOrdemServicoHandler(c).Handle(request, default));
            await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => CriarOrdemServicoHandler(c).Handle(
                request with { OrcamentoOrigemId = criado.Orcamento.Id }, default));
        }
        Assert.Equal(ordens[0].Itens.OrderBy(x => x.Ordem).Select(x => (x.TipoItem, x.NomeSnapshot, x.ValorUnitarioAutorizado, x.Quantidade, x.Subtotal)),
            ordens[1].Itens.OrderBy(x => x.Ordem).Select(x => (x.TipoItem, x.NomeSnapshot, x.ValorUnitarioAutorizado, x.Quantidade, x.Subtotal)));
        Assert.Equal(2, await c.OrdensServico.CountAsync());
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(StatusOrcamento.Rascunho, false)]
    [InlineData(StatusOrcamento.Emitido, false)]
    [InlineData(StatusOrcamento.Emitido, true)]
    [InlineData(StatusOrcamento.Recusado, false)]
    [InlineData(StatusOrcamento.Cancelado, false)]
    [InlineData(StatusOrcamento.Substituido, false)]
    public async Task Flow02_SemAprovado_MantemAcordoDireto(StatusOrcamento? status, bool expirado)
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        if (status.HasValue)
        {
            var quote = new Orcamento(_empresaA, PartesA(), _agendamentoA, null,
                DateOnly.FromDateTime(DateTime.UtcNow).AddDays(expirado ? -1 : 7), null, null, null, 0, 0,
                [ItemSnapshot(_servicoA, 120m)], _usuarioA);
            if (status != StatusOrcamento.Rascunho) quote.Emitir(2026, _usuarioA);
            if (status == StatusOrcamento.Recusado) quote.Recusar(_usuarioA, "Não aprovado");
            if (status == StatusOrcamento.Cancelado) quote.Cancelar(_usuarioA, "Cancelado");
            if (status == StatusOrcamento.Substituido) quote.MarcarSubstituido(_usuarioA, "Substituído");
            c.Orcamentos.Add(quote);
            await c.SaveChangesAsync();
            // Emitido com validade passada também cobre o status efetivo Expirado.
        }
        c.ChangeTracker.Clear();
        Assert.Null(await OrigemFlow02(c, _agendamentoA));
        var ordem = (await CriarOrdemServicoHandler(c).Handle(AcordoFlow02(), default)).OrdemServico;
        Assert.Equal(150m, ordem.TotalAutorizado);
        Assert.Null(ordem.OrcamentoOrigemId);
        Assert.Equal(OrigemOrdemServico.Agendamento, ordem.Origem);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Flow02_MultiplosAprovados_ConflitoSemPersistenciaMesmoComIdExplicito(bool explicito)
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var primeiro = await CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, _servicoA, 120m, _agendamentoA), default);
        await AprovarFlow02Async(c, primeiro.Orcamento.Id);
        var segundo = await CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, _servicoA, 90m, _agendamentoA), default);
        await AprovarFlow02Async(c, segundo.Orcamento.Id);
        var erro = await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => CriarOrdemServicoHandler(c)
            .Handle(AcordoFlow02() with { OrcamentoOrigemId = explicito ? primeiro.Orcamento.Id : null }, default));
        Assert.Contains("mais de um orçamento aprovado", erro.Message);
        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => OrigemFlow02(c, _agendamentoA));
        Assert.Empty(await c.OrdensServico.ToArrayAsync());
        Assert.Empty(await c.OrdensServicoItens.ToArrayAsync());
    }

    [Fact]
    public async Task Flow02_Revisao_UsaAprovadoAtualENaoRascunhoOuSubstituido()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var primeiro = await CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, _servicoA, 120m, _agendamentoA), default);
        await AprovarFlow02Async(c, primeiro.Orcamento.Id);
        var nova = await NovaHandler(c).Handle(new(primeiro.Orcamento.Id), default);
        c.ChangeTracker.Clear();
        Assert.Equal(primeiro.Orcamento.Id, await OrigemFlow02(c, _agendamentoA));
        await AprovarFlow02Async(c, nova.Orcamento.Id);
        Assert.Equal(nova.Orcamento.Id, await OrigemFlow02(c, _agendamentoA));
        Assert.Equal(StatusOrcamento.Substituido, (await c.Orcamentos.SingleAsync(x => x.Id == primeiro.Orcamento.Id)).Status);
        var ordem = (await CriarOrdemServicoHandler(c).Handle(AcordoFlow02(), default)).OrdemServico;
        Assert.Equal(nova.Orcamento.Id, ordem.OrcamentoOrigemId);
    }

    [Fact]
    public async Task Flow02_OutroTenant_NaoSelecionaNemAceitaIdENaoPersisteParcial()
    {
        await using (var b = Contexto(_empresaB, _usuarioB))
        {
            var quote = await b.Orcamentos.Include(x => x.Itens).Include(x => x.Historico).SingleAsync(x => x.Id == _orcamentoB);
            // Simula vínculo cross-tenant corrompido: filtros devem continuar protegendo a seleção.
            quote.VincularAgendamento(_agendamentoA);
            quote.Emitir(2026, _usuarioB);
            new OrcamentosRepositorio(b).AdicionarUltimoHistorico(quote);
            quote.Aprovar(DateOnly.FromDateTime(DateTime.UtcNow), _usuarioB, null);
            new OrcamentosRepositorio(b).AdicionarUltimoHistorico(quote);
            await b.SaveChangesAsync();
        }
        await using var c = Contexto(_empresaA, _usuarioA);
        Assert.Null(await OrigemFlow02(c, _agendamentoA));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => OrigemFlow02(c, _agendamentoB));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => CriarOrdemServicoHandler(c).Handle(
            AcordoFlow02() with { OrcamentoOrigemId = _orcamentoB }, default));
        Assert.Empty(await c.OrdensServico.ToArrayAsync());
        Assert.Empty(await c.OrdensServicoItens.ToArrayAsync());
    }

    [Fact]
    public async Task Flow02_SemAprovadoEItens_RetornaValidacaoPadrao()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var erro = await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => CriarOrdemServicoHandler(c)
            .Handle(AcordoFlow02() with { Itens = [] }, default));
        Assert.Equal("Itens", Assert.Single(erro.Errors).PropertyName);
        Assert.Empty(await c.OrdensServico.ToArrayAsync());
    }

    [Fact]
    public async Task Flow02_PartesDivergentes_NaoCriaOsParcial()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var quote = new Orcamento(_empresaA, PartesA() with { VeiculoId = _veiculoA2 }, _agendamentoA, null,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7), null, null, null, 0, 0,
            [ItemSnapshot(_servicoA, 120m)], _usuarioA);
        quote.Emitir(2026, _usuarioA);
        quote.Aprovar(DateOnly.FromDateTime(DateTime.UtcNow), _usuarioA, null);
        c.Orcamentos.Add(quote);
        await c.SaveChangesAsync();
        c.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => CriarOrdemServicoHandler(c).Handle(AcordoFlow02(), default));
        Assert.Empty(await c.OrdensServico.ToArrayAsync());
        Assert.Empty(await c.OrdensServicoItens.ToArrayAsync());
    }

    private CriarOrdemServicoCommand AcordoFlow02() => new(null, _agendamentoA, null, null, null, 0, 0, null,
        [new(TipoItemOrcamento.Servico, _servicoA, null, null, 150m, 1, null)]);

    private Task<Guid?> OrigemFlow02(DetaraDbContext c, Guid agendamentoId) =>
        new ObterOrigemComercialOrdemServicoHandler(new UsuarioContextoTeste(_empresaA, _usuarioA),
            new AgendaAtendimentoIntegracao(c), new OrcamentosRepositorio(c)).Handle(new(agendamentoId), default);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Flow02_AgendamentoComAprovado_Preserva120MesmoComCatalogo200(bool peloOrcamento)
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var servico = await c.Servicos.SingleAsync(x => x.Id == _servicoA);
        servico.Atualizar(servico.CategoriaServicoId, servico.Nome, null, TipoPrecificacao.Fixo, 150m, 90, 1);
        var agenda = new Agendamento(_empresaA, _clienteA, "João da Silva", _veiculoA, "Honda Civic", "ABC1D23",
            DateTime.UtcNow.AddDays(1), 90, null, null,
            [new(TipoItemAgendamento.Servico, _servicoA, servico.Nome, null, TipoPrecificacao.Fixo, 150m, 90)]);
        c.Agendamentos.Add(agenda);
        await c.SaveChangesAsync();
        var criado = await CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, _servicoA, 120m, agenda.Id), default);
        await AprovarFlow02Async(c, criado.Orcamento.Id);
        servico = await c.Servicos.SingleAsync(x => x.Id == _servicoA);
        servico.Atualizar(servico.CategoriaServicoId, "Catálogo alterado", null, TipoPrecificacao.Fixo, 200m, 90, 1);
        await c.SaveChangesAsync();
        c.ChangeTracker.Clear();

        var resultado = await CriarOrdemServicoHandler(c).Handle(new(peloOrcamento ? criado.Orcamento.Id : null,
            agenda.Id, null, null, null, 0, 0, null,
            peloOrcamento ? [] : [new(TipoItemOrcamento.Servico, _servicoA, null, null, 150m, 1, null)]), default);
        c.ChangeTracker.Clear();
        var ordem = await c.OrdensServico.Include(x => x.Itens).SingleAsync(x => x.Id == resultado.OrdemServico.Id);
        Assert.Equal(120m, ordem.TotalAutorizado);
        Assert.Equal(criado.Orcamento.Id, ordem.OrcamentoOrigemId);
        Assert.Equal(agenda.Id, ordem.AgendamentoOrigemId);
        Assert.Equal(OrigemOrdemServico.Orcamento, ordem.Origem);
        Assert.Equal("Lavagem técnica", Assert.Single(ordem.Itens).NomeSnapshot);
    }

    private async Task AprovarFlow02Async(DetaraDbContext c, Guid id)
    {
        c.ChangeTracker.Clear();
        await EmitirHandler(c).Handle(new(id, null), default);
        c.ChangeTracker.Clear();
        await new AprovarOrcamentoHandler(new UsuarioContextoTeste(_empresaA, _usuarioA),
            new OrcamentosRepositorio(c), new PlataformaAtendimentoConsulta(c), new OrdensServicoRepositorio(c))
            .Handle(new(id, "QA FLOW-02"), default);
        c.ChangeTracker.Clear();
    }
}
