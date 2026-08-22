using Detara.Domain.Notificacoes;

namespace Detara.Application.Notificacoes;

public interface INotificacoesRepositorio
{
    Task<ConfiguracaoNotificacaoEmpresa?> ObterConfiguracaoAsync(CancellationToken cancellationToken);
    Task<TemplateEmailEmpresa?> ObterTemplateAsync(TipoTemplateEmail tipo, bool paraAlteracao, CancellationToken cancellationToken);
    Task<NotificacaoEmail?> ObterPorOrdemServicoAsync(Guid ordemServicoId, bool paraAlteracao, CancellationToken cancellationToken);
    Task<bool> ExistePorOrdemServicoAsync(Guid ordemServicoId, TipoTemplateEmail tipo, CancellationToken cancellationToken);
    void Adicionar(ConfiguracaoNotificacaoEmpresa configuracao);
    void Adicionar(TemplateEmailEmpresa template);
    void Adicionar(NotificacaoEmail notificacao);
    void Remover(TemplateEmailEmpresa template);
    Task SalvarAsync(CancellationToken cancellationToken);
}

public sealed record EmpresaNotificacoesInterna(Guid Id, string Nome);
public sealed record ClienteNotificacoesInterno(Guid Id, string Nome, string? Email);
public sealed record UsuarioNotificacoesInterno(Guid Id, string Nome, string Email);

public interface IPlataformaNotificacoesConsulta
{
    Task<EmpresaNotificacoesInterna?> ObterEmpresaAsync(Guid empresaId, CancellationToken cancellationToken);
    Task<UsuarioNotificacoesInterno?> ObterUsuarioAsync(Guid empresaId, Guid usuarioId, CancellationToken cancellationToken);
}

public interface IClientesNotificacoesConsulta
{
    Task<ClienteNotificacoesInterno?> ObterClienteAsync(Guid empresaId, Guid clienteId, CancellationToken cancellationToken);
}

public sealed record ConteudoTemplateEmail(string Assunto, string CorpoHtml, OrigemTemplateEmail Origem);
public sealed record DadosTemplateEmail(string EmpresaNome, string ClienteNome, string VeiculoDescricao,
    string? Placa, string OrdemServicoCodigo);
public sealed record EmailRenderizado(string Assunto, string CorpoHtmlCompleto);

public interface IRenderizadorTemplateEmail
{
    ConteudoTemplateEmail ObterPadraoVeiculoPronto();
    string SanitizarEValidarCorpo(string corpoHtml);
    void ValidarTokens(string assunto, string corpoHtml);
    EmailRenderizado Renderizar(ConteudoTemplateEmail template, DadosTemplateEmail dados);
}

public sealed record OrdemServicoFinalizadaNotificacoes(Guid EmpresaId, Guid OrdemServicoId,
    string OrdemServicoCodigo, Guid ClienteId, string ClienteNome, string VeiculoDescricao, string? VeiculoPlaca);

public interface IIntegracaoNotificacoesOrdensServico
{
    Task PrepararNotificacaoAsync(OrdemServicoFinalizadaNotificacoes evento, CancellationToken cancellationToken);
}

public interface IFilaNotificacoesServico
{
    Task<int> ProcessarLoteAsync(CancellationToken cancellationToken);
}
