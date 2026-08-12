using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Autenticacao;

internal sealed class UsuarioAutenticacaoRepositorio(DetaraDbContext dbContext)
    : IUsuarioAutenticacaoRepositorio
{
    public async Task<Usuario?> ObterParaLoginAsync(
        string slugEmpresa,
        string email,
        CancellationToken cancellationToken)
    {
        var empresaId = await dbContext.Empresas
            .AsNoTracking()
            .Where(empresa => empresa.EhAtivo && empresa.Slug == slugEmpresa)
            .Select(empresa => (Guid?)empresa.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (empresaId is null)
        {
            return null;
        }

        // Única consulta cross-tenant: antes do login e sempre limitada pelo
        // tenant resolvido a partir do slug informado.
        return await dbContext.Usuarios
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(usuario => usuario.Perfil)
            .ThenInclude(perfil => perfil.Permissoes)
            .SingleOrDefaultAsync(
                usuario => usuario.EmpresaId == empresaId && usuario.Email == email,
                cancellationToken);
    }
}
