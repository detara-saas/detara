using System.Linq.Expressions;
using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using Detara.Domain.Agenda;
using Detara.Domain.Atendimento;
using Detara.Domain.Clientes;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Persistencia;

public sealed class DetaraDbContext(
    DbContextOptions<DetaraDbContext> options,
    IUsuarioContexto usuarioContexto)
    : DbContext(options)
{
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Perfil> Perfis => Set<Perfil>();
    public DbSet<Permissao> Permissoes => Set<Permissao>();
    public DbSet<UsuarioPreferencia> UsuariosPreferencias => Set<UsuarioPreferencia>();
    public DbSet<UsuarioPaginaFavorita> UsuariosPaginasFavoritas => Set<UsuarioPaginaFavorita>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Veiculo> Veiculos => Set<Veiculo>();
    public DbSet<CategoriaServico> CategoriasServico => Set<CategoriaServico>();
    public DbSet<Servico> Servicos => Set<Servico>();
    public DbSet<Pacote> Pacotes => Set<Pacote>();
    public DbSet<PacoteServico> PacotesServicos => Set<PacoteServico>();
    public DbSet<Agendamento> Agendamentos => Set<Agendamento>();
    public DbSet<AgendamentoItem> AgendamentosItens => Set<AgendamentoItem>();
    public DbSet<Orcamento> Orcamentos => Set<Orcamento>();
    public DbSet<OrcamentoItem> OrcamentosItens => Set<OrcamentoItem>();
    public DbSet<HistoricoStatusOrcamento> OrcamentosHistoricosStatus => Set<HistoricoStatusOrcamento>();
    public DbSet<ConfiguracaoOperacionalAtendimento> ConfiguracoesOperacionaisAtendimento => Set<ConfiguracaoOperacionalAtendimento>();
    public DbSet<ChecklistModelo> ChecklistModelos => Set<ChecklistModelo>();
    public DbSet<ChecklistModeloItem> ChecklistModeloItens => Set<ChecklistModeloItem>();
    public DbSet<VeiculoFoto> VeiculosFotos => Set<VeiculoFoto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DetaraDbContext).Assembly);
        AplicarProtecaoTenant(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidarIsolamentoDeEscrita();
        AtualizarAuditoria();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ValidarIsolamentoDeEscrita();
        AtualizarAuditoria();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidarIsolamentoDeEscrita()
    {
        var alteracoesTenant = ChangeTracker.Entries<EntidadeEmpresaBase>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

        foreach (var entry in alteracoesTenant)
        {
            if (!usuarioContexto.EstaAutenticado)
            {
                throw new ViolacaoIsolamentoTenantException();
            }

            var empresaOriginal = entry.State == EntityState.Added
                ? entry.Entity.EmpresaId
                : entry.Property<Guid>(nameof(EntidadeEmpresaBase.EmpresaId)).OriginalValue;

            if (empresaOriginal != usuarioContexto.EmpresaId ||
                entry.Entity.EmpresaId != usuarioContexto.EmpresaId)
            {
                throw new ViolacaoIsolamentoTenantException();
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(EntidadeEmpresaBase.EmpresaId)).IsModified = false;
            }
        }
    }

    private void AplicarProtecaoTenant(ModelBuilder modelBuilder)
    {
        var tiposTenant = modelBuilder.Model.GetEntityTypes()
            .Where(tipo => typeof(EntidadeEmpresaBase).IsAssignableFrom(tipo.ClrType));

        foreach (var tipo in tiposTenant)
        {
            var entidade = Expression.Parameter(tipo.ClrType, "entidade");
            var empresaDaEntidade = Expression.Property(
                entidade,
                nameof(EntidadeEmpresaBase.EmpresaId));
            var empresaDoContexto = Expression.Property(
                Expression.Constant(this),
                nameof(EmpresaIdAtual));
            var filtro = Expression.Lambda(
                Expression.Equal(empresaDaEntidade, empresaDoContexto),
                entidade);

            tipo.SetQueryFilter(filtro);
            tipo.FindProperty(nameof(EntidadeEmpresaBase.EmpresaId))!.IsConcurrencyToken = true;
        }
    }

    private Guid EmpresaIdAtual =>
        usuarioContexto.EstaAutenticado ? usuarioContexto.EmpresaId : Guid.Empty;

    private void AtualizarAuditoria()
    {
        var agora = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<EntidadeBase>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(EntidadeBase.CriadoEmUtc)).CurrentValue = agora;
                entry.Property(nameof(EntidadeBase.AtualizadoEmUtc)).CurrentValue = null;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(EntidadeBase.AtualizadoEmUtc)).CurrentValue = agora;
                entry.Property(nameof(EntidadeBase.CriadoEmUtc)).IsModified = false;
            }
        }
    }
}
