using Detara.Api.Autenticacao;
using Detara.Application.AdministracaoTenant;
using Detara.Contracts.AdministracaoTenant;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Comum;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[ApiController]
[Route("api/usuarios")]
[Authorize(AuthenticationSchemes = EsquemasAutenticacao.Tenant, Policy = Permissoes.AdministracaoUsuario)]
public sealed class UsuariosTenantController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RespostaApi<PaginaResponse<UsuarioTenantListaResponse>>>> Listar(
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 25,
        [FromQuery] string? pesquisa = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var resultado = await sender.Send(
            new ListarUsuariosTenantQuery(pagina, tamanhoPagina, pesquisa, status),
            cancellationToken);
        return Ok(RespostaApi<PaginaResponse<UsuarioTenantListaResponse>>.Ok(new(
            resultado.Itens.Select(MapearLista).ToArray(),
            resultado.Pagina,
            resultado.TamanhoPagina,
            resultado.TotalItens,
            resultado.TotalPaginas)));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RespostaApi<UsuarioTenantDetalheResponse>>> Obter(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(RespostaApi<UsuarioTenantDetalheResponse>.Ok(MapearDetalhe(
            await sender.Send(new ObterUsuarioTenantQuery(id), cancellationToken))));

    [HttpPost]
    public async Task<ActionResult<RespostaApi<UsuarioTenantDetalheResponse>>> Convidar(
        ConvidarUsuarioTenantRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(
            new ConvidarUsuarioTenantCommand(request.Nome, request.Email, request.PerfilId),
            cancellationToken);
        return CreatedAtAction(nameof(Obter), new { id = resultado.Id },
            RespostaApi<UsuarioTenantDetalheResponse>.Ok(
                MapearDetalhe(resultado),
                "Usuário criado. O convite será enviado fora da transação."));
    }

    [HttpPut("{id:guid}/perfil")]
    public async Task<ActionResult<RespostaApi<UsuarioTenantDetalheResponse>>> AlterarPerfil(
        Guid id,
        AlterarPerfilUsuarioTenantRequest request,
        CancellationToken cancellationToken) =>
        Ok(RespostaApi<UsuarioTenantDetalheResponse>.Ok(MapearDetalhe(
            await sender.Send(
                new AlterarPerfilUsuarioTenantCommand(id, request.PerfilId, request.Versao),
                cancellationToken)), "Perfil atualizado."));

    [HttpPost("{id:guid}/inativar")]
    public Task<ActionResult<RespostaApi<UsuarioTenantDetalheResponse>>> Inativar(
        Guid id,
        AlterarStatusUsuarioTenantRequest request,
        CancellationToken cancellationToken) =>
        AlterarStatus(id, false, request.Versao, cancellationToken);

    [HttpPost("{id:guid}/reativar")]
    public Task<ActionResult<RespostaApi<UsuarioTenantDetalheResponse>>> Reativar(
        Guid id,
        AlterarStatusUsuarioTenantRequest request,
        CancellationToken cancellationToken) =>
        AlterarStatus(id, true, request.Versao, cancellationToken);

    [HttpPost("{id:guid}/convite/reenviar")]
    public async Task<ActionResult<RespostaApi<UsuarioTenantDetalheResponse>>> ReenviarConvite(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(RespostaApi<UsuarioTenantDetalheResponse>.Ok(MapearDetalhe(
            await sender.Send(new ReenviarConviteUsuarioTenantCommand(id), cancellationToken)),
            "Convite preparado para reenvio."));

    private async Task<ActionResult<RespostaApi<UsuarioTenantDetalheResponse>>> AlterarStatus(
        Guid id,
        bool ativar,
        long versao,
        CancellationToken cancellationToken) =>
        Ok(RespostaApi<UsuarioTenantDetalheResponse>.Ok(MapearDetalhe(
            await sender.Send(new AlterarStatusUsuarioTenantCommand(id, ativar, versao), cancellationToken)),
            ativar ? "Usuário reativado." : "Usuário inativado."));

    private static UsuarioTenantListaResponse MapearLista(UsuarioTenantResultado resultado) => new(
        resultado.Id, resultado.Nome, resultado.Email, resultado.PerfilId,
        resultado.PerfilNome, resultado.Status, resultado.ConviteExpiraEmUtc,
        resultado.PodeReenviarConvite, resultado.EhUsuarioAtual, resultado.Versao);

    private static UsuarioTenantDetalheResponse MapearDetalhe(UsuarioTenantResultado resultado) => new(
        resultado.Id, resultado.Nome, resultado.Email, resultado.PerfilId,
        resultado.PerfilNome, resultado.Status, resultado.ConviteExpiraEmUtc,
        resultado.PodeReenviarConvite, resultado.EhUsuarioAtual, resultado.Versao);
}
