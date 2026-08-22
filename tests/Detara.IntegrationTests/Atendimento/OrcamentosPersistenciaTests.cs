using Detara.Application.Abstracoes;
using Detara.Application.Atendimento;
using Detara.Application.Agenda;
using Detara.Application.FluxoOperacional;
using Detara.Domain.Agenda;
using Detara.Domain.Atendimento;
using Detara.Domain.Catalogo;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Agenda;
using Detara.Infrastructure.Atendimento;
using Detara.Infrastructure.Catalogo;
using Detara.Infrastructure.Clientes;
using Detara.Infrastructure.Persistencia;
using Detara.Infrastructure.Plataforma;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.Atendimento;

public sealed class OrcamentosPersistenciaTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<DetaraDbContext> _options = null!;
    private Guid _empresaA; private Guid _empresaB; private Guid _clienteA; private Guid _veiculoA; private Guid _veiculoA2; private Guid _servicoA;
    private Guid _clienteSemPlaca; private Guid _veiculoSemPlaca;
    private Guid _agendamentoA; private Guid _clienteB; private Guid _veiculoB; private Guid _servicoB; private Guid _pacoteB; private Guid _agendamentoB; private Guid _orcamentoB;
    private readonly Guid _usuarioA = Guid.NewGuid(); private readonly Guid _usuarioB = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(_connection).Options;
        var empresaA = new Empresa("Estética A", "Estética A Ltda", "11111111000111", "estetica-a", "a@teste.local", "11999999999");
        var empresaB = new Empresa("Estética B", "Estética B Ltda", "22222222000122", "estetica-b");
        _empresaA = empresaA.Id; _empresaB = empresaB.Id;
        await using (var sistema = new DetaraDbContext(_options, UsuarioContextoTeste.Anonimo))
        { await sistema.Database.EnsureCreatedAsync(); sistema.Empresas.AddRange(empresaA, empresaB); await sistema.SaveChangesAsync(); }
        await CriarBaseTenantAsync(_empresaA, true);
        await CriarBaseTenantAsync(_empresaB, false);
        await using var b = Contexto(_empresaB, _usuarioB);
        var criadoB = await CriarHandler(b, _empresaB, _usuarioB).Handle(Comando(_clienteB, _veiculoB, _servicoB, 80m), default);
        _orcamentoB = criadoB.Orcamento.Id;
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task PrecoNegociadoDiferente_NaoAlteraCatalogoNemAgenda()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var criado = await CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, _servicoA, 160m, _agendamentoA), default);
        Assert.Equal(_clienteA, criado.Orcamento.ClienteId);
        Assert.Equal("João da Silva", criado.Orcamento.ClienteNome);
        Assert.Equal(_veiculoA, criado.Orcamento.VeiculoId);
        Assert.Equal("Honda Civic", criado.Orcamento.VeiculoDescricao);
        Assert.Equal(_agendamentoA, criado.Orcamento.AgendamentoOrigemId);
        Assert.Equal(160m, criado.Orcamento.Itens.Single().ValorUnitario);
        Assert.Equal(100m, (await c.Servicos.SingleAsync(x => x.Id == _servicoA)).PrecoBase);
        Assert.Equal(100m, (await c.AgendamentosItens.SingleAsync(x => x.AgendamentoId == _agendamentoA)).PrecoReferenciaSnapshot);
    }

    [Fact]
    public async Task Listagem_PesquisaSnapshotsEPaginaTotalNegociado()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        await CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, _servicoA, 160m), default);
        c.ChangeTracker.Clear();
        Assert.Equal(1, await c.Orcamentos.CountAsync());
        Assert.Equal(1, await c.Orcamentos.CountAsync(x => x.Status == StatusOrcamento.Rascunho));
        Assert.Equal(1, await c.Orcamentos.CountAsync(x => x.VeiculoPlacaSnapshot != null && x.VeiculoPlacaSnapshot.Contains("ABC1D23")));
        var semPesquisa = await new OrcamentosRepositorio(c).ListarAsync(new(1, 25, StatusEfetivoOrcamento.Rascunho, null,
            DateOnly.FromDateTime(DateTime.UtcNow)), default);
        Assert.Equal(1, semPesquisa.TotalItens);
        Assert.Single(semPesquisa.Itens);
        var pagina = await new OrcamentosRepositorio(c).ListarAsync(new(1, 25, StatusEfetivoOrcamento.Rascunho, "ABC1D23",
            DateOnly.FromDateTime(DateTime.UtcNow)), default);
        var item = Assert.Single(pagina.Itens);
        Assert.Equal(160m, item.Total);
    }

    [Fact]
    public async Task OrigemAgenda_PreservaSnapshotMesmoAposCatalogoMudar()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var servico = await c.Servicos.SingleAsync(x => x.Id == _servicoA);
        servico.Atualizar(servico.CategoriaServicoId, "Lavagem atualizada", null, TipoPrecificacao.APartirDe, 120m, 120, 1);
        await c.SaveChangesAsync();
        var criado = await CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, _servicoA, 160m, _agendamentoA), default);
        var item = Assert.Single(criado.Orcamento.Itens);
        Assert.Equal("Lavagem técnica", item.Nome);
        Assert.Equal(100m, item.PrecoReferencia);
        Assert.Equal(160m, item.ValorUnitario);
    }

    [Fact]
    public async Task Emitido_BloqueiaEdicao()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var criado = await CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, _servicoA, 160m, _agendamentoA), default);
        c.ChangeTracker.Clear();
        await EmitirHandler(c).Handle(new(criado.Orcamento.Id, null), default);
        c.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => AtualizarHandler(c).Handle(
            ComandoAtualizar(criado.Orcamento.Id, 210m), default));
    }

    [Fact]
    public async Task NovaProposta_SoSubstituiAnteriorQuandoEmitida()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var a = await CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, _servicoA, 160m), default);
        c.ChangeTracker.Clear();
        await EmitirHandler(c).Handle(new(a.Orcamento.Id, null), default);
        c.ChangeTracker.Clear();
        var b = await NovaHandler(c).Handle(new(a.Orcamento.Id), default);
        Assert.NotEqual(a.Orcamento.Id, b.Orcamento.Id);
        Assert.Equal(160m, b.Orcamento.Itens.Single().ValorUnitario);
        Assert.Equal(StatusOrcamento.Emitido, (await c.Orcamentos.SingleAsync(x => x.Id == a.Orcamento.Id)).Status);
        c.ChangeTracker.Clear(); await AtualizarHandler(c).Handle(ComandoAtualizar(b.Orcamento.Id, 210m), default);
        c.ChangeTracker.Clear();
        await EmitirHandler(c).Handle(new(b.Orcamento.Id, null), default);
        c.ChangeTracker.Clear();
        var antigo = await c.Orcamentos.Include(x => x.Itens).SingleAsync(x => x.Id == a.Orcamento.Id);
        var novo = await c.Orcamentos.Include(x => x.Itens).SingleAsync(x => x.Id == b.Orcamento.Id);
        Assert.Equal(StatusOrcamento.Substituido, antigo.Status); Assert.Equal(160m, antigo.Total);
        Assert.Equal(StatusOrcamento.Emitido, novo.Status); Assert.Equal(210m, novo.Total);
    }

    [Fact]
    public async Task Recusado_ContinuaRecusadoQuandoNovaPropostaEhEmitida()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var a = await CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, _servicoA, 160m), default);
        c.ChangeTracker.Clear();
        await EmitirHandler(c).Handle(new(a.Orcamento.Id, null), default);
        c.ChangeTracker.Clear();
        await new RecusarOrcamentoHandler(new UsuarioContextoTeste(_empresaA, _usuarioA), new OrcamentosRepositorio(c)).Handle(new(a.Orcamento.Id, "Não aceitou."), default);
        c.ChangeTracker.Clear();
        var b = await NovaHandler(c).Handle(new(a.Orcamento.Id), default);
        c.ChangeTracker.Clear(); await AtualizarHandler(c).Handle(ComandoAtualizar(b.Orcamento.Id, 140m), default);
        c.ChangeTracker.Clear();
        await EmitirHandler(c).Handle(new(b.Orcamento.Id, null), default);
        c.ChangeTracker.Clear();
        Assert.Equal(StatusOrcamento.Recusado, (await c.Orcamentos.SingleAsync(x => x.Id == a.Orcamento.Id)).Status);
    }

    [Fact]
    public async Task AprovadoDepoisSubstituido_PreservaHistoricoCompleto()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var a = await CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, _servicoA, 160m), default);
        c.ChangeTracker.Clear();
        await EmitirHandler(c).Handle(new(a.Orcamento.Id, null), default);
        c.ChangeTracker.Clear();
        await new AprovarOrcamentoHandler(new UsuarioContextoTeste(_empresaA, _usuarioA), new OrcamentosRepositorio(c), new PlataformaAtendimentoConsulta(c)).Handle(new(a.Orcamento.Id, "Aprovado presencialmente."), default);
        c.ChangeTracker.Clear(); var b = await NovaHandler(c).Handle(new(a.Orcamento.Id), default); c.ChangeTracker.Clear(); await EmitirHandler(c).Handle(new(b.Orcamento.Id, null), default);
        c.ChangeTracker.Clear();
        var historico = await c.OrcamentosHistoricosStatus.Where(x => x.OrcamentoId == a.Orcamento.Id).OrderBy(x => x.DataUtc).Select(x => x.Status).ToArrayAsync();
        Assert.Equal([StatusOrcamento.Rascunho, StatusOrcamento.Emitido, StatusOrcamento.Aprovado, StatusOrcamento.Substituido], historico);
    }

    [Fact]
    public async Task Expirado_NaoPodeSerAprovado()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var hojeLocal = OrcamentoFluxo.HojeLocal("America/Sao_Paulo");
        var entidade = new Orcamento(_empresaA, PartesA(), null, null, hojeLocal.AddDays(-1),
            null, null, null, 0, 0, [ItemSnapshot(_servicoA, 160m)], _usuarioA);
        entidade.Emitir(DateTime.UtcNow.Year, _usuarioA); c.Orcamentos.Add(entidade); await c.SaveChangesAsync();
        var handler = new AprovarOrcamentoHandler(new UsuarioContextoTeste(_empresaA, _usuarioA), new OrcamentosRepositorio(c), new PlataformaAtendimentoConsulta(c));
        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => handler.Handle(new(entidade.Id, null), default));
    }

    [Fact]
    public async Task PdfOficial_TemAssinaturaEConteudo_EContinuaAposSubstituicao()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var a = await CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, _servicoA, 160m), default);
        c.ChangeTracker.Clear();
        var emitido = await EmitirHandler(c).Handle(new(a.Orcamento.Id, null), default);
        c.ChangeTracker.Clear(); var b = await NovaHandler(c).Handle(new(a.Orcamento.Id), default); c.ChangeTracker.Clear(); await EmitirHandler(c).Handle(new(b.Orcamento.Id, null), default); c.ChangeTracker.Clear();
        var pdf = await new GerarPdfOrcamentoHandler(new UsuarioContextoTeste(_empresaA, _usuarioA), new OrcamentosRepositorio(c),
            new PlataformaAtendimentoConsulta(c), new PdfOrcamentoGenerator()).Handle(new(a.Orcamento.Id), default);
        Assert.True(pdf.Conteudo.Length > 1000); Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(pdf.Conteudo, 0, 5));
        Assert.Contains(emitido.Orcamento.Codigo!, System.Text.Encoding.Latin1.GetString(pdf.Conteudo));
        Assert.DoesNotContain("Interna confidencial", System.Text.Encoding.Latin1.GetString(pdf.Conteudo));
    }

    [Fact]
    public async Task RascunhoCancelado_NaoPossuiPdfOficial()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var criado = await CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, _servicoA, 160m), default);
        c.ChangeTracker.Clear();
        await new CancelarOrcamentoHandler(new UsuarioContextoTeste(_empresaA, _usuarioA), new OrcamentosRepositorio(c)).Handle(new(criado.Orcamento.Id, null), default);
        c.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => new GerarPdfOrcamentoHandler(new UsuarioContextoTeste(_empresaA, _usuarioA),
            new OrcamentosRepositorio(c), new PlataformaAtendimentoConsulta(c), new PdfOrcamentoGenerator()).Handle(new(criado.Orcamento.Id), default));
    }

    [Fact] public async Task EmpresaA_NaoConsultaOrcamentoEmpresaB() { await using var c = Contexto(_empresaA, _usuarioA); Assert.Null(await new OrcamentosRepositorio(c).ObterDetalheAsync(_orcamentoB, default)); }

    [Fact] public async Task EmpresaA_NaoEditaOrcamentoEmpresaB() { await using var c = Contexto(_empresaA, _usuarioA); Assert.Null(await new OrcamentosRepositorio(c).ObterParaAlteracaoAsync(_orcamentoB, default)); }
    [Fact] public async Task EmpresaA_NaoEmiteOrcamentoEmpresaB() { await using var c = Contexto(_empresaA, _usuarioA); await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => EmitirHandler(c).Handle(new(_orcamentoB, null), default)); }
    [Fact] public async Task EmpresaA_NaoAprovaOrcamentoEmpresaB() { await using var c = Contexto(_empresaA, _usuarioA); await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => new AprovarOrcamentoHandler(new UsuarioContextoTeste(_empresaA, _usuarioA), new OrcamentosRepositorio(c), new PlataformaAtendimentoConsulta(c)).Handle(new(_orcamentoB, null), default)); }
    [Fact] public async Task EmpresaA_NaoRecusaOrcamentoEmpresaB() { await using var c = Contexto(_empresaA, _usuarioA); await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => new RecusarOrcamentoHandler(new UsuarioContextoTeste(_empresaA, _usuarioA), new OrcamentosRepositorio(c)).Handle(new(_orcamentoB, null), default)); }
    [Fact] public async Task EmpresaA_NaoCancelaOrcamentoEmpresaB() { await using var c = Contexto(_empresaA, _usuarioA); await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => new CancelarOrcamentoHandler(new UsuarioContextoTeste(_empresaA, _usuarioA), new OrcamentosRepositorio(c)).Handle(new(_orcamentoB, null), default)); }
    [Fact] public async Task EmpresaA_NaoGeraPdfOrcamentoEmpresaB() { await using var c = Contexto(_empresaA, _usuarioA); await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => new GerarPdfOrcamentoHandler(new UsuarioContextoTeste(_empresaA, _usuarioA), new OrcamentosRepositorio(c), new PlataformaAtendimentoConsulta(c), new PdfOrcamentoGenerator()).Handle(new(_orcamentoB), default)); }
    [Fact] public async Task EmpresaA_NaoCriaNovaPropostaDoOrcamentoEmpresaB() { await using var c = Contexto(_empresaA, _usuarioA); await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => NovaHandler(c).Handle(new(_orcamentoB), default)); }
    [Fact] public async Task EmpresaA_NaoAgendaOrcamentoEmpresaB() { await using var c = Contexto(_empresaA, _usuarioA); await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => AgendarHandler(c).Handle(new(_orcamentoB, new DateTime(2026, 9, 1, 9, 0, 0), 90, null, null), default)); }
    [Fact] public async Task EmpresaA_NaoCriaOrcamentoComClienteEmpresaB() { await using var c = Contexto(_empresaA, _usuarioA); await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => CriarHandler(c).Handle(Comando(_clienteB, _veiculoB, _servicoA, 160m), default)); }
    [Fact] public async Task EmpresaA_NaoCriaOrcamentoComVeiculoEmpresaB() { await using var c = Contexto(_empresaA, _usuarioA); await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => CriarHandler(c).Handle(Comando(_clienteA, _veiculoB, _servicoA, 160m), default)); }
    [Fact] public async Task EmpresaA_NaoCriaOrcamentoComServicoEmpresaB() { await using var c = Contexto(_empresaA, _usuarioA); await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, _servicoB, 160m), default)); }
    [Fact] public async Task EmpresaA_NaoCriaOrcamentoComPacoteEmpresaB() { await using var c = Contexto(_empresaA, _usuarioA); var comando = Comando(_clienteA, _veiculoA, _servicoA, 160m) with { Itens = [new(TipoItemOrcamento.Pacote, _pacoteB, null, null, 160m, 1, null)] }; await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => CriarHandler(c).Handle(comando, default)); }
    [Fact] public async Task EmpresaA_NaoUsaAgendamentoEmpresaB() { await using var c = Contexto(_empresaA, _usuarioA); await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, _servicoA, 160m, _agendamentoB), default)); }
    [Fact] public async Task VeiculoMesmoTenant_DeOutroCliente_EhRejeitado() { await using var c = Contexto(_empresaA, _usuarioA); await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => CriarHandler(c).Handle(Comando(_clienteA, _veiculoA2, _servicoA, 160m), default)); }

    [Fact]
    public async Task OrcamentoAprovado_OriginaUmaUnicaOsComSnapshotsEValorExatos()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var criado = await CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, _servicoA, 160m, _agendamentoA), default);
        c.ChangeTracker.Clear();
        await EmitirHandler(c).Handle(new(criado.Orcamento.Id, null), default);
        c.ChangeTracker.Clear();
        await new AprovarOrcamentoHandler(new UsuarioContextoTeste(_empresaA, _usuarioA),
            new OrcamentosRepositorio(c), new PlataformaAtendimentoConsulta(c), new OrdensServicoRepositorio(c))
            .Handle(new(criado.Orcamento.Id, "Aprovado"), default);
        c.ChangeTracker.Clear();

        var comando = new CriarOrdemServicoCommand(criado.Orcamento.Id, _agendamentoA, null, null, null,
            0, 0, null, []);
        var ordem = await CriarOrdemServicoHandler(c).Handle(comando, default);

        Assert.Equal(160m, ordem.OrdemServico.TotalAutorizado);
        Assert.Equal("Lavagem técnica", Assert.Single(ordem.OrdemServico.Itens).NomeSnapshot);
        Assert.Equal(OrigemComercialOrdemServico.Orcamento, ordem.OrdemServico.Itens.Single().OrigemComercial);
        c.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => CriarOrdemServicoHandler(c).Handle(comando, default));
    }

    [Fact]
    public async Task OrcamentoAprovadoSemAgenda_AgendaUmaVez_EPermiteUmaUnicaOs()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var criado = await CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, _servicoA, 160m), default);
        c.ChangeTracker.Clear(); await EmitirHandler(c).Handle(new(criado.Orcamento.Id, null), default);
        c.ChangeTracker.Clear(); await new AprovarOrcamentoHandler(new UsuarioContextoTeste(_empresaA, _usuarioA),
            new OrcamentosRepositorio(c), new PlataformaAtendimentoConsulta(c), new OrdensServicoRepositorio(c))
            .Handle(new(criado.Orcamento.Id, "Aprovado"), default);
        c.ChangeTracker.Clear();

        Assert.Single(await c.Agendamentos.ToArrayAsync());

        var agendamento = await AgendarHandler(c).Handle(new(criado.Orcamento.Id,
            new DateTime(2026, 9, 1, 9, 0, 0), 90, "Cliente confirmou o horário.", null), default);
        c.ChangeTracker.Clear();

        Assert.Equal(agendamento.Id, (await c.Orcamentos.SingleAsync(item => item.Id == criado.Orcamento.Id)).AgendamentoId);
        Assert.Equal(_clienteA, (await c.Agendamentos.SingleAsync(item => item.Id == agendamento.Id)).ClienteId);
        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => AgendarHandler(c).Handle(new(
            criado.Orcamento.Id, new DateTime(2026, 9, 2, 9, 0, 0), 90, null, null), default));

        c.ChangeTracker.Clear();
        var comando = new CriarOrdemServicoCommand(criado.Orcamento.Id, agendamento.Id, null, null,
            null, 0, 0, null, []);
        await CriarOrdemServicoHandler(c).Handle(comando, default);
        c.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => CriarOrdemServicoHandler(c).Handle(comando, default));
    }

    [Fact]
    public async Task VeiculoSemPlaca_PercorreOrcamentoAgendaPdfEOs()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var criado = await CriarHandler(c).Handle(
            Comando(_clienteSemPlaca, _veiculoSemPlaca, _servicoA, 290m), default);

        Assert.Null(criado.Orcamento.VeiculoPlaca);
        Assert.Equal("Sea-Doo GTX 300 · JET-001", criado.Orcamento.VeiculoDescricao);
        c.ChangeTracker.Clear();
        await EmitirHandler(c).Handle(new(criado.Orcamento.Id, null), default);
        c.ChangeTracker.Clear();
        var pdf = await new GerarPdfOrcamentoHandler(
            new UsuarioContextoTeste(_empresaA, _usuarioA), new OrcamentosRepositorio(c),
            new PlataformaAtendimentoConsulta(c), new PdfOrcamentoGenerator())
            .Handle(new(criado.Orcamento.Id), default);
        Assert.True(pdf.Conteudo.Length > 1000);

        c.ChangeTracker.Clear();
        await new AprovarOrcamentoHandler(new UsuarioContextoTeste(_empresaA, _usuarioA),
            new OrcamentosRepositorio(c), new PlataformaAtendimentoConsulta(c),
            new OrdensServicoRepositorio(c)).Handle(new(criado.Orcamento.Id, "Aprovado"), default);
        c.ChangeTracker.Clear();
        var agendamento = await AgendarHandler(c).Handle(new(criado.Orcamento.Id,
            new DateTime(2026, 9, 3, 10, 0, 0), 90, null, null), default);
        Assert.Null(agendamento.VeiculoPlaca);
        Assert.Equal("Sea-Doo GTX 300 · JET-001", agendamento.VeiculoDescricao);

        c.ChangeTracker.Clear();
        var ordem = await CriarOrdemServicoHandler(c).Handle(new(
            criado.Orcamento.Id, agendamento.Id, null, null, null, 0, 0, null, []), default);
        Assert.Null(ordem.OrdemServico.VeiculoPlacaSnapshot);
        Assert.Equal("Sea-Doo GTX 300 · JET-001", ordem.OrdemServico.VeiculoDescricaoSnapshot);
    }

    [Fact]
    public async Task OrcamentoNaoAprovado_NaoOriginaOs()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var criado = await CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, _servicoA, 160m, _agendamentoA), default);
        c.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => CriarOrdemServicoHandler(c).Handle(
            new(criado.Orcamento.Id, _agendamentoA, null, null, null, 0, 0, null, []), default));
    }

    [Fact]
    public async Task CriacaoDireta_ExigeValorAutorizadoEPreservaCatalogo()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var ordem = await CriarOrdemServicoHandler(c).Handle(new(null, _agendamentoA, _clienteA, _veiculoA,
            null, 0, 0, "Cliente autorizou presencialmente.",
            [new(TipoItemOrcamento.Servico, _servicoA, null, null, 175m, 1, null)]), default);

        Assert.Equal(175m, ordem.OrdemServico.TotalAutorizado);
        Assert.Equal(OrigemOrdemServico.Agendamento, ordem.OrdemServico.Origem);
        Assert.NotNull(ordem.OrdemServico.AutorizacaoDiretaEmUtc);
        Assert.Equal(100m, (await c.Servicos.SingleAsync(item => item.Id == _servicoA)).PrecoBase);
    }

    [Fact]
    public async Task NovaOs_SemAgendamento_EhRejeitada()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => CriarOrdemServicoHandler(c).Handle(
            new(null, null, _clienteA, _veiculoA, null, 0, 0, "Autorizado",
                [new(TipoItemOrcamento.Servico, _servicoA, null, null, 175m, 1, null)]), default));
    }

    [Theory]
    [InlineData(StatusAgendamento.Cancelado)]
    [InlineData(StatusAgendamento.NaoCompareceu)]
    [InlineData(StatusAgendamento.Concluido)]
    public async Task AgendamentoFinal_NaoOriginaOs(StatusAgendamento status)
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var agendamento = await c.Agendamentos.SingleAsync(item => item.Id == _agendamentoA);
        if (status == StatusAgendamento.Concluido)
        {
            agendamento.AlterarStatus(StatusAgendamento.Compareceu);
            agendamento.AlterarStatus(StatusAgendamento.Concluido);
        }
        else
        {
            agendamento.AlterarStatus(status, status == StatusAgendamento.Cancelado ? "Cancelado no teste." : null);
        }
        await c.SaveChangesAsync();
        c.ChangeTracker.Clear();

        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => CriarOrdemServicoHandler(c).Handle(
            new(null, _agendamentoA, null, null, null, 0, 0, "Autorizado",
                [new(TipoItemOrcamento.Servico, _servicoA, null, null, 175m, 1, null)]), default));
    }

    [Fact]
    public async Task IndiceUnico_ImpedeSegundaOsMesmoSemGuardDaAplicacao()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        await CriarOrdemServicoHandler(c).Handle(new(null, _agendamentoA, null, null, null,
            0, 0, "Autorizado", [new(TipoItemOrcamento.Servico, _servicoA, null, null, 175m, 1, null)]), default);
        c.ChangeTracker.Clear();

        c.OrdensServico.Add(new OrdemServico(_empresaA, 2026,
            new(_clienteA, "João da Silva", "52998224725", "11999999999", _veiculoA,
                "Honda Civic", "ABC1D23"), OrigemOrdemServico.Agendamento, null, _agendamentoA,
            90, 0, 0, [new(TipoItemOrcamento.Personalizado, null, null, null, "Serviço paralelo",
                null, 100m, 1, 1, OrigemComercialOrdemServico.AcordoDireto, DateTime.UtcNow,
                _usuarioA, null)], _usuarioA, DateTime.UtcNow, "Autorizado em teste de concorrência."));

        await Assert.ThrowsAsync<DbUpdateException>(() => c.SaveChangesAsync());
        c.ChangeTracker.Clear();
        Assert.Single(await c.OrdensServico.Where(item => item.AgendamentoOrigemId == _agendamentoA).ToArrayAsync());
    }

    [Fact]
    public async Task AgendaComOsAtiva_SoPodeSerCanceladaDepoisDaOs()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var ordem = await CriarOrdemServicoHandler(c).Handle(new(null, _agendamentoA, null, null,
            null, 0, 0, "Autorizado",
            [new(TipoItemOrcamento.Servico, _servicoA, null, null, 175m, 1, null)]), default);
        c.ChangeTracker.Clear();
        var handlerAgenda = new AlterarStatusAgendaOperacionalHandler(
            new UsuarioContextoTeste(_empresaA, _usuarioA), new AgendaRepositorio(c),
            new CatalogoAgendaConsulta(c), new FusoHorarioEmpresaConsulta(c), new ConversorFusoHorario(),
            new OrdensServicoRepositorio(c));

        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => handlerAgenda.Handle(
            new(_agendamentoA, StatusAgendamento.Cancelado, "Cancelado"), default));

        c.ChangeTracker.Clear();
        await new CancelarOrdemServicoHandler(new UsuarioContextoTeste(_empresaA, _usuarioA),
            new OrdensServicoRepositorio(c), new PlataformaAtendimentoConsulta(c))
            .Handle(new(ordem.OrdemServico.Id, "Cancelada antes do atendimento."), default);
        c.ChangeTracker.Clear();

        await handlerAgenda.Handle(new(_agendamentoA, StatusAgendamento.Cancelado, "Cancelado"), default);
        Assert.Equal(StatusAgendamento.Cancelado,
            (await c.Agendamentos.SingleAsync(item => item.Id == _agendamentoA)).Status);
    }

    [Fact]
    public async Task OrcamentoAdicional_AprovadoIncorporaItensSemSubstituirOriginal()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        var baseCriada = await CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, _servicoA, 160m, _agendamentoA), default);
        c.ChangeTracker.Clear(); await EmitirHandler(c).Handle(new(baseCriada.Orcamento.Id, null), default);
        c.ChangeTracker.Clear(); await new AprovarOrcamentoHandler(new UsuarioContextoTeste(_empresaA, _usuarioA),
            new OrcamentosRepositorio(c), new PlataformaAtendimentoConsulta(c), new OrdensServicoRepositorio(c))
            .Handle(new(baseCriada.Orcamento.Id, null), default);
        c.ChangeTracker.Clear();
        var ordem = await CriarOrdemServicoHandler(c).Handle(new(baseCriada.Orcamento.Id, _agendamentoA, null, null,
            null, 0, 0, null, []), default);
        c.ChangeTracker.Clear();
        var persistida = await new OrdensServicoRepositorio(c).ObterAsync(ordem.OrdemServico.Id, true, default);
        persistida!.RealizarCheckIn(new(NivelExigenciaOperacional.Desabilitado,
            NivelExigenciaOperacional.Desabilitado, NivelExigenciaOperacional.Desabilitado, null, []), null, null, _usuarioA);
        await c.SaveChangesAsync(); c.ChangeTracker.Clear();
        var repositorioOrdens = new OrdensServicoRepositorio(c);
        persistida = await repositorioOrdens.ObterAsync(ordem.OrdemServico.Id, true, default);
        persistida!.IniciarExecucao(_usuarioA, null);
        repositorioOrdens.AdicionarUltimoHistorico(persistida);
        await c.SaveChangesAsync(); c.ChangeTracker.Clear();

        var adicional = await new CriarOrcamentoAdicionalHandler(new UsuarioContextoTeste(_empresaA, _usuarioA),
            new OrdensServicoRepositorio(c), new OrcamentosRepositorio(c), new CatalogoAtendimentoConsulta(c),
            new PlataformaAtendimentoConsulta(c)).Handle(new(ordem.OrdemServico.Id,
                DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7), null, null, null, 0, 0,
                [new(TipoItemOrcamento.Personalizado, null, "Vitrificação de vidro", null, 80m, 1, null)]), default);
        c.ChangeTracker.Clear(); await EmitirHandler(c).Handle(new(adicional.Orcamento.Id, null), default);
        c.ChangeTracker.Clear(); await new AprovarOrcamentoHandler(new UsuarioContextoTeste(_empresaA, _usuarioA),
            new OrcamentosRepositorio(c), new PlataformaAtendimentoConsulta(c), new OrdensServicoRepositorio(c))
            .Handle(new(adicional.Orcamento.Id, "Aprovado"), default);
        c.ChangeTracker.Clear();

        var atualizada = await c.OrdensServico.Include(item => item.Itens).SingleAsync(item => item.Id == ordem.OrdemServico.Id);
        Assert.Equal(240m, atualizada.TotalAutorizado);
        Assert.Equal(2, atualizada.Itens.Count);
        Assert.Equal(StatusOrcamento.Aprovado, (await c.Orcamentos.SingleAsync(item => item.Id == baseCriada.Orcamento.Id)).Status);
    }

    [Fact]
    public async Task EmpresaA_NaoConsultaOsDaEmpresaB()
    {
        await using var b = Contexto(_empresaB, _usuarioB);
        var ordemB = await CriarOrdemServicoHandler(b, _empresaB, _usuarioB).Handle(new(null, _agendamentoB,
            _clienteB, _veiculoB, null, 0, 0, "Autorizado", [new(TipoItemOrcamento.Servico,
                _servicoB, null, null, 80m, 1, null)]), default);
        await using var a = Contexto(_empresaA, _usuarioA);
        Assert.Null(await new OrdensServicoRepositorio(a).ObterAsync(ordemB.OrdemServico.Id, false, default));
    }

    [Fact]
    public async Task EmpresaA_NaoCriaOsComAgendamentoEmpresaB()
    {
        await using var c = Contexto(_empresaA, _usuarioA);
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => CriarOrdemServicoHandler(c).Handle(
            new(null, _agendamentoB, null, null, null, 0, 0, "Autorizado",
                [new(TipoItemOrcamento.Servico, _servicoA, null, null, 175m, 1, null)]), default));
    }

    private async Task CriarBaseTenantAsync(Guid empresaId, bool empresaA)
    {
        await using var c = Contexto(empresaId, empresaA ? _usuarioA : _usuarioB);
        var cliente = new Cliente(empresaId, empresaA ? "João da Silva" : "Cliente B", TipoPessoa.PessoaFisica,
            empresaA ? "52998224725" : null, empresaA ? "11999999999" : null, null, null, null, null);
        c.Clientes.Add(cliente); await c.SaveChangesAsync();
        var veiculo = new Veiculo(empresaId, cliente.Id, empresaA ? "ABC1D23" : "BBB1B11", empresaA ? "Honda" : "Ford",
            empresaA ? "Civic" : "Focus", null, 2024, 2024, null, null, null);
        c.Veiculos.Add(veiculo); await c.SaveChangesAsync();
        var categoria = new CategoriaServico(empresaId, "Lavagem", null, 1); c.CategoriasServico.Add(categoria); await c.SaveChangesAsync();
        var servico = new Servico(empresaId, categoria.Id, empresaA ? "Lavagem técnica" : "Serviço B", null,
            empresaA ? TipoPrecificacao.APartirDe : TipoPrecificacao.Fixo, empresaA ? 100m : 80m, 90, 1);
        c.Servicos.Add(servico); await c.SaveChangesAsync();
        var pacote = new Pacote(empresaId, empresaA ? "Pacote A" : "Pacote B", null, TipoPrecificacao.Fixo,
            empresaA ? 150m : 90m, [servico.Id]); c.Pacotes.Add(pacote); await c.SaveChangesAsync();
        if (empresaA)
        {
            _clienteA = cliente.Id; _veiculoA = veiculo.Id; _servicoA = servico.Id;
            var agenda = new Agendamento(empresaId, cliente.Id, cliente.Nome, veiculo.Id, "Honda Civic", veiculo.Placa,
                new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc), 90, null, null,
                [new(TipoItemAgendamento.Servico, servico.Id, servico.Nome, servico.Descricao, servico.TipoPrecificacao, servico.PrecoBase, servico.DuracaoEstimadaMinutos)]);
            c.Agendamentos.Add(agenda); await c.SaveChangesAsync(); _agendamentoA = agenda.Id;
            var cliente2 = new Cliente(empresaId, "Maria", TipoPessoa.PessoaFisica, null, null, null, null, null, null);
            c.Clientes.Add(cliente2); await c.SaveChangesAsync();
            var veiculo2 = new Veiculo(empresaId, cliente2.Id, "XYZ9Z99", "Toyota", "Corolla", null, 2024, 2024, null, null, null);
            c.Veiculos.Add(veiculo2); await c.SaveChangesAsync(); _veiculoA2 = veiculo2.Id;
            var clienteSemPlaca = new Cliente(empresaId, "Rafael Marins", TipoPessoa.PessoaFisica,
                null, null, null, null, null, null);
            c.Clientes.Add(clienteSemPlaca); await c.SaveChangesAsync();
            var veiculoSemPlaca = new Veiculo(empresaId, clienteSemPlaca.Id,
                TipoVeiculo.MotoAquatica, null, "JET-001", "Sea-Doo", "GTX 300", null,
                2025, 2025, null, 0, null);
            c.Veiculos.Add(veiculoSemPlaca); await c.SaveChangesAsync();
            _clienteSemPlaca = clienteSemPlaca.Id; _veiculoSemPlaca = veiculoSemPlaca.Id;
        }
        else
        {
            _clienteB = cliente.Id; _veiculoB = veiculo.Id; _servicoB = servico.Id; _pacoteB = pacote.Id;
            var agenda = new Agendamento(empresaId, cliente.Id, cliente.Nome, veiculo.Id, "Ford Focus", veiculo.Placa,
                new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc), 90, null, null,
                [new(TipoItemAgendamento.Servico, servico.Id, servico.Nome, servico.Descricao, servico.TipoPrecificacao, servico.PrecoBase, servico.DuracaoEstimadaMinutos)]);
            c.Agendamentos.Add(agenda); await c.SaveChangesAsync(); _agendamentoB = agenda.Id;
        }
    }

    private CriarOrcamentoHandler CriarHandler(DetaraDbContext c, Guid? empresa = null, Guid? usuario = null) => new(new UsuarioContextoTeste(empresa ?? _empresaA, usuario ?? _usuarioA), new ClientesAtendimentoConsulta(c), new CatalogoAtendimentoConsulta(c), new AgendaAtendimentoIntegracao(c), new OrcamentosRepositorio(c));
    private AtualizarOrcamentoHandler AtualizarHandler(DetaraDbContext c) => new(new UsuarioContextoTeste(_empresaA, _usuarioA), new ClientesAtendimentoConsulta(c), new CatalogoAtendimentoConsulta(c), new AgendaAtendimentoIntegracao(c), new OrcamentosRepositorio(c));
    private EmitirOrcamentoHandler EmitirHandler(DetaraDbContext c) => new(new UsuarioContextoTeste(_empresaA, _usuarioA), new OrcamentosRepositorio(c), new PlataformaAtendimentoConsulta(c));
    private CriarNovaPropostaHandler NovaHandler(DetaraDbContext c) => new(new UsuarioContextoTeste(_empresaA, _usuarioA), new OrcamentosRepositorio(c), new PlataformaAtendimentoConsulta(c));
    private AgendarOrcamentoHandler AgendarHandler(DetaraDbContext c) => new(new UsuarioContextoTeste(_empresaA, _usuarioA),
        new OrcamentosRepositorio(c), new AgendaAtendimentoIntegracao(c), new PlataformaAtendimentoConsulta(c),
        new ConversorFusoHorario());
    private CriarOrdemServicoHandler CriarOrdemServicoHandler(DetaraDbContext c, Guid? empresa = null, Guid? usuario = null) =>
        new(new UsuarioContextoTeste(empresa ?? _empresaA, usuario ?? _usuarioA), new OrdensServicoRepositorio(c),
            new OrcamentosRepositorio(c), new ClientesAtendimentoConsulta(c), new CatalogoAtendimentoConsulta(c),
            new AgendaAtendimentoIntegracao(c), new PlataformaAtendimentoConsulta(c));
    private CriarOrcamentoCommand Comando(Guid cliente, Guid veiculo, Guid servico, decimal valor, Guid? agenda = null) => new(cliente, veiculo, agenda,
        DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), "Cliente", "Interna confidencial", "À vista", 0, 0,
        [new(TipoItemOrcamento.Servico, servico, null, null, valor, 1, null)]);
    private AtualizarOrcamentoCommand ComandoAtualizar(Guid id, decimal valor) => new(id, _clienteA, _veiculoA, null,
        DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), "Cliente", "Interna confidencial", "À vista", 0, 0,
        [new(TipoItemOrcamento.Servico, _servicoA, null, null, valor, 1, null)]);
    private PartesOrcamentoSnapshot PartesA() => new(_clienteA, "João da Silva", "52998224725", "11999999999", _veiculoA, "Honda Civic", "ABC1D23");
    private static ItemOrcamentoSnapshot ItemSnapshot(Guid servico, decimal valor) => new(TipoItemOrcamento.Servico, servico, "Lavagem técnica", null, TipoPrecificacao.APartirDe, 100m, valor, 1, 1, null);
    private DetaraDbContext Contexto(Guid empresaId, Guid usuarioId) => new(_options, new UsuarioContextoTeste(empresaId, usuarioId));

    private sealed class UsuarioContextoTeste(Guid empresaId, Guid usuarioId, bool autenticado = true) : IUsuarioContexto
    { public static UsuarioContextoTeste Anonimo { get; } = new(Guid.Empty, Guid.Empty, false); public Guid UsuarioId { get; } = usuarioId; public Guid EmpresaId { get; } = empresaId; public bool EstaAutenticado { get; } = autenticado; }
}
