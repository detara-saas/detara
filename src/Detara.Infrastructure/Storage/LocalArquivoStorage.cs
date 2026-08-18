using Detara.Application.Abstracoes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Detara.Infrastructure.Storage;

internal sealed class LocalArquivoStorage : IArquivoStorage
{
    private readonly string _rootPath;
    private readonly string _rootPrefix;

    public LocalArquivoStorage(
        IOptions<StorageOptions> options,
        IHostEnvironment ambiente)
    {
        var configurado = options.Value.Local.RootPath;
        if (string.IsNullOrWhiteSpace(configurado))
        {
            throw new InvalidOperationException("Storage:Local:RootPath deve ser configurado.");
        }

        _rootPath = Path.GetFullPath(
            Path.IsPathRooted(configurado)
                ? configurado
                : Path.Combine(ambiente.ContentRootPath, configurado));
        _rootPrefix = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        var webRoot = Path.GetFullPath(Path.Combine(ambiente.ContentRootPath, "wwwroot"));
        if (EstaDentroDe(_rootPath, webRoot))
        {
            throw new InvalidOperationException(
                "O diretório privado de storage não pode ficar dentro de wwwroot.");
        }
    }

    public async Task SalvarAsync(
        string chave,
        Stream conteudo,
        CancellationToken cancellationToken)
    {
        var destino = ResolverCaminho(chave);
        var diretorio = Path.GetDirectoryName(destino)!;
        Directory.CreateDirectory(diretorio);
        var temporario = Path.Combine(diretorio, $".{Path.GetFileName(destino)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var arquivo = new FileStream(
                temporario,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await conteudo.CopyToAsync(arquivo, cancellationToken);
                await arquivo.FlushAsync(cancellationToken);
            }

            File.Move(temporario, destino, false);
        }
        finally
        {
            if (File.Exists(temporario))
            {
                File.Delete(temporario);
            }
        }
    }

    public Task<Stream?> AbrirLeituraAsync(
        string chave,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var caminho = ResolverCaminho(chave);
        Stream? resultado = File.Exists(caminho)
            ? new FileStream(
                caminho,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan)
            : null;
        return Task.FromResult(resultado);
    }

    public Task<bool> ExcluirAsync(
        string chave,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var caminho = ResolverCaminho(chave);
        if (!File.Exists(caminho))
        {
            return Task.FromResult(false);
        }

        File.Delete(caminho);
        return Task.FromResult(true);
    }

    public Task<bool> ExisteAsync(
        string chave,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(ResolverCaminho(chave)));
    }

    private string ResolverCaminho(string chave)
    {
        if (string.IsNullOrWhiteSpace(chave) ||
            Path.IsPathRooted(chave) ||
            chave.Contains('\\'))
        {
            throw new ArgumentException("A chave de storage é inválida.", nameof(chave));
        }

        var segmentos = chave.Split('/', StringSplitOptions.None);
        if (segmentos.Length == 0 || segmentos.Any(segmento =>
                string.IsNullOrWhiteSpace(segmento) ||
                segmento is "." or ".." ||
                segmento.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new ArgumentException("A chave de storage é inválida.", nameof(chave));
        }

        var caminho = Path.GetFullPath(Path.Combine([_rootPath, .. segmentos]));
        if (!caminho.StartsWith(_rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A chave de storage tenta sair do diretório permitido.", nameof(chave));
        }

        return caminho;
    }

    private static bool EstaDentroDe(string caminho, string raiz)
    {
        var raizPrefixo = raiz.EndsWith(Path.DirectorySeparatorChar)
            ? raiz
            : raiz + Path.DirectorySeparatorChar;
        return caminho.Equals(raiz, StringComparison.OrdinalIgnoreCase) ||
               caminho.StartsWith(raizPrefixo, StringComparison.OrdinalIgnoreCase);
    }
}
