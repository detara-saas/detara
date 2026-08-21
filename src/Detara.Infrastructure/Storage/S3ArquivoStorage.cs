using Detara.Application.Abstracoes;
using Microsoft.Extensions.Options;

namespace Detara.Infrastructure.Storage;

internal sealed class S3ArquivoStorage(
    IOptions<StorageOptions> options,
    IS3ObjectClient client) : IArquivoStorage
{
    private readonly string _bucket = options.Value.S3.Bucket;

    public Task SalvarAsync(string chave, Stream conteudo, CancellationToken cancellationToken)
    {
        StorageChave.Validar(chave);
        return client.SalvarAsync(_bucket, chave, conteudo, cancellationToken);
    }

    public Task<Stream?> AbrirLeituraAsync(string chave, CancellationToken cancellationToken)
    {
        StorageChave.Validar(chave);
        return client.AbrirLeituraAsync(_bucket, chave, cancellationToken);
    }

    public async Task<bool> ExcluirAsync(string chave, CancellationToken cancellationToken)
    {
        StorageChave.Validar(chave);
        if (!await client.ExisteAsync(_bucket, chave, cancellationToken))
        {
            return false;
        }

        await client.ExcluirAsync(_bucket, chave, cancellationToken);
        return true;
    }

    public Task<bool> ExisteAsync(string chave, CancellationToken cancellationToken)
    {
        StorageChave.Validar(chave);
        return client.ExisteAsync(_bucket, chave, cancellationToken);
    }
}
