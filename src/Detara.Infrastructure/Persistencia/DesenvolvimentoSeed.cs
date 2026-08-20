using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Detara.Infrastructure.Persistencia;

public static class DesenvolvimentoSeed
{
    private static readonly (string Codigo, string Descricao)[] PermissoesAdministrador =
    [
        ("Clientes.Visualizar", "Visualizar clientes"),
        ("Clientes.Criar", "Criar clientes"),
        ("Clientes.Editar", "Editar clientes"),
        ("Veiculos.Visualizar", "Visualizar veículos"),
        ("Veiculos.Criar", "Criar veículos"),
        ("Veiculos.Editar", "Editar veículos"),
        ("Servicos.Visualizar", "Visualizar serviços"),
        ("Servicos.Criar", "Criar serviços e categorias"),
        ("Servicos.Editar", "Editar serviços e categorias"),
        ("Pacotes.Visualizar", "Visualizar pacotes"),
        ("Pacotes.Criar", "Criar pacotes"),
        ("Pacotes.Editar", "Editar pacotes"),
        ("Agenda.Visualizar", "Visualizar agenda"),
        ("Agenda.Criar", "Criar agendamentos"),
        ("Agenda.Editar", "Editar agenda"),
        ("Orcamentos.Visualizar", "Visualizar orçamentos"),
        ("Orcamentos.Criar", "Criar orçamentos"),
        ("Orcamentos.Editar", "Editar e registrar transições de orçamentos"),
        ("Configuracoes.Visualizar", "Visualizar configurações operacionais"),
        ("Configuracoes.Editar", "Editar configurações operacionais e checklist"),
        ("OrdemServico.Visualizar", "Visualizar ordens de serviço"),
        ("OrdemServico.Criar", "Criar ordens de serviço"),
        ("OrdemServico.Editar", "Editar check-in, evidências e adicionais da ordem de serviço"),
        ("OrdemServico.Finalizar", "Finalizar ordens de serviço"),
        ("Financeiro.Visualizar", "Visualizar financeiro"),
        ("Financeiro.Editar", "Editar vencimentos financeiros"),
        ("Financeiro.RegistrarPagamento", "Registrar pagamentos"),
        ("Financeiro.EstornarPagamento", "Estornar pagamentos"),
        ("Notificacoes.Reenviar", "Reenviar notificações por e-mail"),
        ("Administracao.Usuario", "Administrar usuários")
    ];

    public static async Task InicializarDesenvolvimentoAsync(
        this IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("Seed:Enabled"))
        {
            return;
        }

        var senha = configuration["Seed:SenhaAdministrador"];
        if (string.IsNullOrWhiteSpace(senha))
        {
            throw new InvalidOperationException(
                "Seed__SenhaAdministrador deve ser informada quando o seed estiver habilitado.");
        }

        await using var scope = services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DetaraDbContext>>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DetaraDbContext>>();
        var slug = configuration["Seed:SlugEmpresa"] ?? "empresa-demo";
        var email = configuration["Seed:EmailAdministrador"] ?? "admin@detara.local";

        await using var contextSistema = new DetaraDbContext(options, UsuarioContextoFixo.Anonimo);
        var empresa = await contextSistema.Empresas.SingleOrDefaultAsync(x => x.Slug == slug, cancellationToken);
        if (empresa is null)
        {
            empresa = new Empresa(
                "Empresa Demo",
                "Empresa Demo Ltda",
                "00000000000100",
                slug,
                email);
            contextSistema.Empresas.Add(empresa);
            await contextSistema.SaveChangesAsync(cancellationToken);
        }

        await using var contextTenant = new DetaraDbContext(options, new UsuarioContextoFixo(empresa.Id));
        var perfil = await contextTenant.Perfis
            .Include(x => x.Permissoes)
            .SingleOrDefaultAsync(x => x.Nome == "Administrador", cancellationToken);
        if (perfil is null)
        {
            perfil = new Perfil(empresa.Id, "Administrador");
            contextTenant.Perfis.Add(perfil);
        }

        foreach (var definicao in PermissoesAdministrador)
        {
            var permissao = await contextTenant.Permissoes
                .SingleOrDefaultAsync(x => x.Codigo == definicao.Codigo, cancellationToken);
            if (permissao is null)
            {
                permissao = new Permissao(definicao.Codigo, definicao.Descricao);
                contextTenant.Permissoes.Add(permissao);
            }

            perfil.ConcederPermissao(permissao);
        }

        await contextTenant.SaveChangesAsync(cancellationToken);

        if (!await contextTenant.Usuarios.AnyAsync(x => x.Email == email, cancellationToken))
        {
            var usuario = new Usuario(empresa.Id, perfil.Id, "Administrador Demo", email, "pendente");
            usuario.AlterarSenhaHash(passwordHasher.HashPassword(usuario, senha));
            contextTenant.Usuarios.Add(usuario);
            await contextTenant.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seed de desenvolvimento criado para a empresa {EmpresaId}", empresa.Id);
        }
    }

    public static async Task ValidarMigrationsDesenvolvimentoAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DetaraDbContext>>();
        await using var context = new DetaraDbContext(options, UsuarioContextoFixo.Anonimo);
        var pendentes = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
        if (pendentes.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Existem migrations pendentes ({string.Join(", ", pendentes)}). " +
            "Execute 'dotnet ef database update --project src/Detara.Infrastructure/Detara.Infrastructure.csproj " +
            "--startup-project src/Detara.Api/Detara.Api.csproj' antes de iniciar a API em Development.");
    }

    private sealed class UsuarioContextoFixo(Guid empresaId) : IUsuarioContexto
    {
        public static UsuarioContextoFixo Anonimo { get; } = new(Guid.Empty);
        public Guid UsuarioId { get; } = empresaId == Guid.Empty ? Guid.Empty : Guid.NewGuid();
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado => EmpresaId != Guid.Empty;
    }
}
