using Detara.Domain.Plataforma;
using Detara.Infrastructure.Persistencia;
using Detara.Infrastructure.Plataforma;
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

        var configuration = new ConfigurationManager();
        configuration.AddJsonFile("appsettings.json", optional: true);
        configuration.AddEnvironmentVariables();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Configure ConnectionStrings__DefaultConnection por secret ou variável de ambiente.");
        }

        var options = new DbContextOptionsBuilder<DetaraDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        var service = new PlatformBootstrapService(
            options,
            new PasswordHasher<AdministradorPlataforma>());
        var comando = args[0].ToLowerInvariant();
        switch (comando)
        {
            case "create-admin":
                {
                    var nome = ObterArgumento(args, "--nome") ?? Perguntar("Nome: ");
                    var email = ObterArgumento(args, "--email") ?? Perguntar("E-mail: ");
                    var senha = LerSenhaConfirmada("Senha (mínimo 12 caracteres): ");
                    var id = await service.CriarPrimeiroAdministradorAsync(nome, email, senha);
                    Console.WriteLine($"Primeiro Platform Admin criado com sucesso. Id: {id}");
                    Console.WriteLine("No primeiro login, a configuração de MFA será obrigatória.");
                    return 0;
                }
            case "reset-password":
                {
                    var email = ObterArgumento(args, "--email") ?? Perguntar("E-mail: ");
                    var senha = LerSenhaConfirmada("Nova senha (mínimo 12 caracteres): ");
                    await service.ResetarSenhaAsync(email, senha);
                    Console.WriteLine("Senha redefinida e sessões anteriores revogadas.");
                    return 0;
                }
            case "reset-mfa":
                {
                    var email = ObterArgumento(args, "--email") ?? Perguntar("E-mail: ");
                    if (!Confirmar("Resetar MFA e invalidar todos os recovery codes? [s/N] ")) return 1;
                    await service.ResetarMfaAsync(email);
                    Console.WriteLine("MFA resetado. O próximo login exigirá novo enrollment.");
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
    var segunda = LerSenha("Confirme a senha: ");
    if (!string.Equals(primeira, segunda, StringComparison.Ordinal))
    {
        throw new ArgumentException("As senhas não conferem.");
    }

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
            if (caracteres.Count > 0) caracteres.RemoveAt(caracteres.Count - 1);
            continue;
        }

        if (!char.IsControl(tecla.KeyChar)) caracteres.Add(tecla.KeyChar);
    }
}

static string Perguntar(string prompt)
{
    Console.Write(prompt);
    return Console.ReadLine()?.Trim() ?? string.Empty;
}

static bool Confirmar(string prompt)
{
    Console.Write(prompt);
    return string.Equals(Console.ReadLine()?.Trim(), "s", StringComparison.OrdinalIgnoreCase);
}

static string? ObterArgumento(string[] args, string nome)
{
    var indice = Array.FindIndex(args, x => string.Equals(x, nome, StringComparison.OrdinalIgnoreCase));
    return indice >= 0 && indice + 1 < args.Length ? args[indice + 1].Trim() : null;
}

static string MensagemSegura(Exception exception) => exception switch
{
    ArgumentException or InvalidOperationException => exception.Message,
    DbUpdateException => "A operação conflitou com o estado atual do banco.",
    _ => "Não foi possível concluir a operação. Consulte os logs seguros do ambiente."
};

static void ExibirAjuda()
{
    Console.WriteLine("Detara Platform Bootstrap");
    Console.WriteLine("  create-admin [--nome <nome>] [--email <email>]");
    Console.WriteLine("  reset-password [--email <email>]");
    Console.WriteLine("  reset-mfa [--email <email>]");
    Console.WriteLine("A senha nunca é aceita por argumento e não é exibida no terminal.");
}
