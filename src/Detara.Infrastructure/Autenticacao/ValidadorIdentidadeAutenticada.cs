using Detara.Application.Abstracoes;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Autenticacao;

internal sealed class ValidadorIdentidadeAutenticada(DetaraDbContext dbContext)
    : IValidadorIdentidadeAutenticada
{
    public async Task<bool> EhValidaAsync(
        IdentidadeToken identidade,
        CancellationToken cancellationToken)
    {
        if (identidade.UsuarioId == Guid.Empty ||
            identidade.EmpresaId == Guid.Empty ||
            identidade.PerfilId == Guid.Empty)
        {
            return false;
        }

        var empresaValida = await dbContext.Empresas
            .AsNoTracking()
            .AnyAsync(
                empresa => empresa.Id == identidade.EmpresaId &&
                    empresa.EhAtivo &&
                    empresa.VersaoSeguranca == identidade.EmpresaVersaoSeguranca,
                cancellationToken);
        if (!empresaValida)
        {
            return false;
        }

        var usuario = await dbContext.Usuarios
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(item => item.Perfil)
            .ThenInclude(perfil => perfil.Permissoes)
            .SingleOrDefaultAsync(
                item => item.Id == identidade.UsuarioId &&
                    item.EmpresaId == identidade.EmpresaId,
                cancellationToken);

        if (usuario is null ||
            !usuario.EhAtivo ||
            !usuario.Perfil.EhAtivo ||
            usuario.PerfilId != identidade.PerfilId ||
            (usuario.AtualizadoEmUtc?.Ticks ?? 0) != identidade.UsuarioAtualizadoEmTicks)
        {
            return false;
        }

        var permissoesAtuais = usuario.Perfil.Permissoes
            .Where(permissao => permissao.EhAtivo)
            .Select(permissao => permissao.Codigo)
            .ToHashSet(StringComparer.Ordinal);

        return permissoesAtuais.SetEquals(identidade.Permissoes);
    }
}
