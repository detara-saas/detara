using Detara.Application.Abstracoes;
using Detara.Application.Agenda;
using Detara.Domain.Agenda;
using Detara.Domain.Catalogo;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Agenda;
using Detara.Infrastructure.Catalogo;
using Detara.Infrastructure.Clientes;
using Detara.Infrastructure.Persistencia;
using Detara.Infrastructure.Plataforma;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.Agenda;

public sealed class AgendaPersistenciaTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<DetaraDbContext> _options = null!;
    private Guid _empresaA; private Guid _empresaB; private Guid _clienteA; private Guid _clienteA2; private Guid _veiculoA; private Guid _veiculoA2; private Guid _servicoA; private Guid _pacoteA; private Guid _clienteB; private Guid _veiculoB; private Guid _servicoB; private Guid _pacoteB; private Guid _agendamentoB;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync(); _options = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(_connection).Options;
        var empresaA = new Empresa("Empresa A", "Empresa A Ltda", "11111111000111", "empresa-a"); var empresaB = new Empresa("Empresa B", "Empresa B Ltda", "22222222000122", "empresa-b"); _empresaA = empresaA.Id; _empresaB = empresaB.Id;
        await using (var sistema = new DetaraDbContext(_options, UsuarioContextoTeste.Anonimo)) { await sistema.Database.EnsureCreatedAsync(); sistema.Empresas.AddRange(empresaA, empresaB); await sistema.SaveChangesAsync(); }
        await using (var a = Contexto(_empresaA))
        {
            var cliente = new Cliente(_empresaA, "João da Silva", TipoPessoa.PessoaFisica, null, "11999999999", null, null, null, null); var cliente2 = new Cliente(_empresaA, "Maria", TipoPessoa.PessoaFisica, null, null, null, null, null, null); a.Clientes.AddRange(cliente, cliente2); await a.SaveChangesAsync(); _clienteA = cliente.Id; _clienteA2 = cliente2.Id;
            var veiculo = new Veiculo(_empresaA, cliente.Id, "ABC1D23", "Honda", "Civic", null, 2024, 2024, "Preto", null, null); var veiculo2 = new Veiculo(_empresaA, cliente2.Id, "XYZ9Z99", "Toyota", "Corolla", null, 2023, 2024, "Prata", null, null); a.Veiculos.AddRange(veiculo, veiculo2); await a.SaveChangesAsync(); _veiculoA = veiculo.Id; _veiculoA2 = veiculo2.Id;
            var categoria = new CategoriaServico(_empresaA, "Lavagem", null, 1); a.CategoriasServico.Add(categoria); await a.SaveChangesAsync();
            var servico = new Servico(_empresaA, categoria.Id, "Lavagem técnica", "Referência", TipoPrecificacao.APartirDe, 100m, 90, 1); a.Servicos.Add(servico); await a.SaveChangesAsync(); _servicoA = servico.Id;
            var pacote = new Pacote(_empresaA, "Combo", null, TipoPrecificacao.Fixo, 150m, [servico.Id]); a.Pacotes.Add(pacote); await a.SaveChangesAsync(); _pacoteA = pacote.Id;
        }
        await using (var b = Contexto(_empresaB))
        {
            var cliente = new Cliente(_empresaB, "Cliente B", TipoPessoa.PessoaFisica, null, null, null, null, null, null); b.Clientes.Add(cliente); await b.SaveChangesAsync(); _clienteB = cliente.Id;
            var veiculo = new Veiculo(_empresaB, cliente.Id, "BBB1B11", "Ford", "Focus", null, 2020, 2020, null, null, null); b.Veiculos.Add(veiculo); await b.SaveChangesAsync(); _veiculoB = veiculo.Id;
            var categoria = new CategoriaServico(_empresaB, "Lavagem", null, 1); b.CategoriasServico.Add(categoria); await b.SaveChangesAsync();
            var servico = new Servico(_empresaB, categoria.Id, "Serviço B", null, TipoPrecificacao.Fixo, 80m, 60, 1); b.Servicos.Add(servico); await b.SaveChangesAsync(); _servicoB = servico.Id;
            var pacote = new Pacote(_empresaB, "Pacote B", null, TipoPrecificacao.Fixo, 80m, [servico.Id]); b.Pacotes.Add(pacote); await b.SaveChangesAsync(); _pacoteB = pacote.Id;
            var agendamento = new Agendamento(_empresaB, cliente.Id, cliente.Nome, veiculo.Id, "Ford Focus", veiculo.Placa, new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc), 60, null, null, [new(TipoItemAgendamento.Servico, servico.Id, servico.Nome, null, servico.TipoPrecificacao, servico.PrecoBase, servico.DuracaoEstimadaMinutos)]); b.Agendamentos.Add(agendamento); await b.SaveChangesAsync(); _agendamentoB = agendamento.Id;
        }
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact] public async Task EmpresaA_NaoConsultaAgendamentoEmpresaB() { await using var c = Contexto(_empresaA); Assert.Null(await new AgendaRepositorio(c).ObterDetalheAsync(_agendamentoB, default)); }
    [Fact] public async Task EmpresaA_NaoEditaAgendamentoEmpresaB() { await using var c = Contexto(_empresaA); Assert.Null(await new AgendaRepositorio(c).ObterParaAlteracaoAsync(_agendamentoB, default)); }
    [Fact] public async Task EmpresaA_NaoCancelaAgendamentoEmpresaB() { await using var c = Contexto(_empresaA); var item = await new AgendaRepositorio(c).ObterParaAlteracaoAsync(_agendamentoB, default); Assert.Null(item); }
    [Fact] public async Task EmpresaA_NaoAgendaClienteEmpresaB() { await using var c = Contexto(_empresaA); await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => CriarHandler(c).Handle(Comando(_clienteB, _veiculoB, TipoItemAgendamento.Servico, _servicoA), default)); }
    [Fact] public async Task EmpresaA_NaoAgendaVeiculoEmpresaB() { await using var c = Contexto(_empresaA); await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => CriarHandler(c).Handle(Comando(_clienteA, _veiculoB, TipoItemAgendamento.Servico, _servicoA), default)); }
    [Fact] public async Task EmpresaA_NaoAgendaServicoEmpresaB() { await using var c = Contexto(_empresaA); await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, TipoItemAgendamento.Servico, _servicoB), default)); }
    [Fact] public async Task EmpresaA_NaoAgendaPacoteEmpresaB() { await using var c = Contexto(_empresaA); await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, TipoItemAgendamento.Pacote, _pacoteB), default)); }

    [Fact]
    public async Task VeiculoNaoPertenceAoCliente_MesmoTenant_Rejeitado()
    { await using var c = Contexto(_empresaA); await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => CriarHandler(c).Handle(Comando(_clienteA, _veiculoA2, TipoItemAgendamento.Servico, _servicoA), default)); }

    [Theory]
    [InlineData("cliente")]
    [InlineData("veiculo")]
    [InlineData("servico")]
    [InlineData("pacote")]
    public async Task NovoAgendamento_RejeitaReferenciaInativa(string referencia)
    {
        await using var c = Contexto(_empresaA);
        if (referencia == "cliente") (await c.Clientes.SingleAsync(x => x.Id == _clienteA)).Desativar();
        if (referencia == "veiculo") (await c.Veiculos.SingleAsync(x => x.Id == _veiculoA)).Desativar();
        if (referencia == "servico") (await c.Servicos.SingleAsync(x => x.Id == _servicoA)).Desativar();
        if (referencia == "pacote") (await c.Pacotes.SingleAsync(x => x.Id == _pacoteA)).Desativar();
        await c.SaveChangesAsync();
        var tipo = referencia == "pacote" ? TipoItemAgendamento.Pacote : TipoItemAgendamento.Servico; var id = tipo == TipoItemAgendamento.Pacote ? _pacoteA : _servicoA;
        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() => CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, tipo, id), default));
    }

    [Fact]
    public async Task Sobreposicao_EhPermitida_EAvisoFicaDisponivel()
    {
        await using var c = Contexto(_empresaA); var handler = CriarHandler(c);
        var primeiro = await handler.Handle(Comando(_clienteA, _veiculoA, TipoItemAgendamento.Servico, _servicoA, new(2026, 8, 20, 9, 0, 0), 90), default);
        var segundo = await handler.Handle(Comando(_clienteA2, _veiculoA2, TipoItemAgendamento.Pacote, _pacoteA, new(2026, 8, 20, 9, 30, 0), 90), default);
        Assert.NotEqual(primeiro.Agendamento.Id, segundo.Agendamento.Id);
        Assert.Equal(1, segundo.QuantidadeSobreposicoes);
    }

    [Fact]
    public async Task SnapshotsPersistidos_NaoMudamAposEdicaoDosCadastros()
    {
        await using var c = Contexto(_empresaA); var criado = await CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, TipoItemAgendamento.Servico, _servicoA), default);
        var cliente = await c.Clientes.SingleAsync(x => x.Id == _clienteA); cliente.Atualizar("Nome novo", cliente.TipoPessoa, cliente.CpfCnpj, cliente.Telefone, cliente.WhatsApp, cliente.Email, cliente.DataNascimento, cliente.Observacao);
        var veiculo = await c.Veiculos.SingleAsync(x => x.Id == _veiculoA); veiculo.Atualizar(cliente.Id, "NEW1N11", "Nova", "Descrição", null, 2025, 2025, null, null, null);
        var servico = await c.Servicos.SingleAsync(x => x.Id == _servicoA); servico.Atualizar(servico.CategoriaServicoId, "Lavagem alterada", null, TipoPrecificacao.APartirDe, 120m, 120, 1); await c.SaveChangesAsync();
        var detalhe = await new AgendaRepositorio(c).ObterDetalheAsync(criado.Agendamento.Id, default);
        Assert.NotNull(detalhe); Assert.Equal("João da Silva", detalhe.ClienteNome); Assert.Equal("Honda Civic · ABC1D23", detalhe.VeiculoDescricao); Assert.Equal("ABC1D23", detalhe.VeiculoPlaca); var item = Assert.Single(detalhe.Itens); Assert.Equal("Lavagem técnica", item.Nome); Assert.Equal(100m, item.PrecoReferencia); Assert.Equal(90, item.DuracaoReferenciaMinutos);
    }

    [Fact]
    public async Task AgendamentoExistente_PreservaEPermiteEditarItemInativadoDepois()
    {
        await using var c = Contexto(_empresaA); var criado = await CriarHandler(c).Handle(Comando(_clienteA, _veiculoA, TipoItemAgendamento.Servico, _servicoA), default);
        var servico = await c.Servicos.SingleAsync(x => x.Id == _servicoA); servico.Desativar(); await c.SaveChangesAsync();
        var handler = new AtualizarAgendamentoHandler(new UsuarioContextoTeste(_empresaA), new CatalogoAgendaConsulta(c), new FusoHorarioEmpresaConsulta(c), new ConversorFusoHorario(), new AgendaRepositorio(c));
        var atualizado = await handler.Handle(new(criado.Agendamento.Id, _clienteA, _veiculoA, new DateTime(2026, 8, 20, 10, 0, 0), 120, "Mantida", null, [new(TipoItemAgendamento.Servico, _servicoA)]), default);

        var item = Assert.Single(atualizado.Itens);
        Assert.False(item.ItemAtivoNoCatalogo);
        Assert.Equal("Lavagem técnica", item.Item.Nome);
        Assert.Equal(100m, item.Item.PrecoReferencia);
    }

    [Fact]
    public async Task CriarAgendamentoValidator_ValidaColecaoDeItensSemFalhaDeInferencia()
    {
        var resultado = await new CriarAgendamentoValidator().ValidateAsync(
            Comando(_clienteA, _veiculoA, TipoItemAgendamento.Servico, _servicoA));

        Assert.True(resultado.IsValid);
    }

    [Fact]
    public async Task BuscaCatalogoAgenda_FiltraAntesDaProjecao()
    {
        await using var contexto = Contexto(_empresaA);

        var itens = await new CatalogoAgendaConsulta(contexto)
            .BuscarItensAsync(_empresaA, "Lavagem", false, 20, default);

        Assert.Contains(itens, item => item.Id == _servicoA);
        Assert.DoesNotContain(itens, item => item.Id == _servicoB);
    }

    private CriarAgendamentoHandler CriarHandler(DetaraDbContext c) => new(new UsuarioContextoTeste(_empresaA), new ClientesAgendaConsulta(c), new CatalogoAgendaConsulta(c), new FusoHorarioEmpresaConsulta(c), new ConversorFusoHorario(), new AgendaRepositorio(c));
    private static CriarAgendamentoCommand Comando(Guid cliente, Guid veiculo, TipoItemAgendamento tipo, Guid item, DateTime? inicio = null, int duracao = 90) => new(cliente, veiculo, inicio ?? new DateTime(2026, 8, 20, 9, 0, 0), duracao, null, null, [new(tipo, item)]);
    private DetaraDbContext Contexto(Guid empresaId) => new(_options, new UsuarioContextoTeste(empresaId));
    private sealed class UsuarioContextoTeste(Guid empresaId, bool autenticado = true) : IUsuarioContexto { public static UsuarioContextoTeste Anonimo { get; } = new(Guid.Empty, false); public Guid UsuarioId { get; } = autenticado ? Guid.NewGuid() : Guid.Empty; public Guid EmpresaId { get; } = empresaId; public bool EstaAutenticado { get; } = autenticado; }
}
