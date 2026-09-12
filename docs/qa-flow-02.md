# FLOW-02 — orçamento aprovado como fonte autoritativa da OS

## Summary

Criação pelo agendamento passa a usar automaticamente o orçamento-base aprovado vinculado. A origem da navegação não muda o escopo comercial. Nenhum deploy ou acesso à produção foi realizado.

## Repository State Found

- Worktree inicialmente limpo em `codex/ui-05-agendamento-actions-and-reconnect`.
- UI-05 já mesclada pelo PR #64; `origin/main` em `26e2f0818e7e9292ce684a78167dae02d3bca88a`.
- Branch limpa criada desse ponto: `codex/flow-02-approved-quote-to-service-order`.
- Nenhuma alteração anterior foi empilhada.

## Reproduction

Antes de editar código de produção, teste de integração com SQLite real, cliente, veículo, serviço, agendamento e orçamento persistidos:

| Cenário | Catálogo inicial / referência da agenda | Negociado aprovado | Catálogo posterior | OS persistida antes |
|---|---:|---:|---:|---:|
| Agendamento → nova OS sem ID explícito do orçamento | R$ 150 | R$ 120 | R$ 200 | **R$ 150 (bug)** |
| Orçamento → nova OS com ID explícito | R$ 150 | R$ 120 | R$ 200 | R$ 120 |

O teste `Flow02_AgendamentoComAprovado_Preserva120MesmoComCatalogo200(false)` falhou com `Expected: 120 / Actual: 150`; a variante direta passou. IDs da reprodução são GUIDs efêmeros em banco isolado, identificados aqui como tenant A / cliente A / agenda A / orçamento A.

## Root Cause

`AgendamentoDetalhe.razor` navega para `/ordens-servico/nova?agendamentoId=...`. `OrdemServicoNova.razor` carregava apenas a origem planejada e preenchia preços de referência. O POST existente em `api/ordens-servico` recebe `CriarOrdemServicoCommand`; seu handler só entrava no ramo de snapshot aprovado quando `OrcamentoOrigemId` vinha preenchido. Sem esse ID, copiava os valores do acordo direto recebidos pelo formulário, ignorando propostas aprovadas vinculadas.

## Existing Direct Quote Flow

O detalhe do orçamento navega para a mesma página e o mesmo POST, mas informa `orcamentoId` e `agendamentoId`. O handler já copiava as partes e os itens aprovados para `ItemOrdemServicoSnapshot`, construía `OrdemServico` com desconto/acréscimo do orçamento e salvava uma única vez. Esse bloco foi preservado, sem recalcular o catálogo.

## New Shared Flow

`OrigemComercialOrdemServicoFluxo.ResolverAsync` resolve e valida a fonte antes da bifurcação do handler. Com ou sem ID explícito, um orçamento aprovado válido converge para o bloco de snapshot que já funcionava. Não há segunda implementação de cópia nem de cálculo.

Uma consulta GET protegida em `api/ordens-servico/agendamentos/{agendamentoId}/origem-comercial` usa o mesmo resolver e retorna somente o ID selecionado (ou nulo). Ela não cria OS. A Web reaproveita a consulta de detalhe de orçamento e o resumo aprovado existentes. O POST revalida o estado atual; a prévia não é fonte de autoridade.

## Quote Selection Rule

- Reutilizada `ListarPorAgendamentoAsync`: tenant corrente, vínculo `AgendamentoId`, exclusão de `OrdemServicoOrigemId` (adicionais).
- Exatamente um `StatusOrcamento.Aprovado`: selecionar e validar cliente, veículo, vínculo e dados de autorização.
- Zero aprovados: acordo direto anterior, salvo ID explícito inválido, que continua sendo rejeitado.
- Mais de um aprovado: HTTP 409 com orientação para revisar propostas, inclusive se um ID explícito for enviado. Não há desempate seguro por data nem seleção arbitrária.
- Rascunho, Emitido, Recusado, Cancelado e Substituido não são aprovação. Expirado é o estado efetivo de Emitido fora da validade, também excluído. A aprovação não expira por essa regra do domínio.
- Novas propostas são novos IDs. Rascunho de revisão não substitui aprovação anterior; emitir a revisão marca a anterior elegível como Substituido. O resolver respeita esses estados.

## Commercial Snapshot

Preservados: cliente/veículo e seus snapshots; tipo serviço/pacote/personalizado; catálogo por ID; orçamento e item de origem; nome/descrição; quantidade; unitário; ordem; observação do item; data e responsável pela aprovação.

Itens são brutos: subtotal = soma de unitário × quantidade. `Desconto` e `Acrescimo` são aplicados uma vez na OS. Regressão composta: 2 × 120 + 3 × 90 + 2 × 20 − 30 + 10 = **530**, equivalente nos dois caminhos. Ajustes divergentes enviados no request não substituem o orçamento.

Condições e observações globais continuam no orçamento vinculado, como no fluxo direto existente; o modelo da OS não possui campos equivalentes. Não foi criada coluna duplicada. A UI mantém o resumo aprovado sem editor de preço, conforme a regra já existente para criação direta por orçamento. Acordo direto continua editável.

## Appointment / Quote / Order Links

`EmpresaId` vem do usuário autenticado. Com aprovação: `Origem = Orcamento`, `OrcamentoOrigemId = orçamento aprovado` e `AgendamentoOrigemId = agenda operacional`. Cliente e veículo devem coincidir. Duração permanece a do agendamento no ramo aprovado. Não são removidos histórico nem vínculos anteriores.

## Multi-Tenancy

Sem `EmpresaId` no novo contrato de entrada. Agenda é obtida por integração explícita com empresa autenticada; consultas de orçamento usam os filtros globais já existentes. Testes cobrem agenda B consultada por A, ID explícito de orçamento B e orçamento B com vínculo operacional corrompido apontando para agenda A: não selecionado, não consumido e sem OS parcial. Permissões existentes não foram enfraquecidas; prévia e POST exigem `OrdemServicoCriar`, e leitura do detalhe de orçamento conserva sua própria policy.

## Duplicate Protection

Preservadas verificações por agendamento e por orçamento, além dos índices únicos existentes. Repetição retorna conflito, não uma segunda OS. Casos de ambos os caminhos e índice único sem guard Application são exercitados. Um único `SaveChanges` continua confirmando OS, itens e histórico atomicamente; não há marcação intermediária de orçamento consumido.

## Error Handling

Não encontrado/outro tenant: 404. Proposta inválida, divergência, múltiplos aprovados e duplicidade: 409. Sem aprovado e sem itens: `ValidationException` com campo `Itens`, HTTP 400 no envelope padrão. A validação de coleção vazia foi movida para depois da resolução, permitindo request sem itens quando o backend encontra aprovação. Nenhum erro esperado foi convertido em 500. Falha na prévia não libera edição de catálogo como fallback silencioso.

## Files Changed

- `src/Detara.Application/Atendimento/OrdensServicoOperacoes.cs`
- `src/Detara.Application/Atendimento/OrigemComercialOrdemServicoFluxo.cs`
- `src/Detara.Api/Controllers/OrdensServicoController.cs`
- `src/Detara.Contracts/Atendimento/OrdensServicoContratos.cs`
- `src/Detara.Web/Servicos/OrdensServicoServico.cs`
- `src/Detara.Web/Pages/OrdemServicoNova.razor`
- `tests/Detara.IntegrationTests/Atendimento/OrcamentosPersistenciaTests.cs`
- `tests/Detara.IntegrationTests/Atendimento/OrcamentosPersistenciaFlow02Tests.cs`
- `tests/Detara.IntegrationTests/Autorizacao/ClientesVeiculosAutorizacaoTests.cs`
- `tests/Detara.IntegrationTests/Autorizacao/OrdensServicoFlow02ApiTests.cs`
- `tests/Detara.IntegrationTests/Security/JwtEEndpointsSecurityTests.cs`
- `docs/operational-flow.md`
- Este relatório.

## Database Impact

Migration: **não**. Entidades, mapeamentos e índices inalterados. Nenhum pacote novo.

Pending model changes: **nenhum** — `No changes have been made to the model since the last migration.`

## Tests Added

21 casos novos: reprodução nas duas entradas; comparação composta e aprovação anterior ao agendamento; ausência de aprovação e todos os estados não aprovados, incluindo emissão válida/expirada; múltiplas aprovações com e sem ID; revisões; tenant adversarial; partes divergentes; coleção vazia; GET/POST reais para sucesso, validação, conflito, autorização e anonimato. Teste existente de adicionais ampliado para confirmar que não entram na seleção-base.

## Tests Executed

```text
dotnet restore Detara.sln
dotnet build Detara.sln --configuration Release --no-restore
dotnet test Detara.sln --configuration Release --no-build
dotnet format Detara.sln --verify-no-changes --no-restore
dotnet ef migrations has-pending-model-changes --project src/Detara.Infrastructure --startup-project src/Detara.Api --configuration Release --no-build
git diff --check
```

## Test Results

Validação final local: **804 testes aprovados**, zero falhas/ignorados (218 unitários + 586 integração, dos quais 21 casos novos). Restore aprovado. Build Release: zero avisos e zero erros. Format verify e `git diff --check`: exit code 0. EF: sem pending model changes.

A reprodução pré-fix ficou vermelha somente no caminho pelo agendamento, como esperado. Durante a preparação foram corrigidos o tracking/ordenação das fixtures novas e a contagem esperada de rotas (164 → 165). Uma tentativa intermediária de rebuild coincidiu com testhost ainda em execução e encontrou DLL bloqueada; após aguardar o encerramento, o build e a suíte finais acima passaram.

## Manual Validation

Ambiente local Development + SQL Server Docker, 12/09/2026. Não foi resetada a demonstração existente. Cenários sintéticos adicionais sem contatos reais ou envio de notificações:

- Preparação via API: cliente `28de501c…`, veículo QA-FLOW02, serviço `1674f924…` de R$ 150; agenda `4daf4e0e…`; orçamento `153036b8…` de R$ 120 emitido e aprovado.
- Navegador: detalhe da agenda confirmou referência R$ 150 e vínculo aprovado; clique **Criar ordem de serviço** mostrou automaticamente orçamento, quantidade 1, unitário/subtotal/total R$ 120 e texto contextual discreto, sem MudAlert azul.
- Confirmação pela UI criou OS `f49e51bf…`: aberta, origem Orçamento aprovado, unitário R$ 120, subtotal R$ 120, total R$ 120, observação negociada e link de agendamento corretos.
- Agenda sem orçamento `8708b042…`: formulário preservou acordo direto editável de R$ 150; confirmação criou OS `c6964ed0…` aberta, origem Agendamento, item Acordo direto e total R$ 150.
- Prévia aprovada inspecionada visualmente no tema escuro; reaproveita layout Wide e tokens existentes, sem CSS novo. Matriz completa de temas/resoluções não foi reexecutada nesta correção de fluxo.
- Registros sintéticos foram mantidos para conferência. API/Web iniciadas pelo agente para QA foram encerradas ao terminar a validação, liberando 5090/5080.

## Regression Risk

Baixo e concentrado na seleção da fonte comercial. Agenda sem aprovação mantém regra anterior. Dados históricos com múltiplos aprovados agora exigem revisão explícita, intencionalmente. Não há nova infraestrutura de serialização entre aprovação/revisão e criação simultâneas; preserva-se a atomicidade e proteção de duplicidade existentes, sem redesenhar concorrência nesta task.

## Git Diff Summary

Somente Application, leitura API/contrato mínimo, carregamento da página existente, testes e relatório. Sem mudanças em domínio, consultas de preço do catálogo, check-in, Agenda visual, financeiro, WhatsApp, PWA, reconexão, migrations, Actions ou deploy. A remoção do alerta azul foi substituída por contexto no bloco de origem já existente.

## Git / Pull Request

Base: `main`. Head: `codex/flow-02-approved-quote-to-service-order`.
Mensagem do commit: `fix(order): honor approved quote when creating from appointment`.
SHA, URL/número do PR, estado do push/CI e worktree final são informados na entrega da tarefa após publicação. Merge e deploy não autorizados e não realizados.
