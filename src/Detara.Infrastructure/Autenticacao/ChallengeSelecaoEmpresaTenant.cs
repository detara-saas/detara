using System.Security.Cryptography;
using System.Text.Json;
using Detara.Application.Abstracoes;
using Detara.Application.Autenticacao;
using Microsoft.AspNetCore.DataProtection;

namespace Detara.Infrastructure.Autenticacao;

internal sealed class ChallengeSelecaoEmpresaTenant : IChallengeSelecaoEmpresaTenant
{
    private const string Purpose = "Detara.Tenant.LoginEmpresaChallenge.v1";
    private static readonly TimeSpan ValidadePadrao = TimeSpan.FromMinutes(5);
    private readonly ITimeLimitedDataProtector _protetor;
    private readonly TimeSpan _validade;

    public ChallengeSelecaoEmpresaTenant(IDataProtectionProvider dataProtectionProvider)
        : this(dataProtectionProvider, ValidadePadrao)
    {
    }

    internal ChallengeSelecaoEmpresaTenant(
        IDataProtectionProvider dataProtectionProvider,
        TimeSpan validade)
    {
        _validade = validade;
        _protetor = dataProtectionProvider
            .CreateProtector(Purpose)
            .ToTimeLimitedDataProtector();
    }

    public ChallengeSelecaoEmpresaCriado Criar(
        IReadOnlyCollection<MembershipLoginTenantAutorizada> memberships)
    {
        if (memberships.Count < 2 ||
            memberships.Any(EhInvalida) ||
            memberships.Select(item => item.EmpresaId).Distinct().Count() != memberships.Count)
        {
            throw new InvalidOperationException(
                "O challenge exige memberships válidas e distintas.");
        }

        var payload = new ChallengePayload(1, memberships.ToArray());
        var valor = _protetor.Protect(JsonSerializer.Serialize(payload), _validade);
        return new ChallengeSelecaoEmpresaCriado(valor, DateTime.UtcNow.Add(_validade));
    }

    public IReadOnlyCollection<MembershipLoginTenantAutorizada> Validar(string challenge)
    {
        try
        {
            var json = _protetor.Unprotect(challenge, out _);
            var payload = JsonSerializer.Deserialize<ChallengePayload>(json);
            if (payload is null ||
                payload.Versao != 1 ||
                payload.Memberships.Length < 2 ||
                payload.Memberships.Any(EhInvalida) ||
                payload.Memberships.Select(item => item.EmpresaId).Distinct().Count() !=
                payload.Memberships.Length)
            {
                throw new ChallengeSelecaoEmpresaInvalidoException();
            }

            return payload.Memberships;
        }
        catch (Exception exception) when (
            exception is CryptographicException or JsonException or FormatException)
        {
            throw new ChallengeSelecaoEmpresaInvalidoException();
        }
    }

    private static bool EhInvalida(MembershipLoginTenantAutorizada item) =>
        item.UsuarioId == Guid.Empty ||
        item.EmpresaId == Guid.Empty ||
        item.EmpresaVersaoSeguranca <= 0;

    private sealed record ChallengePayload(
        int Versao,
        MembershipLoginTenantAutorizada[] Memberships);
}
