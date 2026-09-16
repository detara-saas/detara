using Detara.Domain.Entidades;

namespace Detara.Domain.Assinaturas;

public enum StatusAssinaturaEmpresa
{
    EmTeste = 1,
    Ativa = 2,
    EmAtraso = 3,
    Suspensa = 4,
    Cancelada = 5
}

public enum TipoEventoAssinatura
{
    Criada = 1,
    TermoAceito = 2,
    PagamentoConfirmado = 3,
    AtrasoRegistrado = 4,
    Suspensa = 5,
    Reativada = 6,
    Cancelada = 7,
    CondicoesAlteradas = 8
}

public static class CalendarioAssinatura
{
    public const int DiasTeste = 7;
    public const int DiaVencimentoPadrao = 10;
    public const int DiasAteSuspensao = 7;

    public static DateOnly CalcularFimTeste(DateOnly inicio) => inicio.AddDays(DiasTeste);

    public static DateOnly CalcularPrimeiroVencimento(DateOnly fimTeste, int diaVencimento = DiaVencimentoPadrao)
    {
        ValidarDiaVencimento(diaVencimento);
        var candidato = CriarVencimento(fimTeste.Year, fimTeste.Month, diaVencimento);
        return candidato > fimTeste
            ? candidato
            : CriarVencimento(fimTeste.AddMonths(1).Year, fimTeste.AddMonths(1).Month, diaVencimento);
    }

    public static DateOnly CalcularProximoVencimento(DateOnly referencia, int diaVencimento)
    {
        ValidarDiaVencimento(diaVencimento);
        var proximoMes = referencia.AddMonths(1);
        return CriarVencimento(proximoMes.Year, proximoMes.Month, diaVencimento);
    }

    public static bool DeveMarcarAtraso(DateOnly vencimento, DateOnly hoje) => hoje > vencimento;

    public static bool DeveSuspender(DateOnly vencimento, DateOnly hoje) =>
        hoje >= vencimento.AddDays(DiasAteSuspensao);

    private static DateOnly CriarVencimento(int ano, int mes, int dia) =>
        new(ano, mes, Math.Min(dia, DateTime.DaysInMonth(ano, mes)));

    private static void ValidarDiaVencimento(int dia)
    {
        if (dia is < 1 or > 28)
        {
            throw new ArgumentOutOfRangeException(nameof(dia), "O dia de vencimento deve estar entre 1 e 28.");
        }
    }
}

public sealed class AssinaturaEmpresa : EntidadeEmpresaBase
{
    private AssinaturaEmpresa() { }

    public AssinaturaEmpresa(
        Guid empresaId,
        decimal valorMensal,
        DateOnly dataInicio,
        int diaVencimento = CalendarioAssinatura.DiaVencimentoPadrao,
        string? asaasCustomerId = null,
        string? asaasSubscriptionId = null)
        : base(Guid.NewGuid(), empresaId)
    {
        ValidarValor(valorMensal);
        _ = CalendarioAssinatura.CalcularPrimeiroVencimento(dataInicio, diaVencimento);
        ValorMensal = valorMensal;
        DataInicio = dataInicio;
        InicioTeste = dataInicio;
        FimTeste = CalendarioAssinatura.CalcularFimTeste(dataInicio);
        DiaVencimento = diaVencimento;
        PrimeiroVencimento = CalendarioAssinatura.CalcularPrimeiroVencimento(FimTeste, diaVencimento);
        ProximoVencimento = PrimeiroVencimento;
        Status = StatusAssinaturaEmpresa.EmTeste;
        AsaasCustomerId = NormalizarOpcional(asaasCustomerId);
        AsaasSubscriptionId = NormalizarOpcional(asaasSubscriptionId);
        Versao = 1;
    }

    public StatusAssinaturaEmpresa Status { get; private set; }
    public decimal ValorMensal { get; private set; }
    public DateOnly DataInicio { get; private set; }
    public DateOnly InicioTeste { get; private set; }
    public DateOnly FimTeste { get; private set; }
    public int DiaVencimento { get; private set; }
    public DateOnly PrimeiroVencimento { get; private set; }
    public DateOnly ProximoVencimento { get; private set; }
    public DateTime? UltimoPagamentoConfirmadoEmUtc { get; private set; }
    public DateTime? SuspensaEmUtc { get; private set; }
    public DateTime? CanceladaEmUtc { get; private set; }
    public string? AsaasCustomerId { get; private set; }
    public string? AsaasSubscriptionId { get; private set; }
    public long Versao { get; private set; }

    public bool MarcarEmAtraso(long versaoEsperada)
    {
        ValidarVersao(versaoEsperada);
        if (Status == StatusAssinaturaEmpresa.EmAtraso) return false;
        ExigirNaoCancelada();
        if (Status == StatusAssinaturaEmpresa.Suspensa)
            throw new InvalidOperationException("A assinatura suspensa não pode voltar para atraso.");
        AlterarStatus(StatusAssinaturaEmpresa.EmAtraso);
        return true;
    }

    public bool Suspender(DateTime agoraUtc, long versaoEsperada)
    {
        ValidarVersao(versaoEsperada);
        if (Status == StatusAssinaturaEmpresa.Suspensa) return false;
        ExigirNaoCancelada();
        Status = StatusAssinaturaEmpresa.Suspensa;
        SuspensaEmUtc = agoraUtc;
        AvancarVersao();
        return true;
    }

    public bool Reativar(DateTime agoraUtc, long versaoEsperada)
    {
        ValidarVersao(versaoEsperada);
        if (Status == StatusAssinaturaEmpresa.Ativa) return false;
        ExigirNaoCancelada();
        Status = StatusAssinaturaEmpresa.Ativa;
        SuspensaEmUtc = null;
        AvancarVersao();
        return true;
    }

    public bool ConfirmarPagamento(DateTime agoraUtc, DateOnly dataPagamento, string? asaasCustomerId,
        string? asaasSubscriptionId, long versaoEsperada)
    {
        ValidarVersao(versaoEsperada);
        ExigirNaoCancelada();
        if (UltimoPagamentoConfirmadoEmUtc.HasValue &&
            UltimoPagamentoConfirmadoEmUtc.Value == agoraUtc &&
            Status == StatusAssinaturaEmpresa.Ativa)
            return false;

        Status = StatusAssinaturaEmpresa.Ativa;
        UltimoPagamentoConfirmadoEmUtc = agoraUtc;
        SuspensaEmUtc = null;
        ProximoVencimento = CalendarioAssinatura.CalcularProximoVencimento(
            dataPagamento > ProximoVencimento ? dataPagamento : ProximoVencimento,
            DiaVencimento);
        AsaasCustomerId = NormalizarOpcional(asaasCustomerId) ?? AsaasCustomerId;
        AsaasSubscriptionId = NormalizarOpcional(asaasSubscriptionId) ?? AsaasSubscriptionId;
        AvancarVersao();
        return true;
    }

    public bool Cancelar(DateTime agoraUtc, long versaoEsperada)
    {
        ValidarVersao(versaoEsperada);
        if (Status == StatusAssinaturaEmpresa.Cancelada) return false;
        Status = StatusAssinaturaEmpresa.Cancelada;
        CanceladaEmUtc = agoraUtc;
        AvancarVersao();
        return true;
    }

    public bool AlterarCondicoes(decimal valorMensal, DateOnly proximoVencimento, int diaVencimento,
        string? asaasCustomerId, string? asaasSubscriptionId, long versaoEsperada)
    {
        ValidarVersao(versaoEsperada);
        ExigirNaoCancelada();
        ValidarValor(valorMensal);
        _ = CalendarioAssinatura.CalcularProximoVencimento(proximoVencimento, diaVencimento);
        if (ValorMensal == valorMensal && ProximoVencimento == proximoVencimento &&
            DiaVencimento == diaVencimento && AsaasCustomerId == NormalizarOpcional(asaasCustomerId) &&
            AsaasSubscriptionId == NormalizarOpcional(asaasSubscriptionId)) return false;
        ValorMensal = valorMensal;
        ProximoVencimento = proximoVencimento;
        DiaVencimento = diaVencimento;
        AsaasCustomerId = NormalizarOpcional(asaasCustomerId);
        AsaasSubscriptionId = NormalizarOpcional(asaasSubscriptionId);
        AvancarVersao();
        return true;
    }

    private void AlterarStatus(StatusAssinaturaEmpresa status)
    {
        Status = status;
        AvancarVersao();
    }

    private void AvancarVersao()
    {
        Versao++;
        MarcarComoAtualizada();
    }

    private void ValidarVersao(long versaoEsperada)
    {
        if (versaoEsperada != Versao)
            throw new InvalidOperationException("A assinatura foi atualizada por outra operação.");
    }

    private void ExigirNaoCancelada()
    {
        if (Status == StatusAssinaturaEmpresa.Cancelada)
            throw new InvalidOperationException("A assinatura cancelada não pode ser alterada.");
    }

    private static void ValidarValor(decimal valor)
    {
        if (valor <= 0) throw new ArgumentOutOfRangeException(nameof(valor), "O valor mensal deve ser positivo.");
    }

    private static string? NormalizarOpcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
