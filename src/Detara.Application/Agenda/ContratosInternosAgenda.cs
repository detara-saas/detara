using Detara.Domain.Agenda;
using Detara.Domain.Catalogo;

namespace Detara.Application.Agenda;

public sealed record ClienteAgendaInterno(Guid Id, string Nome, string? Telefone, bool EhAtivo);
public sealed record VeiculoAgendaInterno(Guid Id, Guid ClienteId, string Descricao, string? Placa, bool EhAtivo);
public sealed record ClienteVeiculoAgendaInterno(ClienteAgendaInterno Cliente, VeiculoAgendaInterno Veiculo);

public interface IClientesAgendaConsulta
{
    Task<ClienteVeiculoAgendaInterno?> ObterClienteVeiculoAsync(Guid empresaId, Guid clienteId, Guid veiculoId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ClienteAgendaInterno>> BuscarClientesAsync(Guid empresaId, string pesquisa, int limite, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<VeiculoAgendaInterno>> ListarVeiculosAsync(Guid empresaId, Guid clienteId, bool incluirInativos, CancellationToken cancellationToken);
}

public sealed record ItemCatalogoAgendaInterno(
    TipoItemAgendamento TipoItem,
    Guid Id,
    string Nome,
    string? Descricao,
    string? Categoria,
    TipoPrecificacao TipoPrecificacao,
    decimal? PrecoReferencia,
    int? DuracaoReferenciaMinutos,
    bool EhAtivo);

public interface ICatalogoAgendaConsulta
{
    Task<IReadOnlyCollection<ItemCatalogoAgendaInterno>> ObterItensAsync(Guid empresaId, IReadOnlyCollection<(TipoItemAgendamento Tipo, Guid Id)> itens, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ItemCatalogoAgendaInterno>> BuscarItensAsync(Guid empresaId, string? pesquisa, bool incluirInativos, int limite, CancellationToken cancellationToken);
}

public interface IFusoHorarioEmpresaConsulta
{
    Task<string?> ObterAsync(Guid empresaId, CancellationToken cancellationToken);
}

public interface IConversorFusoHorario
{
    DateTime ParaUtc(DateTime dataHoraLocal, string fusoHorario);
    DateTime ParaLocal(DateTime dataHoraUtc, string fusoHorario);
}

public sealed class ConversorFusoHorario : IConversorFusoHorario
{
    public DateTime ParaUtc(DateTime dataHoraLocal, string fusoHorario)
    {
        var zona = ObterZona(fusoHorario);
        var local = DateTime.SpecifyKind(dataHoraLocal, DateTimeKind.Unspecified);
        if (zona.IsInvalidTime(local)) throw new ArgumentException("O horário local não existe no fuso da empresa devido à transição de horário.", nameof(dataHoraLocal));
        if (zona.IsAmbiguousTime(local)) throw new ArgumentException("O horário local é ambíguo no fuso da empresa. Escolha outro horário.", nameof(dataHoraLocal));
        return TimeZoneInfo.ConvertTimeToUtc(local, zona);
    }

    public DateTime ParaLocal(DateTime dataHoraUtc, string fusoHorario)
    {
        var utc = DateTime.SpecifyKind(dataHoraUtc, DateTimeKind.Utc);
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(utc, ObterZona(fusoHorario)), DateTimeKind.Unspecified);
    }

    private static TimeZoneInfo ObterZona(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("O fuso horário da empresa deve ser informado.", nameof(id));
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException excecao) { throw new ArgumentException("O fuso horário configurado para a empresa é inválido.", nameof(id), excecao); }
        catch (InvalidTimeZoneException excecao) { throw new ArgumentException("O fuso horário configurado para a empresa é inválido.", nameof(id), excecao); }
    }
}
