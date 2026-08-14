using Detara.Application.Abstracoes;
using Detara.Application.Autenticacao;
using Detara.Contracts.Comum;
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
            logger.LogError(exception, "Erro inesperado ao processar {Metodo} {Caminho}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("Requisição rejeitada: {Codigo} em {Metodo} {Caminho}",
                codigo,
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
            ViolacaoIsolamentoTenantException => (
                StatusCodes.Status403Forbidden,
                "Você não tem permissão para realizar esta operação.",
                "acesso_negado",
                null),
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "O registro foi alterado ou não pertence ao contexto atual.",
                "conflito_concorrencia",
                null),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Ocorreu um erro inesperado.",
                "erro_interno",
                null)
        };
}
