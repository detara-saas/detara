using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Autenticacao;

internal sealed class UsuarioAutenticacaoRepositorio(DetaraDbContext dbContext)
    : IConsultaIdentidadeLoginTenant
{
    public async Task<IReadOnlyCollection<CandidatoLoginTenant>> ObterCandidatosPorEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        // Exceção cross-tenant exclusiva do login pré-autenticação. A consulta
        // parte somente do e-mail normalizado e projeta o mínimo necessário.
        return await dbContext.Usuarios
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(usuario => usuario.Email == email)
            .OrderBy(usuario => usuario.EmpresaId)
            .Select(usuario => new CandidatoLoginTenant(
                usuario,
                new EmpresaLoginTenant(
                    usuario.Empresa.Id,
                    usuario.Empresa.NomeFantasia,
                    usuario.Empresa.EhAtivo,
                    usuario.Empresa.VersaoSeguranca),
                new PerfilLoginTenant(
                    usuario.Perfil.Id,
                    usuario.Perfil.Nome,
                    usuario.Perfil.EhAtivo,
                    usuario.Perfil.AtualizadoEmUtc.HasValue
                        ? usuario.Perfil.AtualizadoEmUtc.Value.Ticks
                        : 0,
                    usuario.Perfil.Permissoes
                        .Where(permissao => permissao.EhAtivo)
                        .OrderBy(permissao => permissao.Codigo)
                        .Select(permissao => permissao.Codigo)
                        .ToArray())))
            .ToArrayAsync(cancellationToken);
    }

    public Task<CandidatoLoginTenant?> ObterMembershipAsync(
        Guid usuarioId,
        Guid empresaId,
        CancellationToken cancellationToken) =>
        dbContext.Usuarios
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(usuario => usuario.Id == usuarioId && usuario.EmpresaId == empresaId)
            .Select(usuario => new CandidatoLoginTenant(
                usuario,
                new EmpresaLoginTenant(
                    usuario.Empresa.Id,
                    usuario.Empresa.NomeFantasia,
                    usuario.Empresa.EhAtivo,
                    usuario.Empresa.VersaoSeguranca),
                new PerfilLoginTenant(
                    usuario.Perfil.Id,
                    usuario.Perfil.Nome,
                    usuario.Perfil.EhAtivo,
                    usuario.Perfil.AtualizadoEmUtc.HasValue
                        ? usuario.Perfil.AtualizadoEmUtc.Value.Ticks
                        : 0,
                    usuario.Perfil.Permissoes
                        .Where(permissao => permissao.EhAtivo)
                        .OrderBy(permissao => permissao.Codigo)
                        .Select(permissao => permissao.Codigo)
                        .ToArray())))
            .SingleOrDefaultAsync(
                cancellationToken);
}
