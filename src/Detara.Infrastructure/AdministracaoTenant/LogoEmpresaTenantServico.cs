using Detara.Application.Abstracoes;
using Detara.Application.AdministracaoTenant;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace Detara.Infrastructure.AdministracaoTenant;

internal sealed class LogoEmpresaTenantServico(
    DetaraDbContext db,
    IUsuarioContexto usuario,
    IArquivoStorage storage) : ILogoEmpresaTenantServico
{
    internal const long TamanhoMaximo = 2 * 1024 * 1024;
    internal const long PixelsMaximos = 32_000_000;
    private const string ContentTypePng = "image/png";

    public async Task<LogoEmpresaResultado> SalvarAsync(
        Stream conteudo,
        string contentType,
        long tamanho,
        CancellationToken ct)
    {
        var mime = contentType.Trim().ToLowerInvariant();
        if (tamanho is <= 0 or > TamanhoMaximo ||
            mime is not ("image/png" or "image/jpeg"))
            throw ArquivoInvalido();

        var normalizada = await NormalizarAsync(conteudo, tamanho, mime, ct);
        var empresa = await ObterEmpresaAsync(ct);
        var chaveAnterior = empresa.LogoArquivoChave;
        var novaChave = $"empresas/{empresa.Id:N}/branding/logo-{Guid.NewGuid():N}.png";

        await using var arquivo = new MemoryStream(normalizada, writable: false);
        await storage.SalvarAsync(novaChave, arquivo, ct);
        try
        {
            empresa.DefinirLogo(novaChave);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await ExcluirSemFalharAsync(novaChave, ct);
            throw;
        }

        if (chaveAnterior is not null) await ExcluirSemFalharAsync(chaveAnterior, ct);
        return Mapear(empresa);
    }

    public async Task<ArquivoLogoEmpresa?> AbrirAsync(CancellationToken ct)
    {
        var empresa = await db.Empresas.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == usuario.EmpresaId, ct)
            ?? throw new RecursoNaoEncontradoException("Empresa não encontrada.");
        if (empresa.LogoArquivoChave is null) return null;
        var stream = await storage.AbrirLeituraAsync(empresa.LogoArquivoChave, ct);
        return stream is null ? null : new(stream, ContentTypePng, empresa.LogoVersao);
    }

    public async Task<ArquivoLogoEmpresa?> AbrirPublicaAsync(Guid token, CancellationToken ct)
    {
        var logo = await db.Empresas.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.LogoTokenPublico == token && x.EhAtivo && x.LogoArquivoChave != null)
            .Select(x => new { Chave = x.LogoArquivoChave!, x.LogoVersao })
            .SingleOrDefaultAsync(ct);
        if (logo is null) return null;
        var stream = await storage.AbrirLeituraAsync(logo.Chave, ct);
        return stream is null ? null : new(stream, ContentTypePng, logo.LogoVersao);
    }

    public async Task<LogoEmpresaResultado> RemoverAsync(CancellationToken ct)
    {
        var empresa = await ObterEmpresaAsync(ct);
        var chave = empresa.LogoArquivoChave;
        empresa.RemoverLogo();
        await db.SaveChangesAsync(ct);
        if (chave is not null) await ExcluirSemFalharAsync(chave, ct);
        return Mapear(empresa);
    }

    private async Task<Domain.Entidades.Empresa> ObterEmpresaAsync(CancellationToken ct) =>
        await db.Empresas.SingleOrDefaultAsync(x => x.Id == usuario.EmpresaId, ct)
        ?? throw new RecursoNaoEncontradoException("Empresa não encontrada.");

    private async Task ExcluirSemFalharAsync(string chave, CancellationToken ct)
    {
        try { await storage.ExcluirAsync(chave, ct); }
        catch when (!ct.IsCancellationRequested) { /* A referência já é autoritativa; limpeza pode ser posterior. */ }
    }

    private static LogoEmpresaResultado Mapear(Domain.Entidades.Empresa empresa) =>
        new(empresa.LogoArquivoChave is not null, empresa.LogoVersao, empresa.LogoAtualizadaEmUtc,
            empresa.LogoTokenPublico);

    private static async Task<byte[]> NormalizarAsync(Stream origem, long tamanho, string mime, CancellationToken ct)
    {
        await using var limitado = new MemoryStream((int)tamanho);
        await origem.CopyToAsync(limitado, ct);
        if (limitado.Length != tamanho || limitado.Length > TamanhoMaximo) throw ArquivoInvalido();

        using var dados = SKData.CreateCopy(limitado.ToArray());
        using var codec = SKCodec.Create(dados) ?? throw ArquivoInvalido();
        if (codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg) ||
            codec.EncodedFormat == SKEncodedImageFormat.Png && mime != "image/png" ||
            codec.EncodedFormat == SKEncodedImageFormat.Jpeg && mime != "image/jpeg") throw ArquivoInvalido();
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 ||
            (long)info.Width * info.Height > PixelsMaximos) throw ArquivoInvalido();

        using var original = SKBitmap.Decode(dados) ?? throw ArquivoInvalido();
        using var orientada = Orientar(original, codec.EncodedOrigin);
        using var superficie = SKSurface.Create(new SKImageInfo(1200, 400, SKColorType.Rgba8888, SKAlphaType.Premul));
        superficie.Canvas.Clear(SKColors.Transparent);
        var escala = Math.Min(1f, Math.Min(1200f / orientada.Width, 400f / orientada.Height));
        var largura = orientada.Width * escala;
        var altura = orientada.Height * escala;
        var destino = SKRect.Create((1200 - largura) / 2, (400 - altura) / 2, largura, altura);
        using var logo = SKImage.FromBitmap(orientada);
        superficie.Canvas.DrawImage(logo, destino, new SKSamplingOptions(SKCubicResampler.Mitchell));
        using var imagem = superficie.Snapshot();
        using var png = imagem.Encode(SKEncodedImageFormat.Png, 90) ?? throw ArquivoInvalido();
        return png.ToArray();
    }

    private static SKBitmap Orientar(SKBitmap origem, SKEncodedOrigin orientacao)
    {
        var trocaEixos = orientacao is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var destino = new SKBitmap(trocaEixos ? origem.Height : origem.Width, trocaEixos ? origem.Width : origem.Height,
            SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(destino);
        canvas.Clear(SKColors.Transparent);
        switch (orientacao)
        {
            case SKEncodedOrigin.TopRight: canvas.Scale(-1, 1); canvas.Translate(-origem.Width, 0); break;
            case SKEncodedOrigin.BottomRight: canvas.Translate(origem.Width, origem.Height); canvas.RotateDegrees(180); break;
            case SKEncodedOrigin.BottomLeft: canvas.Scale(1, -1); canvas.Translate(0, -origem.Height); break;
            case SKEncodedOrigin.RightTop: canvas.Translate(origem.Height, 0); canvas.RotateDegrees(90); break;
            case SKEncodedOrigin.LeftBottom: canvas.Translate(0, origem.Width); canvas.RotateDegrees(-90); break;
            case SKEncodedOrigin.LeftTop: canvas.Concat(new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1)); break;
            case SKEncodedOrigin.RightBottom: canvas.Concat(new SKMatrix(0, -1, origem.Height, -1, 0, origem.Width, 0, 0, 1)); break;
        }
        canvas.DrawBitmap(origem, 0, 0);
        return destino;
    }

    private static ArgumentException ArquivoInvalido() =>
        new("Envie uma imagem PNG ou JPG válida com até 2 MB e dimensões razoáveis.", "arquivo");
}
