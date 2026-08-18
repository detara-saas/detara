namespace Detara.Application.Abstracoes;

public static class PoliticaImagemUpload
{
    public const long TamanhoMaximoBytes = 10L * 1024 * 1024;
}

internal sealed record ImagemUploadValidada(string ContentType, string Extensao, Stream Conteudo);

internal static class ValidadorArquivoImagem
{
    public static async Task<ImagemUploadValidada> ValidarAsync(Stream conteudo, long tamanhoBytes,
        CancellationToken cancellationToken)
    {
        if (tamanhoBytes <= 0) throw new ArgumentException("O arquivo não pode estar vazio.");
        if (tamanhoBytes > PoliticaImagemUpload.TamanhoMaximoBytes) throw new ArgumentException("A foto deve possuir no máximo 10 MiB.");
        if (!conteudo.CanRead) throw new ArgumentException("Não foi possível ler o arquivo enviado.");

        var assinatura = new byte[12];
        var lidos = 0;
        while (lidos < assinatura.Length)
        {
            var leitura = await conteudo.ReadAsync(assinatura.AsMemory(lidos, assinatura.Length - lidos), cancellationToken);
            if (leitura == 0) break;
            lidos += leitura;
        }

        var tipo = Detectar(assinatura.AsSpan(0, lidos))
            ?? throw new ArgumentException("O conteúdo não é uma imagem JPEG, PNG ou WebP válida.");
        Stream streamParaSalvar;
        if (conteudo.CanSeek)
        {
            conteudo.Seek(-lidos, SeekOrigin.Current);
            streamParaSalvar = conteudo;
        }
        else streamParaSalvar = new StreamPrefixado(assinatura.AsMemory(0, lidos), conteudo);
        return new ImagemUploadValidada(tipo.ContentType, tipo.Extensao, streamParaSalvar);
    }

    private static (string ContentType, string Extensao)? Detectar(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return ("image/jpeg", "jpg");
        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (bytes.Length >= png.Length && bytes[..png.Length].SequenceEqual(png)) return ("image/png", "png");
        return bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8)
            ? ("image/webp", "webp") : null;
    }

    private sealed class StreamPrefixado(ReadOnlyMemory<byte> prefixo, Stream restante) : Stream
    {
        private int _posicaoPrefixo;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var copiados = CopiarPrefixo(buffer.AsSpan(offset, count));
            return copiados > 0 ? copiados : restante.Read(buffer, offset, count);
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var copiados = CopiarPrefixo(buffer.Span);
            return copiados > 0 ? copiados : await restante.ReadAsync(buffer, cancellationToken);
        }
        private int CopiarPrefixo(Span<byte> destino)
        {
            var quantidade = Math.Min(destino.Length, prefixo.Length - _posicaoPrefixo);
            if (quantidade <= 0) return 0;
            prefixo.Span.Slice(_posicaoPrefixo, quantidade).CopyTo(destino);
            _posicaoPrefixo += quantidade;
            return quantidade;
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
