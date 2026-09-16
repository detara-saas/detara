using Detara.Application.Abstracoes;
using Detara.Application.Assinaturas;
using Detara.Application.Plataforma;
using Detara.Domain.Assinaturas;
using Detara.Domain.Entidades;
using Detara.Domain.Plataforma;
using Detara.Infrastructure.Assinaturas;
using Detara.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.Assinaturas;

public sealed class AssinaturasPlataformaServicoTests
{
    [Fact]
    public async Task ConfirmacaoComercial_EhTenantSafeAuditavelEIdempotente()
    {
        await using var conexao = new SqliteConnection("Data Source=:memory:");
        await conexao.OpenAsync();
        var opcoes = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(conexao).Options;
        var empresaA = new Empresa("Empresa A", "Empresa A Ltda", "11111111000111", "empresa-a-confirmacao");
        var empresaB = new Empresa("Empresa B", "Empresa B Ltda", "22222222000122", "empresa-b-confirmacao");
        var administrador = AdministradorAtivo();

        await using var db = new DetaraDbContext(opcoes, new ContextoGlobal());
        await db.Database.EnsureCreatedAsync();
        db.Empresas.AddRange(empresaA, empresaB);
        db.AdministradoresPlataforma.Add(administrador);
        await db.SaveChangesAsync();
        var servico = new AssinaturasPlataformaServico(db, opcoes, new StorageVazio(),
            new RelogioFixo(new DateTimeOffset(2026, 9, 20, 15, 0, 0, TimeSpan.Zero)));

        var criadaA = await servico.CriarAsync(administrador.Id, empresaA.Id,
            new(120m, new DateOnly(2026, 9, 16), 10, null, null), CancellationToken.None);
        await servico.CriarAsync(administrador.Id, empresaB.Id,
            new(120m, new DateOnly(2026, 9, 16), 10, null, null), CancellationToken.None);
        var confirmada = await servico.ConfirmarComercialmenteAsync(administrador.Id, empresaA.Id,
            new(new DateOnly(2026, 9, 20), criadaA.Assinatura.Versao!.Value,
                "Cliente confirmou continuidade."), CancellationToken.None);
        var repetida = await servico.ConfirmarComercialmenteAsync(administrador.Id, empresaA.Id,
            new(new DateOnly(2026, 9, 21), criadaA.Assinatura.Versao.Value,
                "Retry da confirmação."), CancellationToken.None);
        var outraEmpresa = await servico.ObterAsync(empresaB.Id, CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 9, 16), confirmada.Assinatura.InicioTeste);
        Assert.Equal(new DateOnly(2026, 9, 23), confirmada.Assinatura.FimTeste);
        Assert.Equal(new DateOnly(2026, 9, 20), confirmada.Assinatura.DataConfirmacaoComercial);
        Assert.Equal(new DateTime(2026, 9, 20, 15, 0, 0, DateTimeKind.Utc),
            confirmada.Assinatura.ConfirmacaoComercialRegistradaEmUtc);
        Assert.Equal(new DateOnly(2026, 10, 10), confirmada.Assinatura.PrimeiroVencimento);
        Assert.Equal("EmTeste", confirmada.Assinatura.Status);
        Assert.Equal(confirmada.Assinatura.Versao, repetida.Assinatura.Versao);
        Assert.Equal(confirmada.Assinatura.DataConfirmacaoComercial,
            repetida.Assinatura.DataConfirmacaoComercial);
        Assert.Single(repetida.Historico,
            x => x.TipoEvento == nameof(TipoEventoAssinatura.ConfirmacaoComercialRegistrada));
        Assert.Contains("Primeiro vencimento: 10/10/2026", repetida.Historico.Single(
            x => x.TipoEvento == nameof(TipoEventoAssinatura.ConfirmacaoComercialRegistrada)).Motivo);
        Assert.Null(outraEmpresa.Assinatura.DataConfirmacaoComercial);
    }

    [Fact]
    public async Task ConfirmacaoComercial_ExigeAdministradorPlataformaAtivoComMfa()
    {
        await using var conexao = new SqliteConnection("Data Source=:memory:");
        await conexao.OpenAsync();
        var opcoes = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(conexao).Options;
        var empresa = new Empresa("Empresa", "Empresa Ltda", "33333333000133", "empresa-segura-confirmacao");
        await using var db = new DetaraDbContext(opcoes, new ContextoGlobal());
        await db.Database.EnsureCreatedAsync();
        db.Empresas.Add(empresa);
        await db.SaveChangesAsync();
        var servico = new AssinaturasPlataformaServico(db, opcoes, new StorageVazio(), TimeProvider.System);

        await Assert.ThrowsAsync<CredenciaisPlataformaInvalidasException>(() =>
            servico.CriarAsync(Guid.NewGuid(), empresa.Id,
                new(120m, new DateOnly(2026, 9, 16), 10, null, null), CancellationToken.None));
    }

    private static AdministradorPlataforma AdministradorAtivo()
    {
        var administrador = new AdministradorPlataforma("Admin", "admin-plataforma@example.com", "hash");
        administrador.DefinirSegredoTotpProtegido("segredo-protegido");
        administrador.AtivarMfa(1);
        return administrador;
    }

    private sealed class ContextoGlobal : IUsuarioContexto
    {
        public Guid UsuarioId => Guid.Empty;
        public Guid EmpresaId => Guid.Empty;
        public bool EstaAutenticado => false;
    }

    private sealed class RelogioFixo(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }

    private sealed class StorageVazio : IArquivoStorage
    {
        public Task SalvarAsync(string chave, Stream conteudo, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task<Stream?> AbrirLeituraAsync(string chave, CancellationToken cancellationToken) =>
            Task.FromResult<Stream?>(null);
        public Task<bool> ExcluirAsync(string chave, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task<bool> ExisteAsync(string chave, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }
}
