using Detara.Application.Clientes;
using Detara.Infrastructure.Storage;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Detara.IntegrationTests.Storage;

public sealed class LocalArquivoStorageTests : IAsyncLifetime
{
    private readonly string _diretorio = Path.Combine(
        Path.GetTempPath(),
        "detara-storage-tests",
        Guid.NewGuid().ToString("N"));
    private LocalArquivoStorage _storage = null!;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_diretorio);
        _storage = new LocalArquivoStorage(
            Options.Create(new StorageOptions
            {
                Provider = "Local",
                Local = new LocalStorageOptions { RootPath = "storage" }
            }),
            new AmbienteTeste(_diretorio));
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_diretorio))
        {
            Directory.Delete(_diretorio, true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task SalvarAbrirExcluir_UsaChaveLogicaDentroDaRaiz()
    {
        const string chave = "empresas/a/veiculos/b/fotos/foto.jpg";
        var bytes = new byte[] { 1, 2, 3, 4, 5 };

        await _storage.SalvarAsync(chave, new MemoryStream(bytes), default);

        Assert.True(await _storage.ExisteAsync(chave, default));
        await using (var leitura = await _storage.AbrirLeituraAsync(chave, default))
        {
            Assert.NotNull(leitura);
            using var destino = new MemoryStream();
            await leitura.CopyToAsync(destino);
            Assert.Equal(bytes, destino.ToArray());
        }

        Assert.True(await _storage.ExcluirAsync(chave, default));
        Assert.False(await _storage.ExisteAsync(chave, default));
    }

    [Fact]
    public async Task ArquivoInexistente_RetornaAusenciaSemCriarDiretorio()
    {
        Assert.Null(await _storage.AbrirLeituraAsync("empresas/a/inexistente.png", default));
        Assert.False(await _storage.ExcluirAsync("empresas/a/inexistente.png", default));
    }

    [Theory]
    [InlineData("../../segredo.txt")]
    [InlineData("../segredo.txt")]
    [InlineData("empresas/a/../../../segredo.txt")]
    [InlineData("empresas\\a\\foto.jpg")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\segredo.txt")]
    [InlineData("empresas//foto.jpg")]
    public async Task ChaveInvalidaOuPathTraversal_EhBloqueada(string chave)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _storage.ExisteAsync(chave, default));
    }

    [Fact]
    public async Task EscritaConcluida_NaoDeixaArquivoTemporario()
    {
        await _storage.SalvarAsync(
            "empresas/a/veiculos/b/fotos/atomica.webp",
            new MemoryStream([1, 2, 3]),
            default);

        Assert.Empty(Directory.GetFiles(_diretorio, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void RootPathDentroDoWwwroot_EhRejeitado()
    {
        Assert.Throws<InvalidOperationException>(() => new LocalArquivoStorage(
            Options.Create(new StorageOptions
            {
                Provider = "Local",
                Local = new LocalStorageOptions { RootPath = "wwwroot/storage" }
            }),
            new AmbienteTeste(_diretorio)));
    }

    [Theory]
    [MemberData(nameof(ImagensValidas))]
    public async Task MagicBytes_DetectamJpegPngEWebp(
        byte[] bytes,
        string contentType,
        string extensao)
    {
        var resultado = await ValidadorImagemUpload.ValidarAsync(
            new MemoryStream(bytes),
            bytes.Length,
            default);

        Assert.Equal(contentType, resultado.ContentType);
        Assert.Equal(extensao, resultado.Extensao);
        using var copiado = new MemoryStream();
        await resultado.Conteudo.CopyToAsync(copiado);
        Assert.Equal(bytes, copiado.ToArray());
    }

    [Fact]
    public async Task StreamSemSeek_PreservaAssinaturaAoSalvar()
    {
        var bytes = ImagensValidas().First().First() as byte[] ?? [];
        await using var origem = new StreamSomenteLeituraSemSeek(bytes);

        var resultado = await ValidadorImagemUpload.ValidarAsync(origem, bytes.Length, default);
        using var copiado = new MemoryStream();
        await resultado.Conteudo.CopyToAsync(copiado);

        Assert.Equal(bytes, copiado.ToArray());
    }

    [Fact]
    public async Task ConteudoInvalido_MesmoComNomeJpeg_EhRejeitado()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ValidadorImagemUpload.ValidarAsync(
                new MemoryStream("nao-e-imagem"u8.ToArray()),
                12,
                default));
    }

    [Fact]
    public async Task ArquivoMaiorQue10MiB_EhRejeitadoAntesDaLeitura()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ValidadorImagemUpload.ValidarAsync(
                new MemoryStream([0xFF, 0xD8, 0xFF]),
                PoliticaImagemVeiculo.TamanhoMaximoBytes + 1,
                default));
    }

    [Fact]
    public async Task ArquivoVazio_EhRejeitado()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ValidadorImagemUpload.ValidarAsync(new MemoryStream(), 0, default));
    }

    public static IEnumerable<object[]> ImagensValidas()
    {
        yield return [new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4 }, "image/jpeg", "jpg"];
        yield return [new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2 }, "image/png", "png"];
        yield return [new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50, 5 }, "image/webp", "webp"];
    }

    private sealed class AmbienteTeste(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Detara.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class StreamSomenteLeituraSemSeek(byte[] bytes) : Stream
    {
        private readonly MemoryStream _interno = new(bytes);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => _interno.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _interno.ReadAsync(buffer, cancellationToken);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _interno.Dispose(); base.Dispose(disposing); }
        public override async ValueTask DisposeAsync() { await _interno.DisposeAsync(); GC.SuppressFinalize(this); }
    }
}
