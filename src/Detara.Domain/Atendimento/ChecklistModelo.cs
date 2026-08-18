using Detara.Domain.Entidades;

namespace Detara.Domain.Atendimento;

public sealed class ChecklistModelo : EntidadeEmpresaBase
{
    public const string NomePadrao = "Checklist de entrada";
    public const int LimiteItens = 100;
    public const int LimiteDescricaoItem = 240;

    private readonly List<ChecklistModeloItem> _itens = [];

    private ChecklistModelo()
    {
    }

    public ChecklistModelo(
        Guid empresaId,
        string nome,
        string? descricao,
        IReadOnlyCollection<string> itens)
        : base(Guid.NewGuid(), empresaId)
    {
        Atualizar(nome, descricao, itens);
    }

    public string Nome { get; private set; } = NomePadrao;
    public string? Descricao { get; private set; }
    public IReadOnlyCollection<ChecklistModeloItem> Itens => _itens;

    public void Atualizar(
        string nome,
        string? descricao,
        IReadOnlyCollection<string> itens)
    {
        Nome = NormalizarObrigatorio(nome, 120, nameof(nome));
        Descricao = NormalizarOpcional(descricao, 500);
        if (itens.Count > LimiteItens)
        {
            throw new ArgumentException($"O checklist pode possuir no máximo {LimiteItens} itens.", nameof(itens));
        }

        var normalizados = itens.Select(NormalizarDescricaoItem).ToArray();
        if (normalizados.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalizados.Length)
        {
            throw new ArgumentException("O checklist não pode possuir itens duplicados.", nameof(itens));
        }

        _itens.Clear();
        _itens.AddRange(normalizados.Select((item, indice) =>
            new ChecklistModeloItem(EmpresaId, Id, item, indice + 1)));
        MarcarComoAtualizada();
    }

    internal static string NormalizarDescricaoItem(string descricao) =>
        NormalizarObrigatorio(descricao, LimiteDescricaoItem, nameof(descricao));

    private static string NormalizarObrigatorio(string valor, int limite, string parametro)
    {
        var normalizado = string.IsNullOrWhiteSpace(valor)
            ? throw new ArgumentException("O valor deve ser informado.", parametro)
            : valor.Trim();
        return normalizado.Length <= limite
            ? normalizado
            : throw new ArgumentException($"O valor deve possuir no máximo {limite} caracteres.", parametro);
    }

    private static string? NormalizarOpcional(string? valor, int limite)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var normalizado = valor.Trim();
        return normalizado.Length <= limite
            ? normalizado
            : throw new ArgumentException($"O valor deve possuir no máximo {limite} caracteres.");
    }
}
