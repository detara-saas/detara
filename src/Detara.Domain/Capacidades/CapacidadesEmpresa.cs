using Detara.Domain.Entidades;

namespace Detara.Domain.Capacidades;

public static class SegmentosEmpresa
{
    public const string EsteticaAutomotiva = "estetica-automotiva";

    public static bool EhConhecido(string codigo) =>
        string.Equals(codigo, EsteticaAutomotiva, StringComparison.Ordinal);
}

public static class CodigosCapacidadeEmpresa
{
    public const string Clientes = "clientes";
    public const string Servicos = "servicos";
    public const string Pacotes = "pacotes";
    public const string Agenda = "agenda";
    public const string Orcamentos = "orcamentos";
    public const string OrdemServico = "ordem-servico";
    public const string Financeiro = "financeiro";
    public const string Despesas = "despesas";
    public const string Veiculos = "veiculos";
    public const string CheckIn = "check-in";
}

public sealed record DefinicaoCapacidadeEmpresa(
    string Codigo,
    string Nome,
    string Categoria,
    IReadOnlyCollection<string> Dependencias,
    bool Configuravel,
    int Ordem);

public static class CatalogoCapacidadesEmpresa
{
    public const string CategoriaCore = "Core";
    public const string CategoriaAutomotivo = "Automotivo";

    public static IReadOnlyList<DefinicaoCapacidadeEmpresa> Todas { get; } =
    [
        new(CodigosCapacidadeEmpresa.Clientes, "Clientes", CategoriaCore, [], false, 10),
        new(CodigosCapacidadeEmpresa.Servicos, "Serviços", CategoriaCore, [], false, 20),
        new(CodigosCapacidadeEmpresa.Pacotes, "Pacotes", CategoriaCore, [], false, 30),
        new(CodigosCapacidadeEmpresa.Agenda, "Agenda", CategoriaCore, [], false, 40),
        new(CodigosCapacidadeEmpresa.Orcamentos, "Orçamentos", CategoriaCore, [], false, 50),
        new(CodigosCapacidadeEmpresa.OrdemServico, "Ordem de Serviço", CategoriaCore, [], false, 60),
        new(CodigosCapacidadeEmpresa.Financeiro, "Financeiro", CategoriaCore, [], false, 70),
        new(CodigosCapacidadeEmpresa.Despesas, "Despesas", CategoriaCore, [], false, 80),
        new(CodigosCapacidadeEmpresa.Veiculos, "Veículos", CategoriaAutomotivo, [], false, 90),
        new(CodigosCapacidadeEmpresa.CheckIn, "Check-in", CategoriaAutomotivo,
            [CodigosCapacidadeEmpresa.Veiculos], false, 100)
    ];

    private static readonly IReadOnlyDictionary<string, DefinicaoCapacidadeEmpresa> PorCodigo =
        Todas.ToDictionary(item => item.Codigo, StringComparer.Ordinal);

    public static bool TentarObter(string codigo, out DefinicaoCapacidadeEmpresa definicao) =>
        PorCodigo.TryGetValue(codigo, out definicao!);

    public static void ValidarConfiguracao(IReadOnlyDictionary<string, bool> capacidades)
    {
        foreach (var (codigo, habilitada) in capacidades)
        {
            if (!TentarObter(codigo, out var definicao))
                throw new ArgumentException($"A capacidade '{codigo}' não pertence ao catálogo.", nameof(capacidades));

            if (!habilitada) continue;
            foreach (var dependencia in definicao.Dependencias)
            {
                if (!capacidades.TryGetValue(dependencia, out var dependenciaHabilitada) ||
                    !dependenciaHabilitada)
                {
                    throw new ArgumentException(
                        $"A capacidade '{definicao.Nome}' exige a capacidade '{dependencia}'.",
                        nameof(capacidades));
                }
            }
        }
    }
}

public static class PresetCapacidadesEmpresa
{
    public static IReadOnlyCollection<EmpresaCapacidade> Criar(Guid empresaId, string segmentoCodigo)
    {
        if (!SegmentosEmpresa.EhConhecido(segmentoCodigo))
            throw new ArgumentException("O segmento informado não possui preset disponível.", nameof(segmentoCodigo));

        var estado = CatalogoCapacidadesEmpresa.Todas.ToDictionary(item => item.Codigo, _ => true);
        CatalogoCapacidadesEmpresa.ValidarConfiguracao(estado);
        return CatalogoCapacidadesEmpresa.Todas
            .OrderBy(item => item.Ordem)
            .Select(item => new EmpresaCapacidade(empresaId, item.Codigo, estado[item.Codigo]))
            .ToArray();
    }
}

public sealed class EmpresaCapacidade : EntidadeEmpresaBase
{
    private EmpresaCapacidade()
    {
    }

    public EmpresaCapacidade(Guid empresaId, string codigo, bool habilitada)
        : base(Guid.NewGuid(), empresaId)
    {
        if (!CatalogoCapacidadesEmpresa.TentarObter(codigo, out _))
            throw new ArgumentException("A capacidade informada não pertence ao catálogo.", nameof(codigo));

        Codigo = codigo;
        Habilitada = habilitada;
    }

    public string Codigo { get; private set; } = string.Empty;
    public bool Habilitada { get; private set; }
    public long Versao { get; private set; } = 1;

    public void Alterar(bool habilitada, long versaoEsperada)
    {
        if (versaoEsperada != Versao)
            throw new InvalidOperationException("A configuração de capacidades foi alterada por outra operação.");

        Habilitada = habilitada;
        Versao++;
        MarcarComoAtualizada();
    }
}
