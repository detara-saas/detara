namespace Detara.Application.Preferencias;

public static class PaginasDetara
{
    public const string Dashboard = "dashboard";
    public const string DashboardEmpresa = "dashboard-empresa";

    public static readonly IReadOnlySet<string> Permitidas = new HashSet<string>(
        [
            Dashboard, "agenda", "orcamentos", "ordens-servico", "clientes", "veiculos",
            "servicos", "pacotes", "financeiro", "pagamentos", "usuarios", "perfis", "empresa", "configuracoes"
        ],
        StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlySet<string> PaginasIniciaisPermitidas = new HashSet<string>(
        Permitidas.Append(DashboardEmpresa),
        StringComparer.OrdinalIgnoreCase);
}
