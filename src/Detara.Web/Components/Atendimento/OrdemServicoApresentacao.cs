using Detara.Contracts.Atendimento;

namespace Detara.Web.Components.Atendimento;

public static class OrdemServicoApresentacao
{
    public static bool ChecklistHabilitado(
        OrdemServicoDetalheResponse ordem,
        ConfiguracaoOperacionalResponse? configuracao) =>
        (ordem.ChecklistEntradaSnapshot ?? configuracao?.ChecklistEntrada ??
            NivelExigenciaOperacionalContrato.Desabilitado) !=
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
                ordem.FotosEntradaSnapshot ?? configuracao?.FotosEntrada ??
                NivelExigenciaOperacionalContrato.Desabilitado,
            CategoriaFotoOrdemServicoContrato.Durante =>
                ordem.FotosDuranteSnapshot ?? configuracao?.FotosDurante ??
                NivelExigenciaOperacionalContrato.Desabilitado,
            CategoriaFotoOrdemServicoContrato.Saida =>
                ordem.FotosSaidaSnapshot ?? configuracao?.FotosSaida ??
                NivelExigenciaOperacionalContrato.Desabilitado,
            _ => NivelExigenciaOperacionalContrato.Desabilitado
        };

    public static bool ExibirAdicionais(StatusOrdemServicoContrato status, int quantidade) =>
        status is StatusOrdemServicoContrato.Aberta or StatusOrdemServicoContrato.EmExecucao ||
        quantidade > 0;

    public static bool PodeCriarAdicional(StatusOrdemServicoContrato status) =>
        status is StatusOrdemServicoContrato.Aberta or StatusOrdemServicoContrato.EmExecucao;
}
