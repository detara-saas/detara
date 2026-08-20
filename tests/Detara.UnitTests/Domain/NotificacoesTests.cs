using Detara.Domain.Notificacoes;

namespace Detara.UnitTests.Domain;

public sealed class NotificacoesTests
{
    private static NotificacaoEmail Criar(string? email = "cliente@teste.com") => new(Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), TipoTemplateEmail.VeiculoProntoRetirada, email, "Cliente", "Assunto", "<p>Corpo</p>",
        OrigemTemplateEmail.PadraoDetara, null);

    [Fact] public void ComDestinatario_NascePendente() => Assert.Equal(StatusNotificacaoEmail.Pendente, Criar().Status);
    [Fact] public void SemDestinatario_NasceSemDestinatario() => Assert.Equal(StatusNotificacaoEmail.SemDestinatario, Criar(null).Status);

    [Fact]
    public void Sucesso_MarcaAceitaEPreservaIdProvider()
    { var n = Criar(); n.MarcarProcessando(DateTime.UtcNow); n.RegistrarSucesso("email_123", DateTime.UtcNow, TipoTentativaNotificacaoEmail.Automatica, null); Assert.Equal(StatusNotificacaoEmail.Enviada, n.Status); Assert.Equal("email_123", n.ProvedorMensagemId); Assert.Single(n.Tentativas); }

    [Fact]
    public void FalhaTemporaria_AgendaNovaTentativa()
    { var n = Criar(); var agora = DateTime.UtcNow; n.MarcarProcessando(agora); n.RegistrarFalha("temporária", true, 4, agora, agora.AddMinutes(1), TipoTentativaNotificacaoEmail.Automatica, null); Assert.Equal(StatusNotificacaoEmail.Pendente, n.Status); Assert.NotNull(n.ProximaTentativaEmUtc); }

    [Fact]
    public void QuartaFalha_EncerraRetentativas()
    { var n = Criar(); var agora = DateTime.UtcNow; for (var i = 0; i < 4; i++) { n.MarcarProcessando(agora.AddMinutes(i)); n.RegistrarFalha("erro", true, 4, agora.AddMinutes(i), agora.AddMinutes(i + 1), TipoTentativaNotificacaoEmail.Automatica, null); } Assert.Equal(StatusNotificacaoEmail.Falhou, n.Status); Assert.Equal(4, n.QuantidadeTentativas); Assert.Null(n.ProximaTentativaEmUtc); }

    [Fact]
    public void FalhaTerminal_NaoAgendaRetentativa()
    { var n = Criar(); n.MarcarProcessando(DateTime.UtcNow); n.RegistrarFalha("rejeitada", false, 4, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(1), TipoTentativaNotificacaoEmail.Automatica, null); Assert.Equal(StatusNotificacaoEmail.Falhou, n.Status); Assert.Null(n.ProximaTentativaEmUtc); }

    [Fact]
    public void Enviada_NaoPodeSerReenviada()
    { var n = Criar(); n.MarcarProcessando(DateTime.UtcNow); n.RegistrarSucesso("id", DateTime.UtcNow, TipoTentativaNotificacaoEmail.Automatica, null); Assert.Throws<InvalidOperationException>(() => n.PrepararReenvioManual(null, Guid.NewGuid(), DateTime.UtcNow)); }

    [Fact]
    public void SemDestinatario_PodeReceberEmailAtualNoReenvio()
    { var n = Criar(null); var usuario = Guid.NewGuid(); n.PrepararReenvioManual("novo@teste.com", usuario, DateTime.UtcNow); Assert.Equal(StatusNotificacaoEmail.Pendente, n.Status); Assert.Equal("novo@teste.com", n.DestinatarioEmailSnapshot); Assert.Equal(TipoTentativaNotificacaoEmail.Manual, n.TipoProximaTentativa); Assert.Equal(usuario, n.ProximaTentativaSolicitadaPorUsuarioId); }

    [Fact]
    public void Template_RejeitaAssuntoComQuebraDeLinha() => Assert.Throws<ArgumentException>(() =>
        new TemplateEmailEmpresa(Guid.NewGuid(), TipoTemplateEmail.VeiculoProntoRetirada, "Assunto\r\nBcc:x", "<p>Corpo</p>", Guid.NewGuid()));
}
