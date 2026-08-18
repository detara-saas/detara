using Detara.Domain.Entidades;

namespace Detara.Domain.Atendimento;

public sealed class OrdemServicoChecklist : EntidadeEmpresaBase
{
    private readonly List<OrdemServicoChecklistItem> _itens = [];
    private OrdemServicoChecklist() { }

    internal OrdemServicoChecklist(Guid empresaId, Guid ordemServicoId, string nome, IReadOnlyCollection<string> itens)
        : base(Guid.NewGuid(), empresaId)
    {
        OrdemServicoId = ordemServicoId != Guid.Empty ? ordemServicoId : throw new ArgumentException("A ordem de serviço deve ser informada.", nameof(ordemServicoId));
        NomeSnapshot = Normalizar(nome, 120);
        _itens.AddRange(itens.Select((descricao, indice) => new OrdemServicoChecklistItem(empresaId, Id, descricao, indice + 1)));
    }

    public Guid OrdemServicoId { get; private set; }
    public string NomeSnapshot { get; private set; } = string.Empty;
    public IReadOnlyCollection<OrdemServicoChecklistItem> Itens => _itens;
    public OrdemServico OrdemServico { get; private set; } = null!;
    public bool EstaCompleto => _itens.All(item => item.Resposta.HasValue);

    internal void Atualizar(IReadOnlyCollection<RespostaChecklistSnapshot> respostas)
    {
        var porId = respostas.ToDictionary(item => item.ItemId);
        if (porId.Keys.Any(id => _itens.All(item => item.Id != id)))
        {
            throw new ArgumentException("A resposta contém item que não pertence ao checklist.", nameof(respostas));
        }
        foreach (var item in _itens)
        {
            if (porId.TryGetValue(item.Id, out var resposta)) item.Responder(resposta.Resposta, resposta.Observacao);
        }
        MarcarComoAtualizada();
    }

    private static string Normalizar(string valor, int limite)
    {
        var texto = string.IsNullOrWhiteSpace(valor) ? throw new ArgumentException("O nome do checklist deve ser informado.") : valor.Trim();
        return texto.Length <= limite ? texto : throw new ArgumentException($"O nome deve possuir no máximo {limite} caracteres.");
    }
}

public sealed record RespostaChecklistSnapshot(Guid ItemId, RespostaChecklistOrdemServico Resposta, string? Observacao);

public sealed class OrdemServicoChecklistItem : EntidadeEmpresaBase
{
    private OrdemServicoChecklistItem() { }
    internal OrdemServicoChecklistItem(Guid empresaId, Guid checklistId, string descricao, int ordem) : base(Guid.NewGuid(), empresaId)
    {
        ChecklistId = checklistId != Guid.Empty ? checklistId : throw new ArgumentException("O checklist deve ser informado.", nameof(checklistId));
        DescricaoSnapshot = ChecklistModelo.NormalizarDescricaoItem(descricao);
        Ordem = ordem > 0 ? ordem : throw new ArgumentException("A ordem deve ser positiva.", nameof(ordem));
    }

    public Guid ChecklistId { get; private set; }
    public string DescricaoSnapshot { get; private set; } = string.Empty;
    public int Ordem { get; private set; }
    public RespostaChecklistOrdemServico? Resposta { get; private set; }
    public string? Observacao { get; private set; }
    public OrdemServicoChecklist Checklist { get; private set; } = null!;

    internal void Responder(RespostaChecklistOrdemServico resposta, string? observacao)
    {
        Resposta = Enum.IsDefined(resposta) ? resposta : throw new ArgumentException("A resposta do checklist é inválida.", nameof(resposta));
        if (string.IsNullOrWhiteSpace(observacao)) Observacao = null;
        else if (observacao.Trim().Length <= 1000) Observacao = observacao.Trim();
        else throw new ArgumentException("A observação deve possuir no máximo 1000 caracteres.", nameof(observacao));
        MarcarComoAtualizada();
    }
}
