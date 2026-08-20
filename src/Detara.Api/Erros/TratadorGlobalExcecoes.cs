using Detara.Application.Abstracoes;
using Detara.Application.Autenticacao;
using Detara.Contracts.Comum;
using Detara.Application.Plataforma;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Detara.Api.Erros;

internal sealed class TratadorGlobalExcecoes(ILogger<TratadorGlobalExcecoes> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, info, codigo, detalhes) = Mapear(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Erro inesperado {TraceId} ao processar {Metodo} {Caminho}",
                httpContext.TraceIdentifier,
                httpContext.Request.Method,
                httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("Requisição rejeitada: {Codigo}; trace {TraceId}; {Metodo} {Caminho}",
                codigo,
                httpContext.TraceIdentifier,
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            RespostaApi<object>.Falha(info, codigo, detalhes),
            cancellationToken);
        return true;
    }

    private static (int Status, string Info, string Codigo, IReadOnlyDictionary<string, string[]>? Detalhes)
        Mapear(Exception exception) => exception switch
        {
            ValidationException validationException => (
                StatusCodes.Status400BadRequest,
                "Verifique os dados informados.",
                "validacao",
                validationException.Errors
                    .GroupBy(erro => erro.PropertyName)
                    .ToDictionary(
                        grupo => grupo.Key,
                        grupo => grupo.Select(erro => erro.ErrorMessage).Distinct().ToArray())),
            ArgumentException => (
                StatusCodes.Status400BadRequest,
                exception.Message,
                "regra_invalida",
                null),
            CredenciaisInvalidasException => (
                StatusCodes.Status401Unauthorized,
                exception.Message,
                "credenciais_invalidas",
                null),
            CredenciaisPlataformaInvalidasException => (
                StatusCodes.Status401Unauthorized,
                exception.Message,
                "credenciais_invalidas",
                null),
            CodigoMfaInvalidoException => (
                StatusCodes.Status401Unauthorized,
                exception.Message,
                "codigo_mfa_invalido",
                null),
            ConviteAdministradorInvalidoException => (
                StatusCodes.Status400BadRequest,
                exception.Message,
                "convite_invalido",
                null),
            ViolacaoIsolamentoTenantException => (
                StatusCodes.Status403Forbidden,
                "Você não tem permissão para realizar esta operação.",
                "acesso_negado",
                null),
            RecursoNaoEncontradoException => (
                StatusCodes.Status404NotFound,
                exception.Message,
                "nao_encontrado",
                null),
            ConflitoRegraNegocioException => (
                StatusCodes.Status409Conflict,
                exception.Message,
                "conflito_regra",
                null),
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "O registro foi alterado ou não pertence ao contexto atual.",
                "conflito_concorrencia",
                null),
            DbUpdateException => (
                StatusCodes.Status409Conflict,
                "Os dados informados conflitam com um registro existente.",
                "conflito_dados",
                null),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Ocorreu um erro inesperado.",
                "erro_interno",
                null)
        };
}
