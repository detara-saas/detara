using Detara.Domain.Entidades;
using Detara.Infrastructure.Demo;
using Detara.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

return await ExecutarAsync(args);

static async Task<int> ExecutarAsync(string[] args)
{
    try
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            ExibirAjuda();
            return args.Length == 0 ? 1 : 0;
        }

        DemoBootstrapPolicy.ExigirDevelopment(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
        var comando = args[0].ToLowerInvariant();
        if (comando is not ("create" or "reset" or "status"))
        {
            throw new ArgumentException("Comando desconhecido. Use --help.");
        }

        if (comando is "create" or "reset")
        {
            DemoBootstrapPolicy.ExigirConfirmacao(
                args.Contains(DemoBootstrapPolicy.Confirmacao, StringComparer.OrdinalIgnoreCase));
        }

        var argumentosExtras = args.Skip(1).ToArray();
        if (comando == "status" && argumentosExtras.Length > 0 ||
            comando is "create" or "reset" &&
            (argumentosExtras.Length != 1 ||
             !string.Equals(
                 argumentosExtras[0],
                 DemoBootstrapPolicy.Confirmacao,
                 StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Argumentos inválidos. Use --help.");
        }

        var configuration = new ConfigurationManager();
        configuration.AddUserSecrets("002685a8-47cd-45c3-b3ae-d21f8667854c");
        configuration.AddEnvironmentVariables();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Configure ConnectionStrings__DefaultConnection por User Secrets ou variável de ambiente.");
        }

        var options = new DbContextOptionsBuilder<DetaraDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        var service = new DemoBootstrapService(options, new PasswordHasher<Usuario>());

        switch (comando)
        {
            case "status":
                ExibirStatus(await service.ObterStatusAsync());
                return 0;
            case "create":
                {
                    var status = await service.ObterStatusAsync();
                    if (status.Encontrada)
                    {
                        Console.WriteLine("O cenário Prime Detail já existe; nenhuma alteração foi realizada.");
                        ExibirStatus(status);
                        return 0;
                    }

                    Console.WriteLine($"E-mail do administrador Demo: {DemoBootstrapService.EmailAdministrador}");
                    var senha = LerSenhaConfirmada("Senha: ");
                    var resultado = await service.CriarAsync(senha);
                    Console.WriteLine("Prime Detail criada.");
                    ExibirStatus(resultado.Status);
                    ExibirLogin();
                    return 0;
                }
            case "reset":
                {
                    Console.WriteLine($"E-mail do administrador Demo: {DemoBootstrapService.EmailAdministrador}");
                    var senha = LerSenhaConfirmada("Senha: ");
                    var resultado = await service.ResetarAsync(senha);
                    Console.WriteLine("Prime Detail reconstruída.");
                    ExibirStatus(resultado.Status);
                    ExibirLogin();
                    return 0;
                }
            default:
                throw new ArgumentException("Comando desconhecido. Use --help.");
        }
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Falha: {MensagemSegura(exception)}");
        return 1;
    }
}

static string LerSenhaConfirmada(string prompt)
{
    var primeira = LerSenha(prompt);
    var segunda = LerSenha("Confirmar senha: ");
    if (!string.Equals(primeira, segunda, StringComparison.Ordinal))
    {
        throw new ArgumentException("As senhas não conferem.");
    }

    DemoBootstrapPolicy.ValidarSenha(primeira);
    return primeira;
}

static string LerSenha(string prompt)
{
    if (Console.IsInputRedirected)
    {
        throw new InvalidOperationException("A senha deve ser informada em um terminal interativo.");
    }

    Console.Write(prompt);
    var caracteres = new List<char>();
    while (true)
    {
        var tecla = Console.ReadKey(intercept: true);
        if (tecla.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            return new string(caracteres.ToArray());
        }

        if (tecla.Key == ConsoleKey.Backspace)
        {
            if (caracteres.Count > 0)
            {
                caracteres.RemoveAt(caracteres.Count - 1);
            }

            continue;
        }

        if (!char.IsControl(tecla.KeyChar))
        {
            caracteres.Add(tecla.KeyChar);
        }
    }
}

static void ExibirStatus(DemoBootstrapStatus status)
{
    Console.WriteLine($"Demo encontrada: {(status.Encontrada ? "SIM" : "NÃO")}");
    if (!status.Encontrada)
    {
        return;
    }

    Console.WriteLine($"Empresa: {DemoBootstrapService.NomeEmpresa}");
    Console.WriteLine($"Perfis: {status.Perfis}");
    Console.WriteLine($"Usuários: {status.Usuarios}");
    Console.WriteLine($"Clientes: {status.Clientes}");
    Console.WriteLine($"Veículos: {status.Veiculos}");
    Console.WriteLine($"Categorias: {status.Categorias}");
    Console.WriteLine($"Serviços: {status.Servicos}");
    Console.WriteLine($"Pacotes: {status.Pacotes}");
    Console.WriteLine($"Agendamentos: {status.Agendamentos}");
    Console.WriteLine($"Orçamentos: {status.Orcamentos}");
    Console.WriteLine($"OS: {status.OrdensServico}");
    Console.WriteLine($"Recebíveis: {status.ContasReceber}");
    Console.WriteLine($"Pagamentos: {status.Pagamentos}");
}

static void ExibirLogin()
{
    Console.WriteLine("Login:");
    Console.WriteLine(DemoBootstrapService.EmailAdministrador);
    Console.WriteLine("Senha: NÃO repetida pelo Bootstrap.");
    Console.WriteLine("URL: http://localhost:5080");
}

static string MensagemSegura(Exception exception) => exception switch
{
    ArgumentException or InvalidOperationException => exception.Message,
    DbUpdateException => "A operação conflitou com o estado atual do banco.",
    _ => "Não foi possível concluir a operação. Verifique o ambiente local e tente novamente."
};

static void ExibirAjuda()
{
    Console.WriteLine("Detara Demo Bootstrap — Prime Detail Estética Automotiva");
    Console.WriteLine("Disponível exclusivamente com ASPNETCORE_ENVIRONMENT=Development.");
    Console.WriteLine("  create --confirm-local-demo");
    Console.WriteLine("  reset --confirm-local-demo");
    Console.WriteLine("  status");
    Console.WriteLine("A senha é solicitada sem echo, nunca é aceita por argumento e nunca é repetida.");
    Console.WriteLine("Todos os dados criados são sintéticos e locais.");
}
