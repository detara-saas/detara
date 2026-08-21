using System.Net;
using Amazon.S3;
using Amazon.S3.Model;

namespace Detara.Infrastructure.Storage;

internal interface IS3ObjectClient
{
    Task SalvarAsync(string bucket, string chave, Stream conteudo, CancellationToken cancellationToken);
    Task<Stream?> AbrirLeituraAsync(string bucket, string chave, CancellationToken cancellationToken);
    Task<bool> ExisteAsync(string bucket, string chave, CancellationToken cancellationToken);
    Task ExcluirAsync(string bucket, string chave, CancellationToken cancellationToken);
}

internal sealed class AwsS3ObjectClient(IAmazonS3 client) : IS3ObjectClient
{
    public async Task SalvarAsync(
        string bucket,
        string chave,
        Stream conteudo,
        CancellationToken cancellationToken)
    {
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = chave,
            InputStream = conteudo,
            AutoCloseStream = false
        }, cancellationToken);
    }

    public async Task<Stream?> AbrirLeituraAsync(
        string bucket,
        string chave,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetObjectAsync(bucket, chave, cancellationToken);
            return new ResponseOwnedStream(response.ResponseStream, response);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> ExisteAsync(
        string bucket,
        string chave,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetObjectMetadataAsync(bucket, chave, cancellationToken);
            return response.HttpStatusCode == HttpStatusCode.OK;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task ExcluirAsync(
        string bucket,
        string chave,
        CancellationToken cancellationToken)
    {
        await client.DeleteObjectAsync(bucket, chave, cancellationToken);
    }

    private sealed class ResponseOwnedStream(Stream inner, IDisposable owner) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => inner.Read(buffer);
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                owner.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            owner.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
