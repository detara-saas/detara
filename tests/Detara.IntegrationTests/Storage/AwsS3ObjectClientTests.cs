using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Detara.Infrastructure.Storage;

namespace Detara.IntegrationTests.Storage;

public sealed class AwsS3ObjectClientTests
{
    [Fact]
    public async Task SalvarAsync_ConfiguraPutObjectCompativelComCloudflareR2()
    {
        using var s3 = new AmazonS3ClientCaptura();
        var sut = new AwsS3ObjectClient(s3);
        await using var conteudo = new MemoryStream([1, 2, 3]);
        using var cancelamento = new CancellationTokenSource();

        await sut.SalvarAsync("bucket-privado", "empresas/a/os/b/durante/foto.jpg", conteudo, cancelamento.Token);

        var request = Assert.IsType<PutObjectRequest>(s3.Request);
        Assert.Equal("bucket-privado", request.BucketName);
        Assert.Equal("empresas/a/os/b/durante/foto.jpg", request.Key);
        Assert.Same(conteudo, request.InputStream);
        Assert.False(request.AutoCloseStream);
        Assert.True(request.DisablePayloadSigning);
        Assert.True(request.DisableDefaultChecksumValidation);
        Assert.Equal(cancelamento.Token, s3.CancellationToken);
        Assert.True(conteudo.CanRead);
    }

    private sealed class AmazonS3ClientCaptura : AmazonS3Client
    {
        public AmazonS3ClientCaptura()
            : base(new AnonymousAWSCredentials(), new AmazonS3Config { ServiceURL = "https://storage.example.test" })
        {
        }

        public PutObjectRequest? Request { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public override Task<PutObjectResponse> PutObjectAsync(
            PutObjectRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            CancellationToken = cancellationToken;
            return Task.FromResult(new PutObjectResponse());
        }
    }
}
