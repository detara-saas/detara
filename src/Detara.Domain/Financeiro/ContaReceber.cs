using Detara.Domain.Entidades;

namespace Detara.Domain.Financeiro;

public enum StatusContaReceber
{
    EmAberto = 1,
    ParcialmentePago = 2,
    Pago = 3
}

public enum FormaPagamento
{
    Pix = 1,
    Dinheiro = 2,
    CartaoDebito = 3,
    CartaoCredito = 4,
    Boleto = 5,
    Transferencia = 6,
    Outro = 7
}

public enum StatusPagamento
{
    Confirmado = 1,
    Estornado = 2
}

public sealed class ContaReceber : EntidadeEmpresaBase
{
    private readonly List<Pagamento> _pagamentos = [];

    private ContaReceber()
    {
    }

    public ContaReceber(Guid empresaId, Guid ordemServicoId, string ordemServicoCodigo,
        Guid clienteId, string clienteNome, Guid veiculoId, string veiculoDescricao,
        string? veiculoPlaca, decimal subtotalAutorizado, decimal descontoAutorizado,
        decimal acrescimoAutorizado, decimal valorOriginal, DateOnly dataCompetencia)
        : base(Guid.NewGuid(), empresaId)
    {
        if (ordemServicoId == Guid.Empty || clienteId == Guid.Empty || veiculoId == Guid.Empty)
            throw new ArgumentException("Ordem de serviço, cliente e veículo devem ser informados.");
        if (subtotalAutorizado < 0 || descontoAutorizado < 0 || acrescimoAutorizado < 0 || valorOriginal <= 0)
            throw new ArgumentException("Os valores da conta a receber são inválidos.");
        if (subtotalAutorizado - descontoAutorizado + acrescimoAutorizado != valorOriginal)
            throw new ArgumentException("O valor original deve corresponder ao total autorizado da ordem de serviço.");

        OrdemServicoId = ordemServicoId;
        OrdemServicoCodigoSnapshot = Exigir(ordemServicoCodigo, 32, nameof(ordemServicoCodigo));
        ClienteId = clienteId;
        ClienteNomeSnapshot = Exigir(clienteNome, 160, nameof(clienteNome));
        VeiculoId = veiculoId;
        VeiculoDescricaoSnapshot = Exigir(veiculoDescricao, 200, nameof(veiculoDescricao));
        VeiculoPlacaSnapshot = NormalizarOpcional(veiculoPlaca, 10);
        SubtotalAutorizado = subtotalAutorizado;
        DescontoAutorizado = descontoAutorizado;
        AcrescimoAutorizado = acrescimoAutorizado;
        ValorOriginal = valorOriginal;
        DataCompetencia = dataCompetencia;
        DataVencimento = dataCompetencia;
        Status = StatusContaReceber.EmAberto;
        Versao = 1;
    }

    public Guid OrdemServicoId { get; private set; }
    public string OrdemServicoCodigoSnapshot { get; private set; } = string.Empty;
    public Guid ClienteId { get; private set; }
    public string ClienteNomeSnapshot { get; private set; } = string.Empty;
    public Guid VeiculoId { get; private set; }
    public string VeiculoDescricaoSnapshot { get; private set; } = string.Empty;
    public string? VeiculoPlacaSnapshot { get; private set; }
    public decimal SubtotalAutorizado { get; private set; }
    public decimal DescontoAutorizado { get; private set; }
    public decimal AcrescimoAutorizado { get; private set; }
    public decimal ValorOriginal { get; private set; }
    public decimal ValorRecebido { get; private set; }
    public decimal ValorEmAberto => ValorOriginal - ValorRecebido;
    public DateOnly DataCompetencia { get; private set; }
    public DateOnly DataVencimento { get; private set; }
    public StatusContaReceber Status { get; private set; }
    public long Versao { get; private set; }
    public IReadOnlyCollection<Pagamento> Pagamentos => _pagamentos.AsReadOnly();

    public bool EstaVencidaEm(DateOnly hoje) => ValorEmAberto > 0 && DataVencimento < hoje;

    public Pagamento RegistrarPagamento(FormaPagamento forma, decimal valor, decimal taxa,
        int? numeroParcelas, string? observacao, DateTime recebidoEmUtc, Guid usuarioId)
    {
        if (usuarioId == Guid.Empty) throw new ArgumentException("O responsável deve ser informado.", nameof(usuarioId));
        if (valor <= 0) throw new InvalidOperationException("O valor do pagamento deve ser maior que zero.");
        if (valor > ValorEmAberto) throw new InvalidOperationException("O valor do pagamento não pode ultrapassar o saldo em aberto.");
        if (taxa < 0 || taxa > valor) throw new InvalidOperationException("A taxa deve estar entre zero e o valor recebido.");
        if (forma == FormaPagamento.CartaoCredito)
        {
            if (numeroParcelas is < 1 or > 120) throw new InvalidOperationException("O número de parcelas deve estar entre 1 e 120.");
        }
        else if (numeroParcelas.HasValue)
        {
            throw new InvalidOperationException("Parcelas só podem ser informadas para cartão de crédito.");
        }
        if (forma == FormaPagamento.Outro && string.IsNullOrWhiteSpace(observacao))
            throw new InvalidOperationException("Descreva a forma de pagamento na observação.");

        var pagamento = new Pagamento(EmpresaId, Id, forma, valor, taxa, numeroParcelas,
            observacao, recebidoEmUtc, usuarioId);
        _pagamentos.Add(pagamento);
        ValorRecebido += valor;
        RecalcularStatus();
        MarcarAlteracaoFinanceira();
        return pagamento;
    }

    public void EstornarPagamento(Guid pagamentoId, Guid usuarioId, string motivo, DateTime estornadoEmUtc)
    {
        var pagamento = _pagamentos.SingleOrDefault(item => item.Id == pagamentoId)
            ?? throw new InvalidOperationException("Pagamento não encontrado nesta conta.");
        pagamento.Estornar(usuarioId, motivo, estornadoEmUtc);
        ValorRecebido -= pagamento.Valor;
        if (ValorRecebido < 0) throw new InvalidOperationException("O valor recebido da conta não pode ser negativo.");
        RecalcularStatus();
        MarcarAlteracaoFinanceira();
    }

    public void AlterarVencimento(DateOnly dataVencimento)
    {
        if (Status == StatusContaReceber.Pago)
            throw new InvalidOperationException("O vencimento de uma conta paga não pode ser alterado.");
        DataVencimento = dataVencimento;
        MarcarAlteracaoFinanceira();
    }

    private void RecalcularStatus() => Status = ValorRecebido switch
    {
        0 => StatusContaReceber.EmAberto,
        _ when ValorRecebido == ValorOriginal => StatusContaReceber.Pago,
        _ => StatusContaReceber.ParcialmentePago
    };

    private void MarcarAlteracaoFinanceira()
    {
        Versao++;
        MarcarComoAtualizada();
    }

    private static string Exigir(string valor, int limite, string parametro)
    {
        if (string.IsNullOrWhiteSpace(valor)) throw new ArgumentException("O valor deve ser informado.", parametro);
        var resultado = valor.Trim();
        if (resultado.Length > limite) throw new ArgumentException($"O valor deve ter no máximo {limite} caracteres.", parametro);
        return resultado;
    }

    private static string? NormalizarOpcional(string? valor, int limite)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var resultado = valor.Trim();
        return resultado.Length <= limite
            ? resultado
            : throw new ArgumentException($"O valor deve ter no máximo {limite} caracteres.");
    }
}

public sealed class Pagamento : EntidadeEmpresaBase
{
    private Pagamento()
    {
    }

    internal Pagamento(Guid empresaId, Guid contaReceberId, FormaPagamento formaPagamento,
        decimal valor, decimal taxa, int? numeroParcelas, string? observacao,
        DateTime recebidoEmUtc, Guid registradoPorUsuarioId)
        : base(Guid.NewGuid(), empresaId)
    {
        ContaReceberId = contaReceberId;
        FormaPagamento = formaPagamento;
        Valor = valor;
        Taxa = taxa;
        NumeroParcelas = numeroParcelas;
        Observacao = Normalizar(observacao, 1000);
        RecebidoEmUtc = DateTime.SpecifyKind(recebidoEmUtc, DateTimeKind.Utc);
        RegistradoPorUsuarioId = registradoPorUsuarioId;
        RegistradoEmUtc = DateTime.UtcNow;
        Status = StatusPagamento.Confirmado;
    }

    public Guid ContaReceberId { get; private set; }
    public ContaReceber ContaReceber { get; private set; } = null!;
    public FormaPagamento FormaPagamento { get; private set; }
    public decimal Valor { get; private set; }
    public decimal Taxa { get; private set; }
    public decimal ValorLiquido => Valor - Taxa;
    public int? NumeroParcelas { get; private set; }
    public string? Observacao { get; private set; }
    public DateTime RecebidoEmUtc { get; private set; }
    public Guid RegistradoPorUsuarioId { get; private set; }
    public DateTime RegistradoEmUtc { get; private set; }
    public StatusPagamento Status { get; private set; }
    public DateTime? EstornadoEmUtc { get; private set; }
    public Guid? EstornadoPorUsuarioId { get; private set; }
    public string? MotivoEstorno { get; private set; }

    internal void Estornar(Guid usuarioId, string motivo, DateTime estornadoEmUtc)
    {
        if (Status == StatusPagamento.Estornado) throw new InvalidOperationException("Este pagamento já foi estornado.");
        if (usuarioId == Guid.Empty) throw new ArgumentException("O responsável deve ser informado.", nameof(usuarioId));
        MotivoEstorno = Normalizar(motivo, 500)
            ?? throw new InvalidOperationException("O motivo do estorno deve ser informado.");
        Status = StatusPagamento.Estornado;
        EstornadoEmUtc = DateTime.SpecifyKind(estornadoEmUtc, DateTimeKind.Utc);
        EstornadoPorUsuarioId = usuarioId;
        MarcarComoAtualizada();
    }

    private static string? Normalizar(string? valor, int limite)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var resultado = valor.Trim();
        return resultado.Length <= limite ? resultado : throw new ArgumentException($"O texto deve ter no máximo {limite} caracteres.");
    }
}
