using Detara.Domain.Entidades;

namespace Detara.Domain.Atendimento;

public sealed record PartesOrcamentoSnapshot(
    Guid ClienteId,
    string ClienteNome,
    string? ClienteDocumento,
    string? ClienteTelefone,
    Guid VeiculoId,
    string VeiculoDescricao,
    string? VeiculoPlaca);

public sealed class Orcamento : EntidadeEmpresaBase
{
    private readonly List<OrcamentoItem> _itens = [];
    private readonly List<HistoricoStatusOrcamento> _historico = [];
    private Orcamento() { }

    public Orcamento(Guid empresaId, PartesOrcamentoSnapshot partes, Guid? agendamentoOrigemId, Guid? orcamentoOrigemId, DateOnly validoAte,
        string? observacaoCliente, string? observacaoInterna, string? condicoes, decimal desconto, decimal acrescimo,
        IReadOnlyCollection<ItemOrcamentoSnapshot> itens, Guid usuarioId, Guid? ordemServicoOrigemId = null)
        : base(Guid.NewGuid(), empresaId)
    {
        Status = StatusOrcamento.Rascunho;
        AgendamentoOrigemId = ValidarIdOpcional(agendamentoOrigemId);
        AgendamentoId = AgendamentoOrigemId;
        OrcamentoOrigemId = ValidarIdOpcional(orcamentoOrigemId);
        OrdemServicoOrigemId = ValidarIdOpcional(ordemServicoOrigemId);
        if (OrcamentoOrigemId.HasValue && OrdemServicoOrigemId.HasValue)
            throw new ArgumentException("Um orçamento não pode ser simultaneamente substituição e adicional de uma ordem de serviço.");
        AtualizarRascunho(partes, validoAte, observacaoCliente, observacaoInterna, condicoes, desconto, acrescimo, itens);
        RegistrarHistorico(StatusOrcamento.Rascunho, usuarioId, "Orçamento criado.");
    }

    public string? Codigo { get; private set; }
    public Guid ClienteId { get; private set; }
    public string ClienteNomeSnapshot { get; private set; } = string.Empty;
    public string? ClienteDocumentoSnapshot { get; private set; }
    public string? ClienteTelefoneSnapshot { get; private set; }
    public Guid VeiculoId { get; private set; }
    public string VeiculoDescricaoSnapshot { get; private set; } = string.Empty;
    public string? VeiculoPlacaSnapshot { get; private set; }
    public Guid? AgendamentoOrigemId { get; private set; }
    public Guid? AgendamentoId { get; private set; }
    public Guid? OrcamentoOrigemId { get; private set; }
    public Guid? OrdemServicoOrigemId { get; private set; }
    public StatusOrcamento Status { get; private set; }
    public DateOnly ValidoAte { get; private set; }
    public string? ObservacaoCliente { get; private set; }
    public string? ObservacaoInterna { get; private set; }
    public string? Condicoes { get; private set; }
    public decimal Desconto { get; private set; }
    public decimal Acrescimo { get; private set; }
    public DateTime? EmitidoEmUtc { get; private set; }
    public DateTime? AprovadoEmUtc { get; private set; }
    public DateTime? RecusadoEmUtc { get; private set; }
    public DateTime? CanceladoEmUtc { get; private set; }
    public DateTime? SubstituidoEmUtc { get; private set; }
    public Guid? AprovadoPorUsuarioId { get; private set; }
    public IReadOnlyCollection<OrcamentoItem> Itens => _itens;
    public IReadOnlyCollection<HistoricoStatusOrcamento> Historico => _historico;
    public decimal Subtotal => _itens.Sum(x => x.Subtotal);
    public decimal Total => Subtotal - Desconto + Acrescimo;

    public StatusEfetivoOrcamento ObterStatusEfetivo(DateOnly hoje) => Status == StatusOrcamento.Emitido && ValidoAte < hoje
        ? StatusEfetivoOrcamento.Expirado
        : (StatusEfetivoOrcamento)(int)Status;

    public void VincularAgendamento(Guid agendamentoId)
    {
        var id = ExigirId(agendamentoId);
        if (AgendamentoId.HasValue && AgendamentoId.Value != id)
            throw new InvalidOperationException("Este orçamento já está vinculado a outro agendamento.");
        AgendamentoId = id;
        MarcarComoAtualizada();
    }

    public void AtualizarRascunho(PartesOrcamentoSnapshot partes, DateOnly validoAte, string? observacaoCliente, string? observacaoInterna,
        string? condicoes, decimal desconto, decimal acrescimo, IReadOnlyCollection<ItemOrcamentoSnapshot> itens)
    {
        ExigirRascunho();
        ClienteId = ExigirId(partes.ClienteId);
        ClienteNomeSnapshot = NormalizarObrigatorio(partes.ClienteNome, 160);
        ClienteDocumentoSnapshot = NormalizarOpcional(partes.ClienteDocumento, 20);
        ClienteTelefoneSnapshot = NormalizarOpcional(partes.ClienteTelefone, 20);
        VeiculoId = ExigirId(partes.VeiculoId);
        VeiculoDescricaoSnapshot = NormalizarObrigatorio(partes.VeiculoDescricao, 200);
        VeiculoPlacaSnapshot = NormalizarOpcional(partes.VeiculoPlaca, 10);
        ValidoAte = validoAte;
        ObservacaoCliente = NormalizarOpcional(observacaoCliente, 2000);
        ObservacaoInterna = NormalizarOpcional(observacaoInterna, 4000);
        Condicoes = NormalizarOpcional(condicoes, 2000);
        Desconto = ValidarDinheiro(desconto, nameof(desconto));
        Acrescimo = ValidarDinheiro(acrescimo, nameof(acrescimo));
        SubstituirItens(itens);
        if (Total < 0) throw new ArgumentException("O total do orçamento não pode ser negativo.");
        MarcarComoAtualizada();
    }

    public void Emitir(int anoLocal, Guid usuarioId, string? observacao = null)
    {
        ExigirRascunho();
        if (_itens.Count == 0) throw new InvalidOperationException("O orçamento deve possuir ao menos um item.");
        var agora = DateTime.UtcNow;
        Codigo = $"ORC-{anoLocal:D4}-{Id:N}"[..21].ToUpperInvariant();
        Status = StatusOrcamento.Emitido;
        EmitidoEmUtc = agora;
        RegistrarHistorico(Status, usuarioId, observacao, agora);
        MarcarComoAtualizada();
    }

    public void Aprovar(DateOnly hojeLocal, Guid usuarioId, string? observacao)
    {
        if (Status != StatusOrcamento.Emitido) throw new InvalidOperationException("Somente um orçamento emitido pode ser aprovado.");
        if (ValidoAte < hojeLocal) throw new InvalidOperationException("O orçamento está expirado. Crie uma nova proposta com nova validade.");
        var agora = DateTime.UtcNow;
        Status = StatusOrcamento.Aprovado;
        AprovadoEmUtc = agora;
        AprovadoPorUsuarioId = ExigirId(usuarioId);
        RegistrarHistorico(Status, usuarioId, observacao, agora);
        MarcarComoAtualizada();
    }

    public void Recusar(Guid usuarioId, string? observacao) => AlterarEstadoFinal(StatusOrcamento.Recusado, usuarioId, observacao);
    public void Cancelar(Guid usuarioId, string? observacao) => AlterarEstadoFinal(StatusOrcamento.Cancelado, usuarioId, observacao);

    public void MarcarSubstituido(Guid usuarioId, string? observacao)
    {
        if (Status is not (StatusOrcamento.Emitido or StatusOrcamento.Aprovado)) throw new InvalidOperationException("Este orçamento não pode ser marcado como substituído.");
        AlterarEstadoFinal(StatusOrcamento.Substituido, usuarioId, observacao);
    }

    public IReadOnlyCollection<ItemOrcamentoSnapshot> CopiarItens() => _itens.OrderBy(x => x.Ordem).Select(x => x.CriarSnapshot()).ToArray();

    private void AlterarEstadoFinal(StatusOrcamento destino, Guid usuarioId, string? observacao)
    {
        var permitido = destino switch
        {
            StatusOrcamento.Recusado => Status == StatusOrcamento.Emitido,
            StatusOrcamento.Cancelado => Status is StatusOrcamento.Rascunho or StatusOrcamento.Emitido,
            StatusOrcamento.Substituido => Status is StatusOrcamento.Emitido or StatusOrcamento.Aprovado,
            _ => false
        };
        if (!permitido) throw new InvalidOperationException($"A transição de {Status} para {destino} não é permitida.");
        var agora = DateTime.UtcNow;
        Status = destino;
        if (destino == StatusOrcamento.Recusado) RecusadoEmUtc = agora;
        if (destino == StatusOrcamento.Cancelado) CanceladoEmUtc = agora;
        if (destino == StatusOrcamento.Substituido) SubstituidoEmUtc = agora;
        RegistrarHistorico(destino, usuarioId, observacao, agora);
        MarcarComoAtualizada();
    }

    private void SubstituirItens(IReadOnlyCollection<ItemOrcamentoSnapshot> itens)
    {
        if (itens.Count == 0) throw new ArgumentException("O orçamento deve possuir ao menos um item.", nameof(itens));
        var catalogo = itens.Where(x => x.TipoItem != TipoItemOrcamento.Personalizado).Select(x => (x.TipoItem, x.ItemCatalogoId)).ToArray();
        if (catalogo.Any(x => !x.ItemCatalogoId.HasValue) || catalogo.Distinct().Count() != catalogo.Length)
            throw new ArgumentException("Serviços e pacotes não podem se repetir; utilize a quantidade.", nameof(itens));
        _itens.Clear();
        _itens.AddRange(itens.OrderBy(x => x.Ordem).Select((x, i) => new OrcamentoItem(EmpresaId, Id, x with { Ordem = i + 1 })));
    }

    private void RegistrarHistorico(StatusOrcamento status, Guid usuarioId, string? observacao, DateTime? dataUtc = null) =>
        _historico.Add(new HistoricoStatusOrcamento(EmpresaId, Id, status, ExigirId(usuarioId), observacao, dataUtc ?? DateTime.UtcNow));
    private void ExigirRascunho() { if (Status != StatusOrcamento.Rascunho) throw new InvalidOperationException("Este orçamento já foi emitido e não pode ser alterado. Para mudar valores ou serviços, crie uma nova proposta."); }
    private static Guid ExigirId(Guid id) => id != Guid.Empty ? id : throw new ArgumentException("O identificador deve ser informado.");
    private static Guid? ValidarIdOpcional(Guid? id) => id is null || id != Guid.Empty ? id : throw new ArgumentException("O identificador opcional é inválido.");
    private static decimal ValidarDinheiro(decimal valor, string parametro) => valor >= 0 ? decimal.Round(valor, 2) : throw new ArgumentException("O valor não pode ser negativo.", parametro);
    private static string NormalizarObrigatorio(string valor, int limite) { var texto = string.IsNullOrWhiteSpace(valor) ? throw new ArgumentException("O valor deve ser informado.") : valor.Trim(); return texto.Length <= limite ? texto : throw new ArgumentException($"O valor deve possuir no máximo {limite} caracteres."); }
    private static string? NormalizarOpcional(string? valor, int limite) { if (string.IsNullOrWhiteSpace(valor)) return null; var texto = valor.Trim(); return texto.Length <= limite ? texto : throw new ArgumentException($"O valor deve possuir no máximo {limite} caracteres."); }
}
