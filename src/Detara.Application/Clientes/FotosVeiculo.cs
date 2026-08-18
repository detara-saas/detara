using Detara.Application.Abstracoes;
using Detara.Domain.Clientes;
using FluentValidation;
using MediatR;

namespace Detara.Application.Clientes;

public static class PoliticaImagemVeiculo
{
    public const long TamanhoMaximoBytes = PoliticaImagemUpload.TamanhoMaximoBytes;
}

public sealed record VeiculoFotoVisualizacao(
    Guid Id,
    Guid VeiculoId,
    string NomeOriginal,
    string ContentType,
    long TamanhoBytes,
    bool EhPrincipal,
    DateTime CriadoEmUtc);

public sealed record ConteudoVeiculoFoto(
    Stream Conteudo,
    string ContentType,
    string NomeOriginal);

public sealed record ListarFotosVeiculoQuery(Guid VeiculoId)
    : IRequest<IReadOnlyCollection<VeiculoFotoVisualizacao>>;

public sealed record ObterConteudoVeiculoFotoQuery(Guid VeiculoId, Guid FotoId)
    : IRequest<ConteudoVeiculoFoto>;

public sealed record EnviarFotoVeiculoCommand(
    Guid VeiculoId,
    string NomeOriginal,
    long TamanhoBytes,
    Stream Conteudo)
    : IRequest<VeiculoFotoVisualizacao>;

public sealed record DefinirFotoPrincipalVeiculoCommand(Guid VeiculoId, Guid FotoId)
    : IRequest;

public sealed record ExcluirFotoVeiculoCommand(Guid VeiculoId, Guid FotoId)
    : IRequest;

internal sealed class ListarFotosVeiculoValidator : AbstractValidator<ListarFotosVeiculoQuery>
{
    public ListarFotosVeiculoValidator() => RuleFor(item => item.VeiculoId).NotEmpty();
}

internal sealed class ObterConteudoVeiculoFotoValidator : AbstractValidator<ObterConteudoVeiculoFotoQuery>
{
    public ObterConteudoVeiculoFotoValidator()
    {
        RuleFor(item => item.VeiculoId).NotEmpty();
        RuleFor(item => item.FotoId).NotEmpty();
    }
}

internal sealed class EnviarFotoVeiculoValidator : AbstractValidator<EnviarFotoVeiculoCommand>
{
    public EnviarFotoVeiculoValidator()
    {
        RuleFor(item => item.VeiculoId).NotEmpty();
        RuleFor(item => item.NomeOriginal).NotEmpty().MaximumLength(1024);
        RuleFor(item => item.TamanhoBytes)
            .GreaterThan(0).WithMessage("O arquivo não pode estar vazio.")
            .LessThanOrEqualTo(PoliticaImagemVeiculo.TamanhoMaximoBytes)
            .WithMessage("A foto deve possuir no máximo 10 MiB.");
        RuleFor(item => item.Conteudo).NotNull();
    }
}

internal sealed class DefinirFotoPrincipalVeiculoValidator
    : AbstractValidator<DefinirFotoPrincipalVeiculoCommand>
{
    public DefinirFotoPrincipalVeiculoValidator()
    {
        RuleFor(item => item.VeiculoId).NotEmpty();
        RuleFor(item => item.FotoId).NotEmpty();
    }
}

internal sealed class ExcluirFotoVeiculoValidator : AbstractValidator<ExcluirFotoVeiculoCommand>
{
    public ExcluirFotoVeiculoValidator()
    {
        RuleFor(item => item.VeiculoId).NotEmpty();
        RuleFor(item => item.FotoId).NotEmpty();
    }
}

internal sealed class ListarFotosVeiculoHandler(IVeiculoFotosRepositorio repositorio)
    : IRequestHandler<ListarFotosVeiculoQuery, IReadOnlyCollection<VeiculoFotoVisualizacao>>
{
    public async Task<IReadOnlyCollection<VeiculoFotoVisualizacao>> Handle(
        ListarFotosVeiculoQuery request,
        CancellationToken cancellationToken)
    {
        await FotoVeiculoFluxo.ExigirVeiculoAsync(repositorio, request.VeiculoId, false, cancellationToken);
        return (await repositorio.ListarAsync(request.VeiculoId, cancellationToken))
            .OrderByDescending(item => item.EhPrincipal)
            .ThenBy(item => item.CriadoEmUtc)
            .Select(FotoVeiculoFluxo.Mapear)
            .ToArray();
    }
}

internal sealed class ObterConteudoVeiculoFotoHandler(
    IVeiculoFotosRepositorio repositorio,
    IArquivoStorage storage)
    : IRequestHandler<ObterConteudoVeiculoFotoQuery, ConteudoVeiculoFoto>
{
    public async Task<ConteudoVeiculoFoto> Handle(
        ObterConteudoVeiculoFotoQuery request,
        CancellationToken cancellationToken)
    {
        await FotoVeiculoFluxo.ExigirVeiculoAsync(repositorio, request.VeiculoId, false, cancellationToken);
        var foto = await FotoVeiculoFluxo.ExigirFotoAsync(
            repositorio,
            request.VeiculoId,
            request.FotoId,
            false,
            cancellationToken);
        var conteudo = await storage.AbrirLeituraAsync(foto.ChaveStorage, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("O conteúdo da foto não foi encontrado.");
        return new ConteudoVeiculoFoto(conteudo, foto.ContentType, foto.NomeOriginal);
    }
}

internal sealed class EnviarFotoVeiculoHandler(
    IUsuarioContexto usuarioContexto,
    IVeiculoFotosRepositorio repositorio,
    IArquivoStorage storage)
    : IRequestHandler<EnviarFotoVeiculoCommand, VeiculoFotoVisualizacao>
{
    public async Task<VeiculoFotoVisualizacao> Handle(
        EnviarFotoVeiculoCommand request,
        CancellationToken cancellationToken)
    {
        await FotoVeiculoFluxo.ExigirVeiculoAsync(repositorio, request.VeiculoId, true, cancellationToken);
        var imagem = await ValidadorArquivoImagem.ValidarAsync(
            request.Conteudo,
            request.TamanhoBytes,
            cancellationToken);
        var fotoId = Guid.NewGuid();
        var chave = $"empresas/{usuarioContexto.EmpresaId:N}/veiculos/{request.VeiculoId:N}/fotos/{fotoId:N}.{imagem.Extensao}";
        var nomeOriginal = FotoVeiculoFluxo.NormalizarNomeOriginal(request.NomeOriginal);
        var primeiraFoto = !(await repositorio.ListarAsync(request.VeiculoId, cancellationToken)).Any();

        await storage.SalvarAsync(chave, imagem.Conteudo, cancellationToken);
        try
        {
            var foto = new VeiculoFoto(
                fotoId,
                usuarioContexto.EmpresaId,
                request.VeiculoId,
                chave,
                nomeOriginal,
                imagem.ContentType,
                request.TamanhoBytes,
                primeiraFoto,
                usuarioContexto.UsuarioId);
            repositorio.Adicionar(foto);
            await repositorio.SalvarAsync(cancellationToken);
            return FotoVeiculoFluxo.Mapear(foto);
        }
        catch (Exception persistenciaException)
        {
            try
            {
                // Após a gravação física, a compensação não deve ser cancelada junto com a requisição.
                await storage.ExcluirAsync(chave, CancellationToken.None);
            }
            catch (Exception limpezaException)
            {
                throw new AggregateException(
                    "Falha ao persistir a foto e ao remover o arquivo salvo.",
                    persistenciaException,
                    limpezaException);
            }

            throw;
        }
    }
}

internal sealed class DefinirFotoPrincipalVeiculoHandler(IVeiculoFotosRepositorio repositorio)
    : IRequestHandler<DefinirFotoPrincipalVeiculoCommand>
{
    public async Task Handle(
        DefinirFotoPrincipalVeiculoCommand request,
        CancellationToken cancellationToken)
    {
        await FotoVeiculoFluxo.ExigirVeiculoAsync(repositorio, request.VeiculoId, true, cancellationToken);
        var fotos = await repositorio.ListarParaAlteracaoAsync(request.VeiculoId, cancellationToken);
        var principal = fotos.SingleOrDefault(item => item.Id == request.FotoId)
            ?? throw new RecursoNaoEncontradoException("Foto não encontrada.");
        foreach (var foto in fotos)
        {
            foto.DefinirComoPrincipal(foto.Id == principal.Id);
        }

        await repositorio.SalvarAsync(cancellationToken);
    }
}

internal sealed class ExcluirFotoVeiculoHandler(
    IVeiculoFotosRepositorio repositorio,
    IArquivoStorage storage)
    : IRequestHandler<ExcluirFotoVeiculoCommand>
{
    public async Task Handle(
        ExcluirFotoVeiculoCommand request,
        CancellationToken cancellationToken)
    {
        await FotoVeiculoFluxo.ExigirVeiculoAsync(repositorio, request.VeiculoId, true, cancellationToken);
        var fotos = await repositorio.ListarParaAlteracaoAsync(request.VeiculoId, cancellationToken);
        var foto = fotos.SingleOrDefault(item => item.Id == request.FotoId)
            ?? throw new RecursoNaoEncontradoException("Foto não encontrada.");

        if (foto.EhPrincipal)
        {
            var substituta = fotos
                .Where(item => item.Id != foto.Id)
                .OrderBy(item => item.CriadoEmUtc)
                .ThenBy(item => item.Id)
                .FirstOrDefault();
            substituta?.DefinirComoPrincipal(true);
        }

        repositorio.Remover(foto);
        await repositorio.SalvarAsync(cancellationToken);
        // O commit dos metadados já ocorreu; conclua a compensação física mesmo se o cliente desconectar.
        if (!await storage.ExcluirAsync(foto.ChaveStorage, CancellationToken.None))
        {
            throw new IOException("Os metadados foram removidos, mas o arquivo físico não foi encontrado para exclusão.");
        }
    }
}

internal static class FotoVeiculoFluxo
{
    public static async Task ExigirVeiculoAsync(
        IVeiculoFotosRepositorio repositorio,
        Guid veiculoId,
        bool exigirAtivo,
        CancellationToken cancellationToken)
    {
        var veiculo = await repositorio.ObterVeiculoAsync(veiculoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Veículo não encontrado.");
        if (exigirAtivo && !veiculo.EhAtivo)
        {
            throw new ConflitoRegraNegocioException(
                "A galeria de um veículo inativo pode ser visualizada, mas não alterada.");
        }
    }

    public static async Task<VeiculoFoto> ExigirFotoAsync(
        IVeiculoFotosRepositorio repositorio,
        Guid veiculoId,
        Guid fotoId,
        bool paraAlteracao,
        CancellationToken cancellationToken) =>
        await repositorio.ObterAsync(veiculoId, fotoId, paraAlteracao, cancellationToken)
        ?? throw new RecursoNaoEncontradoException("Foto não encontrada.");

    public static VeiculoFotoVisualizacao Mapear(VeiculoFoto foto) =>
        new(
            foto.Id,
            foto.VeiculoId,
            foto.NomeOriginal,
            foto.ContentType,
            foto.TamanhoBytes,
            foto.EhPrincipal,
            foto.CriadoEmUtc);

    public static string NormalizarNomeOriginal(string nome)
    {
        var segmento = nome.Replace('\\', '/').Split('/').LastOrDefault()?.Trim();
        var seguro = new string((segmento ?? string.Empty)
            .Where(caractere => !char.IsControl(caractere))
            .ToArray());
        if (string.IsNullOrWhiteSpace(seguro))
        {
            return "foto";
        }

        return seguro.Length <= 255 ? seguro : seguro[..255];
    }
}
