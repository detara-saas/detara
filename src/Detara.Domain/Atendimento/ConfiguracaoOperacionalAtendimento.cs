using Detara.Domain.Entidades;

namespace Detara.Domain.Atendimento;

public sealed class ConfiguracaoOperacionalAtendimento : EntidadeEmpresaBase
{
    private ConfiguracaoOperacionalAtendimento()
    {
    }

    public ConfiguracaoOperacionalAtendimento(
        Guid empresaId,
        NivelExigenciaOperacional checklistEntrada,
        NivelExigenciaOperacional fotosEntrada,
        NivelExigenciaOperacional fotosSaida)
        : base(Guid.NewGuid(), empresaId)
    {
        Atualizar(checklistEntrada, fotosEntrada, fotosSaida);
    }

    public NivelExigenciaOperacional ChecklistEntrada { get; private set; }
    public NivelExigenciaOperacional FotosEntrada { get; private set; }
    public NivelExigenciaOperacional FotosSaida { get; private set; }

    public void Atualizar(
        NivelExigenciaOperacional checklistEntrada,
        NivelExigenciaOperacional fotosEntrada,
        NivelExigenciaOperacional fotosSaida)
    {
        ChecklistEntrada = Validar(checklistEntrada, nameof(checklistEntrada));
        FotosEntrada = Validar(fotosEntrada, nameof(fotosEntrada));
        FotosSaida = Validar(fotosSaida, nameof(fotosSaida));
        MarcarComoAtualizada();
    }

    private static NivelExigenciaOperacional Validar(
        NivelExigenciaOperacional nivel,
        string parametro) =>
        Enum.IsDefined(nivel)
            ? nivel
            : throw new ArgumentOutOfRangeException(parametro, "O nível de exigência é inválido.");
}
