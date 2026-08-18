using Detara.Application.Veiculos;
using Detara.Application.Clientes;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Clientes;
using Detara.Contracts.Comum;
using Detara.Contracts.Veiculos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[ApiController]
[Route("api/veiculos")]
public sealed class VeiculosController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissoes.VeiculosVisualizar)]
    public async Task<ActionResult<RespostaApi<PaginaResponse<VeiculoListaResponse>>>> Listar(
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 25,
        [FromQuery] string? pesquisa = null,
        [FromQuery] bool? ehAtivo = null,
        [FromQuery] string ordenacao = "veiculo",
        CancellationToken cancellationToken = default)
    {
        var resultado = await sender.Send(
            new ListarVeiculosQuery(new FiltroVeiculos(
                pagina,
                tamanhoPagina,
                pesquisa,
                ehAtivo,
                ordenacao.ToLowerInvariant())),
            cancellationToken);
        return Ok(RespostaApi<PaginaResponse<VeiculoListaResponse>>.Ok(new PaginaResponse<VeiculoListaResponse>(
            resultado.Itens.Select(MapearLista).ToArray(),
            resultado.Pagina,
            resultado.TamanhoPagina,
            resultado.TotalItens,
            resultado.TotalPaginas)));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissoes.VeiculosVisualizar)]
    public async Task<ActionResult<RespostaApi<VeiculoDetalheResponse>>> Obter(
        Guid id,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(new ObterVeiculoQuery(id), cancellationToken);
        return Ok(RespostaApi<VeiculoDetalheResponse>.Ok(MapearDetalhe(resultado)));
    }

    [HttpPost]
    [Authorize(Policy = Permissoes.VeiculosCriar)]
    public async Task<ActionResult<RespostaApi<VeiculoDetalheResponse>>> Criar(
        SalvarVeiculoRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(new CriarVeiculoCommand(
            request.ClienteId,
            request.Placa,
            request.Marca,
            request.Modelo,
            request.Versao,
            request.AnoFabricacao,
            request.AnoModelo,
            request.Cor,
            request.Quilometragem,
            request.Observacao), cancellationToken);
        var response = RespostaApi<VeiculoDetalheResponse>.Ok(
            MapearDetalhe(resultado),
            "Veículo cadastrado com sucesso.");
        return CreatedAtAction(nameof(Obter), new { id = resultado.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissoes.VeiculosEditar)]
    public async Task<ActionResult<RespostaApi<VeiculoDetalheResponse>>> Atualizar(
        Guid id,
        SalvarVeiculoRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(new AtualizarVeiculoCommand(
            id,
            request.ClienteId,
            request.Placa,
            request.Marca,
            request.Modelo,
            request.Versao,
            request.AnoFabricacao,
            request.AnoModelo,
            request.Cor,
            request.Quilometragem,
            request.Observacao), cancellationToken);
        return Ok(RespostaApi<VeiculoDetalheResponse>.Ok(
            MapearDetalhe(resultado),
            "Veículo atualizado com sucesso."));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = Permissoes.VeiculosEditar)]
    public async Task<IActionResult> AlterarStatus(
        Guid id,
        AlterarStatusRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(new AlterarStatusVeiculoCommand(id, request.EhAtivo), cancellationToken);
        return NoContent();
    }

    [HttpGet("{veiculoId:guid}/fotos")]
    [Authorize(Policy = Permissoes.VeiculosVisualizar)]
    public async Task<ActionResult<RespostaApi<IReadOnlyCollection<VeiculoFotoResponse>>>> ListarFotos(
        Guid veiculoId,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(new ListarFotosVeiculoQuery(veiculoId), cancellationToken);
        return Ok(RespostaApi<IReadOnlyCollection<VeiculoFotoResponse>>.Ok(
            resultado.Select(MapearFoto).ToArray()));
    }

    [HttpPost("{veiculoId:guid}/fotos")]
    [Authorize(Policy = Permissoes.VeiculosEditar)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PoliticaImagemVeiculo.TamanhoMaximoBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = PoliticaImagemVeiculo.TamanhoMaximoBytes + 1024 * 1024)]
    public async Task<ActionResult<RespostaApi<VeiculoFotoResponse>>> EnviarFoto(
        Guid veiculoId,
        IFormFile arquivo,
        CancellationToken cancellationToken)
    {
        await using var conteudo = arquivo.OpenReadStream();
        var resultado = await sender.Send(
            new EnviarFotoVeiculoCommand(
                veiculoId,
                arquivo.FileName,
                arquivo.Length,
                conteudo),
            cancellationToken);
        return CreatedAtAction(
            nameof(ObterConteudoFoto),
            new { veiculoId, fotoId = resultado.Id },
            RespostaApi<VeiculoFotoResponse>.Ok(
                MapearFoto(resultado),
                "Foto adicionada ao veículo."));
    }

    [HttpGet("{veiculoId:guid}/fotos/{fotoId:guid}/conteudo")]
    [Authorize(Policy = Permissoes.VeiculosVisualizar)]
    public async Task<IActionResult> ObterConteudoFoto(
        Guid veiculoId,
        Guid fotoId,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(
            new ObterConteudoVeiculoFotoQuery(veiculoId, fotoId),
            cancellationToken);
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.ContentDisposition = $"inline; filename=\"{Uri.EscapeDataString(resultado.NomeOriginal)}\"";
        return File(resultado.Conteudo, resultado.ContentType, enableRangeProcessing: true);
    }

    [HttpPatch("{veiculoId:guid}/fotos/{fotoId:guid}/principal")]
    [Authorize(Policy = Permissoes.VeiculosEditar)]
    public async Task<IActionResult> DefinirFotoPrincipal(
        Guid veiculoId,
        Guid fotoId,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new DefinirFotoPrincipalVeiculoCommand(veiculoId, fotoId),
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("{veiculoId:guid}/fotos/{fotoId:guid}")]
    [Authorize(Policy = Permissoes.VeiculosEditar)]
    public async Task<IActionResult> ExcluirFoto(
        Guid veiculoId,
        Guid fotoId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new ExcluirFotoVeiculoCommand(veiculoId, fotoId), cancellationToken);
        return NoContent();
    }

    private static VeiculoListaResponse MapearLista(VeiculoListaItemResultado item) =>
        new(
            item.Id,
            item.Descricao,
            item.Placa,
            item.ClienteId,
            item.ClienteNome,
            item.AnoModelo,
            item.Cor,
            item.Quilometragem,
            item.EhAtivo);

    private static VeiculoDetalheResponse MapearDetalhe(VeiculoDetalheResultado item) =>
        new(
            item.Id,
            item.ClienteId,
            item.ClienteNome,
            item.Placa,
            item.Marca,
            item.Modelo,
            item.Versao,
            item.AnoFabricacao,
            item.AnoModelo,
            item.Cor,
            item.Quilometragem,
            item.Observacao,
            item.CriadoEmUtc,
            item.AtualizadoEmUtc,
            item.EhAtivo);

    private static VeiculoFotoResponse MapearFoto(VeiculoFotoVisualizacao item) =>
        new(
            item.Id,
            item.VeiculoId,
            item.NomeOriginal,
            item.ContentType,
            item.TamanhoBytes,
            item.EhPrincipal,
            item.CriadoEmUtc);
}
