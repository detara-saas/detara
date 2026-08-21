using Detara.Application;
using Detara.Application.Abstracoes;
using Detara.Application.Autenticacao;
using Detara.Domain.Entidades;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Detara.IntegrationTests.Autenticacao;

public sealed class AutenticarCommandTests
{
    [Fact]
    public async Task EmailInexistente_ExecutaUmaVerificacaoFicticia()
    {
        var senha = new SenhaRastreavel();
        using var provider = CriarServicos([], senha);

        await Assert.ThrowsAsync<CredenciaisInvalidasException>(() => AutenticarAsync(provider));

        Assert.Equal(1, senha.VerificacoesFicticias);
        Assert.Equal(0, senha.VerificacoesReais);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task MembershipInativa_RetornaErroGenerico(
        bool usuarioAtivo,
        bool empresaAtiva,
        bool perfilAtivo)
    {
        var candidato = CriarCandidato("senha-valida", usuarioAtivo, empresaAtiva, perfilAtivo);
        using var provider = CriarServicos([candidato]);

        var exception = await Assert.ThrowsAsync<CredenciaisInvalidasException>(
            () => AutenticarAsync(provider));

        Assert.Equal("E-mail ou senha inválidos.", exception.Message);
    }

    [Fact]
    public async Task MesmoEmail_VerificaTodosOsHashesSemParadaAntecipada()
    {
        var senha = new SenhaRastreavel();
        var candidatos = new[]
        {
            CriarCandidato("senha-valida"),
            CriarCandidato("outra-senha"),
            CriarCandidato("terceira-senha")
        };
        using var provider = CriarServicos(candidatos, senha);

        var resultado = await AutenticarAsync(provider);

        Assert.IsType<SessaoTenantResultado>(resultado);
        Assert.Equal(3, senha.VerificacoesReais);
    }

    [Theory]
    [InlineData("senha-a", "Empresa A")]
    [InlineData("senha-b", "Empresa B")]
    public async Task SenhasDistintas_EmiteTokenSomenteParaMembershipCompativel(
        string senhaInformada,
        string empresaEsperada)
    {
        var empresaA = CriarCandidato("senha-a", nomeEmpresa: "Empresa A");
        var empresaB = CriarCandidato("senha-b", nomeEmpresa: "Empresa B");
        var token = new TokenRastreavel();
        using var provider = CriarServicos([empresaA, empresaB], tokenServico: token);

        var resultado = Assert.IsType<SessaoTenantResultado>(
            await provider.GetRequiredService<ISender>().Send(
                new AutenticarCommand("admin@detara.local", senhaInformada)));
        var candidatoEsperado = empresaEsperada == "Empresa A" ? empresaA : empresaB;

        Assert.Equal(candidatoEsperado.Empresa.Id, resultado.EmpresaId);
        Assert.Equal(candidatoEsperado.Empresa.Id, token.EmpresaEmitida);
    }

    [Fact]
    public async Task MesmaSenha_ExigeSelecaoENaoEmiteToken()
    {
        var empresaA = CriarCandidato("senha-valida", nomeEmpresa: "Empresa A");
        var empresaB = CriarCandidato("senha-valida", nomeEmpresa: "Empresa B");
        var token = new TokenRastreavel();
        using var provider = CriarServicos([empresaB, empresaA], tokenServico: token);

        var resultado = Assert.IsType<SelecaoEmpresaNecessariaResultado>(
            await AutenticarAsync(provider));

        Assert.Null(token.EmpresaEmitida);
        Assert.Equal(["Empresa A", "Empresa B"], resultado.Empresas.Select(x => x.NomeExibicao));
        Assert.DoesNotContain("senha", resultado.Challenge, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SelecaoAutorizada_RevalidaMembershipEEmiteTokenDoTenantEscolhido()
    {
        var empresaA = CriarCandidato("senha-valida", nomeEmpresa: "Empresa A");
        var empresaB = CriarCandidato("senha-valida", nomeEmpresa: "Empresa B");
        var token = new TokenRastreavel();
        using var provider = CriarServicos([empresaA, empresaB], tokenServico: token);
        var selecao = Assert.IsType<SelecaoEmpresaNecessariaResultado>(
            await AutenticarAsync(provider));

        var resultado = await provider.GetRequiredService<ISender>().Send(
            new SelecionarEmpresaCommand(selecao.Challenge, empresaB.Empresa.Id));

        Assert.Equal(empresaB.Empresa.Id, resultado.EmpresaId);
        Assert.Equal(empresaB.Empresa.Id, token.EmpresaEmitida);
    }

    [Fact]
    public async Task EmpresaForaDoChallenge_EhRejeitadaSemConsultaOuToken()
    {
        var empresaA = CriarCandidato("senha-valida");
        var empresaB = CriarCandidato("senha-valida");
        var consulta = new ConsultaFixa([empresaA, empresaB]);
        var token = new TokenRastreavel();
        using var provider = CriarServicos(
            [empresaA, empresaB],
            consulta: consulta,
            tokenServico: token);
        var selecao = Assert.IsType<SelecaoEmpresaNecessariaResultado>(
            await AutenticarAsync(provider));

        await Assert.ThrowsAsync<ChallengeSelecaoEmpresaInvalidoException>(() =>
            provider.GetRequiredService<ISender>().Send(
                new SelecionarEmpresaCommand(selecao.Challenge, Guid.NewGuid())));

        Assert.Equal(0, consulta.ConsultasMembership);
        Assert.Null(token.EmpresaEmitida);
    }

    [Theory]
    [InlineData("usuario")]
    [InlineData("empresa")]
    [InlineData("perfil")]
    [InlineData("versao")]
    public async Task EstadoDeSegurancaAlteradoAposChallenge_EhRejeitado(string alteracao)
    {
        var empresaA = CriarCandidato("senha-valida");
        var empresaB = CriarCandidato("senha-valida");
        var consulta = new ConsultaFixa([empresaA, empresaB]);
        using var provider = CriarServicos([empresaA, empresaB], consulta: consulta);
        var selecao = Assert.IsType<SelecaoEmpresaNecessariaResultado>(
            await AutenticarAsync(provider));
        if (alteracao == "usuario")
        {
            empresaB.Usuario.Desativar();
        }

        var empresaRevalidada = alteracao switch
        {
            "empresa" => empresaB with { Empresa = empresaB.Empresa with { EhAtiva = false } },
            "perfil" => empresaB with { Perfil = empresaB.Perfil with { EhAtivo = false } },
            "versao" => empresaB with
            {
                Empresa = empresaB.Empresa with { VersaoSeguranca = 2 }
            },
            _ => empresaB
        };
        consulta.Candidatos = [empresaA, empresaRevalidada];

        await Assert.ThrowsAsync<ChallengeSelecaoEmpresaInvalidoException>(() =>
            provider.GetRequiredService<ISender>().Send(
                new SelecionarEmpresaCommand(selecao.Challenge, empresaB.Empresa.Id)));
    }

    [Fact]
    public async Task PermissoesDaMembershipSelecionada_SaoAsUnicasRetornadas()
    {
        var candidato = CriarCandidato("senha-valida", permissoes: ["Clientes.Visualizar"]);
        using var provider = CriarServicos([candidato]);

        var resultado = Assert.IsType<SessaoTenantResultado>(await AutenticarAsync(provider));

        Assert.Equal(["Clientes.Visualizar"], resultado.Permissoes);
    }

    private static ServiceProvider CriarServicos(
        IReadOnlyCollection<CandidatoLoginTenant> candidatos,
        ISenhaServico? senhaServico = null,
        ConsultaFixa? consulta = null,
        ITokenServico? tokenServico = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AdicionarApplication();
        services.AddSingleton<IConsultaIdentidadeLoginTenant>(consulta ?? new ConsultaFixa(candidatos));
        services.AddSingleton(senhaServico ?? new SenhaRastreavel());
        services.AddSingleton(tokenServico ?? new TokenRastreavel());
        services.AddSingleton<IChallengeSelecaoEmpresaTenant, ChallengeFixo>();
        return services.BuildServiceProvider();
    }

    private static Task<ResultadoAutenticacao> AutenticarAsync(IServiceProvider provider) =>
        provider.GetRequiredService<ISender>().Send(
            new AutenticarCommand("admin@detara.local", "senha-valida"));

    private static CandidatoLoginTenant CriarCandidato(
        string hash,
        bool usuarioAtivo = true,
        bool empresaAtiva = true,
        bool perfilAtivo = true,
        string nomeEmpresa = "Empresa Demo",
        IReadOnlyCollection<string>? permissoes = null)
    {
        var empresaId = Guid.NewGuid();
        var perfil = new Perfil(empresaId, "Administrador");
        var usuario = new Usuario(
            empresaId,
            perfil.Id,
            "Administrador Demo",
            "admin@detara.local",
            hash);
        if (!usuarioAtivo)
        {
            usuario.Desativar();
        }

        return new CandidatoLoginTenant(
            usuario,
            new EmpresaLoginTenant(empresaId, nomeEmpresa, empresaAtiva, 1),
            new PerfilLoginTenant(
                perfil.Id,
                perfil.Nome,
                perfilAtivo,
                perfil.AtualizadoEmUtc?.Ticks ?? 0,
                permissoes ?? []));
    }

    private sealed class ConsultaFixa(IReadOnlyCollection<CandidatoLoginTenant> candidatos)
        : IConsultaIdentidadeLoginTenant
    {
        public IReadOnlyCollection<CandidatoLoginTenant> Candidatos { get; set; } = candidatos;
        public int ConsultasMembership { get; private set; }

        public Task<IReadOnlyCollection<CandidatoLoginTenant>> ObterCandidatosPorEmailAsync(
            string email,
            CancellationToken cancellationToken) => Task.FromResult(Candidatos);

        public Task<CandidatoLoginTenant?> ObterMembershipAsync(
            Guid usuarioId,
            Guid empresaId,
            CancellationToken cancellationToken)
        {
            ConsultasMembership++;
            return Task.FromResult(Candidatos.SingleOrDefault(
                candidato => candidato.Usuario.Id == usuarioId && candidato.Empresa.Id == empresaId));
        }
    }

    private sealed class SenhaRastreavel : ISenhaServico
    {
        public int VerificacoesFicticias { get; private set; }
        public int VerificacoesReais { get; private set; }
        public string GerarHash(Usuario usuario, string senha) => senha;

        public bool Verificar(Usuario usuario, string senhaHash, string senha)
        {
            VerificacoesReais++;
            return senhaHash == senha;
        }

        public void VerificarContraHashFicticio(string senha) => VerificacoesFicticias++;
    }

    private sealed class TokenRastreavel : ITokenServico
    {
        public Guid? EmpresaEmitida { get; private set; }

        public TokenGerado Gerar(CandidatoLoginTenant candidato)
        {
            EmpresaEmitida = candidato.Empresa.Id;
            return new TokenGerado("token", DateTime.UtcNow.AddMinutes(5));
        }
    }

    private sealed class ChallengeFixo : IChallengeSelecaoEmpresaTenant
    {
        private IReadOnlyCollection<MembershipLoginTenantAutorizada>? _memberships;

        public ChallengeSelecaoEmpresaCriado Criar(
            IReadOnlyCollection<MembershipLoginTenantAutorizada> memberships)
        {
            _memberships = memberships;
            return new ChallengeSelecaoEmpresaCriado("challenge-protegido", DateTime.UtcNow.AddMinutes(5));
        }

        public IReadOnlyCollection<MembershipLoginTenantAutorizada> Validar(string challenge) =>
            challenge == "challenge-protegido" && _memberships is not null
                ? _memberships
                : throw new ChallengeSelecaoEmpresaInvalidoException();
    }
}
