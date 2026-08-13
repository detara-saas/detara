namespace Detara.Application.Preferencias;

public static class PaginasDetara
{
    public static readonly IReadOnlySet<string> Permitidas = new HashSet<string>(
        [
            "dashboard", "agenda", "orcamentos", "ordens-servico", "clientes", "veiculos",
            "servicos", "pacotes", "pagamentos", "usuarios", "perfis", "empresa"
        ],
        StringComparer.OrdinalIgnoreCase);
}
