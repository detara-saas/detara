using Detara.Application.Abstracoes;
using Detara.Domain.Assinaturas;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Detara.Api.Assinaturas;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Detara.IntegrationTests.Assinaturas;

public sealed class AssinaturasPersistenciaTests
{
    [Fact]
    public async Task AssinaturaEAceite_FicamIsoladosPorTenantEEmpresaLegadaPermaneceSemBloqueio()
    {
        await using var conexao = new SqliteConnection("Data Source=:memory:");
        await conexao.OpenAsync();
        var opcoes = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(conexao).Options;
        var empresaA = new Empresa("Empresa A", "Empresa A Ltda", "11111111111", "empresa-a");
        var empresaB = new Empresa("Empresa B", "Empresa B Ltda", "22222222222", "empresa-b");
        await using (var setup = new DetaraDbContext(opcoes, new Contexto(Guid.Empty)))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Empresas.AddRange(empresaA, empresaB);
            await setup.SaveChangesAsync();
        }

        await using (var contextoA = new DetaraDbContext(opcoes, new Contexto(empresaA.Id)))
        {
            var perfil = new Perfil(empresaA.Id, "Administrador");
            var usuario = new Usuario(empresaA.Id, perfil.Id, "Responsável A", "a@example.com", "hash");
            contextoA.Perfis.Add(perfil);
            contextoA.Usuarios.Add(usuario);
            var assinatura = new AssinaturaEmpresa(empresaA.Id, 120m, new DateOnly(2026, 9, 16));
            contextoA.AssinaturasEmpresas.Add(assinatura);
            contextoA.AceitesTermosAssinaturas.Add(new(empresaA.Id, assinatura.Id, usuario.Id, "1.0",
                DateTime.UtcNow, $"empresas/{empresaA.Id:N}/termo.pdf", new string('A', 64), 120m,
                assinatura.DataInicio, assinatura.PrimeiroVencimento, empresaA.RazaoSocial,
                empresaA.CpfCnpj, "Responsável A", "a@example.com", null));
            await contextoA.SaveChangesAsync();
        }

        await using var contextoB = new DetaraDbContext(opcoes, new Contexto(empresaB.Id, Guid.NewGuid()));
        Assert.False(await contextoB.AssinaturasEmpresas.AnyAsync());
        Assert.False(await contextoB.AceitesTermosAssinaturas.AnyAsync());

        // Compatibilidade: ausência de registro explícito não representa suspensão.
        Assert.False(await contextoB.AssinaturasEmpresas.AnyAsync(x => x.Status == StatusAssinaturaEmpresa.Suspensa));
    }

    [Fact]
    public async Task AceiteEHistorico_SaoAppendOnly()
    {
        await using var conexao = new SqliteConnection("Data Source=:memory:");
        await conexao.OpenAsync();
        var empresa = new Empresa("Empresa", "Empresa Ltda", "33333333333", "empresa");
        var empresaId = empresa.Id;
        var opcoes = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(conexao).Options;
        await using var contexto = new DetaraDbContext(opcoes, new Contexto(empresaId));
        await contexto.Database.EnsureCreatedAsync();
        contexto.Empresas.Add(empresa);
        var perfil = new Perfil(empresaId, "Administrador");
        var usuario = new Usuario(empresaId, perfil.Id, "Responsável", "r@example.com", "hash");
        contexto.Perfis.Add(perfil);
        contexto.Usuarios.Add(usuario);

        var assinatura = new AssinaturaEmpresa(empresaId, 120m, new DateOnly(2026, 9, 16));
        var aceite = new AceiteTermoAssinatura(empresaId, assinatura.Id, usuario.Id, "0.3",
            DateTime.UtcNow, "empresas/termo.pdf", new string('B', 64), 120m, assinatura.DataInicio,
            assinatura.PrimeiroVencimento, "Empresa Ltda", "33333333333", "Responsável", "r@example.com", null);
        contexto.AssinaturasEmpresas.Add(assinatura);
        contexto.AceitesTermosAssinaturas.Add(aceite);
        await contexto.SaveChangesAsync();

        contexto.AceitesTermosAssinaturas.Remove(aceite);
        await Assert.ThrowsAsync<InvalidOperationException>(() => contexto.SaveChangesAsync());

        contexto.ChangeTracker.Clear();
        var aceiteHistorico = await contexto.AceitesTermosAssinaturas.SingleAsync();
        Assert.Equal("0.3", aceiteHistorico.VersaoTermo);
        Assert.Equal("1.0", Detara.Application.Assinaturas.TermosAssinatura.VersaoAtual);
    }

    private sealed class Contexto(Guid empresaId, Guid? usuarioId = null) : IUsuarioContexto
    {
        public Guid UsuarioId { get; } = usuarioId ?? Guid.NewGuid();
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado => EmpresaId != Guid.Empty;
    }
}

public sealed class AssinaturaSuspensaMiddlewareTests
{
    [Fact]
    public async Task EmpresaSuspensa_BloqueiaEndpointOperacionalEMantemAssinaturaAcessivel()
    {
        await using var conexao = new SqliteConnection("Data Source=:memory:");
        await conexao.OpenAsync();
        var empresa = new Empresa("Empresa", "Empresa Ltda", "44444444444", "empresa-suspensa");
        var contextoUsuario = new ContextoTeste(empresa.Id);
        var opcoes = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(conexao).Options;
        await using var db = new DetaraDbContext(opcoes, contextoUsuario);
        await db.Database.EnsureCreatedAsync();
        db.Empresas.Add(empresa);
        var assinatura = new AssinaturaEmpresa(empresa.Id, 120m, new DateOnly(2026, 9, 16));
        assinatura.Suspender(DateTime.UtcNow, assinatura.Versao);
        db.AssinaturasEmpresas.Add(assinatura);
        await db.SaveChangesAsync();

        var proximoExecutado = false;
        var middleware = new AssinaturaSuspensaMiddleware(_ => { proximoExecutado = true; return Task.CompletedTask; });
        var operacional = ContextoHttp(empresa.Id, "/api/clientes");
        await middleware.InvokeAsync(operacional, db);
        Assert.Equal(StatusCodes.Status402PaymentRequired, operacional.Response.StatusCode);
        Assert.False(proximoExecutado);

        var permitido = ContextoHttp(empresa.Id, "/api/assinatura");
        await middleware.InvokeAsync(permitido, db);
        Assert.True(proximoExecutado);

        proximoExecutado = false;
        var leituraLogo = ContextoHttp(empresa.Id, "/api/empresa/logo");
        leituraLogo.Request.Method = HttpMethods.Get;
        await middleware.InvokeAsync(leituraLogo, db);
        Assert.True(proximoExecutado);

        proximoExecutado = false;
        var alteracaoLogo = ContextoHttp(empresa.Id, "/api/empresa/logo");
        alteracaoLogo.Request.Method = HttpMethods.Put;
        await middleware.InvokeAsync(alteracaoLogo, db);
        Assert.Equal(StatusCodes.Status402PaymentRequired, alteracaoLogo.Response.StatusCode);
        Assert.False(proximoExecutado);
    }

    [Fact]
    public async Task EmpresaSemAssinatura_ContinuaAcessandoEndpointOperacional()
    {
        await using var conexao = new SqliteConnection("Data Source=:memory:");
        await conexao.OpenAsync();
        var empresa = new Empresa("Legada", "Empresa Legada Ltda", "55555555555", "empresa-legada");
        var contextoUsuario = new ContextoTeste(empresa.Id);
        var opcoes = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(conexao).Options;
        await using var db = new DetaraDbContext(opcoes, contextoUsuario);
        await db.Database.EnsureCreatedAsync();
        db.Empresas.Add(empresa);
        await db.SaveChangesAsync();
        var executado = false;
        var middleware = new AssinaturaSuspensaMiddleware(_ => { executado = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(ContextoHttp(empresa.Id, "/api/clientes"), db);

        Assert.True(executado);
    }

    private static DefaultHttpContext ContextoHttp(Guid empresaId, string caminho)
    {
        var contexto = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        contexto.Request.Path = caminho;
        contexto.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("empresa_id", empresaId.ToString())], "teste"));
        return contexto;
    }

    private sealed class ContextoTeste(Guid empresaId) : IUsuarioContexto
    {
        public Guid UsuarioId { get; } = Guid.NewGuid();
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado => true;
    }
}
