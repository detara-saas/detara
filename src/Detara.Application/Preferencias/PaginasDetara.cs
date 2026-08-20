namespace Detara.Application.Preferencias;

public static class PaginasDetara
{
    public static readonly IReadOnlySet<string> Permitidas = new HashSet<string>(
        [
            "dashboard", "agenda", "orcamentos", "ordens-servico", "clientes", "veiculos",
            "servicos", "pacotes", "financeiro", "pagamentos", "usuarios", "perfis", "empresa", "configuracoes"
        ],
        StringComparer.OrdinalIgnoreCase);
}
