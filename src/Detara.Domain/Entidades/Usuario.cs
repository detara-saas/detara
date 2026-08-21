namespace Detara.Domain.Entidades;

public sealed class Usuario : EntidadeEmpresaBase
{
    private Usuario()
    {
    }

    public Usuario(Guid empresaId, Guid perfilId, string nome, string email, string senhaHash)
        : base(Guid.NewGuid(), empresaId)
    {
        PerfilId = perfilId == Guid.Empty
            ? throw new ArgumentException("O perfil deve ser informado.", nameof(perfilId))
            : perfilId;
        Nome = Exigir(nome, nameof(nome));
        Email = Exigir(email, nameof(email)).ToLowerInvariant();
        SenhaHash = Exigir(senhaHash, nameof(senhaHash));
    }

    public Guid PerfilId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string SenhaHash { get; private set; } = string.Empty;
    public long Versao { get; private set; } = 1;
    public long VersaoSeguranca { get; private set; } = 1;
    public Perfil Perfil { get; private set; } = null!;
    public Empresa Empresa { get; private set; } = null!;

    public void AlterarNome(string nome, long versaoEsperada)
    {
        ExigirVersao(versaoEsperada);
        Nome = Exigir(nome, nameof(nome));
        Versao++;
        MarcarComoAtualizada();
    }

    public void AlterarEmail(string email, long versaoEsperada)
    {
        ExigirVersao(versaoEsperada);
        Email = NormalizarEmail(email);
        IncrementarVersoes();
    }

    public void AlterarPerfil(Guid perfilId, long versaoEsperada)
    {
        ExigirVersao(versaoEsperada);
        PerfilId = perfilId == Guid.Empty
            ? throw new ArgumentException("O perfil deve ser informado.", nameof(perfilId))
            : perfilId;
        IncrementarVersoes();
    }

    public void AlterarSenhaHash(string senhaHash)
    {
        SenhaHash = Exigir(senhaHash, nameof(senhaHash));
        IncrementarVersoes();
    }

    public void AlterarSenhaHash(string senhaHash, long versaoEsperada)
    {
        ExigirVersao(versaoEsperada);
        AlterarSenhaHash(senhaHash);
    }

    public void DesativarAcesso(long versaoEsperada)
    {
        ExigirVersao(versaoEsperada);
        if (!EhAtivo)
        {
            return;
        }

        Desativar();
        IncrementarVersoes();
    }

    public void ReativarAcesso(long versaoEsperada)
    {
        ExigirVersao(versaoEsperada);
        if (EhAtivo)
        {
            return;
        }

        Ativar();
        IncrementarVersoes();
    }

    private void ExigirVersao(long versaoEsperada)
    {
        if (versaoEsperada != Versao)
        {
            throw new InvalidOperationException("O usuário foi atualizado por outra operação.");
        }
    }

    private void IncrementarVersoes()
    {
        Versao++;
        VersaoSeguranca++;
        MarcarComoAtualizada();
    }

    private static string NormalizarEmail(string email) => Exigir(email, nameof(email)).ToLowerInvariant();

    private static string Exigir(string valor, string parametro) =>
        string.IsNullOrWhiteSpace(valor)
            ? throw new ArgumentException("O valor deve ser informado.", parametro)
            : valor.Trim();
}
