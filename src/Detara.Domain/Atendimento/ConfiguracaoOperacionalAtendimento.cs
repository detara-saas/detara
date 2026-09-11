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
        NivelExigenciaOperacional fotosSaida,
        NivelExigenciaOperacional fotosDurante = NivelExigenciaOperacional.Desabilitado)
        : base(Guid.NewGuid(), empresaId)
    {
        Atualizar(checklistEntrada, fotosEntrada, fotosSaida, fotosDurante);
    }

    public NivelExigenciaOperacional ChecklistEntrada { get; private set; }
    public NivelExigenciaOperacional FotosEntrada { get; private set; }
    public NivelExigenciaOperacional FotosSaida { get; private set; }
    public NivelExigenciaOperacional FotosDurante { get; private set; }

    public void Atualizar(
        NivelExigenciaOperacional checklistEntrada,
        NivelExigenciaOperacional fotosEntrada,
        NivelExigenciaOperacional fotosSaida,
        NivelExigenciaOperacional fotosDurante = NivelExigenciaOperacional.Desabilitado)
    {
        ChecklistEntrada = Validar(checklistEntrada, nameof(checklistEntrada));
        FotosEntrada = Validar(fotosEntrada, nameof(fotosEntrada));
        FotosSaida = Validar(fotosSaida, nameof(fotosSaida));
        FotosDurante = Validar(fotosDurante, nameof(fotosDurante));
        MarcarComoAtualizada();
    }

    private static NivelExigenciaOperacional Validar(
        NivelExigenciaOperacional nivel,
        string parametro) =>
        Enum.IsDefined(nivel)
            ? nivel
            : throw new ArgumentOutOfRangeException(parametro, "O nível de exigência é inválido.");
}
