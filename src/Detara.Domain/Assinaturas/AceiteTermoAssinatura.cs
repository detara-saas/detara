using Detara.Domain.Entidades;

namespace Detara.Domain.Assinaturas;

public sealed class AceiteTermoAssinatura : EntidadeEmpresaBase
{
    private AceiteTermoAssinatura() { }

    public AceiteTermoAssinatura(Guid empresaId, Guid assinaturaId, Guid usuarioId, string versaoTermo,
        DateTime aceitoEmUtc, string documentoChave, string hashSha256, decimal valorMensal,
        DateOnly dataInicio, DateOnly primeiroVencimento, string empresaNome, string empresaDocumento,
        string responsavelNome, string responsavelEmail, string? ipAceite)
        : base(Guid.NewGuid(), empresaId)
    {
        if (assinaturaId == Guid.Empty || usuarioId == Guid.Empty) throw new ArgumentException("Assinatura e usuário são obrigatórios.");
        AssinaturaId = assinaturaId;
        UsuarioId = usuarioId;
        VersaoTermo = Exigir(versaoTermo, nameof(versaoTermo));
        AceitoEmUtc = aceitoEmUtc;
        DocumentoChave = Exigir(documentoChave, nameof(documentoChave));
        HashSha256 = Exigir(hashSha256, nameof(hashSha256));
        ValorMensal = valorMensal;
        DataInicio = dataInicio;
        PrimeiroVencimento = primeiroVencimento;
        EmpresaNome = Exigir(empresaNome, nameof(empresaNome));
        EmpresaDocumento = Exigir(empresaDocumento, nameof(empresaDocumento));
        ResponsavelNome = Exigir(responsavelNome, nameof(responsavelNome));
        ResponsavelEmail = Exigir(responsavelEmail, nameof(responsavelEmail));
        IpAceite = string.IsNullOrWhiteSpace(ipAceite) ? null : ipAceite.Trim();
    }

    public Guid AssinaturaId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string VersaoTermo { get; private set; } = string.Empty;
    public DateTime AceitoEmUtc { get; private set; }
    public string DocumentoChave { get; private set; } = string.Empty;
    public string HashSha256 { get; private set; } = string.Empty;
    public decimal ValorMensal { get; private set; }
    public DateOnly DataInicio { get; private set; }
    public DateOnly PrimeiroVencimento { get; private set; }
    public string EmpresaNome { get; private set; } = string.Empty;
    public string EmpresaDocumento { get; private set; } = string.Empty;
    public string ResponsavelNome { get; private set; } = string.Empty;
    public string ResponsavelEmail { get; private set; } = string.Empty;
    public string? IpAceite { get; private set; }

    private static string Exigir(string valor, string parametro) =>
        string.IsNullOrWhiteSpace(valor) ? throw new ArgumentException("Valor obrigatório.", parametro) : valor.Trim();
}
