using Detara.Domain.Atendimento;

namespace Detara.UnitTests.Domain;

public sealed class OrdemServicoTests
{
    private readonly Guid _empresaId = Guid.NewGuid();
    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly Guid _servicoId = Guid.NewGuid();

    [Fact]
    public void Criacao_PreservaTotalAutorizadoCodigoEHistorico()
    {
        var ordem = Criar(valor: 250, desconto: 50, acrescimo: 10);

        Assert.Equal(210, ordem.TotalAutorizado);
        Assert.StartsWith("OS-2026-", ordem.Codigo);
        Assert.Equal(StatusOrdemServico.Aberta, ordem.Status);
        Assert.Equal(StatusOrdemServico.Aberta, Assert.Single(ordem.Historico).Status);
    }

    [Fact]
    public void CheckIn_FazSnapshotIndependenteDoModeloOriginal()
    {
        var itensModelo = new List<string> { "Pintura", "Vidros" };
        var ordem = Criar();
        ordem.RealizarCheckIn(new(NivelExigenciaOperacional.Opcional,
            NivelExigenciaOperacional.Desabilitado, NivelExigenciaOperacional.Desabilitado,
            "Entrada", itensModelo), 12000, "Risco na porta", _usuarioId);
        itensModelo[0] = "Alterado depois";

        Assert.Equal(12000, ordem.QuilometragemEntrada);
        Assert.Equal(["Pintura", "Vidros"], ordem.Checklist!.Itens.OrderBy(item => item.Ordem)
            .Select(item => item.DescricaoSnapshot));
    }

    [Fact]
    public void ChecklistObrigatorio_IncompletoBloqueiaInicio()
    {
        var ordem = Criar();
        ordem.RealizarCheckIn(new(NivelExigenciaOperacional.Obrigatorio,
            NivelExigenciaOperacional.Desabilitado, NivelExigenciaOperacional.Desabilitado,
            "Entrada", ["Pintura", "Vidros"]), null, null, _usuarioId);
        var primeiro = ordem.Checklist!.Itens.First();
        ordem.AtualizarChecklist([new(primeiro.Id, RespostaChecklistOrdemServico.Conforme, null)]);

        Assert.Throws<InvalidOperationException>(() => ordem.IniciarExecucao(_usuarioId, null));
    }

    [Fact]
    public void ChecklistObrigatorio_NaoConformeEhRespostaValida()
    {
        var ordem = Criar();
        ordem.RealizarCheckIn(new(NivelExigenciaOperacional.Obrigatorio,
            NivelExigenciaOperacional.Desabilitado, NivelExigenciaOperacional.Desabilitado,
            "Entrada", ["Pintura"]), null, null, _usuarioId);
        var item = Assert.Single(ordem.Checklist!.Itens);
        ordem.AtualizarChecklist([new(item.Id, RespostaChecklistOrdemServico.NaoConforme, "Risco lateral")]);

        ordem.IniciarExecucao(_usuarioId, null);

        Assert.Equal(StatusOrdemServico.EmExecucao, ordem.Status);
    }

    [Fact]
    public void FotoEntradaObrigatoria_BloqueiaInicioAteAnexoValido()
    {
        var ordem = Criar();
        ordem.RealizarCheckIn(new(NivelExigenciaOperacional.Desabilitado,
            NivelExigenciaOperacional.Obrigatorio, NivelExigenciaOperacional.Desabilitado,
            null, []), null, null, _usuarioId);
        Assert.Throws<InvalidOperationException>(() => ordem.IniciarExecucao(_usuarioId, null));

        ordem.AdicionarFoto(Foto(ordem, CategoriaFotoOrdemServico.Entrada));
        ordem.IniciarExecucao(_usuarioId, null);

        Assert.Equal(StatusOrdemServico.EmExecucao, ordem.Status);
    }

    [Fact]
    public void FotoEntradaOpcional_PermiteInicioSemAnexo()
    {
        var ordem = Criar();
        ordem.RealizarCheckIn(new(NivelExigenciaOperacional.Desabilitado,
            NivelExigenciaOperacional.Opcional, NivelExigenciaOperacional.Desabilitado,
            null, []), null, null, _usuarioId);

        ordem.IniciarExecucao(_usuarioId, null);

        Assert.Equal(StatusOrdemServico.EmExecucao, ordem.Status);
    }

    [Fact]
    public void CheckInObrigatorio_SemCheckIn_BloqueiaInicio()
    {
        var ordem = Criar();

        var excecao = Assert.Throws<InvalidOperationException>(() =>
            ordem.IniciarExecucao(_usuarioId, null, checkInObrigatorio: true));

        Assert.Equal("Realize o check-in antes de iniciar a execução.", excecao.Message);
    }

    [Fact]
    public void CheckInObrigatorio_ComCheckIn_PermiteInicio()
    {
        var ordem = Criar();
        ordem.RealizarCheckIn(new(
            NivelExigenciaOperacional.Desabilitado,
            NivelExigenciaOperacional.Desabilitado,
            NivelExigenciaOperacional.Desabilitado,
            null,
            []), null, null, _usuarioId);

        ordem.IniciarExecucao(_usuarioId, null, checkInObrigatorio: true);

        Assert.Equal(StatusOrdemServico.EmExecucao, ordem.Status);
    }

    [Fact]
    public void CheckInOpcional_SemCheckIn_PermiteInicio()
    {
        var ordem = Criar();

        ordem.IniciarExecucao(_usuarioId, null, checkInObrigatorio: false);

        Assert.Equal(StatusOrdemServico.EmExecucao, ordem.Status);
        Assert.Null(ordem.CheckInEmUtc);
    }

    [Fact]
    public void CheckInOpcional_ComCheckIn_PermiteInicio()
    {
        var ordem = Criar();
        ordem.RealizarCheckIn(new(
            NivelExigenciaOperacional.Opcional,
            NivelExigenciaOperacional.Opcional,
            NivelExigenciaOperacional.Opcional,
            "Entrada",
            ["Pintura"]), null, null, _usuarioId);

        ordem.IniciarExecucao(_usuarioId, null, checkInObrigatorio: false);

        Assert.Equal(StatusOrdemServico.EmExecucao, ordem.Status);
        Assert.NotNull(ordem.CheckInEmUtc);
    }

    [Fact]
    public void FotoComCategoriaInvalida_EhRejeitadaAntesDoArmazenamento()
    {
        var ordem = Criar();
        ordem.RealizarCheckIn(new(NivelExigenciaOperacional.Desabilitado,
            NivelExigenciaOperacional.Opcional, NivelExigenciaOperacional.Opcional, null, []),
            null, null, _usuarioId);

        Assert.Throws<ArgumentException>(() =>
            ordem.ValidarInclusaoFoto((CategoriaFotoOrdemServico)999));
    }

    [Fact]
    public void FotoSaidaObrigatoria_BloqueiaFinalizacaoAteAnexoValido()
    {
        var ordem = CriarEmExecucao(fotosSaida: NivelExigenciaOperacional.Obrigatorio);
        Assert.Throws<InvalidOperationException>(() => ordem.FinalizarExecucao(_usuarioId, null));

        ordem.AdicionarFoto(Foto(ordem, CategoriaFotoOrdemServico.Saida));
        ordem.FinalizarExecucao(_usuarioId, null);

        Assert.Equal(StatusOrdemServico.AguardandoRetirada, ordem.Status);
    }

    [Fact]
    public void FluxoCompleto_RegistraHistoricoENaoPermiteReabertura()
    {
        var ordem = CriarEmExecucao();
        ordem.FinalizarExecucao(_usuarioId, "Pronto");
        ordem.Concluir(_usuarioId, "Veículo entregue");

        Assert.Equal([StatusOrdemServico.Aberta, StatusOrdemServico.EmExecucao,
            StatusOrdemServico.AguardandoRetirada, StatusOrdemServico.Concluida],
            ordem.Historico.Select(item => item.Status));
        Assert.Throws<InvalidOperationException>(() => ordem.IniciarExecucao(_usuarioId, null));
        Assert.Throws<InvalidOperationException>(() => ordem.AtualizarChecklist([]));
    }

    [Fact]
    public void Cancelamento_PreservaEvidenciasEBloqueiaMutacaoPosterior()
    {
        var ordem = Criar();
        ordem.RealizarCheckIn(new(NivelExigenciaOperacional.Desabilitado,
            NivelExigenciaOperacional.Opcional, NivelExigenciaOperacional.Desabilitado, null, []), null, null, _usuarioId);
        var foto = Foto(ordem, CategoriaFotoOrdemServico.Entrada);
        ordem.AdicionarFoto(foto);
        ordem.Cancelar(_usuarioId, "Cliente desistiu");

        Assert.Contains(foto, ordem.Fotos);
        Assert.Throws<InvalidOperationException>(() => ordem.RemoverFoto(foto.Id));
    }

    [Fact]
    public void OrcamentoAdicionalAprovado_IncrementaTotalUmaUnicaVez()
    {
        var ordem = CriarEmExecucao(valor: 160);
        var orcamentoId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var item = Item(80, OrigemComercialOrdemServico.Orcamento, orcamentoId, itemId);

        Assert.True(ordem.IncorporarOrcamentoAdicional(orcamentoId, 10, 0, [item]));
        Assert.False(ordem.IncorporarOrcamentoAdicional(orcamentoId, 10, 0, [item]));
        Assert.Equal(230, ordem.TotalAutorizado);
        Assert.Equal(2, ordem.Itens.Count);
    }

    [Fact]
    public void Cortesia_ComValorZeroEhPermitida_EValorCobradoEhRejeitado()
    {
        var ordem = CriarEmExecucao();
        ordem.AdicionarCortesia(Item(0, OrigemComercialOrdemServico.Cortesia));

        Assert.Equal(2, ordem.Itens.Count);
        Assert.Throws<ArgumentException>(() => ordem.AdicionarCortesia(
            Item(10, OrigemComercialOrdemServico.Cortesia)));
    }

    private OrdemServico Criar(decimal valor = 100, decimal desconto = 0, decimal acrescimo = 0) =>
        new(_empresaId, 2026, new(Guid.NewGuid(), "João", "52998224725", "11999999999",
            Guid.NewGuid(), "Honda Civic", "ABC1D23"), OrigemOrdemServico.AtendimentoDireto,
            null, null, null, desconto, acrescimo,
            [Item(valor, OrigemComercialOrdemServico.AcordoDireto)], _usuarioId, DateTime.UtcNow,
            "Cliente autorizou presencialmente.");

    private OrdemServico CriarEmExecucao(decimal valor = 100,
        NivelExigenciaOperacional fotosSaida = NivelExigenciaOperacional.Desabilitado)
    {
        var ordem = Criar(valor);
        ordem.RealizarCheckIn(new(NivelExigenciaOperacional.Desabilitado,
            NivelExigenciaOperacional.Desabilitado, fotosSaida, null, []), null, null, _usuarioId);
        ordem.IniciarExecucao(_usuarioId, null);
        return ordem;
    }

    private ItemOrdemServicoSnapshot Item(decimal valor, OrigemComercialOrdemServico origem,
        Guid? orcamentoId = null, Guid? orcamentoItemId = null) => new(TipoItemOrcamento.Servico,
        _servicoId, orcamentoId, orcamentoItemId, "Lavagem técnica", "Detalhamento", valor, 1, 1,
        origem, DateTime.UtcNow, _usuarioId, null);

    private OrdemServicoFoto Foto(OrdemServico ordem, CategoriaFotoOrdemServico categoria) =>
        new(_empresaId, ordem.Id, categoria, $"privado/{Guid.NewGuid():N}.jpg", "foto.jpg",
            "image/jpeg", 100, _usuarioId);
}
