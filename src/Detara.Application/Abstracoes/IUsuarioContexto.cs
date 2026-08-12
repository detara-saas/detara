namespace Detara.Application.Abstracoes;

public interface IUsuarioContexto
{
    Guid UsuarioId { get; }
    Guid EmpresaId { get; }
    bool EstaAutenticado { get; }
}
