using Detara.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace Detara.IntegrationTests.Storage;

public sealed class S3ArquivoStorageTests
{
    private readonly S3ClientMemoria _client = new();
    private readonly S3ArquivoStorage _storage;

    public S3ArquivoStorageTests()
    {
        _storage = new S3ArquivoStorage(
            Options.Create(new StorageOptions
            {
                Provider = "S3",
                S3 = new S3StorageOptions { Bucket = "detara-private-tests" }
            }),
            _client);
    }

    [Fact]
    public async Task SalvarAbrirExcluir_UsaBucketPrivadoEChaveLogica()
    {
        const string chave = "empresas/a/veiculos/b/fotos/foto.webp";
        var conteudo = new byte[] { 1, 2, 3, 4 };

        await _storage.SalvarAsync(chave, new MemoryStream(conteudo), default);

        Assert.Equal("detara-private-tests", _client.UltimoBucket);
        Assert.Equal(chave, _client.UltimaChave);
        Assert.True(await _storage.ExisteAsync(chave, default));
        await using var leitura = await _storage.AbrirLeituraAsync(chave, default);
        Assert.NotNull(leitura);
        using var destino = new MemoryStream();
        await leitura.CopyToAsync(destino);
        Assert.Equal(conteudo, destino.ToArray());
        Assert.True(await _storage.ExcluirAsync(chave, default));
        Assert.False(await _storage.ExisteAsync(chave, default));
    }

    [Fact]
    public async Task ExcluirInexistente_NaoEnviaDelete()
    {
        Assert.False(await _storage.ExcluirAsync("empresas/a/inexistente.png", default));
        Assert.Equal(0, _client.Exclusoes);
    }

    [Theory]
    [InlineData("../../segredo.txt")]
    [InlineData("empresas/a/../../../segredo.txt")]
    [InlineData("empresas\\a\\foto.jpg")]
    [InlineData("/etc/passwd")]
    [InlineData("empresas//foto.jpg")]
    public async Task ChaveInvalidaOuPathTraversal_EhBloqueada(string chave)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.ExisteAsync(chave, default));
    }

    private sealed class S3ClientMemoria : IS3ObjectClient
    {
        private readonly Dictionary<string, byte[]> _objetos = [];
        public string? UltimoBucket { get; private set; }
        public string? UltimaChave { get; private set; }
        public int Exclusoes { get; private set; }

        public async Task SalvarAsync(
            string bucket,
            string chave,
            Stream conteudo,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UltimoBucket = bucket;
            UltimaChave = chave;
            using var destino = new MemoryStream();
            await conteudo.CopyToAsync(destino, cancellationToken);
            _objetos[chave] = destino.ToArray();
        }

        public Task<Stream?> AbrirLeituraAsync(
            string bucket,
            string chave,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stream? stream = _objetos.TryGetValue(chave, out var bytes)
                ? new MemoryStream(bytes, writable: false)
                : null;
            return Task.FromResult(stream);
        }

        public Task<bool> ExisteAsync(
            string bucket,
            string chave,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_objetos.ContainsKey(chave));
        }

        public Task ExcluirAsync(
            string bucket,
            string chave,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _objetos.Remove(chave);
            Exclusoes++;
            return Task.CompletedTask;
        }
    }
}
