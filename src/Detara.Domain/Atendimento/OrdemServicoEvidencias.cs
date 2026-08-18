using Detara.Domain.Entidades;

namespace Detara.Domain.Atendimento;

public sealed class OrdemServicoFoto : EntidadeEmpresaBase
{
    private OrdemServicoFoto() { }
    public OrdemServicoFoto(Guid empresaId, Guid ordemServicoId, CategoriaFotoOrdemServico categoria, string chaveStorage,
        string nomeOriginal, string contentType, long tamanhoBytes, Guid usuarioId)
        : base(Guid.NewGuid(), empresaId)
    {
        OrdemServicoId = ordemServicoId != Guid.Empty ? ordemServicoId : throw new ArgumentException("A ordem de serviço deve ser informada.", nameof(ordemServicoId));
        Categoria = Enum.IsDefined(categoria) ? categoria : throw new ArgumentException("A categoria da foto é inválida.", nameof(categoria));
        ChaveStorage = Exigir(chaveStorage, 500);
        NomeOriginal = Exigir(nomeOriginal, 255);
        ContentType = Exigir(contentType, 100);
        TamanhoBytes = tamanhoBytes > 0 ? tamanhoBytes : throw new ArgumentException("O tamanho da foto deve ser positivo.", nameof(tamanhoBytes));
        EnviadaPorUsuarioId = usuarioId != Guid.Empty ? usuarioId : throw new ArgumentException("O usuário deve ser informado.", nameof(usuarioId));
    }
    public Guid OrdemServicoId { get; private set; }
    public CategoriaFotoOrdemServico Categoria { get; private set; }
    public string ChaveStorage { get; private set; } = string.Empty;
    public string NomeOriginal { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long TamanhoBytes { get; private set; }
    public Guid EnviadaPorUsuarioId { get; private set; }
    public OrdemServico OrdemServico { get; private set; } = null!;
    private static string Exigir(string valor, int limite)
    {
        var texto = string.IsNullOrWhiteSpace(valor) ? throw new ArgumentException("O valor deve ser informado.") : valor.Trim();
        return texto.Length <= limite ? texto : throw new ArgumentException($"O valor deve possuir no máximo {limite} caracteres.");
    }
}

public sealed class HistoricoStatusOrdemServico : EntidadeEmpresaBase
{
    private HistoricoStatusOrdemServico() { }
    internal HistoricoStatusOrdemServico(Guid empresaId, Guid ordemServicoId, StatusOrdemServico status,
        Guid usuarioId, string? observacao, DateTime dataUtc) : base(Guid.NewGuid(), empresaId)
    {
        OrdemServicoId = ordemServicoId;
        Status = status;
        UsuarioId = usuarioId;
        DataUtc = dataUtc;
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim().Length <= 1000
            ? observacao.Trim() : throw new ArgumentException("A observação deve possuir no máximo 1000 caracteres.", nameof(observacao));
    }
    public Guid OrdemServicoId { get; private set; }
    public StatusOrdemServico Status { get; private set; }
    public DateTime DataUtc { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string? Observacao { get; private set; }
    public OrdemServico OrdemServico { get; private set; } = null!;
}
