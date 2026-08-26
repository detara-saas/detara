using Detara.Domain.Entidades;

namespace Detara.Domain.Notificacoes;

public sealed class TemplateComunicacaoEmpresa : EntidadeEmpresaBase
{
    private TemplateComunicacaoEmpresa() { }

    public TemplateComunicacaoEmpresa(Guid empresaId, CanalComunicacaoCliente canal,
        TipoTemplateComunicacao tipo, string nome, string? assunto,
        string conteudo, Guid usuarioId) : base(Guid.NewGuid(), empresaId)
    {
        if (!Enum.IsDefined(canal))
            throw new ArgumentException("O canal do template é inválido.", nameof(canal));
        if (!Enum.IsDefined(tipo))
            throw new ArgumentException("O tipo do template é inválido.", nameof(tipo));
        Canal = canal;
        Tipo = tipo;
        CriadoPorUsuarioId = usuarioId;
        Atualizar(nome, assunto, conteudo, usuarioId);
    }

    public CanalComunicacaoCliente Canal { get; private set; }
    public TipoTemplateComunicacao Tipo { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string? Assunto { get; private set; }
    public string Conteudo { get; private set; } = string.Empty;
    public Guid CriadoPorUsuarioId { get; private set; }
    public Guid AtualizadoPorUsuarioId { get; private set; }

    public void Atualizar(string nome, string? assunto, string conteudo, Guid usuarioId)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("O usuário deve ser informado.", nameof(usuarioId));
        var nomeNormalizado = nome?.Trim() ?? string.Empty;
        if (nomeNormalizado.Length is < 1 or > 160)
            throw new ArgumentException("O nome do template deve possuir entre 1 e 160 caracteres.", nameof(nome));

        var conteudoNormalizado = conteudo?.Trim() ?? string.Empty;
        if (Canal == CanalComunicacaoCliente.Email)
        {
            var assuntoNormalizado = assunto?.Trim() ?? string.Empty;
            if (assuntoNormalizado.Length is < 1 or > 200 ||
                assuntoNormalizado.IndexOfAny(['\r', '\n']) >= 0)
                throw new ArgumentException(
                    "O assunto deve possuir entre 1 e 200 caracteres e não pode conter quebras de linha.",
                    nameof(assunto));
            if (conteudoNormalizado.Length is < 1 or > 50 * 1024)
                throw new ArgumentException(
                    "O corpo do e-mail deve ser informado e possuir no máximo 50 KB.",
                    nameof(conteudo));
            Assunto = assuntoNormalizado;
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(assunto))
                throw new ArgumentException("O template de WhatsApp não possui assunto.", nameof(assunto));
            if (conteudoNormalizado.Length is < 1 or > 4096)
                throw new ArgumentException(
                    "A mensagem de WhatsApp deve possuir entre 1 e 4096 caracteres.",
                    nameof(conteudo));
            Assunto = null;
        }

        Nome = nomeNormalizado;
        Conteudo = conteudoNormalizado;
        AtualizadoPorUsuarioId = usuarioId;
        MarcarComoAtualizada();
    }
}
