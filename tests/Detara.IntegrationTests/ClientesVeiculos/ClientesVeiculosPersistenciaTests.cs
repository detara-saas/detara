using Detara.Application.Abstracoes;
using Detara.Application.Clientes;
using Detara.Application.Veiculos;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Clientes;
using Detara.Infrastructure.Persistencia;
using Detara.Infrastructure.Veiculos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.ClientesVeiculos;

public sealed class ClientesVeiculosPersistenciaTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<DetaraDbContext> _options = null!;
    private Guid _empresaAId;
    private Guid _empresaBId;
    private Guid _clienteAId;
    private Guid _clienteBId;
    private Guid _veiculoAId;
    private Guid _veiculoBId;

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
        var clienteA = CriarCliente(_empresaAId, "Cliente A", "52998224725", "41999990001");
        contextA.Clientes.Add(clienteA);
        await contextA.SaveChangesAsync();
        _clienteAId = clienteA.Id;
        var veiculoA = CriarVeiculo(_empresaAId, clienteA.Id, "ABC1D23");
        contextA.Veiculos.Add(veiculoA);
        await contextA.SaveChangesAsync();
        _veiculoAId = veiculoA.Id;

        await using var contextB = CriarContexto(_empresaBId);
        var clienteB = CriarCliente(_empresaBId, "Cliente B", "11144477735", "41999990002");
        contextB.Clientes.Add(clienteB);
        await contextB.SaveChangesAsync();
        _clienteBId = clienteB.Id;
        var veiculoB = CriarVeiculo(_empresaBId, clienteB.Id, "XYZ9Z99");
        contextB.Veiculos.Add(veiculoB);
        await contextB.SaveChangesAsync();
        _veiculoBId = veiculoB.Id;
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task EmpresaA_NaoPodeConsultarClienteEmpresaB()
    {
        await using var context = CriarContexto(_empresaAId);
        Assert.Null(await context.Clientes.SingleOrDefaultAsync(item => item.Id == _clienteBId));
    }

    [Fact]
    public async Task EmpresaA_NaoPodeEditarClienteEmpresaB()
    {
        await using var context = CriarContexto(_empresaAId);
        var cliente = await context.Clientes.IgnoreQueryFilters().SingleAsync(item => item.Id == _clienteBId);
        cliente.Atualizar("Tentativa", TipoPessoa.PessoaFisica, "11144477735", null, null, null, null, null);
        await Assert.ThrowsAsync<ViolacaoIsolamentoTenantException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task EmpresaA_NaoPodeInativarClienteEmpresaB()
    {
        await using var context = CriarContexto(_empresaAId);
        var cliente = await context.Clientes.IgnoreQueryFilters().SingleAsync(item => item.Id == _clienteBId);
        cliente.Desativar();
        await Assert.ThrowsAsync<ViolacaoIsolamentoTenantException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task EmpresaA_NaoPodeConsultarVeiculoEmpresaB()
    {
        await using var context = CriarContexto(_empresaAId);
        Assert.Null(await context.Veiculos.SingleOrDefaultAsync(item => item.Id == _veiculoBId));
    }

    [Fact]
    public async Task EmpresaA_NaoPodeEditarVeiculoEmpresaB()
    {
        await using var context = CriarContexto(_empresaAId);
        var veiculo = await context.Veiculos.IgnoreQueryFilters().SingleAsync(item => item.Id == _veiculoBId);
        veiculo.Atualizar(_clienteBId, "XYZ9Z99", "Marca", "Alterado", null, 2020, 2020, null, 0, null);
        await Assert.ThrowsAsync<ViolacaoIsolamentoTenantException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task EmpresaA_NaoPodeAssociarVeiculoAClienteEmpresaB()
    {
        await using var context = CriarContexto(_empresaAId);
        context.Veiculos.Add(CriarVeiculo(_empresaAId, _clienteBId, "DEF2E34"));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task CpfCnpjDuplicadoDentroDaMesmaEmpresa_EhBloqueado()
    {
        await using var context = CriarContexto(_empresaAId);
        context.Clientes.Add(CriarCliente(_empresaAId, "Duplicado", "52998224725", null));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task CpfCnpjIgualEmEmpresasDiferentes_EhPermitido()
    {
        await using var context = CriarContexto(_empresaBId);
        context.Clientes.Add(CriarCliente(_empresaBId, "Documento compartilhado", "52998224725", null));
        await context.SaveChangesAsync();
        Assert.True(await context.Clientes.AnyAsync(item => item.Nome == "Documento compartilhado"));
    }

    [Fact]
    public async Task PlacaDuplicadaDentroDaMesmaEmpresa_EhBloqueada()
    {
        await using var context = CriarContexto(_empresaAId);
        context.Veiculos.Add(CriarVeiculo(_empresaAId, _clienteAId, "ABC1D23"));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task PlacaIgualEmEmpresasDiferentes_EhPermitida()
    {
        await using var context = CriarContexto(_empresaBId);
        context.Veiculos.Add(CriarVeiculo(_empresaBId, _clienteBId, "ABC1D23"));
        await context.SaveChangesAsync();
        Assert.True(await context.Veiculos.AnyAsync(item => item.Placa == "ABC1D23"));
    }

    [Fact]
    public async Task DoisVeiculosSemPlacaNoMesmoTenant_SaoPermitidos()
    {
        await using var context = CriarContexto(_empresaAId);
        context.Veiculos.AddRange(
            new Veiculo(_empresaAId, _clienteAId, TipoVeiculo.MotoAquatica, null, "JET-001",
                "Sea-Doo", "GTX 300", null, 2025, 2025, null, 0, null),
            new Veiculo(_empresaAId, _clienteAId, TipoVeiculo.Embarcacao, " ", "JET-001",
                "Yamaha", "242X", null, 2024, 2024, null, 0, null));

        await context.SaveChangesAsync();

        Assert.Equal(2, await context.Veiculos.CountAsync(item => item.Placa == null));
    }

    [Fact]
    public async Task CriarVeiculoSemPlaca_PersisteTipoEAlternativa_EPermiteBusca()
    {
        await using var context = CriarContexto(_empresaAId);
        var resultado = await new CriarVeiculoCommandHandler(
            new UsuarioContextoTeste(_empresaAId),
            new ClientesRepositorio(context),
            new VeiculosRepositorio(context)).Handle(
                new CriarVeiculoCommand(_clienteAId, TipoVeiculo.MotoAquatica, null,
                    "  JET-001  ", "Sea-Doo", "GTX 300", null, 2025, 2025, null, 0, null),
                CancellationToken.None);

        Assert.Null(resultado.Placa);
        Assert.Equal(TipoVeiculo.MotoAquatica, resultado.Tipo);
        Assert.Equal("JET-001", resultado.IdentificacaoAlternativa);
        var pagina = await new VeiculosRepositorio(context).ListarAsync(
            new FiltroVeiculos(1, 10, "JET-001", true), CancellationToken.None);
        var item = Assert.Single(pagina.Itens, item => item.Id == resultado.Id);
        Assert.Equal("Sea-Doo GTX 300 · JET-001", item.Descricao);
    }

    [Fact]
    public async Task EdicaoVeiculo_PermiteAdicionarERemoverPlaca()
    {
        await using var context = CriarContexto(_empresaAId);
        var veiculos = new VeiculosRepositorio(context);
        var handler = new AtualizarVeiculoCommandHandler(
            new UsuarioContextoTeste(_empresaAId), new ClientesRepositorio(context), veiculos);

        var comPlaca = await handler.Handle(new AtualizarVeiculoCommand(
            _veiculoAId, _clienteAId, TipoVeiculo.Carro, "DEF-2E34", "REF-01",
            "Honda", "Civic", null, 2024, 2024, "Preto", 1000, null), default);
        Assert.Equal("DEF2E34", comPlaca.Placa);

        var semPlaca = await handler.Handle(new AtualizarVeiculoCommand(
            _veiculoAId, _clienteAId, TipoVeiculo.Carro, " ", "REF-01",
            "Honda", "Civic", null, 2024, 2024, "Preto", 1000, null), default);
        Assert.Null(semPlaca.Placa);
        var item = Assert.Single((await veiculos.ListarAsync(
            new FiltroVeiculos(1, 10, "REF-01", true), default)).Itens);
        Assert.Equal("Honda Civic · REF-01", item.Descricao);
    }

    [Fact]
    public async Task Validator_RejeitaPlacaInvalida_EAceitaAusente()
    {
        var invalido = await new CriarVeiculoCommandValidator().ValidateAsync(
            new CriarVeiculoCommand(_clienteAId, TipoVeiculo.Carro, "AB-123", null,
                "Honda", "Civic", null, null, null, null, null, null));
        var ausente = await new CriarVeiculoCommandValidator().ValidateAsync(
            new CriarVeiculoCommand(_clienteAId, TipoVeiculo.Carro, null, null,
                "Porsche", "911", null, null, null, null, null, null));

        Assert.Contains(invalido.Errors, erro => erro.PropertyName == nameof(CriarVeiculoCommand.Placa));
        Assert.True(ausente.IsValid);
    }

    [Fact]
    public async Task PesquisaClientePorPlaca_UsaPaginacaoNoBanco()
    {
        await using var context = CriarContexto(_empresaAId);
        var repositorio = new ClientesRepositorio(context);
        var pagina = await repositorio.ListarAsync(
            new FiltroClientes(1, 10, "ABC-1D23", null, null),
            CancellationToken.None);
        var cliente = Assert.Single(pagina.Itens);
        Assert.Equal(_clienteAId, cliente.Id);
        Assert.Equal(1, cliente.QuantidadeVeiculos);
    }

    [Fact]
    public async Task PaginacaoClientes_RetornaPaginaETotalCorretos()
    {
        await using var context = CriarContexto(_empresaAId);
        for (var indice = 0; indice < 11; indice++)
        {
            context.Clientes.Add(CriarCliente(_empresaAId, $"Cliente extra {indice:00}", null, null));
        }

        await context.SaveChangesAsync();
        var pagina = await new ClientesRepositorio(context).ListarAsync(
            new FiltroClientes(2, 10, null, null, null),
            CancellationToken.None);
        Assert.Equal(12, pagina.TotalItens);
        Assert.Equal(2, pagina.Itens.Count);
        Assert.Equal(2, pagina.TotalPaginas);
    }

    [Fact]
    public async Task PesquisaVeiculoPorTelefoneDoCliente_RetornaVeiculo()
    {
        await using var context = CriarContexto(_empresaAId);
        var pagina = await new VeiculosRepositorio(context).ListarAsync(
            new FiltroVeiculos(1, 10, "999990001", null),
            CancellationToken.None);
        Assert.Equal(_veiculoAId, Assert.Single(pagina.Itens).Id);
    }

    [Fact]
    public async Task EdicaoVeiculo_CarregaNomeDoClienteEPreservaVinculoSemAlteracao()
    {
        await using var context = CriarContexto(_empresaAId);
        var veiculos = new VeiculosRepositorio(context);
        var detalhe = await new ObterVeiculoQueryHandler(veiculos)
            .Handle(new ObterVeiculoQuery(_veiculoAId), CancellationToken.None);

        Assert.Equal(_clienteAId, detalhe.ClienteId);
        Assert.Equal("Cliente A", detalhe.ClienteNome);

        var atualizado = await new AtualizarVeiculoCommandHandler(
            new UsuarioContextoTeste(_empresaAId),
            new ClientesRepositorio(context),
            veiculos).Handle(
                new AtualizarVeiculoCommand(
                    detalhe.Id,
                    detalhe.ClienteId,
                    detalhe.Tipo,
                    detalhe.Placa,
                    detalhe.IdentificacaoAlternativa,
                    detalhe.Marca,
                    detalhe.Modelo,
                    detalhe.Versao,
                    detalhe.AnoFabricacao,
                    detalhe.AnoModelo,
                    detalhe.Cor,
                    detalhe.Quilometragem,
                    detalhe.Observacao),
                CancellationToken.None);

        Assert.Equal(_clienteAId, atualizado.ClienteId);
        Assert.Equal("Cliente A", atualizado.ClienteNome);
    }

    [Fact]
    public async Task EdicaoVeiculo_RejeitaClienteDeOutroTenant()
    {
        await using var context = CriarContexto(_empresaAId);
        var handler = new AtualizarVeiculoCommandHandler(
            new UsuarioContextoTeste(_empresaAId),
            new ClientesRepositorio(context),
            new VeiculosRepositorio(context));

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => handler.Handle(
            new AtualizarVeiculoCommand(
                _veiculoAId,
                _clienteBId,
                TipoVeiculo.Carro,
                "ABC1D23",
                null,
                "Honda",
                "Civic",
                null,
                2024,
                2024,
                "Preto",
                1000,
                null),
            CancellationToken.None));
    }

    private DetaraDbContext CriarContexto(Guid empresaId) =>
        new(_options, new UsuarioContextoTeste(empresaId));

    private static Cliente CriarCliente(Guid empresaId, string nome, string? documento, string? telefone) =>
        new(empresaId, nome, TipoPessoa.PessoaFisica, documento, telefone, null, null, null, null);

    private static Veiculo CriarVeiculo(Guid empresaId, Guid clienteId, string placa) =>
        new(empresaId, clienteId, placa, "Honda", "Civic", null, 2024, 2024, "Preto", 1000, null);

    private sealed class UsuarioContextoTeste(Guid empresaId) : IUsuarioContexto
    {
        public Guid UsuarioId { get; } = Guid.NewGuid();
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado => true;
    }
}
