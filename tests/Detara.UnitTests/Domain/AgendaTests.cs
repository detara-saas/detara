using Detara.Domain.Agenda;
using Detara.Domain.Catalogo;
using Detara.Domain.Entidades;

namespace Detara.UnitTests.Domain;

public sealed class AgendaTests
{
    [Fact]
    public void Workflow_PermiteTransicoesOperacionaisEProtegeEstadoFinal()
    {
        var agendamento = CriarAgendamento();

        agendamento.AlterarStatus(StatusAgendamento.Confirmado);
        agendamento.AlterarStatus(StatusAgendamento.Compareceu);
        agendamento.AlterarStatus(StatusAgendamento.Concluido);

        Assert.Equal(StatusAgendamento.Concluido, agendamento.Status);
        Assert.Throws<InvalidOperationException>(() => agendamento.AlterarStatus(StatusAgendamento.Cancelado));
        Assert.Throws<InvalidOperationException>(() => agendamento.Reagendar(DateTime.UtcNow.AddDays(1), 60));
    }

    [Fact]
    public void Workflow_NaoPermitePularDeConfirmadoParaConcluido()
    {
        var agendamento = CriarAgendamento();
        agendamento.AlterarStatus(StatusAgendamento.Confirmado);

        Assert.Throws<InvalidOperationException>(() => agendamento.AlterarStatus(StatusAgendamento.Concluido));
    }

    [Fact]
    public void Cancelamento_PreservaMotivoNormalizado()
    {
        var agendamento = CriarAgendamento();

        agendamento.AlterarStatus(StatusAgendamento.Cancelado, "  Cliente desistiu  ");

        Assert.Equal(StatusAgendamento.Cancelado, agendamento.Status);
        Assert.Equal("Cliente desistiu", agendamento.MotivoCancelamento);
    }

    [Fact]
    public void SnapshotCatalogo_NaoMudaQuandoServicoEAtualizado()
    {
        var empresaId = Guid.NewGuid();
        var servico = new Servico(empresaId, Guid.NewGuid(), "Lavagem técnica", "Original", TipoPrecificacao.APartirDe, 100m, 90, 1);
        var agendamento = CriarAgendamento(empresaId, new(TipoItemAgendamento.Servico, servico.Id, servico.Nome, servico.Descricao, servico.TipoPrecificacao, servico.PrecoBase, servico.DuracaoEstimadaMinutos));

        servico.Atualizar(servico.CategoriaServicoId, "Lavagem premium", "Alterada", TipoPrecificacao.APartirDe, 120m, 120, 1);

        var snapshot = Assert.Single(agendamento.Itens);
        Assert.Equal("Lavagem técnica", snapshot.NomeSnapshot);
        Assert.Equal(100m, snapshot.PrecoReferenciaSnapshot);
        Assert.Equal(90, snapshot.DuracaoReferenciaMinutosSnapshot);
        Assert.Equal(90, agendamento.DuracaoPlanejadaMinutos);
    }

    [Fact]
    public void SnapshotClienteVeiculo_NaoMudaQuandoCadastrosSaoAtualizados()
    {
        var empresaId = Guid.NewGuid();
        var cliente = new Cliente(empresaId, "João da Silva", TipoPessoa.PessoaFisica, null, null, null, null, null, null);
        var veiculo = new Veiculo(empresaId, cliente.Id, "ABC1D23", "Honda", "Civic", null, 2024, 2024, "Preto", null, null);
        var agendamento = new Agendamento(empresaId, cliente.Id, cliente.Nome, veiculo.Id, $"{veiculo.Marca} {veiculo.Modelo}", veiculo.Placa, DateTime.UtcNow.AddDays(1), 90, null, null, [ItemPadrao()]);

        cliente.Atualizar("João Atualizado", TipoPessoa.PessoaFisica, null, null, null, null, null, null);
        veiculo.Atualizar(cliente.Id, "XYZ9Z99", "Toyota", "Corolla", null, 2025, 2025, "Prata", null, null);

        Assert.Equal("João da Silva", agendamento.ClienteNomeSnapshot);
        Assert.Equal("Honda Civic", agendamento.VeiculoDescricaoSnapshot);
        Assert.Equal("ABC1D23", agendamento.VeiculoPlacaSnapshot);
    }

    private static Agendamento CriarAgendamento() => CriarAgendamento(Guid.NewGuid(), ItemPadrao());
    private static Agendamento CriarAgendamento(Guid empresaId, ItemAgendamentoSnapshot item) => new(empresaId, Guid.NewGuid(), "Cliente", Guid.NewGuid(), "Honda Civic", "ABC1D23", DateTime.UtcNow.AddDays(1), 90, null, null, [item]);
    private static ItemAgendamentoSnapshot ItemPadrao() => new(TipoItemAgendamento.Servico, Guid.NewGuid(), "Lavagem", null, TipoPrecificacao.Fixo, 100m, 90);
}
