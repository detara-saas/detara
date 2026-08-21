using Detara.Application.Abstracoes;
using Detara.Application.Autenticacao;
using Detara.Infrastructure.Autenticacao;
using Microsoft.AspNetCore.DataProtection;

namespace Detara.IntegrationTests.Autenticacao;

public sealed class ChallengeSelecaoEmpresaTenantTests
{
    [Fact]
    public void ChallengeValido_PreservaSomenteMembershipsAutorizadas()
    {
        var servico = CriarServico(TimeSpan.FromMinutes(5));
        var memberships = CriarMemberships();

        var criado = servico.Criar(memberships);
        var recuperadas = servico.Validar(criado.Valor);

        Assert.Equal(memberships, recuperadas);
        Assert.InRange(
            criado.ExpiraEmUtc,
            DateTime.UtcNow.AddMinutes(4).AddSeconds(50),
            DateTime.UtcNow.AddMinutes(5).AddSeconds(1));
    }

    [Fact]
    public void ChallengeAlterado_EhRejeitadoComErroGenerico()
    {
        var servico = CriarServico(TimeSpan.FromMinutes(5));
        var criado = servico.Criar(CriarMemberships());
        var alterado = criado.Valor[..^1] + (criado.Valor[^1] == 'A' ? "B" : "A");

        var exception = Assert.Throws<ChallengeSelecaoEmpresaInvalidoException>(
            () => servico.Validar(alterado));

        Assert.Equal("Não foi possível concluir a seleção. Faça login novamente.", exception.Message);
    }

    [Fact]
    public async Task ChallengeExpirado_EhRejeitado()
    {
        var servico = CriarServico(TimeSpan.FromMilliseconds(20));
        var criado = servico.Criar(CriarMemberships());
        await Task.Delay(80);

        Assert.Throws<ChallengeSelecaoEmpresaInvalidoException>(() => servico.Validar(criado.Valor));
    }

    [Fact]
    public void UmaUnicaMembership_NaoGeraChallenge()
    {
        var servico = CriarServico(TimeSpan.FromMinutes(5));

        Assert.Throws<InvalidOperationException>(() => servico.Criar([CriarMemberships()[0]]));
    }

    private static ChallengeSelecaoEmpresaTenant CriarServico(TimeSpan validade)
    {
        var provider = new EphemeralDataProtectionProvider();
        return new ChallengeSelecaoEmpresaTenant(provider, validade);
    }

    private static MembershipLoginTenantAutorizada[] CriarMemberships() =>
    [
        new(Guid.NewGuid(), Guid.NewGuid(), 10, 1, 20),
        new(Guid.NewGuid(), Guid.NewGuid(), 11, 1, 21)
    ];
}
