using Detara.Domain.Entidades;

namespace Detara.Domain.Atendimento;

public sealed class HistoricoStatusOrcamento : EntidadeEmpresaBase
{
    private HistoricoStatusOrcamento() { }

    internal HistoricoStatusOrcamento(Guid empresaId, Guid orcamentoId, StatusOrcamento status, Guid usuarioId, string? observacao, DateTime dataUtc)
        : base(Guid.NewGuid(), empresaId)
    {
        OrcamentoId = orcamentoId != Guid.Empty ? orcamentoId : throw new ArgumentException("O orçamento deve ser informado.", nameof(orcamentoId));
        Status = Enum.IsDefined(status) ? status : throw new ArgumentException("O status é inválido.", nameof(status));
        UsuarioId = usuarioId != Guid.Empty ? usuarioId : throw new ArgumentException("O usuário deve ser informado.", nameof(usuarioId));
        Observacao = NormalizarOpcional(observacao, 1000);
        DataUtc = dataUtc.Kind == DateTimeKind.Utc ? dataUtc : throw new ArgumentException("A data deve estar em UTC.", nameof(dataUtc));
    }

    public Guid OrcamentoId { get; private set; }
    public StatusOrcamento Status { get; private set; }
    public DateTime DataUtc { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string? Observacao { get; private set; }
    public Orcamento Orcamento { get; private set; } = null!;

    private static string? NormalizarOpcional(string? valor, int limite)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var normalizado = valor.Trim();
        return normalizado.Length <= limite ? normalizado : throw new ArgumentException($"A observação deve possuir no máximo {limite} caracteres.");
    }
}
