namespace Detara.Application.Abstracoes;

public interface IArquivoStorage
{
    Task SalvarAsync(
        string chave,
        Stream conteudo,
        CancellationToken cancellationToken);

    Task<Stream?> AbrirLeituraAsync(
        string chave,
        CancellationToken cancellationToken);

    Task<bool> ExcluirAsync(
        string chave,
        CancellationToken cancellationToken);

    Task<bool> ExisteAsync(
        string chave,
        CancellationToken cancellationToken);
}
