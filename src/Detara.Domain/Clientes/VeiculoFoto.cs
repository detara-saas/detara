using Detara.Domain.Entidades;

namespace Detara.Domain.Clientes;

public sealed class VeiculoFoto : EntidadeEmpresaBase
{
    private VeiculoFoto()
    {
    }

    public VeiculoFoto(
        Guid id,
        Guid empresaId,
        Guid veiculoId,
        string chaveStorage,
        string nomeOriginal,
        string contentType,
        long tamanhoBytes,
        bool ehPrincipal,
        Guid criadoPorUsuarioId)
        : base(
            id != Guid.Empty
                ? id
                : throw new ArgumentException("O identificador deve ser informado.", nameof(id)),
            empresaId)
    {
        VeiculoId = veiculoId != Guid.Empty
            ? veiculoId
            : throw new ArgumentException("O veículo deve ser informado.", nameof(veiculoId));
        ChaveStorage = Exigir(chaveStorage, 500, nameof(chaveStorage));
        NomeOriginal = Exigir(nomeOriginal, 255, nameof(nomeOriginal));
        ContentType = contentType is "image/jpeg" or "image/png" or "image/webp"
            ? contentType
            : throw new ArgumentException("O tipo da imagem não é suportado.", nameof(contentType));
        TamanhoBytes = tamanhoBytes > 0
            ? tamanhoBytes
            : throw new ArgumentOutOfRangeException(nameof(tamanhoBytes), "O arquivo não pode estar vazio.");
        EhPrincipal = ehPrincipal;
        CriadoPorUsuarioId = criadoPorUsuarioId != Guid.Empty
            ? criadoPorUsuarioId
            : throw new ArgumentException("O usuário responsável deve ser informado.", nameof(criadoPorUsuarioId));
    }

    public Guid VeiculoId { get; private set; }
    public string ChaveStorage { get; private set; } = string.Empty;
    public string NomeOriginal { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long TamanhoBytes { get; private set; }
    public bool EhPrincipal { get; private set; }
    public Guid CriadoPorUsuarioId { get; private set; }

    public void DefinirComoPrincipal(bool ehPrincipal)
    {
        EhPrincipal = ehPrincipal;
        MarcarComoAtualizada();
    }

    private static string Exigir(string valor, int limite, string parametro)
    {
        var normalizado = string.IsNullOrWhiteSpace(valor)
            ? throw new ArgumentException("O valor deve ser informado.", parametro)
            : valor.Trim();
        return normalizado.Length <= limite
            ? normalizado
            : throw new ArgumentException($"O valor deve possuir no máximo {limite} caracteres.", parametro);
    }
}
