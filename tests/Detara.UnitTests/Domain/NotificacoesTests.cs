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
    public void Enviada_NaoPodeReceberRetry()
    { var n = Criar(); n.MarcarProcessando(DateTime.UtcNow); n.RegistrarSucesso("id", DateTime.UtcNow, TipoTentativaNotificacaoEmail.Automatica, null); Assert.Throws<InvalidOperationException>(() => n.PrepararNovaTentativaManual(null, Guid.NewGuid(), DateTime.UtcNow)); }

    [Fact]
    public void SemDestinatario_PodeReceberEmailAtualNoReenvio()
    { var n = Criar(null); var usuario = Guid.NewGuid(); n.PrepararNovaTentativaManual("novo@teste.com", usuario, DateTime.UtcNow); Assert.Equal(StatusNotificacaoEmail.Pendente, n.Status); Assert.Equal("novo@teste.com", n.DestinatarioEmailSnapshot); Assert.Equal(TipoTentativaNotificacaoEmail.Manual, n.TipoProximaTentativa); Assert.Equal(usuario, n.ProximaTentativaSolicitadaPorUsuarioId); }

    [Fact]
    public void EnvioManual_NascePendenteComUsuarioSolicitante()
    {
        var usuario = Guid.NewGuid();
        var notificacao = new NotificacaoEmail(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), TipoTemplateEmail.VeiculoProntoRetirada, "cliente@teste.com",
            "Cliente", "Assunto", "<p>Corpo</p>", OrigemTemplateEmail.PadraoDetara,
            null, TipoTentativaNotificacaoEmail.Manual, usuario);

        Assert.Equal(StatusNotificacaoEmail.Pendente, notificacao.Status);
        Assert.Equal(TipoTentativaNotificacaoEmail.Manual, notificacao.TipoProximaTentativa);
        Assert.Equal(usuario, notificacao.ProximaTentativaSolicitadaPorUsuarioId);
    }

    [Fact]
    public void Template_RejeitaAssuntoComQuebraDeLinha() => Assert.Throws<ArgumentException>(() =>
        new TemplateEmailEmpresa(Guid.NewGuid(), TipoTemplateEmail.VeiculoProntoRetirada, "Assunto\r\nBcc:x", "<p>Corpo</p>", Guid.NewGuid()));

    [Fact]
    public void Configuracao_RejeitaCanalAutomaticoInvalido() =>
        Assert.Throws<ArgumentException>(() => new ConfiguracaoNotificacaoEmpresa(
            Guid.NewGuid(), (CanalComunicacaoVeiculoPronto)99, null, Guid.NewGuid()));

    [Fact]
    public void Configuracao_AtivacaoWhatsAppRegistraAuditoria()
    {
        var usuarioId = Guid.NewGuid();
        var configuracao = new ConfiguracaoNotificacaoEmpresa(Guid.NewGuid(),
            CanalComunicacaoVeiculoPronto.WhatsApp, true, null, usuarioId);

        Assert.True(configuracao.PermitirComunicacaoWhatsApp);
        Assert.Equal(usuarioId, configuracao.UsuarioAtivacaoWhatsAppId);
        Assert.NotNull(configuracao.DataAtivacaoWhatsAppEmUtc);
    }

    [Fact]
    public void ComunicacaoWhatsApp_ComDestinatarioNascePendente()
    {
        var comunicacao = new ComunicacaoCliente(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), CanalComunicacaoCliente.WhatsApp,
            TipoComunicacaoCliente.VeiculoPronto, "Mensagem", "11999998888",
            OrigemComunicacaoCliente.Manual, Guid.NewGuid());

        Assert.Equal(StatusComunicacaoCliente.Pendente, comunicacao.Status);
        Assert.Equal(CanalComunicacaoCliente.WhatsApp, comunicacao.Canal);
    }

    [Fact]
    public void ComunicacaoSemDestinatario_RegistraFalha()
    {
        var comunicacao = new ComunicacaoCliente(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), CanalComunicacaoCliente.Email,
            TipoComunicacaoCliente.VeiculoPronto, "Mensagem", null,
            OrigemComunicacaoCliente.Automatica, null);

        Assert.Equal(StatusComunicacaoCliente.Falhou, comunicacao.Status);
        Assert.Contains("e-mail", comunicacao.UltimoErroSeguro);
    }

    [Fact]
    public void TesteWhatsApp_NaoInventaClienteOuOrdemServico()
    {
        var comunicacao = ComunicacaoCliente.CriarTesteWhatsApp(Guid.NewGuid(),
            Guid.NewGuid(), "Mensagem de teste", "5541999990000", Guid.NewGuid());

        Assert.Equal(TipoComunicacaoCliente.TesteWhatsApp, comunicacao.Tipo);
        Assert.Null(comunicacao.ClienteId);
        Assert.Null(comunicacao.OrdemServicoId);
        Assert.Equal(StatusComunicacaoCliente.Pendente, comunicacao.Status);
    }

    [Fact]
    public void Comunicacao_ProcessadaComSucessoRegistraDataEnvio()
    {
        var comunicacao = new ComunicacaoCliente(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), CanalComunicacaoCliente.Email,
            TipoComunicacaoCliente.VeiculoPronto, "Mensagem", "cliente@teste.com",
            OrigemComunicacaoCliente.Automatica, null);
        var agora = DateTime.UtcNow;

        comunicacao.MarcarProcessando(agora);
        comunicacao.RegistrarEnvio("provider-id", agora);

        Assert.Equal(StatusComunicacaoCliente.Enviado, comunicacao.Status);
        Assert.Equal(agora, comunicacao.DataEnvioUtc);
    }

    [Fact]
    public void SessaoWhatsApp_UsaChaveIsoladaEAtualizaEstadoSeguro()
    {
        var empresaId = Guid.NewGuid();
        var ultimaConexao = DateTime.UtcNow;
        var sessao = new SessaoWhatsAppEmpresa(empresaId, $"tenant-{empresaId:N}");

        sessao.AtualizarStatus(StatusSessaoWhatsApp.Conectada, ultimaConexao,
            numeroConectado: "5541999990000");

        Assert.Equal(empresaId, sessao.EmpresaId);
        Assert.Equal($"tenant-{empresaId:N}", sessao.SessionKey);
        Assert.Equal(StatusSessaoWhatsApp.Conectada, sessao.Status);
        Assert.Equal(ultimaConexao, sessao.UltimaConexaoEmUtc);
        Assert.Equal("5541999990000", sessao.NumeroConectado);
        Assert.Null(sessao.UltimoErroSeguro);
    }

    [Fact]
    public void SessaoWhatsApp_StatusIdenticoNaoIncrementaVersao()
    {
        var empresaId = Guid.NewGuid();
        var sessao = new SessaoWhatsAppEmpresa(empresaId, $"tenant-{empresaId:N}");
        var versao = sessao.Versao;

        sessao.AtualizarStatus(StatusSessaoWhatsApp.Desconectada, null);

        Assert.Equal(versao, sessao.Versao);
    }

    [Theory]
    [InlineData("curta")]
    [InlineData("tenant/com/barra")]
    [InlineData("tenant com espaco")]
    public void SessaoWhatsApp_RejeitaChaveInsegura(string sessionKey) =>
        Assert.Throws<ArgumentException>(() =>
            new SessaoWhatsAppEmpresa(Guid.NewGuid(), sessionKey));
}
