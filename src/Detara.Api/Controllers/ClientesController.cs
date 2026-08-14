using Detara.Application.Clientes;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Clientes;
using Detara.Contracts.Comum;
using Detara.Domain.Entidades;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[ApiController]
[Route("api/clientes")]
public sealed class ClientesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissoes.ClientesVisualizar)]
    public async Task<ActionResult<RespostaApi<PaginaResponse<ClienteListaResponse>>>> Listar(
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 25,
        [FromQuery] string? pesquisa = null,
        [FromQuery] bool? ehAtivo = null,
        [FromQuery] string? tipoPessoa = null,
        [FromQuery] string ordenacao = "nome",
        CancellationToken cancellationToken = default)
    {
        var resultado = await sender.Send(
            new ListarClientesQuery(new FiltroClientes(
                pagina,
                tamanhoPagina,
                pesquisa,
                ehAtivo,
                ConverterTipoPessoa(tipoPessoa),
                ordenacao.ToLowerInvariant())),
            cancellationToken);
        return Ok(RespostaApi<PaginaResponse<ClienteListaResponse>>.Ok(new PaginaResponse<ClienteListaResponse>(
            resultado.Itens.Select(MapearLista).ToArray(),
            resultado.Pagina,
            resultado.TamanhoPagina,
            resultado.TotalItens,
            resultado.TotalPaginas)));
    }

    [HttpGet("busca")]
    [Authorize(Policy = Permissoes.ClientesVisualizar)]
    public async Task<ActionResult<RespostaApi<IReadOnlyCollection<ClienteBuscaResponse>>>> Buscar(
        [FromQuery] string pesquisa,
        [FromQuery] int limite = 15,
        CancellationToken cancellationToken = default)
    {
        var resultado = await sender.Send(new BuscarClientesQuery(pesquisa, limite), cancellationToken);
        return Ok(RespostaApi<IReadOnlyCollection<ClienteBuscaResponse>>.Ok(
            resultado.Select(item => new ClienteBuscaResponse(
                item.Id,
                item.Nome,
                item.Telefone,
                item.CpfCnpj)).ToArray()));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissoes.ClientesVisualizar)]
    public async Task<ActionResult<RespostaApi<ClienteDetalheResponse>>> Obter(
        Guid id,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(new ObterClienteQuery(id), cancellationToken);
        return Ok(RespostaApi<ClienteDetalheResponse>.Ok(MapearDetalhe(resultado)));
    }

    [HttpPost]
    [Authorize(Policy = Permissoes.ClientesCriar)]
    public async Task<ActionResult<RespostaApi<ClienteDetalheResponse>>> Criar(
        SalvarClienteRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(new CriarClienteCommand(
            request.Nome,
            request.TipoPessoa,
            request.CpfCnpj,
            request.Telefone,
            request.WhatsApp,
            request.Email,
            request.DataNascimento,
            request.Observacao), cancellationToken);
        var response = RespostaApi<ClienteDetalheResponse>.Ok(
            MapearDetalhe(resultado),
            "Cliente cadastrado com sucesso.");
        return CreatedAtAction(nameof(Obter), new { id = resultado.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissoes.ClientesEditar)]
    public async Task<ActionResult<RespostaApi<ClienteDetalheResponse>>> Atualizar(
        Guid id,
        SalvarClienteRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(new AtualizarClienteCommand(
            id,
            request.Nome,
            request.TipoPessoa,
            request.CpfCnpj,
            request.Telefone,
            request.WhatsApp,
            request.Email,
            request.DataNascimento,
            request.Observacao), cancellationToken);
        return Ok(RespostaApi<ClienteDetalheResponse>.Ok(
            MapearDetalhe(resultado),
            "Cliente atualizado com sucesso."));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = Permissoes.ClientesEditar)]
    public async Task<IActionResult> AlterarStatus(
        Guid id,
        AlterarStatusRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(new AlterarStatusClienteCommand(id, request.EhAtivo), cancellationToken);
        return NoContent();
    }

    private static TipoPessoa? ConverterTipoPessoa(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        return Enum.TryParse<TipoPessoa>(valor, true, out var tipo) && Enum.IsDefined(tipo)
            ? tipo
            : throw new ArgumentException("O filtro de tipo de pessoa é inválido.", nameof(valor));
    }

    private static ClienteListaResponse MapearLista(ClienteListaItemResultado item) =>
        new(
            item.Id,
            item.Nome,
            item.TipoPessoa.ToString(),
            item.CpfCnpj,
            item.Telefone,
            item.QuantidadeVeiculos,
            item.EhAtivo);

    private static ClienteDetalheResponse MapearDetalhe(ClienteDetalheResultado item) =>
        new(
            item.Id,
            item.Nome,
            item.TipoPessoa.ToString(),
            item.CpfCnpj,
            item.Telefone,
            item.WhatsApp,
            item.Email,
            item.DataNascimento,
            item.Observacao,
            item.CriadoEmUtc,
            item.AtualizadoEmUtc,
            item.EhAtivo,
            item.Veiculos.Select(veiculo => new VeiculoResumoClienteResponse(
                veiculo.Id,
                veiculo.Descricao,
                veiculo.Placa,
                veiculo.AnoModelo,
                veiculo.Cor,
                veiculo.Quilometragem,
                veiculo.EhAtivo)).ToArray());
}
