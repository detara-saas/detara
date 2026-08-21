using Detara.Application.Abstracoes;
using MediatR;

namespace Detara.Application.Autenticacao;

public sealed record AutenticarCommand(string Email, string Senha)
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
    IReadOnlyCollection<string> Permissoes) : ResultadoAutenticacao;

public sealed record SelecaoEmpresaTenantItemResultado(Guid EmpresaId, string NomeExibicao);

public sealed record SelecaoEmpresaNecessariaResultado(
    string Challenge,
    DateTime ExpiraEmUtc,
    IReadOnlyCollection<SelecaoEmpresaTenantItemResultado> Empresas) : ResultadoAutenticacao;

internal sealed class AutenticarCommandHandler(
    IConsultaIdentidadeLoginTenant consulta,
    ISenhaServico senhaServico,
    ITokenServico tokenServico,
    IChallengeSelecaoEmpresaTenant challengeServico)
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
            return CriarSessao(candidatosValidos[0], tokenServico);
        }

        var memberships = candidatosValidos
            .Select(CriarMembershipAutorizada)
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

    internal static SessaoTenantResultado CriarSessao(
        CandidatoLoginTenant candidato,
        ITokenServico tokenServico)
    {
        var token = tokenServico.Gerar(candidato);

        return new SessaoTenantResultado(
            token.Valor,
            token.ExpiraEmUtc,
            candidato.Usuario.Id,
            candidato.Empresa.Id,
            candidato.Usuario.Nome,
            candidato.Perfil.Nome,
            candidato.Perfil.PermissoesAtivas);
    }

    internal static MembershipLoginTenantAutorizada CriarMembershipAutorizada(
        CandidatoLoginTenant candidato) => new(
            candidato.Usuario.Id,
            candidato.Empresa.Id,
            candidato.Usuario.AtualizadoEmUtc?.Ticks ?? 0,
            candidato.Empresa.VersaoSeguranca,
            candidato.Perfil.AtualizadoEmTicks);
}

internal sealed class SelecionarEmpresaCommandHandler(
    IConsultaIdentidadeLoginTenant consulta,
    IChallengeSelecaoEmpresaTenant challengeServico,
    ITokenServico tokenServico)
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
            (candidato.Usuario.AtualizadoEmUtc?.Ticks ?? 0) != autorizada.UsuarioAtualizadoEmTicks ||
            candidato.Empresa.VersaoSeguranca != autorizada.EmpresaVersaoSeguranca ||
            candidato.Perfil.AtualizadoEmTicks != autorizada.PerfilAtualizadoEmTicks)
        {
            throw new ChallengeSelecaoEmpresaInvalidoException();
        }

        return AutenticarCommandHandler.CriarSessao(candidato, tokenServico);
    }
}
