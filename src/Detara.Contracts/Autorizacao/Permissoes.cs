namespace Detara.Contracts.Autorizacao;

public static class Permissoes
{
    public const string ClientesVisualizar = "Clientes.Visualizar";
    public const string ClientesCriar = "Clientes.Criar";
    public const string ClientesEditar = "Clientes.Editar";
    public const string VeiculosVisualizar = "Veiculos.Visualizar";
    public const string VeiculosCriar = "Veiculos.Criar";
    public const string VeiculosEditar = "Veiculos.Editar";

    public static readonly IReadOnlyCollection<string> ModulosClientesVeiculos =
    [
        ClientesVisualizar,
        ClientesCriar,
        ClientesEditar,
        VeiculosVisualizar,
        VeiculosCriar,
        VeiculosEditar
    ];
}
