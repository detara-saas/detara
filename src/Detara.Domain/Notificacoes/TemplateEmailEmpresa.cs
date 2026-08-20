using Detara.Domain.Entidades;

namespace Detara.Domain.Notificacoes;

public sealed class TemplateEmailEmpresa : EntidadeEmpresaBase
{
    private TemplateEmailEmpresa() { }

    public TemplateEmailEmpresa(Guid empresaId, TipoTemplateEmail tipo, string assunto,
        string corpoHtmlSanitizado, Guid usuarioId) : base(Guid.NewGuid(), empresaId)
    {
        if (!Enum.IsDefined(tipo)) throw new ArgumentException("O tipo do template é inválido.", nameof(tipo));
        Tipo = tipo;
        CriadoPorUsuarioId = usuarioId;
        Atualizar(assunto, corpoHtmlSanitizado, usuarioId);
    }

    public TipoTemplateEmail Tipo { get; private set; }
    public string Assunto { get; private set; } = string.Empty;
    public string CorpoHtmlSanitizado { get; private set; } = string.Empty;
    public Guid CriadoPorUsuarioId { get; private set; }
    public Guid AtualizadoPorUsuarioId { get; private set; }

    public void Atualizar(string assunto, string corpoHtmlSanitizado, Guid usuarioId)
    {
        if (usuarioId == Guid.Empty) throw new ArgumentException("O usuário deve ser informado.", nameof(usuarioId));
        var assuntoNormalizado = assunto?.Trim() ?? string.Empty;
        if (assuntoNormalizado.Length is < 1 or > 200 || assuntoNormalizado.IndexOfAny(['\r', '\n']) >= 0)
            throw new ArgumentException("O assunto deve possuir entre 1 e 200 caracteres e não pode conter quebras de linha.", nameof(assunto));
        if (string.IsNullOrWhiteSpace(corpoHtmlSanitizado) || corpoHtmlSanitizado.Length > 50 * 1024)
            throw new ArgumentException("O corpo do e-mail deve ser informado e possuir no máximo 50 KB.", nameof(corpoHtmlSanitizado));
        Assunto = assuntoNormalizado;
        CorpoHtmlSanitizado = corpoHtmlSanitizado;
        AtualizadoPorUsuarioId = usuarioId;
        MarcarComoAtualizada();
    }
}
