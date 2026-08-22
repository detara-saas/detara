using Detara.Domain.Entidades;

namespace Detara.Domain.Atendimento;

public sealed record PartesOrdemServicoSnapshot(
    Guid ClienteId,
    string ClienteNome,
    string? ClienteDocumento,
    string? ClienteTelefone,
    Guid VeiculoId,
    string VeiculoDescricao,
    string VeiculoPlaca);

public sealed record ConfiguracaoCheckInSnapshot(
    NivelExigenciaOperacional ChecklistEntrada,
    NivelExigenciaOperacional FotosEntrada,
    NivelExigenciaOperacional FotosSaida,
    string? ChecklistNome,
    IReadOnlyCollection<string> ChecklistItens);

public sealed class OrdemServico : EntidadeEmpresaBase
{
    private readonly List<OrdemServicoItem> _itens = [];
    private readonly List<OrdemServicoFoto> _fotos = [];
    private readonly List<HistoricoStatusOrdemServico> _historico = [];
    private OrdemServico() { }

    public OrdemServico(Guid empresaId, int anoLocal, PartesOrdemServicoSnapshot partes,
        OrigemOrdemServico origem, Guid? orcamentoOrigemId, Guid? agendamentoOrigemId,
        int? duracaoPlanejadaMinutos, decimal descontoAutorizado, decimal acrescimoAutorizado,
        IReadOnlyCollection<ItemOrdemServicoSnapshot> itens, Guid usuarioId,
        DateTime? autorizacaoDiretaEmUtc = null, string? observacaoAutorizacaoDireta = null)
        : base(Guid.NewGuid(), empresaId)
    {
        if (itens.Count == 0) throw new ArgumentException("A ordem de serviço deve possuir ao menos um item autorizado.", nameof(itens));
        Origem = Enum.IsDefined(origem) ? origem : throw new ArgumentException("A origem da ordem de serviço é inválida.", nameof(origem));
        OrcamentoOrigemId = ValidarIdOpcional(orcamentoOrigemId);
        AgendamentoOrigemId = ValidarIdOpcional(agendamentoOrigemId);
        if (Origem == OrigemOrdemServico.Orcamento && !OrcamentoOrigemId.HasValue || Origem != OrigemOrdemServico.Orcamento && OrcamentoOrigemId.HasValue)
            throw new ArgumentException("A origem por orçamento é inconsistente.", nameof(orcamentoOrigemId));
        if (Origem == OrigemOrdemServico.Agendamento && !AgendamentoOrigemId.HasValue || Origem != OrigemOrdemServico.Agendamento && AgendamentoOrigemId.HasValue)
            throw new ArgumentException("A origem por agendamento é inconsistente.", nameof(agendamentoOrigemId));

        ClienteId = ExigirId(partes.ClienteId);
        ClienteNomeSnapshot = NormalizarObrigatorio(partes.ClienteNome, 160);
        ClienteDocumentoSnapshot = NormalizarOpcional(partes.ClienteDocumento, 20);
        ClienteTelefoneSnapshot = NormalizarOpcional(partes.ClienteTelefone, 20);
        VeiculoId = ExigirId(partes.VeiculoId);
        VeiculoDescricaoSnapshot = NormalizarObrigatorio(partes.VeiculoDescricao, 200);
        VeiculoPlacaSnapshot = NormalizarObrigatorio(partes.VeiculoPlaca, 10);
        DuracaoPlanejadaMinutos = duracaoPlanejadaMinutos is null or > 0 and <= 43200
            ? duracaoPlanejadaMinutos : throw new ArgumentException("A duração planejada é inválida.", nameof(duracaoPlanejadaMinutos));
        DescontoAutorizado = ValidarDinheiro(descontoAutorizado, nameof(descontoAutorizado));
        AcrescimoAutorizado = ValidarDinheiro(acrescimoAutorizado, nameof(acrescimoAutorizado));
        _itens.AddRange(itens.OrderBy(item => item.Ordem).Select((item, indice) =>
            new OrdemServicoItem(empresaId, Id, item with { Ordem = indice + 1 })));
        if (TotalAutorizado < 0) throw new ArgumentException("O total autorizado não pode ser negativo.");

        if (Origem != OrigemOrdemServico.Orcamento)
        {
            AutorizacaoDiretaEmUtc = autorizacaoDiretaEmUtc is { Kind: DateTimeKind.Utc }
                ? autorizacaoDiretaEmUtc : throw new ArgumentException("A autorização direta deve registrar data UTC.", nameof(autorizacaoDiretaEmUtc));
            AutorizacaoDiretaPorUsuarioId = ExigirId(usuarioId);
            ObservacaoAutorizacaoDireta = NormalizarOpcional(observacaoAutorizacaoDireta, 1000);
        }

        Codigo = $"OS-{anoLocal:D4}-{Id:N}"[..20].ToUpperInvariant();
        Status = StatusOrdemServico.Aberta;
        RegistrarHistorico(Status, usuarioId, "Ordem de serviço criada.");
    }

    public string Codigo { get; private set; } = string.Empty;
    public OrigemOrdemServico Origem { get; private set; }
    public Guid? OrcamentoOrigemId { get; private set; }
    public Guid? AgendamentoOrigemId { get; private set; }
    public Guid ClienteId { get; private set; }
    public string ClienteNomeSnapshot { get; private set; } = string.Empty;
    public string? ClienteDocumentoSnapshot { get; private set; }
    public string? ClienteTelefoneSnapshot { get; private set; }
    public Guid VeiculoId { get; private set; }
    public string VeiculoDescricaoSnapshot { get; private set; } = string.Empty;
    public string VeiculoPlacaSnapshot { get; private set; } = string.Empty;
    public int? DuracaoPlanejadaMinutos { get; private set; }
    public StatusOrdemServico Status { get; private set; }
    public decimal DescontoAutorizado { get; private set; }
    public decimal AcrescimoAutorizado { get; private set; }
    public DateTime? AutorizacaoDiretaEmUtc { get; private set; }
    public Guid? AutorizacaoDiretaPorUsuarioId { get; private set; }
    public string? ObservacaoAutorizacaoDireta { get; private set; }
    public DateTime? CheckInEmUtc { get; private set; }
    public Guid? CheckInPorUsuarioId { get; private set; }
    public int? QuilometragemEntrada { get; private set; }
    public string? ObservacaoEntrada { get; private set; }
    public NivelExigenciaOperacional? ChecklistEntradaSnapshot { get; private set; }
    public NivelExigenciaOperacional? FotosEntradaSnapshot { get; private set; }
    public NivelExigenciaOperacional? FotosSaidaSnapshot { get; private set; }
    public OrdemServicoChecklist? Checklist { get; private set; }
    public DateTime? IniciadaEmUtc { get; private set; }
    public Guid? IniciadaPorUsuarioId { get; private set; }
    public DateTime? ExecucaoFinalizadaEmUtc { get; private set; }
    public Guid? ExecucaoFinalizadaPorUsuarioId { get; private set; }
    public DateTime? ConcluidaEmUtc { get; private set; }
    public Guid? ConcluidaPorUsuarioId { get; private set; }
    public DateTime? CanceladaEmUtc { get; private set; }
    public Guid? CanceladaPorUsuarioId { get; private set; }
    public string? MotivoCancelamento { get; private set; }
    public IReadOnlyCollection<OrdemServicoItem> Itens => _itens;
    public IReadOnlyCollection<OrdemServicoFoto> Fotos => _fotos;
    public IReadOnlyCollection<HistoricoStatusOrdemServico> Historico => _historico;
    public decimal SubtotalAutorizado => _itens.Sum(item => item.Subtotal);
    public decimal TotalAutorizado => SubtotalAutorizado - DescontoAutorizado + AcrescimoAutorizado;

    public void RealizarCheckIn(ConfiguracaoCheckInSnapshot configuracao, int? quilometragemEntrada,
        string? observacaoEntrada, Guid usuarioId)
    {
        ExigirStatus(StatusOrdemServico.Aberta);
        if (CheckInEmUtc.HasValue) throw new InvalidOperationException("O check-in desta ordem de serviço já foi realizado.");
        if (quilometragemEntrada < 0) throw new ArgumentException("A quilometragem de entrada não pode ser negativa.", nameof(quilometragemEntrada));
        ChecklistEntradaSnapshot = ValidarNivel(configuracao.ChecklistEntrada);
        FotosEntradaSnapshot = ValidarNivel(configuracao.FotosEntrada);
        FotosSaidaSnapshot = ValidarNivel(configuracao.FotosSaida);
        if (ChecklistEntradaSnapshot != NivelExigenciaOperacional.Desabilitado)
        {
            if (configuracao.ChecklistItens.Count == 0) throw new InvalidOperationException("O checklist habilitado não possui itens para o snapshot.");
            Checklist = new OrdemServicoChecklist(EmpresaId, Id,
                configuracao.ChecklistNome ?? ChecklistModelo.NomePadrao, configuracao.ChecklistItens);
        }
        QuilometragemEntrada = quilometragemEntrada;
        ObservacaoEntrada = NormalizarOpcional(observacaoEntrada, 2000);
        CheckInEmUtc = DateTime.UtcNow;
        CheckInPorUsuarioId = ExigirId(usuarioId);
        MarcarComoAtualizada();
    }

    public void AtualizarChecklist(IReadOnlyCollection<RespostaChecklistSnapshot> respostas)
    {
        ExigirMutavel();
        if (Checklist is null) throw new InvalidOperationException("Esta ordem de serviço não possui checklist habilitado.");
        Checklist.Atualizar(respostas);
        MarcarComoAtualizada();
    }

    public void ValidarInclusaoFoto(CategoriaFotoOrdemServico categoria)
    {
        ExigirMutavel();
        if (!Enum.IsDefined(categoria))
            throw new ArgumentException("A categoria da foto é inválida.", nameof(categoria));
        if (!CheckInEmUtc.HasValue) throw new InvalidOperationException("Realize o check-in antes de anexar fotos à ordem de serviço.");
        if (categoria == CategoriaFotoOrdemServico.Entrada && Status != StatusOrdemServico.Aberta)
            throw new InvalidOperationException("Fotos de entrada só podem ser anexadas antes do início da execução.");
        if (categoria is CategoriaFotoOrdemServico.Durante or CategoriaFotoOrdemServico.Saida && Status != StatusOrdemServico.EmExecucao)
            throw new InvalidOperationException("Fotos durante a execução ou de saída exigem uma ordem em execução.");
        if (categoria == CategoriaFotoOrdemServico.Entrada && FotosEntradaSnapshot == NivelExigenciaOperacional.Desabilitado ||
            categoria == CategoriaFotoOrdemServico.Saida && FotosSaidaSnapshot == NivelExigenciaOperacional.Desabilitado)
            throw new InvalidOperationException("Esta categoria de foto está desabilitada no snapshot do check-in.");
    }

    public void AdicionarFoto(OrdemServicoFoto foto)
    {
        ValidarInclusaoFoto(foto.Categoria);
        if (foto.EmpresaId != EmpresaId || foto.OrdemServicoId != Id) throw new InvalidOperationException("A foto não pertence a esta ordem de serviço.");
        _fotos.Add(foto);
        MarcarComoAtualizada();
    }

    public OrdemServicoFoto RemoverFoto(Guid fotoId)
    {
        ExigirMutavel();
        var foto = _fotos.SingleOrDefault(item => item.Id == fotoId) ?? throw new InvalidOperationException("Foto não encontrada.");
        if (foto.Categoria == CategoriaFotoOrdemServico.Entrada && Status != StatusOrdemServico.Aberta ||
            foto.Categoria is CategoriaFotoOrdemServico.Durante or CategoriaFotoOrdemServico.Saida && Status != StatusOrdemServico.EmExecucao)
            throw new InvalidOperationException("A foto não pode mais ser removida nesta etapa.");
        _fotos.Remove(foto);
        MarcarComoAtualizada();
        return foto;
    }

    public void IniciarExecucao(
        Guid usuarioId,
        string? observacao,
        bool checkInObrigatorio = true)
    {
        ExigirStatus(StatusOrdemServico.Aberta);
        if (!CheckInEmUtc.HasValue && checkInObrigatorio)
            throw new InvalidOperationException("Realize o check-in antes de iniciar a execução.");
        if (ChecklistEntradaSnapshot == NivelExigenciaOperacional.Obrigatorio && Checklist is { EstaCompleto: false })
            throw new InvalidOperationException("Responda todos os itens obrigatórios do checklist antes de iniciar.");
        if (FotosEntradaSnapshot == NivelExigenciaOperacional.Obrigatorio && !_fotos.Any(foto => foto.Categoria == CategoriaFotoOrdemServico.Entrada))
            throw new InvalidOperationException("Anexe ao menos uma foto de entrada antes de iniciar.");
        IniciadaEmUtc = DateTime.UtcNow;
        IniciadaPorUsuarioId = ExigirId(usuarioId);
        AlterarStatus(StatusOrdemServico.EmExecucao, usuarioId, observacao);
    }

    public void FinalizarExecucao(Guid usuarioId, string? observacao)
    {
        ExigirStatus(StatusOrdemServico.EmExecucao);
        if (FotosSaidaSnapshot == NivelExigenciaOperacional.Obrigatorio && !_fotos.Any(foto => foto.Categoria == CategoriaFotoOrdemServico.Saida))
            throw new InvalidOperationException("Anexe ao menos uma foto de saída antes de finalizar a execução.");
        ExecucaoFinalizadaEmUtc = DateTime.UtcNow;
        ExecucaoFinalizadaPorUsuarioId = ExigirId(usuarioId);
        AlterarStatus(StatusOrdemServico.AguardandoRetirada, usuarioId, observacao);
    }

    public void Concluir(Guid usuarioId, string? observacao)
    {
        ExigirStatus(StatusOrdemServico.AguardandoRetirada);
        ConcluidaEmUtc = DateTime.UtcNow;
        ConcluidaPorUsuarioId = ExigirId(usuarioId);
        AlterarStatus(StatusOrdemServico.Concluida, usuarioId, observacao);
    }

    public void Cancelar(Guid usuarioId, string motivo)
    {
        if (Status is not (StatusOrdemServico.Aberta or StatusOrdemServico.EmExecucao))
            throw new InvalidOperationException("Somente uma ordem aberta ou em execução pode ser cancelada.");
        MotivoCancelamento = NormalizarObrigatorio(motivo, 1000);
        CanceladaEmUtc = DateTime.UtcNow;
        CanceladaPorUsuarioId = ExigirId(usuarioId);
        AlterarStatus(StatusOrdemServico.Cancelada, usuarioId, MotivoCancelamento);
    }

    public bool IncorporarOrcamentoAdicional(Guid orcamentoId, decimal desconto, decimal acrescimo,
        IReadOnlyCollection<ItemOrdemServicoSnapshot> itens)
    {
        ExigirMutavel();
        if (itens.Count == 0 || itens.Any(item => item.OrcamentoOrigemId != orcamentoId || !item.OrcamentoItemOrigemId.HasValue))
            throw new ArgumentException("Os itens adicionais devem preservar sua origem no orçamento.", nameof(itens));
        var ids = itens.Select(item => item.OrcamentoItemOrigemId!.Value).ToHashSet();
        var existentes = _itens.Count(item => item.OrcamentoItemOrigemId.HasValue && ids.Contains(item.OrcamentoItemOrigemId.Value));
        if (existentes == itens.Count) return false;
        if (existentes != 0) throw new InvalidOperationException("A incorporação anterior do orçamento adicional ficou inconsistente.");
        var proximaOrdem = _itens.Count + 1;
        _itens.AddRange(itens.OrderBy(item => item.Ordem).Select((item, indice) =>
            new OrdemServicoItem(EmpresaId, Id, item with { Ordem = proximaOrdem + indice })));
        DescontoAutorizado += ValidarDinheiro(desconto, nameof(desconto));
        AcrescimoAutorizado += ValidarDinheiro(acrescimo, nameof(acrescimo));
        MarcarComoAtualizada();
        return true;
    }

    public void AdicionarCortesia(ItemOrdemServicoSnapshot item)
    {
        ExigirStatus(StatusOrdemServico.EmExecucao);
        if (item.OrigemComercial != OrigemComercialOrdemServico.Cortesia || item.ValorUnitarioAutorizado != 0)
            throw new ArgumentException("Somente itens gratuitos com origem Cortesia podem ser adicionados diretamente durante a execução.", nameof(item));
        _itens.Add(new OrdemServicoItem(EmpresaId, Id, item with { Ordem = _itens.Count + 1 }));
        MarcarComoAtualizada();
    }

    private void AlterarStatus(StatusOrdemServico status, Guid usuarioId, string? observacao)
    {
        Status = status;
        RegistrarHistorico(status, usuarioId, observacao);
        MarcarComoAtualizada();
    }
    private void RegistrarHistorico(StatusOrdemServico status, Guid usuarioId, string? observacao) =>
        _historico.Add(new HistoricoStatusOrdemServico(EmpresaId, Id, status, ExigirId(usuarioId), observacao, DateTime.UtcNow));
    private void ExigirStatus(StatusOrdemServico status)
    {
        if (Status != status) throw new InvalidOperationException($"A ordem de serviço deve estar com status {status}.");
    }
    private void ExigirMutavel()
    {
        if (Status is StatusOrdemServico.Concluida or StatusOrdemServico.Cancelada)
            throw new InvalidOperationException("Ordens concluídas ou canceladas são históricas e não podem ser alteradas.");
    }
    private static NivelExigenciaOperacional ValidarNivel(NivelExigenciaOperacional nivel) => Enum.IsDefined(nivel)
        ? nivel : throw new ArgumentException("O nível de exigência é inválido.");
    private static Guid ExigirId(Guid id) => id != Guid.Empty ? id : throw new ArgumentException("O identificador deve ser informado.");
    private static Guid? ValidarIdOpcional(Guid? id) => id is null || id != Guid.Empty ? id : throw new ArgumentException("O identificador opcional é inválido.");
    private static decimal ValidarDinheiro(decimal valor, string parametro) => valor >= 0 ? decimal.Round(valor, 2) : throw new ArgumentException("O valor não pode ser negativo.", parametro);
    private static string NormalizarObrigatorio(string valor, int limite)
    {
        var texto = string.IsNullOrWhiteSpace(valor) ? throw new ArgumentException("O valor deve ser informado.") : valor.Trim();
        return texto.Length <= limite ? texto : throw new ArgumentException($"O valor deve possuir no máximo {limite} caracteres.");
    }
    private static string? NormalizarOpcional(string? valor, int limite)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var texto = valor.Trim();
        return texto.Length <= limite ? texto : throw new ArgumentException($"O valor deve possuir no máximo {limite} caracteres.");
    }
}
