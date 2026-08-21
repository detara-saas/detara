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
[Route("api/empresa")]
[Authorize(AuthenticationSchemes = EsquemasAutenticacao.Tenant)]
public sealed class EmpresaTenantController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissoes.ConfiguracoesVisualizar)]
    public async Task<ActionResult<RespostaApi<EmpresaTenantResponse>>> Obter(
        CancellationToken cancellationToken) =>
        Ok(RespostaApi<EmpresaTenantResponse>.Ok(Mapear(
            await sender.Send(new ObterEmpresaTenantQuery(), cancellationToken))));

    [HttpPut]
    [Authorize(Policy = Permissoes.ConfiguracoesEditar)]
    public async Task<ActionResult<RespostaApi<EmpresaTenantResponse>>> Atualizar(
        AtualizarEmpresaTenantRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(new AtualizarEmpresaTenantCommand(
            request.NomeFantasia,
            request.RazaoSocial,
            request.CpfCnpj,
            request.Email,
            request.Telefone,
            request.FusoHorario,
            request.Versao), cancellationToken);
        return Ok(RespostaApi<EmpresaTenantResponse>.Ok(
            Mapear(resultado),
            "Dados da empresa atualizados."));
    }

    private static EmpresaTenantResponse Mapear(EmpresaTenantResultado resultado) => new(
        resultado.NomeFantasia,
        resultado.RazaoSocial,
        resultado.CpfCnpj,
        resultado.Email,
        resultado.Telefone,
        resultado.Slug,
        resultado.FusoHorario,
        resultado.EhAtiva,
        resultado.CriadoEmUtc,
        resultado.Versao);
}
