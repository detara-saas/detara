namespace Detara.Domain.Entidades;

public sealed class Cliente : EntidadeEmpresaBase
{
    private readonly List<Veiculo> _veiculos = [];

    private Cliente()
    {
    }

    public Cliente(
        Guid empresaId,
        string nome,
        TipoPessoa tipoPessoa,
        string? cpfCnpj,
        string? telefone,
        string? whatsApp,
        string? email,
        DateOnly? dataNascimento,
        string? observacao)
        : base(Guid.NewGuid(), empresaId)
    {
        Atualizar(nome, tipoPessoa, cpfCnpj, telefone, whatsApp, email, dataNascimento, observacao);
    }

    public string Nome { get; private set; } = string.Empty;
    public TipoPessoa TipoPessoa { get; private set; }
    public string? CpfCnpj { get; private set; }
    public string? Telefone { get; private set; }
    public string? WhatsApp { get; private set; }
    public string? Email { get; private set; }
    public DateOnly? DataNascimento { get; private set; }
    public string? Observacao { get; private set; }
    public IReadOnlyCollection<Veiculo> Veiculos => _veiculos;

    public void Atualizar(
        string nome,
        TipoPessoa tipoPessoa,
        string? cpfCnpj,
        string? telefone,
        string? whatsApp,
        string? email,
        DateOnly? dataNascimento,
        string? observacao)
    {
        Nome = Exigir(nome, 160, nameof(nome));
        TipoPessoa = Enum.IsDefined(tipoPessoa)
            ? tipoPessoa
            : throw new ArgumentException("O tipo de pessoa é inválido.", nameof(tipoPessoa));
        CpfCnpj = DocumentoFiscal.Normalizar(cpfCnpj);
        if ((!string.IsNullOrWhiteSpace(cpfCnpj) && CpfCnpj is null) ||
            (CpfCnpj is not null && !DocumentoFiscal.EhValido(CpfCnpj, TipoPessoa)))
        {
            throw new ArgumentException("O CPF/CNPJ informado é inválido.", nameof(cpfCnpj));
        }

        Telefone = NormalizarTelefone(telefone, nameof(telefone));
        WhatsApp = NormalizarTelefone(whatsApp, nameof(whatsApp));
        Email = NormalizarOpcional(email, 200)?.ToLowerInvariant();
        DataNascimento = TipoPessoa == TipoPessoa.PessoaFisica ? dataNascimento : null;
        if (DataNascimento > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ArgumentException("A data de nascimento não pode estar no futuro.", nameof(dataNascimento));
        }

        Observacao = NormalizarOpcional(observacao, 2000);
        MarcarComoAtualizada();
    }

    private static string Exigir(string valor, int limite, string parametro)
    {
        var normalizado = string.IsNullOrWhiteSpace(valor)
            ? throw new ArgumentException("O nome deve ser informado.", parametro)
            : valor.Trim();
        return normalizado.Length <= limite
            ? normalizado
            : throw new ArgumentException($"O valor deve possuir no máximo {limite} caracteres.", parametro);
    }

    private static string? NormalizarOpcional(string? valor, int limite)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var normalizado = valor.Trim();
        return normalizado.Length <= limite
            ? normalizado
            : throw new ArgumentException($"O valor deve possuir no máximo {limite} caracteres.");
    }

    private static string? NormalizarTelefone(string? valor, string parametro)
    {
        var normalizado = DocumentoFiscal.Normalizar(valor);
        if (!string.IsNullOrWhiteSpace(valor) && normalizado is null)
        {
            throw new ArgumentException("O telefone deve conter dígitos.", parametro);
        }

        return normalizado is null or { Length: >= 8 and <= 15 }
            ? normalizado
            : throw new ArgumentException("O telefone deve possuir entre 8 e 15 dígitos.", parametro);
    }
}
