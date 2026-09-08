using Detara.Application.Abstracoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Detara.Infrastructure.Persistencia;

// Exclusivo das ferramentas EF/bundle; não inicia API, workers, seed ou integrações.
public sealed class DetaraDesignTimeFactory : IDesignTimeDbContextFactory<DetaraDbContext>
{
    public DetaraDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Ferramentas EF exigem ConnectionStrings__DefaultConnection no ambiente.");
        return new(new DbContextOptionsBuilder<DetaraDbContext>().UseSqlServer(connection).Options, new ContextoMigration());
    }

    private sealed class ContextoMigration : IUsuarioContexto
    {
        public Guid EmpresaId => Guid.Empty;
        public Guid UsuarioId => Guid.Empty;
        public bool EstaAutenticado => false;
    }
}
