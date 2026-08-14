using Detara.Application.Abstracoes;
using Detara.Application.Catalogo;
using Detara.Domain.Entidades;
using Detara.Domain.Catalogo;
using Detara.Infrastructure.Catalogo;
using Detara.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.Catalogo;

public sealed class CatalogoPersistenciaTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<DetaraDbContext> _options = null!;
    private Guid _empresaAId;
    private Guid _empresaBId;
    private Guid _categoriaAId;
    private Guid _categoriaBId;
    private Guid _servicoAId;
    private Guid _servicoA2Id;
    private Guid _servicoBId;
    private Guid _pacoteAId;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(_connection).Options;
        var empresaA = new Empresa("Empresa A", "Empresa A Ltda", "11111111000111", "empresa-a");
        var empresaB = new Empresa("Empresa B", "Empresa B Ltda", "22222222000122", "empresa-b");
        _empresaAId = empresaA.Id;
        _empresaBId = empresaB.Id;

        await using var contextA = CriarContexto(_empresaAId);
        await contextA.Database.EnsureCreatedAsync();
        contextA.Empresas.AddRange(empresaA, empresaB);
        var categoriaA = new CategoriaServico(_empresaAId, "Lavagem", null, 1);
        contextA.CategoriasServico.Add(categoriaA);
        await contextA.SaveChangesAsync();
        _categoriaAId = categoriaA.Id;
        var servicoA = new Servico(_empresaAId, categoriaA.Id, "Lavagem técnica", null, TipoPrecificacao.Fixo, 100, 60, 1);
        var servicoA2 = new Servico(_empresaAId, categoriaA.Id, "Descontaminação", null, TipoPrecificacao.Fixo, 80, 30, 2);
        contextA.Servicos.AddRange(servicoA, servicoA2);
        await contextA.SaveChangesAsync();
        _servicoAId = servicoA.Id;
        _servicoA2Id = servicoA2.Id;
        var pacoteA = new Pacote(_empresaAId, "Combo cuidado", null, TipoPrecificacao.Fixo, 150, [servicoA.Id, servicoA2.Id]);
        contextA.Pacotes.Add(pacoteA);
        await contextA.SaveChangesAsync();
        _pacoteAId = pacoteA.Id;

        await using var contextB = CriarContexto(_empresaBId);
        var categoriaB = new CategoriaServico(_empresaBId, "Lavagem", null, 1);
        contextB.CategoriasServico.Add(categoriaB);
        await contextB.SaveChangesAsync();
        _categoriaBId = categoriaB.Id;
        var servicoB = new Servico(_empresaBId, categoriaB.Id, "Lavagem técnica", null, TipoPrecificacao.Fixo, 90, 50, 1);
        contextB.Servicos.Add(servicoB);
        await contextB.SaveChangesAsync();
        _servicoBId = servicoB.Id;
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Tenant_NaoConsultaNemEditaCatalogoDeOutraEmpresa()
    {
        await using var context = CriarContexto(_empresaAId);
        Assert.Null(await context.CategoriasServico.SingleOrDefaultAsync(item => item.Id == _categoriaBId));
        Assert.Null(await context.Servicos.SingleOrDefaultAsync(item => item.Id == _servicoBId));

        var servico = await context.Servicos.IgnoreQueryFilters().SingleAsync(item => item.Id == _servicoBId);
        servico.Desativar();
        await Assert.ThrowsAsync<ViolacaoIsolamentoTenantException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Servico_NaoPodeUsarCategoriaDeOutroTenant()
    {
        await using var context = CriarContexto(_empresaAId);
        context.Servicos.Add(new Servico(_empresaAId, _categoriaBId, "Associação inválida", null, TipoPrecificacao.Fixo, 10, 10, 1));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Pacote_NaoPodeUsarServicoDeOutroTenant()
    {
        await using var context = CriarContexto(_empresaAId);
        context.Pacotes.Add(new Pacote(_empresaAId, "Pacote inválido", null, TipoPrecificacao.Fixo, 10, [_servicoBId]));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task NomesSaoUnicosNoEscopoCorreto()
    {
        await using var context = CriarContexto(_empresaAId);
        context.CategoriasServico.Add(new CategoriaServico(_empresaAId, "Lavagem", null, 2));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        var categoriaNova = new CategoriaServico(_empresaAId, "Polimento", null, 2);
        context.CategoriasServico.Add(categoriaNova);
        await context.SaveChangesAsync();
        context.Servicos.Add(new Servico(_empresaAId, categoriaNova.Id, "Lavagem técnica", null, TipoPrecificacao.Fixo, 200, 90, 1));
        await context.SaveChangesAsync();

        context.Servicos.Add(new Servico(_empresaAId, _categoriaAId, "Lavagem técnica", null, TipoPrecificacao.Fixo, 200, 90, 3));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Pacote_CalculaSomaDuracaoEEconomiaSemPersistirDerivados()
    {
        await using var context = CriarContexto(_empresaAId);
        var detalhe = await new PacotesRepositorio(context).ObterDetalheAsync(_pacoteAId, CancellationToken.None);

        Assert.NotNull(detalhe);
        Assert.Equal(180m, detalhe.SomaServicos);
        Assert.Equal(30m, detalhe.Economia);
        Assert.Equal(90, detalhe.DuracaoEstimadaMinutos);
        Assert.Equal([1, 2], detalhe.Servicos.Select(item => item.Ordem));
    }

    [Fact]
    public async Task Pacote_PodeSubstituirComposicao()
    {
        await using var context = CriarContexto(_empresaAId);
        var repositorio = new PacotesRepositorio(context);
        var pacote = await repositorio.ObterParaAlteracaoAsync(_pacoteAId, CancellationToken.None);
        Assert.NotNull(pacote);
        repositorio.RemoverComposicaoAtual(pacote);
        pacote.Atualizar("Combo expresso", null, TipoPrecificacao.Fixo, 75, [_servicoA2Id]);
        repositorio.AdicionarComposicaoAtual(pacote);
        await repositorio.SalvarAsync(CancellationToken.None);

        var detalhe = await repositorio.ObterDetalheAsync(_pacoteAId, CancellationToken.None);
        Assert.NotNull(detalhe);
        Assert.Single(detalhe.Servicos);
        Assert.Equal(_servicoA2Id, detalhe.Servicos.Single().ServicoId);
    }

    [Fact]
    public async Task PesquisaFiltroEPaginacaoSaoExecutadosNoBanco()
    {
        await using var context = CriarContexto(_empresaAId);
        var repositorio = new ServicosRepositorio(context);
        var porNome = await repositorio.ListarAsync(new FiltroServicos(1, 10, "Descontaminação", null, null), CancellationToken.None);
        var porCategoria = await repositorio.ListarAsync(new FiltroServicos(1, 10, null, true, _categoriaAId), CancellationToken.None);

        Assert.Equal(_servicoA2Id, Assert.Single(porNome.Itens).Id);
        Assert.Equal(2, porCategoria.TotalItens);
        Assert.Equal(2, porCategoria.Itens.Count);
    }

    private DetaraDbContext CriarContexto(Guid empresaId) => new(_options, new UsuarioContextoTeste(empresaId));

    private sealed class UsuarioContextoTeste(Guid empresaId) : IUsuarioContexto
    {
        public Guid UsuarioId { get; } = Guid.NewGuid();
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado => true;
    }
}
