using Detara.Domain.Entidades;

namespace Detara.Domain.Plataforma;

public sealed class CodigoRecuperacaoAdministradorPlataforma : EntidadeBase
{
    private CodigoRecuperacaoAdministradorPlataforma()
    {
    }

    public CodigoRecuperacaoAdministradorPlataforma(Guid administradorPlataformaId, string codigoHash)
        : base(Guid.NewGuid())
    {
        AdministradorPlataformaId = administradorPlataformaId == Guid.Empty
            ? throw new ArgumentException("O administrador deve ser informado.", nameof(administradorPlataformaId))
            : administradorPlataformaId;
        CodigoHash = string.IsNullOrWhiteSpace(codigoHash)
            ? throw new ArgumentException("O hash deve ser informado.", nameof(codigoHash))
            : codigoHash;
    }

    public Guid AdministradorPlataformaId { get; private set; }
    public string CodigoHash { get; private set; } = string.Empty;
    public DateTime? UtilizadoEmUtc { get; private set; }

    public bool Disponivel => UtilizadoEmUtc is null;

    public void MarcarUtilizado(DateTime agoraUtc)
    {
        if (UtilizadoEmUtc is not null)
        {
            throw new InvalidOperationException("O código de recuperação já foi utilizado.");
        }

        UtilizadoEmUtc = agoraUtc;
        MarcarComoAtualizada();
    }
}
