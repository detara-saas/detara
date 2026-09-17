using Detara.Domain.Entidades;

namespace Detara.Domain.Identidade;

public enum TipoIdentidadeSessao
{
    Tenant = 1,
    AdministradorPlataforma = 2
}

public sealed class SessaoAutenticacao : EntidadeBase
{
    private SessaoAutenticacao()
    {
    }

    private SessaoAutenticacao(
        Guid id,
        TipoIdentidadeSessao tipoIdentidade,
        Guid? usuarioId,
        Guid? empresaId,
        Guid? administradorPlataformaId,
        long versaoSegurancaIdentidade,
        long? versaoSegurancaEmpresa,
        Guid familiaId,
        string tokenHash,
        bool persistente,
        DateTime expiraEmUtc)
        : base(id == Guid.Empty ? throw new ArgumentException("O identificador da sessão deve ser informado.", nameof(id)) : id)
    {
        TipoIdentidade = tipoIdentidade;
        UsuarioId = usuarioId;
        EmpresaId = empresaId;
        AdministradorPlataformaId = administradorPlataformaId;
        VersaoSegurancaIdentidade = versaoSegurancaIdentidade > 0
            ? versaoSegurancaIdentidade
            : throw new ArgumentOutOfRangeException(nameof(versaoSegurancaIdentidade));
        VersaoSegurancaEmpresa = versaoSegurancaEmpresa;
        FamiliaId = familiaId == Guid.Empty
            ? throw new ArgumentException("A família da sessão deve ser informada.", nameof(familiaId))
            : familiaId;
        TokenHash = Exigir(tokenHash, nameof(tokenHash));
        Persistente = persistente;
        ExpiraEmUtc = expiraEmUtc;
    }

    public TipoIdentidadeSessao TipoIdentidade { get; private set; }
    public Guid? UsuarioId { get; private set; }
    public Guid? EmpresaId { get; private set; }
    public Guid? AdministradorPlataformaId { get; private set; }
    public long VersaoSegurancaIdentidade { get; private set; }
    public long? VersaoSegurancaEmpresa { get; private set; }
    public Guid FamiliaId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public bool Persistente { get; private set; }
    public DateTime ExpiraEmUtc { get; private set; }
    public DateTime? UltimoUsoEmUtc { get; private set; }
    public DateTime? RevogadoEmUtc { get; private set; }
    public Guid? SubstituidoPorId { get; private set; }
    public string? MotivoRevogacao { get; private set; }
    public long Versao { get; private set; } = 1;

    public bool EstaAtivaEm(DateTime agoraUtc) =>
        RevogadoEmUtc is null && ExpiraEmUtc > agoraUtc;

    public static SessaoAutenticacao CriarTenant(
        Guid id,
        Guid usuarioId,
        Guid empresaId,
        long usuarioVersaoSeguranca,
        long empresaVersaoSeguranca,
        Guid familiaId,
        string tokenHash,
        bool persistente,
        DateTime expiraEmUtc)
    {
        if (usuarioId == Guid.Empty || empresaId == Guid.Empty)
        {
            throw new ArgumentException("Usuário e empresa são obrigatórios para a sessão tenant.");
        }

        return new(
            id,
            TipoIdentidadeSessao.Tenant,
            usuarioId,
            empresaId,
            null,
            usuarioVersaoSeguranca,
            empresaVersaoSeguranca,
            familiaId,
            tokenHash,
            persistente,
            expiraEmUtc);
    }

    public static SessaoAutenticacao CriarAdministradorPlataforma(
        Guid id,
        Guid administradorPlataformaId,
        long versaoSeguranca,
        Guid familiaId,
        string tokenHash,
        DateTime expiraEmUtc)
    {
        if (administradorPlataformaId == Guid.Empty)
        {
            throw new ArgumentException("O administrador é obrigatório para a sessão da plataforma.");
        }

        return new(
            id,
            TipoIdentidadeSessao.AdministradorPlataforma,
            null,
            null,
            administradorPlataformaId,
            versaoSeguranca,
            null,
            familiaId,
            tokenHash,
            false,
            expiraEmUtc);
    }

    public void SubstituirPor(Guid novaSessaoId, DateTime agoraUtc)
    {
        if (!EstaAtivaEm(agoraUtc) || novaSessaoId == Guid.Empty)
        {
            throw new InvalidOperationException("A sessão não pode ser rotacionada.");
        }

        UltimoUsoEmUtc = agoraUtc;
        RevogadoEmUtc = agoraUtc;
        SubstituidoPorId = novaSessaoId;
        MotivoRevogacao = "rotacionado";
        Versao++;
        MarcarComoAtualizada();
    }

    public void Revogar(DateTime agoraUtc, string motivo)
    {
        if (RevogadoEmUtc is not null)
        {
            return;
        }

        RevogadoEmUtc = agoraUtc;
        MotivoRevogacao = Exigir(motivo, nameof(motivo));
        Versao++;
        MarcarComoAtualizada();
    }

    private static string Exigir(string valor, string parametro) =>
        string.IsNullOrWhiteSpace(valor)
            ? throw new ArgumentException("O valor deve ser informado.", parametro)
            : valor.Trim();
}
