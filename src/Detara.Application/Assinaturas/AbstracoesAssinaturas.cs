namespace Detara.Application.Assinaturas;

public static class TermosAssinatura
{
    public const string VersaoAtual = "1.0";
}

public sealed record DocumentoAssinatura(Stream Conteudo, string NomeArquivo, string ContentType);

public sealed record TermoAceitoResultado(Guid Id, string VersaoTermo, DateTime AceitoEmUtc,
    string HashSha256, string ResponsavelNome, string ResponsavelEmail);
public sealed record AssinaturaEmpresaResultado(bool PossuiAssinatura, Guid? Id, string? Status,
    decimal? ValorMensal, DateOnly? DataInicio, DateOnly? FimTeste, int? DiaVencimento,
    DateOnly? PrimeiroVencimento, DateOnly? ProximoVencimento, long? Versao,
    string? AsaasCustomerId, string? AsaasSubscriptionId,
    TermoAceitoResultado? TermoAceito);
public sealed record CriarAssinaturaEntrada(decimal ValorMensal, DateOnly DataInicio, int DiaVencimento,
    string? AsaasCustomerId, string? AsaasSubscriptionId);
public sealed record AlterarCondicoesAssinaturaEntrada(decimal ValorMensal, DateOnly ProximoVencimento,
    int DiaVencimento, string? AsaasCustomerId, string? AsaasSubscriptionId, long Versao, string Motivo);
public sealed record AlterarStatusAssinaturaEntrada(long Versao, string Motivo);
public sealed record ConfirmarPagamentoAssinaturaEntrada(DateOnly DataPagamento, string? ReferenciaPagamento,
    string? AsaasCustomerId, string? AsaasSubscriptionId, long Versao, string Motivo);
public sealed record HistoricoAssinaturaResultado(Guid Id, string TipoEvento, string? StatusAnterior,
    string StatusNovo, DateTime OcorridoEmUtc, string Motivo, string? Responsavel,
    string? ReferenciaPagamento);
public sealed record AssinaturaPlataformaResultado(Guid EmpresaId, string EmpresaNome,
    AssinaturaEmpresaResultado Assinatura, IReadOnlyCollection<HistoricoAssinaturaResultado> Historico);

public sealed record DadosTermoAssinatura(
    string EmpresaNome,
    string EmpresaDocumento,
    string ResponsavelNome,
    string ResponsavelEmail,
    decimal ValorMensal,
    DateOnly DataInicio,
    DateOnly FimTeste,
    DateOnly PrimeiroVencimento,
    int DiaVencimento,
    string VersaoTermo,
    DateTime? AceitoEmUtc = null);

public interface IGeradorPdfTermoAssinatura
{
    byte[] Gerar(DadosTermoAssinatura dados);
}

public interface IAssinaturasTenantServico
{
    Task<AssinaturaEmpresaResultado> ObterAsync(CancellationToken cancellationToken);
    Task<DocumentoAssinatura> GerarPreviaAsync(CancellationToken cancellationToken);
    Task<AssinaturaEmpresaResultado> AceitarAsync(string? ipAceite, CancellationToken cancellationToken);
    Task<DocumentoAssinatura> AbrirTermoAceitoAsync(CancellationToken cancellationToken);
}

public interface IAssinaturasPlataformaServico
{
    Task<IReadOnlyCollection<AssinaturaPlataformaResultado>> ListarAsync(CancellationToken cancellationToken);
    Task<AssinaturaPlataformaResultado> ObterAsync(Guid empresaId, CancellationToken cancellationToken);
    Task<AssinaturaPlataformaResultado> CriarAsync(Guid administradorId, Guid empresaId,
        CriarAssinaturaEntrada request, CancellationToken cancellationToken);
    Task<AssinaturaPlataformaResultado> AlterarCondicoesAsync(Guid administradorId, Guid empresaId,
        AlterarCondicoesAssinaturaEntrada request, CancellationToken cancellationToken);
    Task<AssinaturaPlataformaResultado> ConfirmarPagamentoAsync(Guid administradorId, Guid empresaId,
        ConfirmarPagamentoAssinaturaEntrada request, CancellationToken cancellationToken);
    Task<AssinaturaPlataformaResultado> MarcarAtrasoAsync(Guid administradorId, Guid empresaId,
        AlterarStatusAssinaturaEntrada request, CancellationToken cancellationToken);
    Task<AssinaturaPlataformaResultado> SuspenderAsync(Guid administradorId, Guid empresaId,
        AlterarStatusAssinaturaEntrada request, CancellationToken cancellationToken);
    Task<AssinaturaPlataformaResultado> ReativarAsync(Guid administradorId, Guid empresaId,
        AlterarStatusAssinaturaEntrada request, CancellationToken cancellationToken);
    Task<AssinaturaPlataformaResultado> CancelarAsync(Guid administradorId, Guid empresaId,
        AlterarStatusAssinaturaEntrada request, CancellationToken cancellationToken);
    Task<DocumentoAssinatura> AbrirTermoAceitoAsync(Guid empresaId, CancellationToken cancellationToken);
}
