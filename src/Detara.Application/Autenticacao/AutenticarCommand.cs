using Detara.Application.Abstracoes;
using MediatR;

namespace Detara.Application.Autenticacao;

public sealed record AutenticarCommand(string Email, string Senha, bool ManterConectado = false)
    : IRequest<ResultadoAutenticacao>;

public sealed record SelecionarEmpresaCommand(string Challenge, Guid EmpresaId)
    : IRequest<SessaoTenantResultado>;

public abstract record ResultadoAutenticacao;

public sealed record SessaoTenantResultado(
    string Token,
    DateTime ExpiraEmUtc,
    Guid UsuarioId,
    Guid EmpresaId,
    string Nome,
    string Perfil,
    IReadOnlyCollection<string> Permissoes,
    RefreshTokenCriado RefreshToken) : ResultadoAutenticacao;

public sealed record RenovarSessaoTenantCommand(string RefreshToken)
    : IRequest<SessaoTenantResultado>;

public sealed record EncerrarSessaoTenantCommand(string? RefreshToken) : IRequest;

public sealed record SelecaoEmpresaTenantItemResultado(Guid EmpresaId, string NomeExibicao);

public sealed record SelecaoEmpresaNecessariaResultado(
    string Challenge,
    DateTime ExpiraEmUtc,
    IReadOnlyCollection<SelecaoEmpresaTenantItemResultado> Empresas) : ResultadoAutenticacao;

internal sealed class AutenticarCommandHandler(
    IConsultaIdentidadeLoginTenant consulta,
    ISenhaServico senhaServico,
    ITokenServico tokenServico,
    IChallengeSelecaoEmpresaTenant challengeServico,
    ISessoesAutenticacaoServico sessoes)
    : IRequestHandler<AutenticarCommand, ResultadoAutenticacao>
{
    public async Task<ResultadoAutenticacao> Handle(
        AutenticarCommand request,
        CancellationToken cancellationToken)
    {
        var candidatos = await consulta.ObterCandidatosPorEmailAsync(
            request.Email.Trim().ToLowerInvariant(),
            cancellationToken);

        if (candidatos.Count == 0)
        {
            senhaServico.VerificarContraHashFicticio(request.Senha);
            throw new CredenciaisInvalidasException();
        }

        var candidatosValidos = new List<CandidatoLoginTenant>(candidatos.Count);
        foreach (var candidato in candidatos)
        {
            var senhaValida = senhaServico.Verificar(
                candidato.Usuario,
                candidato.Usuario.SenhaHash,
                request.Senha);
            if (senhaValida &&
                candidato.Usuario.EhAtivo &&
                candidato.Empresa.EhAtiva &&
                candidato.Perfil.EhAtivo)
            {
                candidatosValidos.Add(candidato);
            }
        }

        if (candidatosValidos.Count == 0)
        {
            throw new CredenciaisInvalidasException();
        }

        if (candidatosValidos.Count == 1)
        {
            return await CriarSessaoAsync(
                candidatosValidos[0],
                request.ManterConectado,
                tokenServico,
                sessoes,
                cancellationToken);
        }

        var memberships = candidatosValidos
            .Select(candidato => CriarMembershipAutorizada(candidato, request.ManterConectado))
            .ToArray();
        var challenge = challengeServico.Criar(memberships);
        var empresas = candidatosValidos
            .OrderBy(candidato => candidato.Empresa.NomeExibicao, StringComparer.OrdinalIgnoreCase)
            .Select(candidato => new SelecaoEmpresaTenantItemResultado(
                candidato.Empresa.Id,
                candidato.Empresa.NomeExibicao))
            .ToArray();

        return new SelecaoEmpresaNecessariaResultado(
            challenge.Valor,
            challenge.ExpiraEmUtc,
            empresas);
    }

    internal static async Task<SessaoTenantResultado> CriarSessaoAsync(
        CandidatoLoginTenant candidato,
        bool manterConectado,
        ITokenServico tokenServico,
        ISessoesAutenticacaoServico sessoes,
        CancellationToken cancellationToken)
    {
        var token = tokenServico.Gerar(candidato);
        var refreshToken = await sessoes.CriarTenantAsync(
            candidato.Usuario.Id,
            candidato.Empresa.Id,
            manterConectado,
            cancellationToken);

        return new SessaoTenantResultado(
            token.Valor,
            token.ExpiraEmUtc,
            candidato.Usuario.Id,
            candidato.Empresa.Id,
            candidato.Usuario.Nome,
            candidato.Perfil.Nome,
            candidato.Perfil.PermissoesAtivas,
            refreshToken);
    }

    internal static MembershipLoginTenantAutorizada CriarMembershipAutorizada(
        CandidatoLoginTenant candidato,
        bool manterConectado = false) => new(
            candidato.Usuario.Id,
            candidato.Empresa.Id,
        candidato.Usuario.VersaoSeguranca,
        candidato.Empresa.VersaoSeguranca,
        candidato.Perfil.AtualizadoEmTicks,
        manterConectado);
}

internal sealed class SelecionarEmpresaCommandHandler(
    IConsultaIdentidadeLoginTenant consulta,
    IChallengeSelecaoEmpresaTenant challengeServico,
    ITokenServico tokenServico,
    ISessoesAutenticacaoServico sessoes)
    : IRequestHandler<SelecionarEmpresaCommand, SessaoTenantResultado>
{
    public async Task<SessaoTenantResultado> Handle(
        SelecionarEmpresaCommand request,
        CancellationToken cancellationToken)
    {
        var autorizadas = challengeServico.Validar(request.Challenge);
        var autorizada = autorizadas.SingleOrDefault(item => item.EmpresaId == request.EmpresaId)
            ?? throw new ChallengeSelecaoEmpresaInvalidoException();
        var candidato = await consulta.ObterMembershipAsync(
            autorizada.UsuarioId,
            autorizada.EmpresaId,
            cancellationToken);
        if (candidato is null ||
            !candidato.Usuario.EhAtivo ||
            !candidato.Empresa.EhAtiva ||
            !candidato.Perfil.EhAtivo ||
            candidato.Usuario.VersaoSeguranca != autorizada.UsuarioVersaoSeguranca ||
            candidato.Empresa.VersaoSeguranca != autorizada.EmpresaVersaoSeguranca ||
            candidato.Perfil.AtualizadoEmTicks != autorizada.PerfilAtualizadoEmTicks)
        {
            throw new ChallengeSelecaoEmpresaInvalidoException();
        }

        return await AutenticarCommandHandler.CriarSessaoAsync(
            candidato,
            autorizada.ManterConectado,
            tokenServico,
            sessoes,
            cancellationToken);
    }
}

internal sealed class RenovarSessaoTenantCommandHandler(
    ISessoesAutenticacaoServico sessoes,
    ITokenServico tokenServico)
    : IRequestHandler<RenovarSessaoTenantCommand, SessaoTenantResultado>
{
    public async Task<SessaoTenantResultado> Handle(
        RenovarSessaoTenantCommand request,
        CancellationToken cancellationToken)
    {
        var renovada = await sessoes.RenovarTenantAsync(request.RefreshToken, cancellationToken);
        var token = tokenServico.Gerar(renovada.Candidato);
        return new SessaoTenantResultado(
            token.Valor,
            token.ExpiraEmUtc,
            renovada.Candidato.Usuario.Id,
            renovada.Candidato.Empresa.Id,
            renovada.Candidato.Usuario.Nome,
            renovada.Candidato.Perfil.Nome,
            renovada.Candidato.Perfil.PermissoesAtivas,
            renovada.RefreshToken);
    }
}

internal sealed class EncerrarSessaoTenantCommandHandler(ISessoesAutenticacaoServico sessoes)
    : IRequestHandler<EncerrarSessaoTenantCommand>
{
    public async Task Handle(
        EncerrarSessaoTenantCommand request,
        CancellationToken cancellationToken) =>
        await sessoes.RevogarTenantAsync(request.RefreshToken, cancellationToken);
}
