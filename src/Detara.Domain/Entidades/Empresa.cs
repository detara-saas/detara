namespace Detara.Domain.Entidades;

public sealed class Empresa : EntidadeBase
{
    private Empresa()
    {
    }

    public Empresa(
        string nomeFantasia,
        string razaoSocial,
        string cpfCnpj,
        string slug,
        string? email = null,
        string? telefone = null,
        string fusoHorario = "America/Sao_Paulo",
        string segmentoCodigo = Capacidades.SegmentosEmpresa.EsteticaAutomotiva)
        : base(Guid.NewGuid())
    {
        NomeFantasia = Exigir(nomeFantasia, nameof(nomeFantasia));
        RazaoSocial = Exigir(razaoSocial, nameof(razaoSocial));
        CpfCnpj = Exigir(cpfCnpj, nameof(cpfCnpj));
        Slug = NormalizarSlug(slug);
        Email = NormalizarOpcional(email);
        Telefone = NormalizarOpcional(telefone);
        FusoHorario = Exigir(fusoHorario, nameof(fusoHorario));
        if (!Capacidades.SegmentosEmpresa.EhConhecido(segmentoCodigo))
            throw new ArgumentException("O segmento informado não é suportado.", nameof(segmentoCodigo));
        SegmentoCodigo = segmentoCodigo;
    }

    public string NomeFantasia { get; private set; } = string.Empty;
    public string RazaoSocial { get; private set; } = string.Empty;
    public string CpfCnpj { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Telefone { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string FusoHorario { get; private set; } = "America/Sao_Paulo";
    public string SegmentoCodigo { get; private set; } = Capacidades.SegmentosEmpresa.EsteticaAutomotiva;
    public long VersaoSeguranca { get; private set; } = 1;
    public long VersaoCadastro { get; private set; } = 1;
    public string? LogoArquivoChave { get; private set; }
    public long LogoVersao { get; private set; }
    public DateTime? LogoAtualizadaEmUtc { get; private set; }
    public Guid? LogoTokenPublico { get; private set; }

    public void DefinirLogo(string arquivoChave)
    {
        LogoArquivoChave = Exigir(arquivoChave, nameof(arquivoChave));
        LogoVersao++;
        LogoAtualizadaEmUtc = DateTime.UtcNow;
        LogoTokenPublico = Guid.NewGuid();
        MarcarComoAtualizada();
    }

    public void RemoverLogo()
    {
        if (LogoArquivoChave is null) return;
        LogoArquivoChave = null;
        LogoVersao++;
        LogoAtualizadaEmUtc = DateTime.UtcNow;
        LogoTokenPublico = null;
        MarcarComoAtualizada();
    }

    public void AtualizarCadastro(
        string nomeFantasia,
        string razaoSocial,
        string cpfCnpj,
        string? email,
        string? telefone,
        string fusoHorario,
        long versaoEsperada)
    {
        if (versaoEsperada != VersaoCadastro)
        {
            throw new InvalidOperationException("Os dados da empresa foram atualizados por outra operação.");
        }

        NomeFantasia = Exigir(nomeFantasia, nameof(nomeFantasia));
        RazaoSocial = Exigir(razaoSocial, nameof(razaoSocial));
        CpfCnpj = Exigir(cpfCnpj, nameof(cpfCnpj));
        Email = NormalizarOpcional(email)?.ToLowerInvariant();
        Telefone = NormalizarOpcional(telefone);
        FusoHorario = Exigir(fusoHorario, nameof(fusoHorario));
        VersaoCadastro++;
        MarcarComoAtualizada();
    }

    public void AlterarFusoHorario(string fusoHorario)
    {
        FusoHorario = Exigir(fusoHorario, nameof(fusoHorario));
        MarcarComoAtualizada();
    }

    public void Suspender()
    {
        if (!EhAtivo)
        {
            return;
        }

        Desativar();
        VersaoSeguranca++;
    }

    public void Reativar()
    {
        if (EhAtivo)
        {
            return;
        }

        Ativar();
        VersaoSeguranca++;
    }

    private static string Exigir(string valor, string parametro) =>
        string.IsNullOrWhiteSpace(valor)
            ? throw new ArgumentException("O valor deve ser informado.", parametro)
            : valor.Trim();

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string NormalizarSlug(string valor)
    {
        var slug = Exigir(valor, nameof(valor)).ToLowerInvariant();
        if (slug.Length > 63 ||
            slug[0] == '-' ||
            slug[^1] == '-' ||
            slug.Any(caractere => caractere is not (>= 'a' and <= 'z')
                and not (>= '0' and <= '9')
                and not '-'))
        {
            throw new ArgumentException(
                "O slug deve ser um rótulo DNS válido com até 63 caracteres.",
                nameof(valor));
        }

        return slug;
    }
}
