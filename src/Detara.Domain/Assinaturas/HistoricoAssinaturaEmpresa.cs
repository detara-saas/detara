using Detara.Domain.Entidades;

namespace Detara.Domain.Assinaturas;

public sealed class HistoricoAssinaturaEmpresa : EntidadeEmpresaBase
{
    private HistoricoAssinaturaEmpresa() { }

    public HistoricoAssinaturaEmpresa(Guid empresaId, Guid assinaturaId, TipoEventoAssinatura tipoEvento,
        StatusAssinaturaEmpresa? statusAnterior, StatusAssinaturaEmpresa statusNovo, DateTime ocorridoEmUtc,
        string motivo, Guid? usuarioId = null, Guid? administradorPlataformaId = null,
        string? referenciaPagamento = null)
        : base(Guid.NewGuid(), empresaId)
    {
        if (assinaturaId == Guid.Empty) throw new ArgumentException("Assinatura obrigatória.", nameof(assinaturaId));
        if (usuarioId is null && administradorPlataformaId is null)
            throw new ArgumentException("O responsável pela alteração deve ser informado.");
        AssinaturaId = assinaturaId;
        TipoEvento = tipoEvento;
        StatusAnterior = statusAnterior;
        StatusNovo = statusNovo;
        OcorridoEmUtc = ocorridoEmUtc;
        Motivo = string.IsNullOrWhiteSpace(motivo) ? throw new ArgumentException("Motivo obrigatório.", nameof(motivo)) : motivo.Trim();
        UsuarioId = usuarioId;
        AdministradorPlataformaId = administradorPlataformaId;
        ReferenciaPagamento = string.IsNullOrWhiteSpace(referenciaPagamento) ? null : referenciaPagamento.Trim();
    }

    public Guid AssinaturaId { get; private set; }
    public TipoEventoAssinatura TipoEvento { get; private set; }
    public StatusAssinaturaEmpresa? StatusAnterior { get; private set; }
    public StatusAssinaturaEmpresa StatusNovo { get; private set; }
    public DateTime OcorridoEmUtc { get; private set; }
    public string Motivo { get; private set; } = string.Empty;
    public Guid? UsuarioId { get; private set; }
    public Guid? AdministradorPlataformaId { get; private set; }
    public string? ReferenciaPagamento { get; private set; }
}
