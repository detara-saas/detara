using System.Security.Cryptography;
using Detara.Application.Abstracoes;
using Detara.Application.Assinaturas;
using Detara.Domain.Assinaturas;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Assinaturas;

internal sealed class AssinaturasTenantServico(
    DetaraDbContext db,
    IUsuarioContexto usuarioContexto,
    IArquivoStorage storage,
    IGeradorPdfTermoAssinatura gerador,
    TimeProvider relogio) : IAssinaturasTenantServico
{
    public async Task<AssinaturaEmpresaResultado> ObterAsync(CancellationToken cancellationToken)
    {
        var assinatura = await db.AssinaturasEmpresas.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (assinatura is null) return Vazia();
        var aceite = await db.AceitesTermosAssinaturas.AsNoTracking()
            .Where(x => x.AssinaturaId == assinatura.Id)
            .OrderByDescending(x => x.AceitoEmUtc)
            .FirstOrDefaultAsync(cancellationToken);
        return Mapear(assinatura, aceite);
    }

    public async Task<DocumentoAssinatura> GerarPreviaAsync(CancellationToken cancellationToken)
    {
        var (assinatura, empresa, usuario) = await ObterDadosAsync(cancellationToken);
        var pdf = gerador.Gerar(CriarDados(assinatura, empresa, usuario, null));
        return Documento(pdf, $"termo-adesao-detara-v{TermosAssinatura.VersaoAtual}-previa.pdf");
    }

    public async Task<AssinaturaEmpresaResultado> AceitarAsync(string? ipAceite, CancellationToken cancellationToken)
    {
        var (assinatura, empresa, usuario) = await ObterDadosAsync(cancellationToken);
        var existente = await db.AceitesTermosAssinaturas
            .SingleOrDefaultAsync(x => x.AssinaturaId == assinatura.Id &&
                x.VersaoTermo == TermosAssinatura.VersaoAtual, cancellationToken);
        if (existente is not null) return Mapear(assinatura, existente);

        var agora = relogio.GetUtcNow().UtcDateTime;
        var dataConfirmacao = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            agora, TimeZoneInfo.FindSystemTimeZoneById(empresa.FusoHorario)));
        var confirmacaoRegistrada = assinatura.ConfirmarComercialmente(
            dataConfirmacao, agora, assinatura.Versao);
        var pdf = gerador.Gerar(CriarDados(assinatura, empresa, usuario, agora));
        var aceiteId = Guid.NewGuid();
        var chave = $"empresas/{empresa.Id:N}/assinaturas/{assinatura.Id:N}/termos/{aceiteId:N}.pdf";
        var hash = Convert.ToHexString(SHA256.HashData(pdf));
        await using (var conteudo = new MemoryStream(pdf, writable: false))
            await storage.SalvarAsync(chave, conteudo, cancellationToken);

        var aceite = new AceiteTermoAssinatura(empresa.Id, assinatura.Id, usuario.Id,
            TermosAssinatura.VersaoAtual, agora, chave, hash, assinatura.ValorMensal,
            assinatura.DataInicio, assinatura.PrimeiroVencimento, empresa.RazaoSocial,
            empresa.CpfCnpj, usuario.Nome, usuario.Email, ipAceite);
        db.AceitesTermosAssinaturas.Add(aceite);
        if (confirmacaoRegistrada)
        {
            db.HistoricosAssinaturasEmpresas.Add(new HistoricoAssinaturaEmpresa(
                empresa.Id, assinatura.Id, TipoEventoAssinatura.ConfirmacaoComercialRegistrada,
                assinatura.Status, assinatura.Status, agora,
                $"Confirmação comercial registrada em {dataConfirmacao:dd/MM/yyyy}. " +
                $"Primeiro vencimento: {assinatura.PrimeiroVencimento:dd/MM/yyyy}.",
                usuarioId: usuario.Id));
        }
        db.HistoricosAssinaturasEmpresas.Add(new HistoricoAssinaturaEmpresa(
            empresa.Id, assinatura.Id, TipoEventoAssinatura.TermoAceito, assinatura.Status,
            assinatura.Status, agora, $"Termo v{TermosAssinatura.VersaoAtual} aceito eletronicamente.",
            usuarioId: usuario.Id));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await storage.ExcluirAsync(chave, CancellationToken.None);
            db.ChangeTracker.Clear();
            var concorrente = await db.AceitesTermosAssinaturas.AsNoTracking()
                .SingleOrDefaultAsync(x => x.AssinaturaId == assinatura.Id &&
                    x.VersaoTermo == TermosAssinatura.VersaoAtual, cancellationToken);
            if (concorrente is null) throw;
            return Mapear(assinatura, concorrente);
        }
        return Mapear(assinatura, aceite);
    }

    public async Task<DocumentoAssinatura> AbrirTermoAceitoAsync(CancellationToken cancellationToken)
    {
        var aceite = await db.AceitesTermosAssinaturas.AsNoTracking()
            .OrderByDescending(x => x.AceitoEmUtc).FirstOrDefaultAsync(cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Nenhum termo aceito foi encontrado.");
        var stream = await storage.AbrirLeituraAsync(aceite.DocumentoChave, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("O documento aceito não está disponível.");
        return new(stream, $"termo-adesao-detara-v{aceite.VersaoTermo}.pdf", "application/pdf");
    }

    private async Task<(AssinaturaEmpresa Assinatura, Detara.Domain.Entidades.Empresa Empresa,
        Detara.Domain.Entidades.Usuario Usuario)> ObterDadosAsync(CancellationToken cancellationToken)
    {
        var assinatura = await db.AssinaturasEmpresas.SingleOrDefaultAsync(cancellationToken)
            ?? throw new RecursoNaoEncontradoException("A empresa ainda não possui assinatura comercial.");
        var empresa = await db.Empresas.SingleAsync(x => x.Id == usuarioContexto.EmpresaId, cancellationToken);
        var usuario = await db.Usuarios.AsNoTracking()
            .SingleAsync(x => x.Id == usuarioContexto.UsuarioId, cancellationToken);
        return (assinatura, empresa, usuario);
    }

    private static DadosTermoAssinatura CriarDados(AssinaturaEmpresa assinatura,
        Detara.Domain.Entidades.Empresa empresa, Detara.Domain.Entidades.Usuario usuario, DateTime? aceitoEmUtc) =>
        new(empresa.RazaoSocial, empresa.CpfCnpj, usuario.Nome, usuario.Email,
            assinatura.ValorMensal, assinatura.DataInicio, assinatura.FimTeste,
            assinatura.PrimeiroVencimento, assinatura.DiaVencimento,
            TermosAssinatura.VersaoAtual, aceitoEmUtc);

    private static DocumentoAssinatura Documento(byte[] bytes, string nome) =>
        new(new MemoryStream(bytes, writable: false), nome, "application/pdf");

    internal static AssinaturaEmpresaResultado Mapear(AssinaturaEmpresa assinatura, AceiteTermoAssinatura? aceite) =>
        new(true, assinatura.Id, assinatura.Status.ToString(), assinatura.ValorMensal,
            assinatura.InicioTeste, assinatura.FimTeste, assinatura.DataConfirmacaoComercial,
            assinatura.ConfirmacaoComercialRegistradaEmUtc, assinatura.DiaVencimento,
            assinatura.PrimeiroVencimento, assinatura.ProximoVencimento, assinatura.Versao,
            assinatura.AsaasCustomerId, assinatura.AsaasSubscriptionId,
            aceite is null ? null : new(aceite.Id, aceite.VersaoTermo, aceite.AceitoEmUtc,
                aceite.HashSha256, aceite.ResponsavelNome, aceite.ResponsavelEmail));

    internal static AssinaturaEmpresaResultado Vazia() =>
        new(false, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
}
