using System.Security.Cryptography;
using System.Text;
using Detara.Application.Abstracoes;
using Detara.Application.Assinaturas;
using Detara.Domain.Assinaturas;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Assinaturas;
using Detara.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.Assinaturas;

public sealed class AssinaturasTenantServicoTests
{
    [Fact]
    public async Task Aceite_UsaDadosDoTenantPersisteHashEPdfImutavelEPermaneceIdempotente()
    {
        await using var conexao = new SqliteConnection("Data Source=:memory:");
        await conexao.OpenAsync();
        var opcoes = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(conexao).Options;
        var empresa = new Empresa("Empresa Teste", "Empresa Teste Ltda", "12345678000199", "empresa-teste");
        var perfil = new Perfil(empresa.Id, "Administrador");
        var usuario = new Usuario(empresa.Id, perfil.Id, "Responsável Legal", "responsavel@example.com", "hash");
        var assinatura = new AssinaturaEmpresa(empresa.Id, 135.50m, new DateOnly(2026, 9, 16));
        var contextoUsuario = new Contexto(empresa.Id, usuario.Id);

        await using var db = new DetaraDbContext(opcoes, contextoUsuario);
        await db.Database.EnsureCreatedAsync();
        db.Empresas.Add(empresa);
        db.Perfis.Add(perfil);
        db.Usuarios.Add(usuario);
        db.AssinaturasEmpresas.Add(assinatura);
        await db.SaveChangesAsync();

        var storage = new StorageEmMemoria();
        var gerador = new GeradorDeterministico();
        var instante = new DateTimeOffset(2026, 9, 16, 14, 30, 0, TimeSpan.Zero);
        var servico = new AssinaturasTenantServico(db, contextoUsuario, storage, gerador,
            new RelogioFixo(instante));

        var primeiro = await servico.AceitarAsync("127.0.0.1", CancellationToken.None);
        var documentoOriginal = storage.Arquivos.Values.Single().ToArray();
        var segundo = await servico.AceitarAsync("127.0.0.1", CancellationToken.None);

        Assert.NotNull(primeiro.TermoAceito);
        Assert.Equal(primeiro.TermoAceito!.Id, segundo.TermoAceito!.Id);
        Assert.Equal(1, gerador.QuantidadeGeracoes);
        Assert.Equal(1, storage.QuantidadeSalvamentos);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(documentoOriginal)), primeiro.TermoAceito.HashSha256);
        Assert.Equal("1.0", primeiro.TermoAceito.VersaoTermo);
        Assert.Equal("Empresa Teste Ltda", gerador.UltimosDados!.EmpresaNome);
        Assert.Equal("12345678000199", gerador.UltimosDados.EmpresaDocumento);
        Assert.Equal("Responsável Legal", gerador.UltimosDados.ResponsavelNome);
        Assert.Equal(135.50m, gerador.UltimosDados.ValorMensal);
        Assert.Equal(new DateOnly(2026, 10, 10), gerador.UltimosDados.PrimeiroVencimento);
        Assert.Equal(instante.UtcDateTime, gerador.UltimosDados.AceitoEmUtc);
        Assert.Equal(new DateOnly(2026, 9, 16), primeiro.DataConfirmacaoComercial);
        Assert.Equal(instante.UtcDateTime, primeiro.ConfirmacaoComercialRegistradaEmUtc);
        Assert.Equal(new DateOnly(2026, 9, 23), primeiro.FimTeste);
        Assert.Single(await db.AceitesTermosAssinaturas.ToListAsync());
        Assert.Single(await db.HistoricosAssinaturasEmpresas
            .Where(x => x.TipoEvento == TipoEventoAssinatura.TermoAceito).ToListAsync());
        Assert.Single(await db.HistoricosAssinaturasEmpresas
            .Where(x => x.TipoEvento == TipoEventoAssinatura.ConfirmacaoComercialRegistrada).ToListAsync());

        empresa.AtualizarCadastro("Empresa Renomeada", "Empresa Renomeada Ltda", "99999999000199",
            null, null, "America/Sao_Paulo", empresa.VersaoCadastro);
        await db.SaveChangesAsync();
        var documentoAceito = await servico.AbrirTermoAceitoAsync(CancellationToken.None);
        await using var streamAceito = documentoAceito.Conteudo;
        using var copia = new MemoryStream();
        await streamAceito.CopyToAsync(copia);
        Assert.Equal(documentoOriginal, copia.ToArray());
    }

    [Fact]
    public async Task Aceite_AposFimDoTesteUsaConfirmacaoComoBaseDoPrimeiroVencimento()
    {
        await using var conexao = new SqliteConnection("Data Source=:memory:");
        await conexao.OpenAsync();
        var opcoes = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(conexao).Options;
        var empresa = new Empresa("Empresa Tardia", "Empresa Tardia Ltda", "44444444000144",
            "empresa-confirmacao-tardia");
        var perfil = new Perfil(empresa.Id, "Administrador");
        var usuario = new Usuario(empresa.Id, perfil.Id, "Responsável", "tardia@example.com", "hash");
        var assinatura = new AssinaturaEmpresa(empresa.Id, 120m, new DateOnly(2026, 10, 1));
        var contextoUsuario = new Contexto(empresa.Id, usuario.Id);

        await using var db = new DetaraDbContext(opcoes, contextoUsuario);
        await db.Database.EnsureCreatedAsync();
        db.AddRange(empresa, perfil, usuario, assinatura);
        await db.SaveChangesAsync();
        var servico = new AssinaturasTenantServico(db, contextoUsuario, new StorageEmMemoria(),
            new GeradorDeterministico(),
            new RelogioFixo(new DateTimeOffset(2026, 10, 12, 15, 0, 0, TimeSpan.Zero)));

        var resultado = await servico.AceitarAsync("127.0.0.1", CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 10, 1), resultado.InicioTeste);
        Assert.Equal(new DateOnly(2026, 10, 8), resultado.FimTeste);
        Assert.Equal(new DateOnly(2026, 10, 12), resultado.DataConfirmacaoComercial);
        Assert.Equal(new DateOnly(2026, 11, 10), resultado.PrimeiroVencimento);
    }

    [Fact]
    public async Task AceiteV1Historico_NaoInfereConfirmacaoNemAlteraDocumento()
    {
        await using var conexao = new SqliteConnection("Data Source=:memory:");
        await conexao.OpenAsync();
        var opcoes = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(conexao).Options;
        var empresa = new Empresa("Empresa Histórica", "Empresa Histórica Ltda", "55555555000155",
            "empresa-aceite-historico");
        var perfil = new Perfil(empresa.Id, "Administrador");
        var usuario = new Usuario(empresa.Id, perfil.Id, "Responsável", "historico@example.com", "hash");
        var assinatura = new AssinaturaEmpresa(empresa.Id, 120m, new DateOnly(2026, 9, 16));
        var aceite = new AceiteTermoAssinatura(empresa.Id, assinatura.Id, usuario.Id, "1.0",
            new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc), "empresas/historico/termo.pdf",
            new string('D', 64), 120m, assinatura.DataInicio, assinatura.PrimeiroVencimento,
            empresa.RazaoSocial, empresa.CpfCnpj, usuario.Nome, usuario.Email, null);
        var contextoUsuario = new Contexto(empresa.Id, usuario.Id);

        await using var db = new DetaraDbContext(opcoes, contextoUsuario);
        await db.Database.EnsureCreatedAsync();
        db.AddRange(empresa, perfil, usuario, assinatura, aceite);
        await db.SaveChangesAsync();
        var storage = new StorageEmMemoria();
        var gerador = new GeradorDeterministico();
        var servico = new AssinaturasTenantServico(db, contextoUsuario, storage, gerador,
            new RelogioFixo(new DateTimeOffset(2026, 9, 20, 15, 0, 0, TimeSpan.Zero)));

        var resultado = await servico.AceitarAsync("127.0.0.1", CancellationToken.None);

        Assert.Null(resultado.DataConfirmacaoComercial);
        Assert.Null(resultado.ConfirmacaoComercialRegistradaEmUtc);
        Assert.Equal(aceite.Id, resultado.TermoAceito!.Id);
        Assert.Equal(0, gerador.QuantidadeGeracoes);
        Assert.Equal(0, storage.QuantidadeSalvamentos);
        Assert.Empty(await db.HistoricosAssinaturasEmpresas.ToListAsync());
    }

    [Fact]
    public async Task TermoAceito_DeOutroTenantNaoPodeSerAberto()
    {
        await using var conexao = new SqliteConnection("Data Source=:memory:");
        await conexao.OpenAsync();
        var opcoes = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(conexao).Options;
        var empresaA = new Empresa("Empresa A", "Empresa A Ltda", "11111111000111", "empresa-a-termo");
        var empresaB = new Empresa("Empresa B", "Empresa B Ltda", "22222222000122", "empresa-b-termo");
        var usuarioAId = Guid.NewGuid();

        await using (var setup = new DetaraDbContext(opcoes, new Contexto(Guid.Empty, Guid.Empty)))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Empresas.AddRange(empresaA, empresaB);
            await setup.SaveChangesAsync();
        }

        await using (var setupA = new DetaraDbContext(opcoes, new Contexto(empresaA.Id, usuarioAId)))
        {
            var perfilA = new Perfil(empresaA.Id, "Administrador");
            var usuarioA = new Usuario(empresaA.Id, perfilA.Id, "Responsável A", "a@example.com", "hash");
            usuarioAId = usuarioA.Id;
            setupA.Perfis.Add(perfilA);
            setupA.Usuarios.Add(usuarioA);
            var assinatura = new AssinaturaEmpresa(empresaA.Id, 120m, new DateOnly(2026, 9, 16));
            setupA.AssinaturasEmpresas.Add(assinatura);
            setupA.AceitesTermosAssinaturas.Add(new AceiteTermoAssinatura(empresaA.Id, assinatura.Id,
                usuarioAId, "1.0", DateTime.UtcNow, "empresas/a/termo.pdf", new string('C', 64),
                120m, assinatura.DataInicio, assinatura.PrimeiroVencimento, empresaA.RazaoSocial,
                empresaA.CpfCnpj, "Responsável A", "a@example.com", null));
            await setupA.SaveChangesAsync();
        }

        var contextoB = new Contexto(empresaB.Id, Guid.NewGuid());
        await using var dbB = new DetaraDbContext(opcoes, contextoB);
        var servicoB = new AssinaturasTenantServico(dbB, contextoB, new StorageEmMemoria(),
            new GeradorDeterministico(), TimeProvider.System);

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            servicoB.AbrirTermoAceitoAsync(CancellationToken.None));
    }

    [Fact]
    public void GeradorReal_ProduzDocumentoPdf()
    {
        var pdf = new PdfTermoAdesaoGenerator().Gerar(new DadosTermoAssinatura(
            "Empresa Ltda", "12345678000199", "Responsável", "responsavel@example.com", 120m,
            new DateOnly(2026, 9, 16), new DateOnly(2026, 9, 23), new DateOnly(2026, 10, 10),
            10, "1.0"));

        Assert.True(pdf.Length > 1_000);
        Assert.True(pdf.AsSpan().StartsWith("%PDF"u8));
        Assert.Equal("1.0", TermosAssinatura.VersaoAtual);
    }

    private sealed class Contexto(Guid empresaId, Guid usuarioId) : IUsuarioContexto
    {
        public Guid UsuarioId { get; } = usuarioId;
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado => EmpresaId != Guid.Empty;
    }

    private sealed class RelogioFixo(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }

    private sealed class GeradorDeterministico : IGeradorPdfTermoAssinatura
    {
        public int QuantidadeGeracoes { get; private set; }
        public DadosTermoAssinatura? UltimosDados { get; private set; }

        public byte[] Gerar(DadosTermoAssinatura dados)
        {
            QuantidadeGeracoes++;
            UltimosDados = dados;
            return Encoding.UTF8.GetBytes($"PDF|{dados.EmpresaNome}|{dados.EmpresaDocumento}|{dados.ValorMensal}|{dados.AceitoEmUtc:O}");
        }
    }

    private sealed class StorageEmMemoria : IArquivoStorage
    {
        public Dictionary<string, byte[]> Arquivos { get; } = [];
        public int QuantidadeSalvamentos { get; private set; }

        public async Task SalvarAsync(string chave, Stream conteudo, CancellationToken cancellationToken)
        {
            using var memoria = new MemoryStream();
            await conteudo.CopyToAsync(memoria, cancellationToken);
            Arquivos.Add(chave, memoria.ToArray());
            QuantidadeSalvamentos++;
        }

        public Task<Stream?> AbrirLeituraAsync(string chave, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Stream?>(Arquivos.TryGetValue(chave, out var conteudo)
                ? new MemoryStream(conteudo, writable: false)
                : null);
        }

        public Task<bool> ExcluirAsync(string chave, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Arquivos.Remove(chave));
        }

        public Task<bool> ExisteAsync(string chave, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Arquivos.ContainsKey(chave));
        }
    }
}
