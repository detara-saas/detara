using Detara.Application.Abstracoes;
using Detara.Application.Assinaturas;
using Detara.Application.Plataforma;
using Detara.Domain.Assinaturas;
using Detara.Domain.Plataforma;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Assinaturas;

internal sealed class AssinaturasPlataformaServico(
    DetaraDbContext db,
    DbContextOptions<DetaraDbContext> dbOptions,
    IArquivoStorage storage,
    TimeProvider relogio) : IAssinaturasPlataformaServico
{
    public async Task<IReadOnlyCollection<AssinaturaPlataformaResultado>> ListarAsync(CancellationToken cancellationToken)
    {
        var empresas = await db.Empresas.AsNoTracking().OrderBy(x => x.NomeFantasia).ToArrayAsync(cancellationToken);
        var resultado = new List<AssinaturaPlataformaResultado>(empresas.Length);
        foreach (var empresa in empresas)
            resultado.Add(await ObterAsync(empresa.Id, cancellationToken));
        return resultado;
    }

    public async Task<AssinaturaPlataformaResultado> ObterAsync(Guid empresaId, CancellationToken cancellationToken)
    {
        var empresa = await db.Empresas.AsNoTracking().SingleOrDefaultAsync(x => x.Id == empresaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Empresa não encontrada.");
        var assinatura = await db.AssinaturasEmpresas.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(x => x.EmpresaId == empresaId, cancellationToken);
        if (assinatura is null) return new(empresaId, empresa.NomeFantasia, AssinaturasTenantServico.Vazia(), []);
        var aceite = await db.AceitesTermosAssinaturas.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && x.AssinaturaId == assinatura.Id)
            .OrderByDescending(x => x.AceitoEmUtc).FirstOrDefaultAsync(cancellationToken);
        var historico = await db.HistoricosAssinaturasEmpresas.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && x.AssinaturaId == assinatura.Id)
            .OrderByDescending(x => x.OcorridoEmUtc)
            .Select(x => new HistoricoAssinaturaResultado(x.Id, x.TipoEvento.ToString(),
                x.StatusAnterior == null ? null : x.StatusAnterior.ToString(), x.StatusNovo.ToString(),
                x.OcorridoEmUtc, x.Motivo,
                x.AdministradorPlataformaId != null ? "Administrador Detara" : "Responsável da empresa",
                x.ReferenciaPagamento))
            .ToArrayAsync(cancellationToken);
        return new(empresaId, empresa.NomeFantasia, AssinaturasTenantServico.Mapear(assinatura, aceite), historico);
    }

    public async Task<AssinaturaPlataformaResultado> CriarAsync(Guid administradorId, Guid empresaId,
        CriarAssinaturaEntrada request, CancellationToken cancellationToken)
    {
        await ValidarAdministradorAsync(administradorId, cancellationToken);
        await using var contexto = CriarContexto(empresaId);
        if (!await contexto.Empresas.AnyAsync(x => x.Id == empresaId, cancellationToken))
            throw new RecursoNaoEncontradoException("Empresa não encontrada.");
        if (await contexto.AssinaturasEmpresas.AnyAsync(cancellationToken))
            throw new ConflitoRegraNegocioException("A empresa já possui assinatura comercial.");
        var assinatura = new AssinaturaEmpresa(empresaId, request.ValorMensal, request.DataInicio,
            request.DiaVencimento, request.AsaasCustomerId, request.AsaasSubscriptionId);
        var agora = relogio.GetUtcNow().UtcDateTime;
        contexto.AssinaturasEmpresas.Add(assinatura);
        contexto.HistoricosAssinaturasEmpresas.Add(new(empresaId, assinatura.Id,
            TipoEventoAssinatura.Criada, null, assinatura.Status, agora,
            "Assinatura comercial criada.", administradorPlataformaId: administradorId));
        AdicionarAuditoria(contexto, administradorId, empresaId, assinatura.Id, "AssinaturaCriada", "Assinatura comercial criada.");
        await contexto.SaveChangesAsync(cancellationToken);
        return await ObterAsync(empresaId, cancellationToken);
    }

    public Task<AssinaturaPlataformaResultado> AlterarCondicoesAsync(Guid administradorId, Guid empresaId,
        AlterarCondicoesAssinaturaEntrada request, CancellationToken cancellationToken) =>
        AlterarAsync(administradorId, empresaId, request.Versao, TipoEventoAssinatura.CondicoesAlteradas,
            request.Motivo, null, (assinatura, agora) => assinatura.AlterarCondicoes(
                request.ValorMensal, request.ProximoVencimento, request.DiaVencimento,
                request.AsaasCustomerId, request.AsaasSubscriptionId, request.Versao), cancellationToken);

    public async Task<AssinaturaPlataformaResultado> ConfirmarPagamentoAsync(Guid administradorId, Guid empresaId,
        ConfirmarPagamentoAssinaturaEntrada request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.ReferenciaPagamento) &&
            await db.HistoricosAssinaturasEmpresas.IgnoreQueryFilters().AsNoTracking().AnyAsync(x =>
                x.EmpresaId == empresaId && x.TipoEvento == TipoEventoAssinatura.PagamentoConfirmado &&
                x.ReferenciaPagamento == request.ReferenciaPagamento.Trim(), cancellationToken))
            return await ObterAsync(empresaId, cancellationToken);
        return await AlterarAsync(administradorId, empresaId, request.Versao,
            TipoEventoAssinatura.PagamentoConfirmado, request.Motivo, request.ReferenciaPagamento,
            (assinatura, agora) => assinatura.ConfirmarPagamento(agora, request.DataPagamento,
                request.AsaasCustomerId, request.AsaasSubscriptionId, request.Versao), cancellationToken);
    }

    public Task<AssinaturaPlataformaResultado> MarcarAtrasoAsync(Guid administradorId, Guid empresaId,
        AlterarStatusAssinaturaEntrada request, CancellationToken cancellationToken) =>
        AlterarAsync(administradorId, empresaId, request.Versao, TipoEventoAssinatura.AtrasoRegistrado,
            request.Motivo, null, (assinatura, _) => assinatura.MarcarEmAtraso(request.Versao), cancellationToken);

    public Task<AssinaturaPlataformaResultado> SuspenderAsync(Guid administradorId, Guid empresaId,
        AlterarStatusAssinaturaEntrada request, CancellationToken cancellationToken) =>
        AlterarAsync(administradorId, empresaId, request.Versao, TipoEventoAssinatura.Suspensa,
            request.Motivo, null, (assinatura, agora) => assinatura.Suspender(agora, request.Versao), cancellationToken);

    public Task<AssinaturaPlataformaResultado> ReativarAsync(Guid administradorId, Guid empresaId,
        AlterarStatusAssinaturaEntrada request, CancellationToken cancellationToken) =>
        AlterarAsync(administradorId, empresaId, request.Versao, TipoEventoAssinatura.Reativada,
            request.Motivo, null, (assinatura, agora) => assinatura.Reativar(agora, request.Versao), cancellationToken);

    public Task<AssinaturaPlataformaResultado> CancelarAsync(Guid administradorId, Guid empresaId,
        AlterarStatusAssinaturaEntrada request, CancellationToken cancellationToken) =>
        AlterarAsync(administradorId, empresaId, request.Versao, TipoEventoAssinatura.Cancelada,
            request.Motivo, null, (assinatura, agora) => assinatura.Cancelar(agora, request.Versao), cancellationToken);

    public async Task<DocumentoAssinatura> AbrirTermoAceitoAsync(Guid empresaId, CancellationToken cancellationToken)
    {
        var aceite = await db.AceitesTermosAssinaturas.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.EmpresaId == empresaId).OrderByDescending(x => x.AceitoEmUtc)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Nenhum termo aceito foi encontrado.");
        var stream = await storage.AbrirLeituraAsync(aceite.DocumentoChave, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("O documento aceito não está disponível.");
        return new(stream, $"termo-adesao-detara-v{aceite.VersaoTermo}.pdf", "application/pdf");
    }

    private async Task<AssinaturaPlataformaResultado> AlterarAsync(Guid administradorId, Guid empresaId,
        long versao, TipoEventoAssinatura evento, string motivo, string? referencia,
        Func<AssinaturaEmpresa, DateTime, bool> alterar, CancellationToken cancellationToken)
    {
        await ValidarAdministradorAsync(administradorId, cancellationToken);
        await using var contexto = CriarContexto(empresaId);
        var assinatura = await contexto.AssinaturasEmpresas.SingleOrDefaultAsync(cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Assinatura não encontrada.");
        if (assinatura.Versao != versao)
        {
            var jaAplicada = evento switch
            {
                TipoEventoAssinatura.AtrasoRegistrado => assinatura.Status == StatusAssinaturaEmpresa.EmAtraso,
                TipoEventoAssinatura.Suspensa => assinatura.Status == StatusAssinaturaEmpresa.Suspensa,
                TipoEventoAssinatura.Reativada => assinatura.Status == StatusAssinaturaEmpresa.Ativa,
                TipoEventoAssinatura.Cancelada => assinatura.Status == StatusAssinaturaEmpresa.Cancelada,
                _ => false
            };
            if (jaAplicada) return await ObterAsync(empresaId, cancellationToken);
            throw new ConflitoRegraNegocioException("A assinatura foi atualizada por outra operação.");
        }
        var anterior = assinatura.Status;
        var agora = relogio.GetUtcNow().UtcDateTime;
        if (!alterar(assinatura, agora)) return await ObterAsync(empresaId, cancellationToken);
        contexto.HistoricosAssinaturasEmpresas.Add(new(empresaId, assinatura.Id, evento,
            anterior, assinatura.Status, agora, motivo, administradorPlataformaId: administradorId,
            referenciaPagamento: referencia));
        AdicionarAuditoria(contexto, administradorId, empresaId, assinatura.Id,
            $"Assinatura{evento}", motivo);
        try { await contexto.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConflitoRegraNegocioException("A assinatura foi atualizada por outra operação."); }
        return await ObterAsync(empresaId, cancellationToken);
    }

    private async Task ValidarAdministradorAsync(Guid administradorId, CancellationToken cancellationToken)
    {
        if (!await db.AdministradoresPlataforma.AsNoTracking()
            .AnyAsync(x => x.Id == administradorId && x.EhAtivo && x.MfaHabilitado, cancellationToken))
            throw new CredenciaisPlataformaInvalidasException();
    }

    private DetaraDbContext CriarContexto(Guid empresaId) => new(dbOptions, new ContextoAssinaturaPlataforma(empresaId));

    private static void AdicionarAuditoria(DetaraDbContext contexto, Guid administradorId, Guid empresaId,
        Guid assinaturaId, string acao, string motivo) => contexto.AuditoriasPlataforma.Add(new(
            administradorId, acao, empresaId, assinaturaId, null, motivo));

    private sealed class ContextoAssinaturaPlataforma(Guid empresaId) : IUsuarioContexto
    {
        public Guid UsuarioId => Guid.Parse("00000000-0000-0000-0000-000000000012");
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado => true;
    }
}
