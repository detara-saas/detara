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
[Route("api/perfis")]
[Authorize(AuthenticationSchemes = EsquemasAutenticacao.Tenant, Policy = Detara.Contracts.Autorizacao.Permissoes.AdministracaoUsuario)]
public sealed class PerfisTenantController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RespostaApi<IReadOnlyCollection<PerfilTenantResumoResponse>>>> Listar(
        CancellationToken cancellationToken) =>
        Ok(RespostaApi<IReadOnlyCollection<PerfilTenantResumoResponse>>.Ok(
            (await sender.Send(new ListarPerfisTenantQuery(), cancellationToken))
                .Select(MapearResumo).ToArray()));

    [HttpGet("permissoes")]
    public async Task<ActionResult<RespostaApi<IReadOnlyCollection<PermissaoTenantResponse>>>> ListarPermissoes(
        CancellationToken cancellationToken) =>
        Ok(RespostaApi<IReadOnlyCollection<PermissaoTenantResponse>>.Ok(
            (await sender.Send(new ListarPermissoesTenantQuery(), cancellationToken))
                .Select(x => new PermissaoTenantResponse(x.Codigo, x.Descricao, x.Grupo, x.PodeConceder))
                .ToArray()));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RespostaApi<PerfilTenantDetalheResponse>>> Obter(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(RespostaApi<PerfilTenantDetalheResponse>.Ok(MapearDetalhe(
            await sender.Send(new ObterPerfilTenantQuery(id), cancellationToken))));

    [HttpPost]
    public async Task<ActionResult<RespostaApi<PerfilTenantDetalheResponse>>> Criar(
        SalvarPerfilTenantRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(
            new CriarPerfilTenantCommand(request.Nome, request.Descricao, request.Permissoes),
            cancellationToken);
        return CreatedAtAction(nameof(Obter), new { id = resultado.Id },
            RespostaApi<PerfilTenantDetalheResponse>.Ok(
                MapearDetalhe(resultado), "Perfil criado."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RespostaApi<PerfilTenantDetalheResponse>>> Atualizar(
        Guid id,
        SalvarPerfilTenantRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(new AtualizarPerfilTenantCommand(
            id, request.Nome, request.Descricao, request.Permissoes, request.Versao ?? 0),
            cancellationToken);
        return Ok(RespostaApi<PerfilTenantDetalheResponse>.Ok(
            MapearDetalhe(resultado), "Perfil atualizado."));
    }

    [HttpPost("{id:guid}/inativar")]
    public Task<ActionResult<RespostaApi<PerfilTenantDetalheResponse>>> Inativar(
        Guid id,
        AlterarStatusPerfilTenantRequest request,
        CancellationToken cancellationToken) =>
        AlterarStatus(id, false, request.Versao, cancellationToken);

    [HttpPost("{id:guid}/reativar")]
    public Task<ActionResult<RespostaApi<PerfilTenantDetalheResponse>>> Reativar(
        Guid id,
        AlterarStatusPerfilTenantRequest request,
        CancellationToken cancellationToken) =>
        AlterarStatus(id, true, request.Versao, cancellationToken);

    private async Task<ActionResult<RespostaApi<PerfilTenantDetalheResponse>>> AlterarStatus(
        Guid id,
        bool ativar,
        long versao,
        CancellationToken cancellationToken) =>
        Ok(RespostaApi<PerfilTenantDetalheResponse>.Ok(MapearDetalhe(
            await sender.Send(new AlterarStatusPerfilTenantCommand(id, ativar, versao), cancellationToken)),
            ativar ? "Perfil reativado." : "Perfil inativado."));

    private static PerfilTenantResumoResponse MapearResumo(PerfilTenantResumoResultado resultado) => new(
        resultado.Id, resultado.Nome, resultado.Descricao, resultado.EhAtivo,
        resultado.EhSistema, resultado.QuantidadeUsuarios, resultado.QuantidadePermissoes,
        resultado.Versao);

    private static PerfilTenantDetalheResponse MapearDetalhe(PerfilTenantDetalheResultado resultado) => new(
        resultado.Id, resultado.Nome, resultado.Descricao, resultado.EhAtivo,
        resultado.EhSistema, resultado.QuantidadeUsuarios, resultado.Permissoes,
        resultado.Versao);
}
