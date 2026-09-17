using System.Security.Cryptography;
using System.Text;
using Detara.Application.Abstracoes;
using Detara.Application.Autenticacao;
using Detara.Domain.Identidade;
using Detara.Infrastructure.Persistencia;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Detara.Infrastructure.Autenticacao;

internal sealed class SessoesAutenticacaoServico(
    DetaraDbContext db,
    IConsultaIdentidadeLoginTenant consultaTenant,
    IOptions<SessaoAutenticacaoOptions> options,
    TimeProvider timeProvider,
    ILogger<SessoesAutenticacaoServico> logger) : ISessoesAutenticacaoServico
{
    private readonly SessaoAutenticacaoOptions _options = options.Value;

    public async Task<RefreshTokenCriado> CriarTenantAsync(
        Guid usuarioId,
        Guid empresaId,
        bool persistente,
        CancellationToken cancellationToken)
    {
        var candidato = await consultaTenant.ObterMembershipAsync(usuarioId, empresaId, cancellationToken);
        if (!EhMembershipValida(candidato))
        {
            throw new SessaoAutenticacaoInvalidaException();
        }

        var agora = ObterAgoraUtc();
        var expiraEm = persistente
            ? agora.AddDays(_options.DuracaoPersistenteDias)
            : agora.AddHours(_options.DuracaoSessaoHoras);
        var (sessao, token) = CriarTenant(
            candidato!,
            Guid.NewGuid(),
            persistente,
            expiraEm);
        db.SessoesAutenticacao.Add(sessao);
        await db.SaveChangesAsync(cancellationToken);
        await LimparExpiradasAsync(agora, cancellationToken);
        logger.LogInformation(
            "Sessão tenant criada para {UsuarioId} na empresa {EmpresaId}; persistente: {Persistente}.",
            usuarioId,
            empresaId,
            persistente);
        return new(token, expiraEm, persistente);
    }

    public async Task<SessaoTenantRenovada> RenovarTenantAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var sessao = await ObterEValidarTokenAsync(
            refreshToken,
            TipoIdentidadeSessao.Tenant,
            cancellationToken);
        var candidato = await consultaTenant.ObterMembershipAsync(
            sessao.UsuarioId!.Value,
            sessao.EmpresaId!.Value,
            cancellationToken);
        if (!EhMembershipValida(candidato) ||
            candidato!.Usuario.VersaoSeguranca != sessao.VersaoSegurancaIdentidade ||
            candidato.Empresa.VersaoSeguranca != sessao.VersaoSegurancaEmpresa)
        {
            await RevogarFamiliaAsync(sessao.FamiliaId, "identidade_alterada", cancellationToken);
            throw new SessaoAutenticacaoInvalidaException();
        }

        var agora = ObterAgoraUtc();
        var (novaSessao, novoToken) = CriarTenant(
            candidato,
            sessao.FamiliaId,
            sessao.Persistente,
            sessao.ExpiraEmUtc);
        sessao.SubstituirPor(novaSessao.Id, agora);
        db.SessoesAutenticacao.Add(novaSessao);
        await SalvarRotacaoAsync(cancellationToken);
        logger.LogInformation(
            "Sessão tenant renovada para {UsuarioId} na empresa {EmpresaId}.",
            sessao.UsuarioId,
            sessao.EmpresaId);
        return new(candidato, new(novoToken, novaSessao.ExpiraEmUtc, novaSessao.Persistente));
    }

    public async Task<RefreshTokenCriado> CriarPlataformaAsync(
        Guid administradorId,
        CancellationToken cancellationToken)
    {
        var administrador = await db.AdministradoresPlataforma.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == administradorId, cancellationToken);
        if (administrador is null || !administrador.EhAtivo || !administrador.MfaHabilitado)
        {
            throw new SessaoAutenticacaoInvalidaException();
        }

        var agora = ObterAgoraUtc();
        var expiraEm = agora.AddHours(_options.DuracaoPlataformaHoras);
        var (sessao, token) = CriarPlataforma(
            administrador.Id,
            administrador.VersaoSeguranca,
            Guid.NewGuid(),
            expiraEm);
        db.SessoesAutenticacao.Add(sessao);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Sessão de Platform Admin criada para {AdministradorId} após MFA.", administradorId);
        return new(token, expiraEm, false);
    }

    public async Task<SessaoPlataformaRenovada> RenovarPlataformaAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var sessao = await ObterEValidarTokenAsync(
            refreshToken,
            TipoIdentidadeSessao.AdministradorPlataforma,
            cancellationToken);
        var administrador = await db.AdministradoresPlataforma.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == sessao.AdministradorPlataformaId, cancellationToken);
        if (administrador is null ||
            !administrador.EhAtivo ||
            !administrador.MfaHabilitado ||
            administrador.VersaoSeguranca != sessao.VersaoSegurancaIdentidade)
        {
            await RevogarFamiliaAsync(sessao.FamiliaId, "identidade_alterada", cancellationToken);
            throw new SessaoAutenticacaoInvalidaException();
        }

        var agora = ObterAgoraUtc();
        var (novaSessao, novoToken) = CriarPlataforma(
            administrador.Id,
            administrador.VersaoSeguranca,
            sessao.FamiliaId,
            sessao.ExpiraEmUtc);
        sessao.SubstituirPor(novaSessao.Id, agora);
        db.SessoesAutenticacao.Add(novaSessao);
        await SalvarRotacaoAsync(cancellationToken);
        logger.LogInformation("Sessão de Platform Admin renovada para {AdministradorId}.", administrador.Id);
        return new(
            administrador.Id,
            administrador.Nome,
            administrador.Email,
            administrador.VersaoSeguranca,
            new(novoToken, novaSessao.ExpiraEmUtc, false));
    }

    public Task RevogarTenantAsync(string? refreshToken, CancellationToken cancellationToken) =>
        RevogarAtualAsync(refreshToken, TipoIdentidadeSessao.Tenant, cancellationToken);

    public Task RevogarPlataformaAsync(string? refreshToken, CancellationToken cancellationToken) =>
        RevogarAtualAsync(refreshToken, TipoIdentidadeSessao.AdministradorPlataforma, cancellationToken);

    public async Task RevogarTodasDoUsuarioAsync(
        Guid usuarioId,
        string motivo,
        CancellationToken cancellationToken)
    {
        var agora = ObterAgoraUtc();
        var sessoes = await db.SessoesAutenticacao
            .Where(x => x.TipoIdentidade == TipoIdentidadeSessao.Tenant &&
                x.UsuarioId == usuarioId &&
                x.RevogadoEmUtc == null)
            .ToArrayAsync(cancellationToken);
        foreach (var sessao in sessoes)
        {
            sessao.Revogar(agora, motivo);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Sessões tenant revogadas para {UsuarioId}; motivo: {Motivo}.", usuarioId, motivo);
    }

    private async Task<SessaoAutenticacao> ObterEValidarTokenAsync(
        string refreshToken,
        TipoIdentidadeSessao tipo,
        CancellationToken cancellationToken)
    {
        var id = ExtrairId(refreshToken);
        var sessao = id is null
            ? null
            : await db.SessoesAutenticacao.SingleOrDefaultAsync(
                x => x.Id == id && x.TipoIdentidade == tipo,
                cancellationToken);
        if (sessao is null || !HashConfere(sessao.TokenHash, refreshToken))
        {
            throw new SessaoAutenticacaoInvalidaException();
        }

        var agora = ObterAgoraUtc();
        if (sessao.EstaAtivaEm(agora))
        {
            return sessao;
        }

        if (sessao.MotivoRevogacao == "rotacionado" &&
            sessao.RevogadoEmUtc <= agora.AddSeconds(-_options.JanelaConcorrenciaSegundos))
        {
            logger.LogWarning(
                "Replay de refresh detectado para a família {FamiliaId}; identidade {TipoIdentidade}.",
                sessao.FamiliaId,
                sessao.TipoIdentidade);
            await RevogarFamiliaAsync(sessao.FamiliaId, "replay_detectado", cancellationToken);
        }

        throw new SessaoAutenticacaoInvalidaException();
    }

    private async Task RevogarAtualAsync(
        string? refreshToken,
        TipoIdentidadeSessao tipo,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var id = ExtrairId(refreshToken);
        var sessao = id is null
            ? null
            : await db.SessoesAutenticacao.SingleOrDefaultAsync(
                x => x.Id == id && x.TipoIdentidade == tipo,
                cancellationToken);
        if (sessao is null || !HashConfere(sessao.TokenHash, refreshToken))
        {
            return;
        }

        await RevogarFamiliaAsync(sessao.FamiliaId, "logout", cancellationToken);
        logger.LogInformation(
            "Sessão revogada por logout; família {FamiliaId}; identidade {TipoIdentidade}.",
            sessao.FamiliaId,
            tipo);
    }

    private async Task RevogarFamiliaAsync(
        Guid familiaId,
        string motivo,
        CancellationToken cancellationToken)
    {
        var agora = ObterAgoraUtc();
        var sessoes = await db.SessoesAutenticacao
            .Where(x => x.FamiliaId == familiaId && x.RevogadoEmUtc == null)
            .ToArrayAsync(cancellationToken);
        foreach (var sessao in sessoes)
        {
            sessao.Revogar(agora, motivo);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SalvarRotacaoAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw new SessaoAutenticacaoInvalidaException();
        }
    }

    private async Task LimparExpiradasAsync(DateTime agora, CancellationToken cancellationToken)
    {
        var limite = agora.AddDays(-7);
        await db.SessoesAutenticacao
            .Where(x => x.ExpiraEmUtc < limite || x.RevogadoEmUtc < limite)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static bool EhMembershipValida(CandidatoLoginTenant? candidato) =>
        candidato is not null &&
        candidato.Usuario.EhAtivo &&
        candidato.Empresa.EhAtiva &&
        candidato.Perfil.EhAtivo;

    private static (SessaoAutenticacao Sessao, string Token) CriarTenant(
        CandidatoLoginTenant candidato,
        Guid familiaId,
        bool persistente,
        DateTime expiraEmUtc)
    {
        var id = Guid.NewGuid();
        var token = GerarToken(id);
        var sessao = SessaoAutenticacao.CriarTenant(
            id,
            candidato.Usuario.Id,
            candidato.Empresa.Id,
            candidato.Usuario.VersaoSeguranca,
            candidato.Empresa.VersaoSeguranca,
            familiaId,
            Hash(token),
            persistente,
            expiraEmUtc);
        return (sessao, token);
    }

    private static (SessaoAutenticacao Sessao, string Token) CriarPlataforma(
        Guid administradorId,
        long versaoSeguranca,
        Guid familiaId,
        DateTime expiraEmUtc)
    {
        var id = Guid.NewGuid();
        var token = GerarToken(id);
        var sessao = SessaoAutenticacao.CriarAdministradorPlataforma(
            id,
            administradorId,
            versaoSeguranca,
            familiaId,
            Hash(token),
            expiraEmUtc);
        return (sessao, token);
    }

    private static string GerarToken(Guid id) =>
        $"{id:N}.{WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48))}";

    private static Guid? ExtrairId(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 160)
        {
            return null;
        }

        var separador = token.IndexOf('.');
        return separador == 32 && Guid.TryParseExact(token[..separador], "N", out var id)
            ? id
            : null;
    }

    private static string Hash(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static bool HashConfere(string hashPersistido, string token)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(hashPersistido),
                SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private DateTime ObterAgoraUtc() => timeProvider.GetUtcNow().UtcDateTime;
}
