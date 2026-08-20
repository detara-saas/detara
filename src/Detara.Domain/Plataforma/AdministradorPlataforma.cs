using Detara.Domain.Entidades;
using System.Net.Mail;

namespace Detara.Domain.Plataforma;

public sealed class AdministradorPlataforma : EntidadeBase
{
    private AdministradorPlataforma()
    {
    }

    public AdministradorPlataforma(string nome, string email, string senhaHash)
        : base(Guid.NewGuid())
    {
        Nome = Exigir(nome, nameof(nome), 160);
        Email = NormalizarEmail(email);
        EmailNormalizado = Email.ToUpperInvariant();
        SenhaHash = Exigir(senhaHash, nameof(senhaHash), 500);
    }

    public string Nome { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string EmailNormalizado { get; private set; } = string.Empty;
    public string SenhaHash { get; private set; } = string.Empty;
    public bool MfaHabilitado { get; private set; }
    public string? SegredoTotpProtegido { get; private set; }
    public long? UltimoTimestepTotpAceito { get; private set; }
    public long VersaoSeguranca { get; private set; } = 1;
    public DateTime? UltimoLoginEmUtc { get; private set; }

    public void DefinirSegredoTotpProtegido(string segredoProtegido)
    {
        if (MfaHabilitado)
        {
            throw new InvalidOperationException("A autenticação multifator já está configurada.");
        }

        SegredoTotpProtegido = Exigir(segredoProtegido, nameof(segredoProtegido), 2000);
        UltimoTimestepTotpAceito = null;
        MarcarComoAtualizada();
    }

    public void AtivarMfa(long timestepAceito)
    {
        if (string.IsNullOrWhiteSpace(SegredoTotpProtegido))
        {
            throw new InvalidOperationException("O segredo TOTP deve ser configurado antes da ativação.");
        }

        MfaHabilitado = true;
        RegistrarTimestepTotp(timestepAceito);
        VersaoSeguranca++;
    }

    public void RegistrarTimestepTotp(long timestepAceito)
    {
        if (UltimoTimestepTotpAceito is not null && timestepAceito <= UltimoTimestepTotpAceito)
        {
            throw new InvalidOperationException("O código de autenticação já foi utilizado.");
        }

        UltimoTimestepTotpAceito = timestepAceito;
        MarcarComoAtualizada();
    }

    public void RegistrarLogin(DateTime agoraUtc)
    {
        UltimoLoginEmUtc = agoraUtc;
        MarcarComoAtualizada();
    }

    public void AlterarSenhaHash(string senhaHash)
    {
        SenhaHash = Exigir(senhaHash, nameof(senhaHash), 500);
        VersaoSeguranca++;
        MarcarComoAtualizada();
    }

    public void ResetarMfa()
    {
        MfaHabilitado = false;
        SegredoTotpProtegido = null;
        UltimoTimestepTotpAceito = null;
        VersaoSeguranca++;
        MarcarComoAtualizada();
    }

    public void DesativarComRevogacao()
    {
        if (!EhAtivo)
        {
            return;
        }

        Desativar();
        VersaoSeguranca++;
    }

    private static string NormalizarEmail(string email)
    {
        var normalizado = Exigir(email, nameof(email), 200).ToLowerInvariant();
        if (normalizado.Contains('\r') ||
            normalizado.Contains('\n') ||
            !MailAddress.TryCreate(normalizado, out var endereco) ||
            !string.Equals(endereco.Address, normalizado, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("O e-mail informado é inválido.", nameof(email));
        }

        return normalizado;
    }

    private static string Exigir(string valor, string parametro, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ArgumentException("O valor deve ser informado.", parametro);
        }

        var normalizado = valor.Trim();
        return normalizado.Length <= maximo
            ? normalizado
            : throw new ArgumentException($"O valor deve possuir no máximo {maximo} caracteres.", parametro);
    }
}
