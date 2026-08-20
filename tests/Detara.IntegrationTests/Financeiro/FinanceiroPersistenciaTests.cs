using Detara.Application.Abstracoes;
using Detara.Application.Agenda;
using Detara.Application.Atendimento;
using Detara.Application.Financeiro;
using Detara.Domain.Atendimento;
using Detara.Domain.Entidades;
using Detara.Domain.Financeiro;
using Detara.Infrastructure.Atendimento;
using Detara.Infrastructure.Financeiro;
using Detara.Infrastructure.Persistencia;
using Detara.Infrastructure.Plataforma;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.Financeiro;

public sealed class FinanceiroPersistenciaTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<DetaraDbContext> _options = null!;
    private Guid _empresaA; private Guid _empresaB;
    private readonly Guid _usuarioA = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(_connection).Options;
        var empresaA = new Empresa("Estética A", "Estética A Ltda", "11111111000111", "estetica-a",
            fusoHorario: "America/Sao_Paulo");
        var empresaB = new Empresa("Estética B", "Estética B Ltda", "22222222000122", "estetica-b");
        _empresaA = empresaA.Id; _empresaB = empresaB.Id;
        await using var sistema = new DetaraDbContext(_options, UsuarioContextoTeste.Anonimo);
        await sistema.Database.EnsureCreatedAsync();
        sistema.Empresas.AddRange(empresaA, empresaB);
        await sistema.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task FinalizarExecucao_CriaContaNoMesmoSaveComSnapshotDaOs()
    {
        var ordem = CriarOrdemEmExecucao(240);
        await using (var preparar = Contexto(_empresaA))
        {
            preparar.OrdensServico.Add(ordem);
            await preparar.SaveChangesAsync();
        }

        await using var contexto = Contexto(_empresaA);
        var repositorio = new FinanceiroRepositorio(contexto);
        Assert.Equal(0, await contexto.ContasReceber.CountAsync());
        var handler = new FinalizarExecucaoHandler(new UsuarioContextoTeste(_empresaA, _usuarioA),
            new OrdensServicoRepositorio(contexto), new PlataformaAtendimentoConsulta(contexto),
            new IntegracaoFinanceiroOrdensServico(repositorio, new PlataformaFinanceiroConsulta(contexto),
                new ConversorFusoHorario()));

        await handler.Handle(new(ordem.Id, "Serviços concluídos"), default);

        var conta = await contexto.ContasReceber.SingleAsync();
        Assert.Equal(ordem.Id, conta.OrdemServicoId);
        Assert.Equal(ordem.Codigo, conta.OrdemServicoCodigoSnapshot);
        Assert.Equal("João Silva", conta.ClienteNomeSnapshot);
        Assert.Equal("BMW 323i", conta.VeiculoDescricaoSnapshot);
        Assert.Equal(240, conta.ValorOriginal);
        Assert.Equal(conta.DataCompetencia, conta.DataVencimento);
        Assert.Equal(StatusOrdemServico.AguardandoRetirada,
            (await contexto.OrdensServico.SingleAsync(x => x.Id == ordem.Id)).Status);

        await new ConcluirOrdemServicoHandler(new UsuarioContextoTeste(_empresaA, _usuarioA),
            new OrdensServicoRepositorio(contexto), new PlataformaAtendimentoConsulta(contexto))
            .Handle(new(ordem.Id, "Veículo entregue"), default);
        Assert.Equal(1, await contexto.ContasReceber.CountAsync());
    }

    [Fact]
    public async Task CancelarOsAntesDaRetirada_NaoCriaConta()
    {
        var ordem = CriarOrdemEmExecucao(240);
        await using var contexto = Contexto(_empresaA);
        contexto.OrdensServico.Add(ordem);
        await contexto.SaveChangesAsync();

        await new CancelarOrdemServicoHandler(new UsuarioContextoTeste(_empresaA, _usuarioA),
            new OrdensServicoRepositorio(contexto), new PlataformaAtendimentoConsulta(contexto))
            .Handle(new(ordem.Id, "Cliente desistiu"), default);

        Assert.Equal(0, await contexto.ContasReceber.CountAsync());
    }

    [Fact]
    public async Task Integracao_RepetidaEhIdempotente_ETotalZeroNaoCriaConta()
    {
        await using var contexto = Contexto(_empresaA);
        var repositorio = new FinanceiroRepositorio(contexto);
        var integracao = new IntegracaoFinanceiroOrdensServico(repositorio,
            new PlataformaFinanceiroConsulta(contexto), new ConversorFusoHorario());
        var evento = Evento(Guid.NewGuid(), 240);

        await integracao.PrepararContaReceberAsync(evento, default);
        await integracao.PrepararContaReceberAsync(evento, default);
        await contexto.SaveChangesAsync();
        await integracao.PrepararContaReceberAsync(evento, default);
        await integracao.PrepararContaReceberAsync(Evento(Guid.NewGuid(), 0), default);
        await contexto.SaveChangesAsync();

        Assert.Equal(1, await contexto.ContasReceber.CountAsync());
    }

    [Fact]
    public async Task PagamentoPersistido_EstornoPreservaHistoricoESaldo()
    {
        var conta = await CriarContaAsync(_empresaA);
        await using var contexto = Contexto(_empresaA);
        var entidade = await contexto.ContasReceber.Include(x => x.Pagamentos).SingleAsync(x => x.Id == conta.Id);
        var pix = entidade.RegistrarPagamento(FormaPagamento.Pix, 100, 0, null, null, DateTime.UtcNow, _usuarioA);
        var cartao = entidade.RegistrarPagamento(FormaPagamento.CartaoCredito, 140, 4, 2, null, DateTime.UtcNow, _usuarioA);
        contexto.Pagamentos.AddRange(pix, cartao);
        await contexto.SaveChangesAsync();
        entidade.EstornarPagamento(pix.Id, _usuarioA, "Pagamento duplicado", DateTime.UtcNow);
        await contexto.SaveChangesAsync();

        contexto.ChangeTracker.Clear();
        var atual = await contexto.ContasReceber.Include(x => x.Pagamentos).SingleAsync(x => x.Id == conta.Id);
        Assert.Equal(140, atual.ValorRecebido);
        Assert.Equal(100, atual.ValorEmAberto);
        Assert.Equal(StatusContaReceber.ParcialmentePago, atual.Status);
        Assert.Equal(2, atual.Pagamentos.Count);
        Assert.Equal(StatusPagamento.Estornado, atual.Pagamentos.Single(x => x.Id == pix.Id).Status);
    }

    [Fact]
    public async Task PagamentosConcorrentes_NaoUltrapassamSaldo()
    {
        var conta = await CriarContaAsync(_empresaA, 100);
        await using var contextoA = Contexto(_empresaA);
        await using var contextoB = Contexto(_empresaA);
        var a = await contextoA.ContasReceber.Include(x => x.Pagamentos).SingleAsync(x => x.Id == conta.Id);
        var b = await contextoB.ContasReceber.Include(x => x.Pagamentos).SingleAsync(x => x.Id == conta.Id);
        a.RegistrarPagamento(FormaPagamento.Pix, 80, 0, null, null, DateTime.UtcNow, _usuarioA);
        b.RegistrarPagamento(FormaPagamento.Dinheiro, 80, 0, null, null, DateTime.UtcNow, _usuarioA);
        contextoA.Pagamentos.Add(a.Pagamentos.Single());
        contextoB.Pagamentos.Add(b.Pagamentos.Single());

        await contextoA.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextoB.SaveChangesAsync());

        await using var verificar = Contexto(_empresaA);
        Assert.Equal(80, (await verificar.ContasReceber.SingleAsync(x => x.Id == conta.Id)).ValorRecebido);
        Assert.Equal(1, await verificar.Pagamentos.CountAsync(x => x.ContaReceberId == conta.Id));
    }

    [Fact]
    public async Task TenantA_NaoConsultaNemAlteraContaDoTenantB()
    {
        var contaB = await CriarContaAsync(_empresaB);
        await using var contextoA = Contexto(_empresaA);
        Assert.Null(await contextoA.ContasReceber.SingleOrDefaultAsync(x => x.Id == contaB.Id));

        var forjada = await contextoA.ContasReceber.IgnoreQueryFilters().Include(x => x.Pagamentos)
            .SingleAsync(x => x.Id == contaB.Id);
        forjada.AlterarVencimento(forjada.DataVencimento.AddDays(1));
        await Assert.ThrowsAsync<ViolacaoIsolamentoTenantException>(() => contextoA.SaveChangesAsync());
    }

    [Fact]
    public async Task TenantA_NaoRegistraNaoEstornaENaoAlteraVencimentoDoTenantB()
    {
        var contaB = await CriarContaAsync(_empresaB);
        await using (var contextoB = Contexto(_empresaB))
        {
            var b = await contextoB.ContasReceber.Include(x => x.Pagamentos).SingleAsync(x => x.Id == contaB.Id);
            var pagamento = b.RegistrarPagamento(FormaPagamento.Pix, 50, 0, null, null, DateTime.UtcNow, Guid.NewGuid());
            contextoB.Pagamentos.Add(pagamento);
            await contextoB.SaveChangesAsync();
            contaB = b;
        }

        await using var contextoA = Contexto(_empresaA);
        var usuarioA = new UsuarioContextoTeste(_empresaA, _usuarioA);
        var repositorio = new FinanceiroRepositorio(contextoA);
        var plataforma = new PlataformaFinanceiroConsulta(contextoA);
        var conversor = new ConversorFusoHorario();

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            new RegistrarPagamentoHandler(usuarioA, repositorio, plataforma, conversor).Handle(
                new(contaB.Id, FormaPagamento.Dinheiro, 10, 0, null, null, DateTime.Now), default));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            new EstornarPagamentoHandler(usuarioA, repositorio, plataforma, conversor).Handle(
                new(contaB.Id, contaB.Pagamentos.Single().Id, "Tentativa indevida"), default));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            new AlterarVencimentoContaReceberHandler(usuarioA, repositorio, plataforma, conversor).Handle(
                new(contaB.Id, contaB.DataVencimento.AddDays(1)), default));
    }

    [Fact]
    public async Task TenantA_NaoCriaContaUsandoFatoDaOsDoTenantB()
    {
        await using var contextoA = Contexto(_empresaA);
        var integracao = new IntegracaoFinanceiroOrdensServico(new FinanceiroRepositorio(contextoA),
            new PlataformaFinanceiroConsulta(contextoA), new ConversorFusoHorario());
        var eventoB = Evento(Guid.NewGuid(), 240) with { EmpresaId = _empresaB };

        await integracao.PrepararContaReceberAsync(eventoB, default);

        await Assert.ThrowsAsync<ViolacaoIsolamentoTenantException>(() => contextoA.SaveChangesAsync());
    }

    [Fact]
    public async Task Resumo_UsaCompetenciaEExcluiPagamentoEstornado()
    {
        var conta = await CriarContaAsync(_empresaA, 240, new DateOnly(2026, 8, 18));
        await using var contexto = Contexto(_empresaA);
        var atual = await contexto.ContasReceber.Include(x => x.Pagamentos).SingleAsync(x => x.Id == conta.Id);
        var estornado = atual.RegistrarPagamento(FormaPagamento.Pix, 100, 2, null, null,
            new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc), _usuarioA);
        var confirmado = atual.RegistrarPagamento(FormaPagamento.Dinheiro, 50, 0, null, null,
            new DateTime(2026, 8, 18, 13, 0, 0, DateTimeKind.Utc), _usuarioA);
        atual.EstornarPagamento(estornado.Id, _usuarioA, "Correção", DateTime.UtcNow);
        contexto.Pagamentos.AddRange(estornado, confirmado);
        await contexto.SaveChangesAsync();

        var resumo = await new FinanceiroRepositorio(contexto).ObterResumoAsync(new(2026, 8, 1),
            new(2026, 8, 31), new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), new(2026, 8, 19), default);

        Assert.Equal(240, resumo.Faturado);
        Assert.Equal(50, resumo.RecebidoBruto);
        Assert.Equal(0, resumo.Taxas);
        Assert.Equal(190, resumo.EmAbertoAtual);
        Assert.Equal(190, resumo.VencidoAtual);
    }

    [Fact]
    public async Task Listagem_PesquisaSnapshotsECalculaVencida()
    {
        await CriarContaAsync(_empresaA, 240, new DateOnly(2026, 8, 17));
        await CriarContaAsync(_empresaB, 90, new DateOnly(2026, 8, 17));
        await using var contexto = Contexto(_empresaA);
        var resultado = await new FinanceiroRepositorio(contexto).ListarAsync(new(1, 25, null, true,
            null, null, "ABC1D23", new DateOnly(2026, 8, 18)), default);

        var item = Assert.Single(resultado.Itens);
        Assert.True(item.Vencida);
        Assert.Equal(240, item.ValorOriginal);
    }

    private async Task<ContaReceber> CriarContaAsync(Guid empresaId, decimal valor = 240,
        DateOnly? competencia = null)
    {
        await using var contexto = Contexto(empresaId);
        var conta = new ContaReceber(empresaId, Guid.NewGuid(), $"OS-2026-{Guid.NewGuid():N}"[..20],
            Guid.NewGuid(), "João Silva", Guid.NewGuid(), "BMW 323i", "ABC1D23",
            valor, 0, 0, valor, competencia ?? new DateOnly(2026, 8, 18));
        contexto.ContasReceber.Add(conta);
        await contexto.SaveChangesAsync();
        return conta;
    }

    private OrdemServico CriarOrdemEmExecucao(decimal valor)
    {
        var ordem = new OrdemServico(_empresaA, 2026, new(Guid.NewGuid(), "João Silva", null, null,
            Guid.NewGuid(), "BMW 323i", "ABC1D23"), OrigemOrdemServico.AtendimentoDireto,
            null, null, 90, 0, 0, [new(TipoItemOrcamento.Servico, Guid.NewGuid(), null, null,
                "Lavagem técnica", null, valor, 1, 1, OrigemComercialOrdemServico.AcordoDireto,
                DateTime.UtcNow, _usuarioA, null)], _usuarioA, DateTime.UtcNow, "Autorizado");
        ordem.RealizarCheckIn(new(NivelExigenciaOperacional.Desabilitado,
            NivelExigenciaOperacional.Desabilitado, NivelExigenciaOperacional.Desabilitado,
            null, []), null, null, _usuarioA);
        ordem.IniciarExecucao(_usuarioA, null);
        return ordem;
    }

    private OrdemServicoFinalizadaFinanceiro Evento(Guid ordemId, decimal valor) => new(_empresaA,
        ordemId, "OS-2026-ABC", Guid.NewGuid(), "João Silva", Guid.NewGuid(), "BMW 323i",
        "ABC1D23", valor, 0, 0, valor, new DateTime(2026, 8, 18, 3, 30, 0, DateTimeKind.Utc));

    private DetaraDbContext Contexto(Guid empresaId) => new(_options,
        new UsuarioContextoTeste(empresaId, empresaId == _empresaA ? _usuarioA : Guid.NewGuid()));

    private sealed class UsuarioContextoTeste(Guid empresaId, Guid usuarioId, bool autenticado = true) : IUsuarioContexto
    {
        public static UsuarioContextoTeste Anonimo { get; } = new(Guid.Empty, Guid.Empty, false);
        public Guid UsuarioId { get; } = autenticado ? usuarioId : Guid.Empty;
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado { get; } = autenticado;
    }
}
