using Detara.Domain.Atendimento;
using Detara.Domain.Catalogo;

namespace Detara.UnitTests.Domain;

public sealed class OrcamentoTests
{
    private readonly Guid _empresaId = Guid.NewGuid();
    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly Guid _servicoId = Guid.NewGuid();

    [Fact]
    public void Total_EhDerivadoDeQuantidadeDescontoEAcrescimo()
    {
        var orcamento = Criar(valor: 80m, quantidade: 2, desconto: 15m, acrescimo: 5m);
        Assert.Equal(160m, orcamento.Subtotal);
        Assert.Equal(150m, orcamento.Total);
    }

    [Fact]
    public void TotalNegativo_EhRejeitado()
    {
        Assert.Throws<ArgumentException>(() => Criar(valor: 10m, desconto: 11m));
    }

    [Fact]
    public void ItemPersonalizado_NaoExigeCatalogo()
    {
        var item = new ItemOrcamentoSnapshot(TipoItemOrcamento.Personalizado, null, "Remoção intensiva de barro", null,
            null, null, 60m, 1, 1, null);
        var orcamento = new Orcamento(_empresaId, Partes(), null, null, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),
            null, null, null, 0, 0, [item], _usuarioId);
        Assert.Null(Assert.Single(orcamento.Itens).ItemCatalogoId);
        Assert.Equal(60m, orcamento.Total);
    }

    [Fact]
    public void CatalogoDuplicado_EhRejeitado()
    {
        var item = Item(100m, 1);
        Assert.Throws<ArgumentException>(() => new Orcamento(_empresaId, Partes(), null, null,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7), null, null, null, 0, 0, [item, item with { Ordem = 2 }], _usuarioId));
    }

    [Fact]
    public void Emitido_NaoPodeSerEditado()
    {
        var orcamento = Criar(160m);
        orcamento.Emitir(2026, _usuarioId);
        var excecao = Assert.Throws<InvalidOperationException>(() => orcamento.AtualizarRascunho(Partes(), orcamento.ValidoAte,
            null, null, null, 0, 0, [Item(210m, 1)]));
        Assert.Contains("nova proposta", excecao.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(160m, orcamento.Total);
    }

    [Fact]
    public void Emitir_GeraCodigoEstavelEHistorico()
    {
        var orcamento = Criar(160m);
        orcamento.Emitir(2026, _usuarioId, "Apresentado presencialmente.");
        Assert.StartsWith("ORC-2026-", orcamento.Codigo);
        Assert.Equal(StatusOrcamento.Emitido, orcamento.Status);
        Assert.Equal([StatusOrcamento.Rascunho, StatusOrcamento.Emitido], orcamento.Historico.Select(x => x.Status));
    }

    [Fact]
    public void Expirado_EhCalculadoSemAlterarStatusPersistido_EAprovacaoEhBloqueada()
    {
        var ontem = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var orcamento = new Orcamento(_empresaId, Partes(), null, null, ontem, null, null, null, 0, 0, [Item(160m, 1)], _usuarioId);
        orcamento.Emitir(2026, _usuarioId);
        Assert.Equal(StatusEfetivoOrcamento.Expirado, orcamento.ObterStatusEfetivo(ontem.AddDays(1)));
        Assert.Equal(StatusOrcamento.Emitido, orcamento.Status);
        Assert.Throws<InvalidOperationException>(() => orcamento.Aprovar(ontem.AddDays(1), _usuarioId, null));
    }

    [Fact]
    public void Recusado_NaoPodeVirarSubstituido()
    {
        var orcamento = Criar(160m); orcamento.Emitir(2026, _usuarioId); orcamento.Recusar(_usuarioId, "Cliente recusou.");
        Assert.Throws<InvalidOperationException>(() => orcamento.MarcarSubstituido(_usuarioId, null));
        Assert.Equal(StatusOrcamento.Recusado, orcamento.Status);
    }

    [Fact]
    public void AprovadoPodeSerSubstituido_EHistoricoPreservaAprovacao()
    {
        var orcamento = Criar(160m); orcamento.Emitir(2026, _usuarioId); orcamento.Aprovar(orcamento.ValidoAte, _usuarioId, null); orcamento.MarcarSubstituido(_usuarioId, "Nova proposta emitida.");
        Assert.Equal(StatusOrcamento.Substituido, orcamento.Status);
        Assert.Equal([StatusOrcamento.Rascunho, StatusOrcamento.Emitido, StatusOrcamento.Aprovado, StatusOrcamento.Substituido], orcamento.Historico.Select(x => x.Status));
    }

    [Fact]
    public void Cortesia_ComValorZero_EhPermitida()
    {
        Assert.Equal(0m, Criar(0m).Total);
    }

    private Orcamento Criar(decimal valor = 100m, int quantidade = 1, decimal desconto = 0, decimal acrescimo = 0) =>
        new(_empresaId, Partes(), null, null, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), "Cliente", "Interna", "À vista",
            desconto, acrescimo, [Item(valor, quantidade)], _usuarioId);
    private PartesOrcamentoSnapshot Partes() => new(Guid.NewGuid(), "João da Silva", "52998224725", "11999999999", Guid.NewGuid(), "Honda Civic", "ABC1D23");
    private ItemOrcamentoSnapshot Item(decimal valor, int quantidade) => new(TipoItemOrcamento.Servico, _servicoId,
        "Lavagem técnica", "Avaliação prévia", TipoPrecificacao.APartirDe, 100m, valor, quantidade, 1, null);
}
