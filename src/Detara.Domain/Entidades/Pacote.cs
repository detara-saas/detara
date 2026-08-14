using Detara.Domain.Catalogo;

namespace Detara.Domain.Entidades;

public sealed class Pacote : EntidadeEmpresaBase
{
    private readonly List<PacoteServico> _servicos = [];

    private Pacote() { }

    public Pacote(Guid empresaId, string nome, string? descricao, TipoPrecificacao tipoPrecificacao, decimal? preco, IReadOnlyCollection<Guid> servicoIds)
        : base(Guid.NewGuid(), empresaId)
    {
        AtualizarDados(nome, descricao, tipoPrecificacao, preco);
        SubstituirServicos(servicoIds);
    }

    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }
    public TipoPrecificacao TipoPrecificacao { get; private set; }
    public decimal? Preco { get; private set; }
    public IReadOnlyCollection<PacoteServico> Servicos => _servicos;

    public void Atualizar(string nome, string? descricao, TipoPrecificacao tipoPrecificacao, decimal? preco, IReadOnlyCollection<Guid> servicoIds)
    {
        AtualizarDados(nome, descricao, tipoPrecificacao, preco);
        SubstituirServicos(servicoIds);
    }

    private void AtualizarDados(string nome, string? descricao, TipoPrecificacao tipoPrecificacao, decimal? preco)
    {
        Nome = TextoCatalogo.Exigir(nome, 160, nameof(nome), 2);
        Descricao = TextoCatalogo.NormalizarOpcional(descricao, 2000);
        TipoPrecificacao = tipoPrecificacao;
        Preco = PrecificacaoCatalogo.Validar(tipoPrecificacao, preco, nameof(preco));
        MarcarComoAtualizada();
    }

    private void SubstituirServicos(IReadOnlyCollection<Guid> servicoIds)
    {
        if (servicoIds.Count == 0)
        {
            throw new ArgumentException("O pacote deve possuir ao menos um serviço.", nameof(servicoIds));
        }

        if (servicoIds.Any(id => id == Guid.Empty) || servicoIds.Distinct().Count() != servicoIds.Count)
        {
            throw new ArgumentException("Os serviços do pacote devem ser válidos e não podem se repetir.", nameof(servicoIds));
        }

        _servicos.Clear();
        _servicos.AddRange(servicoIds.Select((id, indice) => new PacoteServico(EmpresaId, Id, id, indice + 1)));
    }
}
