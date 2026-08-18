using Detara.Domain.Atendimento;

namespace Detara.Application.Atendimento;

public interface IConfiguracoesOperacionaisRepositorio
{
    Task<ConfiguracaoOperacionalAtendimento?> ObterConfiguracaoAsync(
        bool paraAlteracao,
        CancellationToken cancellationToken);

    Task<ChecklistModelo?> ObterChecklistAsync(
        bool paraAlteracao,
        CancellationToken cancellationToken);

    void Adicionar(ConfiguracaoOperacionalAtendimento configuracao);
    void Adicionar(ChecklistModelo checklist);
    void RemoverItensAtuais(ChecklistModelo checklist);
    void AdicionarItensAtuais(ChecklistModelo checklist);
    Task SalvarAsync(CancellationToken cancellationToken);
}
