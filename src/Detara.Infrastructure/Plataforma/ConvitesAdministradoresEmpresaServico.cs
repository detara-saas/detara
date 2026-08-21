using System.Security.Cryptography;
using System.Text;
using Detara.Application.Abstracoes;
using Detara.Application.Plataforma;
using Detara.Domain.Entidades;
using Detara.Domain.Plataforma;
using Detara.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Plataforma;

internal sealed class ConvitesAdministradoresEmpresaServico(
    DetaraDbContext db,
    DbContextOptions<DetaraDbContext> dbOptions,
    IPasswordHasher<Usuario> passwordHasher)
    : IConvitesAdministradoresEmpresaServico
{
    public async Task<ConviteAdministradorValidadoResultado> ValidarAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var hash = HashToken(token);
        var convite = await db.ConvitesAdministradoresEmpresa.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken)
            ?? throw new ConviteAdministradorInvalidoException();
        var empresa = await db.Empresas.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == convite.EmpresaId && x.EhAtivo, cancellationToken)
            ?? throw new ConviteAdministradorInvalidoException();
        var usuario = await db.Usuarios.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.EmpresaId == convite.EmpresaId && x.Id == convite.UsuarioId && !x.EhAtivo,
                cancellationToken)
            ?? throw new ConviteAdministradorInvalidoException();
        if (!convite.PodeSerAceito(hash, DateTime.UtcNow))
        {
            throw new ConviteAdministradorInvalidoException();
        }

        return new(empresa.NomeFantasia, MascararEmail(usuario.Email), convite.ExpiraEmUtc!.Value);
    }

    public async Task AceitarAsync(
        string token,
        string senha,
        string? traceId,
        CancellationToken cancellationToken)
    {
        var hash = HashToken(token);
        var referencia = await db.ConvitesAdministradoresEmpresa.AsNoTracking()
            .Where(x => x.TokenHash == hash)
            .Select(x => new { x.Id, x.EmpresaId, x.UsuarioId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ConviteAdministradorInvalidoException();
        await using var contexto = new DetaraDbContext(dbOptions, new ContextoConvite(referencia.EmpresaId));
        await using var transacao = await contexto.Database.BeginTransactionAsync(cancellationToken);
        var convite = await contexto.ConvitesAdministradoresEmpresa
            .SingleOrDefaultAsync(x => x.Id == referencia.Id && x.EmpresaId == referencia.EmpresaId, cancellationToken)
            ?? throw new ConviteAdministradorInvalidoException();
        var empresaAtiva = await contexto.Empresas.AnyAsync(
            x => x.Id == referencia.EmpresaId && x.EhAtivo,
            cancellationToken);
        var usuario = await contexto.Usuarios.SingleOrDefaultAsync(
            x => x.Id == referencia.UsuarioId,
            cancellationToken);
        if (!empresaAtiva ||
            usuario is null ||
            usuario.EhAtivo ||
            !convite.PodeSerAceito(hash, DateTime.UtcNow))
        {
            throw new ConviteAdministradorInvalidoException();
        }

        usuario.AlterarSenhaHash(passwordHasher.HashPassword(usuario, senha));
        usuario.Ativar();
        convite.MarcarAceito(DateTime.UtcNow);
        if (convite.Origem == OrigemConviteAcessoEmpresa.AdministradorInicialPlataforma &&
            convite.CriadoPorAdministradorPlataformaId.HasValue)
        {
            contexto.AuditoriasPlataforma.Add(new AuditoriaPlataforma(
                convite.CriadoPorAdministradorPlataformaId.Value,
                AcoesAuditoriaPlataforma.ConviteAceito,
                convite.EmpresaId,
                usuario.Id,
                traceId,
                "Convite aceito; senha definida pelo próprio administrador tenant."));
        }
        await contexto.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
    }

    internal static string HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Convert.ToBase64String(SHA256.HashData(Array.Empty<byte>()));
        }

        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));
    }

    private static string MascararEmail(string email)
    {
        var partes = email.Split('@', 2);
        if (partes.Length != 2) return "***";
        var local = partes[0].Length <= 2 ? partes[0][..1] + "***" : partes[0][..2] + "***";
        return $"{local}@{partes[1]}";
    }

    private sealed class ContextoConvite(Guid empresaId) : IUsuarioContexto
    {
        public Guid UsuarioId { get; } = Guid.Parse("00000000-0000-0000-0000-000000000012");
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado => true;
    }
}
