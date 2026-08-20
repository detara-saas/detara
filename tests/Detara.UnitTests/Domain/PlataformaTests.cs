using Detara.Domain.Plataforma;

namespace Detara.UnitTests.Domain;

public sealed class PlataformaTests
{
    [Fact]
    public void AdministradorPlataforma_NormalizaIdentidadeESegregaTenant()
    {
        var administrador = new AdministradorPlataforma(
            "  Operador da Plataforma  ",
            "  ADMIN@DETARA.COM.BR ",
            "hash-seguro");

        Assert.Equal("Operador da Plataforma", administrador.Nome);
        Assert.Equal("admin@detara.com.br", administrador.Email);
        Assert.Equal("ADMIN@DETARA.COM.BR", administrador.EmailNormalizado);
        Assert.False(administrador.MfaHabilitado);
        Assert.DoesNotContain(
            administrador.GetType().GetProperties(),
            propriedade => propriedade.Name.Equals("EmpresaId", StringComparison.Ordinal));
    }

    [Fact]
    public void AdministradorPlataforma_RejeitaReplayTotpERevogaSessoesAoResetarMfa()
    {
        var administrador = new AdministradorPlataforma("Admin", "admin@detara.com.br", "hash");
        administrador.DefinirSegredoTotpProtegido("segredo-protegido");
        administrador.AtivarMfa(100);
        var versaoAtivacao = administrador.VersaoSeguranca;

        Assert.Throws<InvalidOperationException>(() => administrador.RegistrarTimestepTotp(100));
        Assert.Throws<InvalidOperationException>(() => administrador.RegistrarTimestepTotp(99));

        administrador.RegistrarTimestepTotp(101);
        administrador.ResetarMfa();

        Assert.False(administrador.MfaHabilitado);
        Assert.Null(administrador.SegredoTotpProtegido);
        Assert.Null(administrador.UltimoTimestepTotpAceito);
        Assert.True(administrador.VersaoSeguranca > versaoAtivacao);
    }

    [Fact]
    public void CodigoRecuperacao_EUsoUnico()
    {
        var codigo = new CodigoRecuperacaoAdministradorPlataforma(Guid.NewGuid(), "hash");

        codigo.MarcarUtilizado(DateTime.UtcNow);

        Assert.False(codigo.Disponivel);
        Assert.Throws<InvalidOperationException>(() => codigo.MarcarUtilizado(DateTime.UtcNow));
    }

    [Fact]
    public void Convite_ExpiraETokenAntigoEInvalidadoNoReenvio()
    {
        var agora = DateTime.UtcNow;
        var convite = CriarConvite(agora);
        var envioEm = convite.ProximaTentativaEnvioEmUtc!.Value.AddMilliseconds(1);
        convite.IniciarEnvio("hash-original", envioEm.AddHours(24), envioEm);
        convite.RegistrarEnvio("provider-id", envioEm);

        Assert.True(convite.PodeSerAceito("hash-original", agora.AddMinutes(1)));
        Assert.False(convite.PodeSerAceito("hash-original", agora.AddDays(2)));

        convite.PrepararReenvio(agora.AddMinutes(2), Guid.NewGuid());

        Assert.Null(convite.TokenHash);
        Assert.False(convite.PodeSerAceito("hash-original", agora.AddMinutes(3)));
        Assert.Equal(StatusConviteAdministradorEmpresa.Pendente, convite.Status);
    }

    [Fact]
    public void Convite_AceitoNaoPodeSerReutilizado()
    {
        var agora = DateTime.UtcNow;
        var convite = CriarConvite(agora);
        var envioEm = convite.ProximaTentativaEnvioEmUtc!.Value.AddMilliseconds(1);
        convite.IniciarEnvio("hash", envioEm.AddHours(24), envioEm);
        convite.RegistrarEnvio("provider-id", envioEm);
        convite.MarcarAceito(envioEm.AddMinutes(1));

        Assert.Equal(StatusConviteAdministradorEmpresa.Aceito, convite.Status);
        Assert.Null(convite.TokenHash);
        Assert.False(convite.PodeSerAceito("hash", agora.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() =>
            convite.PrepararReenvio(agora.AddMinutes(3), Guid.NewGuid()));
    }

    private static ConviteAdministradorEmpresa CriarConvite(DateTime agora)
    {
        var convite = new ConviteAdministradorEmpresa(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "ADMIN@EMPRESA.COM.BR",
            Guid.NewGuid());

        // O construtor usa o relógio do domínio apenas para agendar a primeira tentativa.
        Assert.True(convite.ProximaTentativaEnvioEmUtc <= agora.AddSeconds(1));
        return convite;
    }
}
