using Detara.Domain.Entidades;

namespace Detara.Domain.Financeiro;

public enum StatusContaPagar { Pendente = 1, Pago = 2, Cancelado = 3 }
public enum OrigemContaPagar { Avulsa = 1, Recorrente = 2 }

internal static class RegrasDespesa
{
    public static string Texto(string? valor, int limite) => Opcional(valor, limite)
        ?? throw new ArgumentException("Preencha o texto obrigatório.");
    public static string? Opcional(string? valor, int limite)
    {
        var texto = string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
        if (texto?.Length > limite) throw new ArgumentException($"O texto deve ter até {limite} caracteres.");
        return texto;
    }
    public static decimal Dinheiro(decimal valor) => valor > 0 && valor <= 9999999999999999.99m && decimal.Round(valor, 2) == valor
        ? valor : throw new ArgumentException("Informe um valor positivo com até duas casas decimais.");
    public static DateOnly Mes(DateOnly valor)
    {
        if (valor.Year is < 2000 or > 9998 || valor.Day != 1)
            throw new ArgumentException("Informe uma competência válida (primeiro dia do mês, entre 2000 e 9998).");
        return valor;
    }
    public static void Categoria(CategoriaDespesa categoria, Guid empresaId)
    {
        if (categoria.EmpresaId != empresaId || !categoria.EhAtivo)
            throw new InvalidOperationException("Escolha uma categoria ativa desta empresa.");
    }
}

public sealed class CategoriaDespesa : EntidadeEmpresaBase
{
    private CategoriaDespesa() { }
    public CategoriaDespesa(Guid empresaId, string nome) : base(Guid.NewGuid(), empresaId) => Renomear(nome);
    public string Nome { get; private set; } = string.Empty;
    public string NomeNormalizado { get; private set; } = string.Empty;
    public long Versao { get; private set; }
    public void Renomear(string nome)
    {
        Nome = RegrasDespesa.Texto(nome, 100);
        NomeNormalizado = Nome.ToUpperInvariant();
        Versao++;
    }
    public void DefinirAtividade(bool ativa) { EhAtivo = ativa; Versao++; }
}

public sealed class ContaPagar : EntidadeEmpresaBase
{
    private readonly List<PagamentoContaPagar> _pagamentos = [];
    private ContaPagar() { }
    public ContaPagar(Guid empresaId, string descricao, CategoriaDespesa categoria, decimal valor,
        DateOnly competencia, DateOnly vencimento, string? fornecedor = null, string? observacao = null)
        : base(Guid.NewGuid(), empresaId)
    {
        Editar(descricao, categoria, valor, competencia, vencimento, fornecedor, observacao);
    }
    public string Descricao { get; private set; } = string.Empty;
    public Guid CategoriaDespesaId { get; private set; }
    public string CategoriaNomeSnapshot { get; private set; } = string.Empty;
    public OrigemContaPagar Origem { get; private set; } = OrigemContaPagar.Avulsa;
    public Guid? DespesaRecorrenteId { get; private set; }
    public DateOnly Competencia { get; private set; }
    public DateOnly DataVencimento { get; private set; }
    public decimal Valor { get; private set; }
    public StatusContaPagar Status { get; private set; } = StatusContaPagar.Pendente;
    public DateOnly? DataPagamento { get; private set; }
    public decimal? ValorPago { get; private set; }
    public string? Fornecedor { get; private set; }
    public string? Observacao { get; private set; }
    public Guid? CanceladoPorUsuarioId { get; private set; }
    public DateTime? CanceladoEmUtc { get; private set; }
    public long Versao { get; private set; }
    public IReadOnlyCollection<PagamentoContaPagar> Pagamentos => _pagamentos.AsReadOnly();
    public bool EstaVencidaEm(DateOnly hoje) => Status == StatusContaPagar.Pendente && DataVencimento < hoje;
    public void Editar(string descricao, CategoriaDespesa categoria, decimal valor, DateOnly competencia,
        DateOnly vencimento, string? fornecedor, string? observacao)
    {
        ExigirPendente();
        RegrasDespesa.Categoria(categoria, EmpresaId);
        if (Origem == OrigemContaPagar.Recorrente && Competencia != competencia)
            throw new InvalidOperationException("A competência identifica a ocorrência recorrente e não pode ser alterada.");
        if (vencimento == default) throw new ArgumentException("Informe o vencimento.");
        Descricao = RegrasDespesa.Texto(descricao, 200);
        CategoriaDespesaId = categoria.Id;
        CategoriaNomeSnapshot = categoria.Nome;
        Valor = RegrasDespesa.Dinheiro(valor);
        Competencia = RegrasDespesa.Mes(competencia);
        DataVencimento = vencimento;
        Fornecedor = RegrasDespesa.Opcional(fornecedor, 160);
        Observacao = RegrasDespesa.Opcional(observacao, 2000);
        Versao++;
    }
    internal void VincularRecorrencia(Guid id) { DespesaRecorrenteId = id; Origem = OrigemContaPagar.Recorrente; }
    public PagamentoContaPagar RegistrarPagamento(DateOnly data, decimal valor, Guid usuarioId, DateTime agoraUtc)
    {
        ExigirPendente();
        if (data == default || usuarioId == Guid.Empty) throw new ArgumentException("Informe data e responsável pelo pagamento.");
        var pagamento = new PagamentoContaPagar(EmpresaId, Id, data, RegrasDespesa.Dinheiro(valor), usuarioId, agoraUtc);
        _pagamentos.Add(pagamento);
        Status = StatusContaPagar.Pago;
        DataPagamento = data;
        ValorPago = valor;
        Versao++;
        return pagamento;
    }
    public void EstornarPagamento(Guid usuarioId, string motivo, DateTime agoraUtc)
    {
        if (Status != StatusContaPagar.Pago) throw new InvalidOperationException("Somente uma conta paga permite estorno.");
        _pagamentos.Single(p => p.Status == StatusPagamento.Confirmado).Estornar(usuarioId, motivo, agoraUtc);
        Status = StatusContaPagar.Pendente;
        DataPagamento = null;
        ValorPago = null;
        Versao++;
    }
    public void Cancelar(Guid usuarioId, DateTime agoraUtc)
    {
        ExigirPendente();
        if (usuarioId == Guid.Empty) throw new ArgumentException("Informe o responsável.");
        Status = StatusContaPagar.Cancelado;
        CanceladoPorUsuarioId = usuarioId;
        CanceladoEmUtc = agoraUtc;
        Versao++;
    }
    private void ExigirPendente()
    {
        if (Status != StatusContaPagar.Pendente) throw new InvalidOperationException("Esta operação exige uma conta pendente.");
    }
}

public sealed class PagamentoContaPagar : EntidadeEmpresaBase
{
    private PagamentoContaPagar() { }
    internal PagamentoContaPagar(Guid empresaId, Guid contaId, DateOnly data, decimal valor, Guid usuarioId, DateTime agora)
        : base(Guid.NewGuid(), empresaId)
    { ContaPagarId = contaId; DataPagamento = data; Valor = valor; RegistradoPorUsuarioId = usuarioId; RegistradoEmUtc = agora; }
    public Guid ContaPagarId { get; private set; }
    public DateOnly DataPagamento { get; private set; }
    public decimal Valor { get; private set; }
    public Guid RegistradoPorUsuarioId { get; private set; }
    public DateTime RegistradoEmUtc { get; private set; }
    public StatusPagamento Status { get; private set; } = StatusPagamento.Confirmado;
    public Guid? EstornadoPorUsuarioId { get; private set; }
    public DateTime? EstornadoEmUtc { get; private set; }
    public string? MotivoEstorno { get; private set; }
    internal void Estornar(Guid usuarioId, string motivo, DateTime agora)
    {
        if (Status != StatusPagamento.Confirmado) throw new InvalidOperationException("Pagamento já estornado.");
        if (usuarioId == Guid.Empty) throw new ArgumentException("Informe o responsável.");
        MotivoEstorno = RegrasDespesa.Texto(motivo, 500);
        Status = StatusPagamento.Estornado;
        EstornadoPorUsuarioId = usuarioId;
        EstornadoEmUtc = agora;
    }
}

public sealed class DespesaRecorrente : EntidadeEmpresaBase
{
    private DespesaRecorrente() { }
    public DespesaRecorrente(Guid empresaId, string descricao, CategoriaDespesa categoria, decimal valor,
        int diaVencimento, DateOnly inicio, DateOnly? fim, DateOnly hoje, string? fornecedor = null, string? observacao = null)
        : base(Guid.NewGuid(), empresaId)
    {
        CompetenciaInicial = RegrasDespesa.Mes(inicio);
        ProximaCompetencia = inicio > hoje ? inicio : new DateOnly(hoje.Year, hoje.Month, 1);
        Editar(descricao, categoria, valor, diaVencimento, fim, fornecedor, observacao);
    }
    public string Descricao { get; private set; } = string.Empty;
    public Guid CategoriaDespesaId { get; private set; }
    public decimal Valor { get; private set; }
    public int DiaVencimento { get; private set; }
    public DateOnly CompetenciaInicial { get; private set; }
    public DateOnly? CompetenciaFinal { get; private set; }
    public DateOnly ProximaCompetencia { get; private set; }
    public string? Fornecedor { get; private set; }
    public string? Observacao { get; private set; }
    public long Versao { get; private set; }
    public void Editar(string descricao, CategoriaDespesa categoria, decimal valor, int dia,
        DateOnly? fim, string? fornecedor, string? observacao)
    {
        RegrasDespesa.Categoria(categoria, EmpresaId);
        if (dia is < 1 or > 31) throw new ArgumentException("O dia deve estar entre 1 e 31.");
        if (fim.HasValue && RegrasDespesa.Mes(fim.Value) < CompetenciaInicial)
            throw new ArgumentException("A competência final não pode preceder a inicial.");
        Descricao = RegrasDespesa.Texto(descricao, 200);
        CategoriaDespesaId = categoria.Id;
        Valor = RegrasDespesa.Dinheiro(valor);
        DiaVencimento = dia;
        CompetenciaFinal = fim;
        Fornecedor = RegrasDespesa.Opcional(fornecedor, 160);
        Observacao = RegrasDespesa.Opcional(observacao, 2000);
        Versao++;
    }
    public void DefinirAtividade(bool ativa, DateOnly hoje)
    {
        if (ativa && !EhAtivo)
        {
            var atual = new DateOnly(hoje.Year, hoje.Month, 1);
            if (ProximaCompetencia < atual) ProximaCompetencia = atual;
        }
        EhAtivo = ativa;
        Versao++;
    }
    public bool PodeMaterializar(DateOnly hoje) => EhAtivo && ProximaCompetencia <= hoje &&
        (!CompetenciaFinal.HasValue || ProximaCompetencia <= CompetenciaFinal);
    public DateOnly CalcularVencimento(DateOnly competencia) => new(competencia.Year, competencia.Month,
        Math.Min(DiaVencimento, DateTime.DaysInMonth(competencia.Year, competencia.Month)));
    public ContaPagar Materializar(CategoriaDespesa categoria, DateOnly hoje)
    {
        if (!PodeMaterializar(hoje)) throw new InvalidOperationException("Não há competência a materializar.");
        var conta = new ContaPagar(EmpresaId, Descricao, categoria, Valor, ProximaCompetencia,
            CalcularVencimento(ProximaCompetencia), Fornecedor, Observacao);
        conta.VincularRecorrencia(Id);
        AvancarCompetencia();
        return conta;
    }
    public void AvancarCompetencia() { ProximaCompetencia = ProximaCompetencia.AddMonths(1); Versao++; }
}
