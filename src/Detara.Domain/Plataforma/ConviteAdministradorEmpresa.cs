using Detara.Domain.Entidades;
using System.Security.Cryptography;
using System.Text;

namespace Detara.Domain.Plataforma;

public enum StatusConviteAdministradorEmpresa
{
    Pendente = 1,
    Processando = 2,
    Enviado = 3,
    FalhaEnvio = 4,
    Aceito = 5,
    Expirado = 6,
    Invalidado = 7
}

public enum OrigemConviteAcessoEmpresa
{
    AdministradorInicialPlataforma = 1,
    UsuarioTenant = 2
}

public sealed class ConviteAdministradorEmpresa : EntidadeBase
{
    private ConviteAdministradorEmpresa()
    {
    }

    public ConviteAdministradorEmpresa(
        Guid empresaId,
        Guid usuarioId,
        string emailDestinoSnapshot,
        Guid criadoPorAdministradorPlataformaId)
        : base(Guid.NewGuid())
    {
        EmpresaId = Exigir(empresaId, nameof(empresaId));
        UsuarioId = Exigir(usuarioId, nameof(usuarioId));
        CriadoPorAdministradorPlataformaId = Exigir(
            criadoPorAdministradorPlataformaId,
            nameof(criadoPorAdministradorPlataformaId));
        Origem = OrigemConviteAcessoEmpresa.AdministradorInicialPlataforma;
        EmailDestinoSnapshot = NormalizarEmail(emailDestinoSnapshot);
        Status = StatusConviteAdministradorEmpresa.Pendente;
        ProximaTentativaEnvioEmUtc = DateTime.UtcNow;
    }

    public Guid EmpresaId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string EmailDestinoSnapshot { get; private set; } = string.Empty;
    public string? TokenHash { get; private set; }
    public OrigemConviteAcessoEmpresa Origem { get; private set; }
    public StatusConviteAdministradorEmpresa Status { get; private set; }
    public DateTime? ExpiraEmUtc { get; private set; }
    public DateTime? ProcessamentoIniciadoEmUtc { get; private set; }
    public DateTime? EnviadoEmUtc { get; private set; }
    public DateTime? AceitoEmUtc { get; private set; }
    public DateTime? InvalidadoEmUtc { get; private set; }
    public Guid? CriadoPorAdministradorPlataformaId { get; private set; }
    public Guid? CriadoPorUsuarioId { get; private set; }
    public int QuantidadeTentativasEnvio { get; private set; }
    public DateTime? ProximaTentativaEnvioEmUtc { get; private set; }
    public string? UltimoErroSeguro { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public long Versao { get; private set; } = 1;

    public static ConviteAdministradorEmpresa CriarParaUsuarioTenant(
        Guid empresaId,
        Guid usuarioId,
        string emailDestinoSnapshot,
        Guid criadoPorUsuarioId)
    {
        var convite = new ConviteAdministradorEmpresa
        {
            Id = Guid.NewGuid(),
            CriadoEmUtc = DateTime.UtcNow,
            EhAtivo = true,
            EmpresaId = Exigir(empresaId, nameof(empresaId)),
            UsuarioId = Exigir(usuarioId, nameof(usuarioId)),
            EmailDestinoSnapshot = NormalizarEmail(emailDestinoSnapshot),
            Origem = OrigemConviteAcessoEmpresa.UsuarioTenant,
            CriadoPorUsuarioId = Exigir(criadoPorUsuarioId, nameof(criadoPorUsuarioId)),
            Status = StatusConviteAdministradorEmpresa.Pendente,
            ProximaTentativaEnvioEmUtc = DateTime.UtcNow
        };
        return convite;
    }

    public void IniciarEnvio(string tokenHash, DateTime expiraEmUtc, DateTime agoraUtc)
    {
        if (Status != StatusConviteAdministradorEmpresa.Pendente ||
            ProximaTentativaEnvioEmUtc > agoraUtc)
        {
            throw new InvalidOperationException("O convite não está disponível para envio.");
        }

        TokenHash = string.IsNullOrWhiteSpace(tokenHash)
            ? throw new ArgumentException("O hash do token deve ser informado.", nameof(tokenHash))
            : tokenHash;
        ExpiraEmUtc = expiraEmUtc > agoraUtc
            ? expiraEmUtc
            : throw new ArgumentException("A expiração deve estar no futuro.", nameof(expiraEmUtc));
        Status = StatusConviteAdministradorEmpresa.Processando;
        ProcessamentoIniciadoEmUtc = agoraUtc;
        ProximaTentativaEnvioEmUtc = null;
        UltimoErroSeguro = null;
        Versao++;
        MarcarComoAtualizada();
    }

    public void RegistrarEnvio(string providerMessageId, DateTime agoraUtc)
    {
        ExigirProcessando();
        QuantidadeTentativasEnvio++;
        Status = StatusConviteAdministradorEmpresa.Enviado;
        EnviadoEmUtc = agoraUtc;
        ProcessamentoIniciadoEmUtc = null;
        ProviderMessageId = string.IsNullOrWhiteSpace(providerMessageId)
            ? "aceita-sem-id"
            : providerMessageId.Trim();
        Versao++;
        MarcarComoAtualizada();
    }

    public void RegistrarFalha(string erroSeguro, DateTime agoraUtc, DateTime? proximaTentativaUtc)
    {
        ExigirProcessando();
        QuantidadeTentativasEnvio++;
        Status = proximaTentativaUtc is null
            ? StatusConviteAdministradorEmpresa.FalhaEnvio
            : StatusConviteAdministradorEmpresa.Pendente;
        ProximaTentativaEnvioEmUtc = proximaTentativaUtc;
        ProcessamentoIniciadoEmUtc = null;
        UltimoErroSeguro = Limitar(erroSeguro, 500);
        Versao++;
        MarcarComoAtualizada();
    }

    public void PrepararReenvio(DateTime agoraUtc, Guid administradorPlataformaId)
    {
        if (Origem != OrigemConviteAcessoEmpresa.AdministradorInicialPlataforma)
        {
            throw new InvalidOperationException("O convite não pertence ao provisionamento Platform.");
        }

        if (Status is StatusConviteAdministradorEmpresa.Aceito or
            StatusConviteAdministradorEmpresa.Invalidado)
        {
            throw new InvalidOperationException("O convite não pode ser reenviado neste estado.");
        }

        CriadoPorAdministradorPlataformaId = Exigir(administradorPlataformaId, nameof(administradorPlataformaId));
        TokenHash = null;
        ExpiraEmUtc = null;
        Status = StatusConviteAdministradorEmpresa.Pendente;
        ProximaTentativaEnvioEmUtc = agoraUtc;
        ProcessamentoIniciadoEmUtc = null;
        UltimoErroSeguro = null;
        ProviderMessageId = null;
        Versao++;
        MarcarComoAtualizada();
    }

    public void PrepararReenvioTenant(DateTime agoraUtc, Guid usuarioId)
    {
        if (Origem != OrigemConviteAcessoEmpresa.UsuarioTenant)
        {
            throw new InvalidOperationException("O convite não pertence à administração Tenant.");
        }

        if (Status is StatusConviteAdministradorEmpresa.Aceito or StatusConviteAdministradorEmpresa.Invalidado)
        {
            throw new InvalidOperationException("O convite não pode ser reenviado neste estado.");
        }

        CriadoPorUsuarioId = Exigir(usuarioId, nameof(usuarioId));
        TokenHash = null;
        ExpiraEmUtc = null;
        Status = StatusConviteAdministradorEmpresa.Pendente;
        ProximaTentativaEnvioEmUtc = agoraUtc;
        ProcessamentoIniciadoEmUtc = null;
        UltimoErroSeguro = null;
        ProviderMessageId = null;
        Versao++;
        MarcarComoAtualizada();
    }

    public bool PodeSerAceito(string tokenHash, DateTime agoraUtc) =>
        Status is (StatusConviteAdministradorEmpresa.Enviado or StatusConviteAdministradorEmpresa.FalhaEnvio) &&
        TokenHash is not null &&
        ExpiraEmUtc > agoraUtc &&
        ComparacaoTempoConstante(TokenHash, tokenHash);

    public void MarcarAceito(DateTime agoraUtc)
    {
        if (Status is not (StatusConviteAdministradorEmpresa.Enviado or StatusConviteAdministradorEmpresa.FalhaEnvio) ||
            ExpiraEmUtc <= agoraUtc)
        {
            throw new InvalidOperationException("O convite é inválido ou expirou.");
        }

        Status = StatusConviteAdministradorEmpresa.Aceito;
        AceitoEmUtc = agoraUtc;
        TokenHash = null;
        ProximaTentativaEnvioEmUtc = null;
        Versao++;
        MarcarComoAtualizada();
    }

    public void MarcarExpirado(DateTime agoraUtc)
    {
        if (Status is StatusConviteAdministradorEmpresa.Aceito or StatusConviteAdministradorEmpresa.Invalidado)
        {
            return;
        }

        Status = StatusConviteAdministradorEmpresa.Expirado;
        TokenHash = null;
        InvalidadoEmUtc = agoraUtc;
        ProximaTentativaEnvioEmUtc = null;
        Versao++;
        MarcarComoAtualizada();
    }

    private void ExigirProcessando()
    {
        if (Status != StatusConviteAdministradorEmpresa.Processando)
        {
            throw new InvalidOperationException("O convite não está em processamento.");
        }
    }

    private static Guid Exigir(Guid id, string parametro) => id == Guid.Empty
        ? throw new ArgumentException("O identificador deve ser informado.", parametro)
        : id;

    private static bool ComparacaoTempoConstante(string esperado, string recebido)
    {
        var esperadoBytes = Encoding.UTF8.GetBytes(esperado);
        var recebidoBytes = Encoding.UTF8.GetBytes(recebido);
        return esperadoBytes.Length == recebidoBytes.Length &&
            CryptographicOperations.FixedTimeEquals(esperadoBytes, recebidoBytes);
    }

    private static string NormalizarEmail(string email)
    {
        var normalizado = Limitar(email, 200).ToLowerInvariant();
        if (normalizado.Contains('\r') || normalizado.Contains('\n'))
        {
            throw new ArgumentException("O e-mail informado é inválido.", nameof(email));
        }

        return normalizado;
    }

    private static string Limitar(string valor, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ArgumentException("O valor deve ser informado.", nameof(valor));
        }

        var normalizado = valor.Trim();
        return normalizado.Length <= maximo
            ? normalizado
            : throw new ArgumentException($"O valor deve possuir no máximo {maximo} caracteres.", nameof(valor));
    }
}
