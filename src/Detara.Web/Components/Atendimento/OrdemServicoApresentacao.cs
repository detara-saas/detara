using Detara.Contracts.Atendimento;

namespace Detara.Web.Components.Atendimento;

public enum AcaoPrincipalOrdemServico
{
    Nenhuma,
    RealizarCheckIn,
    IniciarExecucao,
    FinalizarExecucao,
    ConfirmarEntrega
}

public static class OrdemServicoApresentacao
{
    public static AcaoPrincipalOrdemServico ProximaAcao(OrdemServicoDetalheResponse ordem) => ordem.Status switch
    {
        StatusOrdemServicoContrato.Aberta when !ordem.CheckInEmUtc.HasValue =>
            AcaoPrincipalOrdemServico.RealizarCheckIn,
        StatusOrdemServicoContrato.Aberta => AcaoPrincipalOrdemServico.IniciarExecucao,
        StatusOrdemServicoContrato.EmExecucao => AcaoPrincipalOrdemServico.FinalizarExecucao,
        StatusOrdemServicoContrato.AguardandoRetirada => AcaoPrincipalOrdemServico.ConfirmarEntrega,
        _ => AcaoPrincipalOrdemServico.Nenhuma
    };

    public static bool ChecklistHabilitado(
        OrdemServicoDetalheResponse ordem,
        ConfiguracaoOperacionalResponse? configuracao) =>
        ordem.Checklist is not null || NivelVisivel(
            ordem.ChecklistEntradaSnapshot,
            configuracao?.ChecklistEntrada ?? ordem.ConfiguracaoOperacionalAtual?.ChecklistEntrada) !=
            NivelExigenciaOperacionalContrato.Desabilitado;

    public static IReadOnlyList<CategoriaFotoOrdemServicoContrato> CategoriasFotoHabilitadas(
        OrdemServicoDetalheResponse ordem,
        ConfiguracaoOperacionalResponse? configuracao) =>
        Enum.GetValues<CategoriaFotoOrdemServicoContrato>()
            .Where(categoria => NivelFoto(ordem, configuracao, categoria) !=
                NivelExigenciaOperacionalContrato.Desabilitado)
            .ToArray();

    public static NivelExigenciaOperacionalContrato NivelFoto(
        OrdemServicoDetalheResponse ordem,
        ConfiguracaoOperacionalResponse? configuracao,
        CategoriaFotoOrdemServicoContrato categoria) => categoria switch
        {
            CategoriaFotoOrdemServicoContrato.Entrada =>
                NivelVisivel(ordem.FotosEntradaSnapshot,
                    configuracao?.FotosEntrada ?? ordem.ConfiguracaoOperacionalAtual?.FotosEntrada),
            CategoriaFotoOrdemServicoContrato.Durante =>
                NivelVisivel(ordem.FotosDuranteSnapshot,
                    configuracao?.FotosDurante ?? ordem.ConfiguracaoOperacionalAtual?.FotosDurante),
            CategoriaFotoOrdemServicoContrato.Saida =>
                NivelVisivel(ordem.FotosSaidaSnapshot,
                    configuracao?.FotosSaida ?? ordem.ConfiguracaoOperacionalAtual?.FotosSaida),
            _ => NivelExigenciaOperacionalContrato.Desabilitado
        };

    public static IReadOnlyList<CategoriaFotoOrdemServicoContrato> CategoriasFotoParaEtapa(
        OrdemServicoDetalheResponse ordem,
        ConfiguracaoOperacionalResponse? configuracao)
    {
        var habilitadas = CategoriasFotoHabilitadas(ordem, configuracao);
        return Enum.GetValues<CategoriaFotoOrdemServicoContrato>()
            .Where(categoria => ordem.Fotos.Any(foto => foto.Categoria == categoria) ||
                habilitadas.Contains(categoria) && categoria switch
                {
                    CategoriaFotoOrdemServicoContrato.Entrada => ordem.CheckInEmUtc.HasValue,
                    CategoriaFotoOrdemServicoContrato.Durante or CategoriaFotoOrdemServicoContrato.Saida =>
                        ordem.Status is StatusOrdemServicoContrato.EmExecucao or
                            StatusOrdemServicoContrato.AguardandoRetirada or
                            StatusOrdemServicoContrato.Concluida or
                            StatusOrdemServicoContrato.Cancelada,
                    _ => false
                })
            .ToArray();
    }

    public static bool ExibirAdicionais(StatusOrdemServicoContrato status, int quantidade) =>
        status is StatusOrdemServicoContrato.Aberta or StatusOrdemServicoContrato.EmExecucao ||
        quantidade > 0;

    public static bool PodeCriarAdicional(StatusOrdemServicoContrato status) =>
        status is StatusOrdemServicoContrato.Aberta or StatusOrdemServicoContrato.EmExecucao;

    private static NivelExigenciaOperacionalContrato NivelVisivel(
        NivelExigenciaOperacionalContrato? snapshot,
        NivelExigenciaOperacionalContrato? atual) =>
        snapshot == NivelExigenciaOperacionalContrato.Obrigatorio
            ? NivelExigenciaOperacionalContrato.Obrigatorio
            : atual ?? snapshot ?? NivelExigenciaOperacionalContrato.Desabilitado;
}
