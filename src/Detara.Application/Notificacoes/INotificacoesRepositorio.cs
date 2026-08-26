using Detara.Domain.Notificacoes;
using Detara.Domain.Atendimento;

namespace Detara.Application.Notificacoes;

public interface INotificacoesRepositorio
{
    Task<ConfiguracaoNotificacaoEmpresa?> ObterConfiguracaoAsync(CancellationToken cancellationToken);
    Task<TemplateComunicacaoEmpresa?> ObterTemplateAsync(CanalComunicacaoCliente canal,
        TipoTemplateComunicacao tipo, bool paraAlteracao, CancellationToken cancellationToken);
    Task<NotificacaoEmail?> ObterUltimaPorOrdemServicoAsync(Guid ordemServicoId, bool paraAlteracao, CancellationToken cancellationToken);
    Task<NotificacaoEmail?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ComunicacaoCliente?> ObterComunicacaoPorIdAsync(Guid id, bool paraAlteracao,
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ComunicacaoCliente>> ObterComunicacoesPorOrdemServicoAsync(
        Guid ordemServicoId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ComunicacaoCliente>> ObterTestesWhatsAppAsync(
        int limite, CancellationToken cancellationToken);
    Task<bool> ExisteComunicacaoPendenteAsync(Guid ordemServicoId, CancellationToken cancellationToken);
    Task<bool> ExisteComunicacaoEnviadaRecenteAsync(Guid ordemServicoId,
        CanalComunicacaoCliente canal, TipoComunicacaoCliente tipo,
        string mensagem, string destinatario, DateTime desdeEmUtc,
        CancellationToken cancellationToken);
    Task<SessaoWhatsAppEmpresa?> ObterSessaoWhatsAppAsync(bool paraAlteracao,
        CancellationToken cancellationToken);
    Task<bool> ExistePorOrdemServicoAsync(Guid ordemServicoId, TipoTemplateEmail tipo, CancellationToken cancellationToken);
    void Adicionar(ConfiguracaoNotificacaoEmpresa configuracao);
    void Adicionar(TemplateComunicacaoEmpresa template);
    void Adicionar(NotificacaoEmail notificacao);
    void Adicionar(ComunicacaoCliente comunicacao);
    void Adicionar(SessaoWhatsAppEmpresa sessao);
    void Remover(TemplateComunicacaoEmpresa template);
    Task<bool> TentarAdicionarESalvarAsync(NotificacaoEmail notificacao, CancellationToken cancellationToken);
    Task<bool> TentarAdicionarComunicacaoESalvarAsync(ComunicacaoCliente comunicacao,
        NotificacaoEmail? notificacaoEmail, CancellationToken cancellationToken);
    Task<bool> TentarSalvarAlteracaoAsync(CancellationToken cancellationToken);
    Task SalvarAsync(CancellationToken cancellationToken);
}

public sealed record EmpresaNotificacoesInterna(Guid Id, string Nome);
public sealed record ClienteNotificacoesInterno(Guid Id, string Nome, string? Email, string? WhatsApp);
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

public sealed record OrdemServicoNotificacoesInterna(Guid Id, string Codigo,
    StatusOrdemServico Status, Guid ClienteId, string ClienteNome, string VeiculoDescricao,
    string? VeiculoPlaca);

public interface IAtendimentoNotificacoesConsulta
{
    Task<OrdemServicoNotificacoesInterna?> ObterOrdemServicoAsync(Guid empresaId,
        Guid ordemServicoId, CancellationToken cancellationToken);
}

public sealed record ConteudoTemplateEmail(string Assunto, string CorpoHtml, OrigemTemplateEmail Origem);
public sealed record ConteudoTemplateWhatsApp(string Nome, string Mensagem,
    OrigemTemplateComunicacao Origem);
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

public interface IRenderizadorTemplateWhatsApp
{
    ConteudoTemplateWhatsApp ObterPadraoVeiculoPronto();
    string SanitizarEValidarMensagem(string mensagem);
    void ValidarTokens(string mensagem);
    string RenderizarVeiculoPronto(ConteudoTemplateWhatsApp template, DadosTemplateEmail dados);
    string RenderizarTeste(string empresaNome);
}

public sealed record OrdemServicoFinalizadaNotificacoes(Guid EmpresaId, Guid OrdemServicoId,
    string OrdemServicoCodigo, Guid ClienteId, string ClienteNome, string VeiculoDescricao, string? VeiculoPlaca);

public interface IIntegracaoNotificacoesOrdensServico
{
    Task PrepararNotificacaoAsync(OrdemServicoFinalizadaNotificacoes evento, CancellationToken cancellationToken);
}

public interface IComunicacaoClienteService
{
    Task<ComunicacaoCliente> PrepararManualAsync(Guid ordemServicoId,
        CanalComunicacaoCliente canal, Guid solicitacaoId, CancellationToken cancellationToken);
}

public interface IFilaNotificacoesServico
{
    Task<int> ProcessarLoteAsync(CancellationToken cancellationToken);
}
