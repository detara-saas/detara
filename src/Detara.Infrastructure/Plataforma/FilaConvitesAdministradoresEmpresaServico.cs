using System.Net;
using System.Security.Cryptography;
using System.Text;
using Detara.Application.Comunicacao;
using Detara.Application.Plataforma;
using Detara.Domain.Plataforma;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Detara.Infrastructure.Plataforma;

internal sealed class FilaConvitesAdministradoresEmpresaServico(
    IServiceScopeFactory scopeFactory,
    IOptions<PlataformaOptions> options,
    IOptions<WebPublicaOptions> webOptions,
    ILogger<FilaConvitesAdministradoresEmpresaServico> logger)
    : IFilaConvitesAdministradoresEmpresaServico
{
    public async Task<int> ProcessarLoteAsync(CancellationToken cancellationToken)
    {
        var config = options.Value;
        var lote = Math.Clamp(config.ConvitesTamanhoLote, 1, 50);
        Guid[] candidatos;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DetaraDbContext>();
            var agora = DateTime.UtcNow;
            candidatos = await db.ConvitesAdministradoresEmpresa.AsNoTracking()
                .Where(x =>
                    (x.Status == StatusConviteAdministradorEmpresa.Pendente &&
                        x.ProximaTentativaEnvioEmUtc <= agora) ||
                    (x.Status == StatusConviteAdministradorEmpresa.Processando &&
                        x.ProcessamentoIniciadoEmUtc <= agora.AddMinutes(-10)))
                .OrderBy(x => x.ProximaTentativaEnvioEmUtc)
                .ThenBy(x => x.CriadoEmUtc)
                .Take(lote)
                .Select(x => x.Id)
                .ToArrayAsync(cancellationToken);
        }

        var processados = 0;
        foreach (var conviteId in candidatos)
        {
            if (await ProcessarAsync(conviteId, cancellationToken)) processados++;
        }

        return processados;
    }

    private async Task<bool> ProcessarAsync(Guid conviteId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DetaraDbContext>();
        var provedor = scope.ServiceProvider.GetRequiredService<IProvedorEmail>();
        var convite = await db.ConvitesAdministradoresEmpresa
            .SingleOrDefaultAsync(x => x.Id == conviteId, cancellationToken);
        if (convite is null)
        {
            return false;
        }

        var agora = DateTime.UtcNow;
        if (convite.Status == StatusConviteAdministradorEmpresa.Processando &&
            convite.ProcessamentoIniciadoEmUtc <= agora.AddMinutes(-10))
        {
            convite.RegistrarFalha(
                "Processamento anterior interrompido; nova tentativa agendada.",
                agora,
                agora);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (convite.Status != StatusConviteAdministradorEmpresa.Pendente)
        {
            return false;
        }

        var empresa = await db.Empresas.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == convite.EmpresaId, cancellationToken);
        var usuario = await db.Usuarios.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.EmpresaId == convite.EmpresaId && x.Id == convite.UsuarioId,
                cancellationToken);
        if (empresa is null || usuario is null || usuario.EhAtivo)
        {
            return false;
        }

        var token = GerarToken();
        var hash = ConvitesAdministradoresEmpresaServico.HashToken(token);
        var horas = Math.Clamp(options.Value.ConviteExpiracaoHoras, 24, 168);
        try
        {
            convite.IniciarEnvio(hash, agora.AddHours(horas), agora);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }

        var baseUrl = webOptions.Value.PublicBaseUrl.TrimEnd('/');
        var link = $"{baseUrl}/ativar-conta#token={Uri.EscapeDataString(token)}";
        var empresaNome = WebUtility.HtmlEncode(empresa.NomeFantasia);
        var usuarioNome = WebUtility.HtmlEncode(usuario.Nome);
        var linkSeguro = WebUtility.HtmlEncode(link);
        var corpo = $$"""
            <!doctype html>
            <html lang="pt-BR"><body style="font-family:Arial,sans-serif;color:#111827;line-height:1.6">
            <div style="max-width:620px;margin:auto;padding:32px">
              <p style="color:#00a67e;font-weight:700;letter-spacing:.08em">DETARA</p>
              <h1 style="font-size:24px">Ative seu acesso à Detara</h1>
              <p>Olá, {{usuarioNome}}.</p>
              <p>Você recebeu acesso à empresa <strong>{{empresaNome}}</strong> na Detara.</p>
              <p>Defina sua própria senha e ative o acesso pelo botão abaixo.</p>
              <p style="margin:28px 0"><a href="{{linkSeguro}}" style="background:#00a67e;color:#fff;padding:12px 20px;text-decoration:none;border-radius:8px">Ativar minha conta</a></p>
              <p>Este convite expira em {{horas}} horas. Se você não esperava este convite, ignore esta mensagem.</p>
              <p>Equipe Detara</p>
            </div></body></html>
            """;
        var tentativa = convite.QuantidadeTentativasEnvio + 1;
        var assunto = convite.Origem == OrigemConviteAcessoEmpresa.UsuarioTenant
            ? "Você recebeu acesso à Detara"
            : "Você foi convidado para administrar sua empresa na Detara";
        ResultadoEnvioEmail resultado;
        try
        {
            resultado = await provedor.EnviarAsync(new MensagemEmailProvedor(
                convite.EmailDestinoSnapshot,
                assunto,
                corpo,
                null,
                $"convite-administrador/{convite.Id:N}/{tentativa}"), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Provedor recusou o envio do convite {ConviteId}.", convite.Id);
            resultado = new ResultadoEnvioEmail(
                false,
                true,
                null,
                "Falha temporária ao contatar o provedor de e-mail.");
        }
        var concluidoEm = DateTime.UtcNow;
        if (resultado.Sucesso)
        {
            convite.RegistrarEnvio(resultado.MensagemId ?? "aceita-sem-id", concluidoEm);
        }
        else
        {
            var maximo = Math.Clamp(options.Value.ConvitesMaximoTentativas, 1, 10);
            DateTime? proxima = resultado.FalhaTemporaria && tentativa < maximo
                ? CalcularProximaTentativa(concluidoEm, tentativa)
                : null;
            convite.RegistrarFalha(
                resultado.ErroSeguro ?? "Falha no provedor de e-mail.",
                concluidoEm,
                proxima);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(exception, "Concorrência ao concluir convite {ConviteId}.", conviteId);
            return false;
        }
    }

    private static DateTime CalcularProximaTentativa(DateTime agoraUtc, int tentativa) => tentativa switch
    {
        <= 1 => agoraUtc.AddMinutes(1),
        2 => agoraUtc.AddMinutes(5),
        _ => agoraUtc.AddMinutes(30)
    };

    private static string GerarToken()
    {
        var base64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

public sealed class ConvitesAdministradoresEmpresaWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<PlataformaOptions> options,
    IHostEnvironment environment,
    ILogger<ConvitesAdministradoresEmpresaWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }

        var intervalo = TimeSpan.FromSeconds(Math.Clamp(
            options.Value.ConvitesIntervaloSegundos,
            5,
            300));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider
                    .GetRequiredService<IFilaConvitesAdministradoresEmpresaServico>()
                    .ProcessarLoteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha ao processar a fila de convites administrativos.");
            }

            await Task.Delay(intervalo, stoppingToken);
        }
    }
}
