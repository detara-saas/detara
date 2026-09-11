namespace Detara.Web.Components.Shared.DesignSystem;

public static class AmbienteApresentacao
{
    public static bool ExibirIndicadorDesenvolvimento(string? ambiente) =>
        string.Equals(ambiente, "Development", StringComparison.OrdinalIgnoreCase);
}
