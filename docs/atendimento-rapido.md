# Atendimento rápido

## Uso e limite comercial

A opção **Atendimento → Atendimento rápido**, também acessível pelas ações rápidas do Dashboard Operação, registra um único serviço ou pacote do catálogo já realizado e integralmente pago. Não é um atalho para iniciar um atendimento ainda em execução.

O operador informa cliente, veículo quando a empresa utiliza a capability, serviço/pacote, valor efetivamente cobrado e forma de pagamento. O catálogo sugere o preço; o valor negociado é preservado na OS. Um único veículo do cliente é selecionado automaticamente. Data/hora da empresa, taxa e observação ficam em detalhes expansíveis. Cartão de crédito apresenta parcelas; a forma Outro exige descrição. Parcelas representam a informação do cartão, não contas em aberto no Detara.

Não há campo independente de total e pagamento parcial: o único valor informado é simultaneamente o total comercial e o recebimento bruto integral. Taxa diminui o líquido, sem criar saldo a receber. Novos cadastros, múltiplos meios de pagamento, múltiplas linhas e pagamento parcial não fazem parte desta versão.

## Persistência e autoria

- OS com origem `AtendimentoRapido` e status `Concluida`, usando entidades, snapshots e histórico existentes.
- Cliente, veículo e item de catálogo validados na empresa autenticada. A capability determina a obrigatoriedade do veículo; ID enviado com capability OFF é rejeitado.
- Data/hora realizada convertida pelo fuso da empresa, sem datas futuras. Registro/auditoria preservam a hora real do lançamento.
- Agenda, orçamento, check-in, início de execução, fotos e respostas de checklist não são criados artificialmente.
- Financeiro é dono da conta paga e do pagamento confirmado. O domínio financeiro existente protege taxas, saldo, parcelas e estorno.
- OS, histórico, conta e pagamento são inseridos no mesmo contexto e único `SaveChanges`, com transação do EF.
- Nenhum aviso automático de veículo pronto é enfileirado. A OS histórica não apresenta ações de check-in ou comunicação de retirada.

A migration `AddQuickServiceEntry` adiciona apenas `SolicitacaoAtendimentoRapidoId`, `AtendimentoRapidoHash` e índice único filtrado `(EmpresaId, SolicitacaoAtendimentoRapidoId)` em `OrdensServico`. As colunas são opcionais para os registros anteriores. Não altera FKs, valores ou status existentes. Correções financeiras continuam utilizando o estorno auditado do Financeiro.

## Idempotência e falhas

O frontend cria uma chave por confirmação e congela o comando quando não recebe resposta conclusiva. Uma nova tentativa envia exatamente o mesmo comando. O servidor consulta a chave no tenant autenticado e compara o hash do comando; se já existir, devolve os mesmos IDs, sem novo recebimento. Mesmo depois de estorno, uma repetição não recria o pagamento.

Mesma chave com outro conteúdo retorna 409. Na disputa concorrente antes da consulta, o índice único permite somente uma gravação, revertendo a tentativa perdedora integralmente; ela pode receber 409 e confirmar novamente com a mesma chave. Não há retry automático ou fila offline. Uma validação 400 permite corrigir o formulário. A confirmação pendente fica na memória da página, portanto deve ser resolvida antes de fechar/recarregar; não há persistência de dados financeiros no browser.

## Autorização

A policy composta `Atendimento.RegistrarRapido` exige todas as permissões existentes:

- `OrdemServico.Criar`;
- `OrdemServico.Finalizar`;
- `Financeiro.RegistrarPagamento`.

O controller usa essa policy inclusive nas buscas auxiliares. A consulta de veículos também exige capability `veiculos`. O tenant sempre vem do usuário autenticado, nunca do payload. Não exige permissão de Orçamento para usar o fluxo. A página de sucesso só mostra links para OS/Financeiro quando o usuário possui a respectiva permissão de leitura.

## Validação

Testes de domínio, HTTP, persistência e cliente HTTP cobrem conclusão histórica sem etapas fictícias, pagamento integral, valores inválidos, data futura, isolamento de tenants, capabilities, permissões, repetição, índice único, rollback da tentativa duplicada, taxa, competência no fuso e estorno. A suíte de persistência usa SQLite normalmente; para executar a mesma suíte com migrations reais no SQL Server, informe `DETARA_QUICK_ENTRY_TEST_SQL` apontando exclusivamente a uma instância de QA isolada. Os testes criam bancos com nomes aleatórios `DetaraQuickEntryQa_*`; nunca aponte essa variável à produção.

Inspeções de navegador e resultados finais de comandos são registrados na entrega da alteração. A migration não deve ser aplicada em banco persistente sem autorização da tarefa/operador.

### Evidências da implementação

- `dotnet restore Detara.sln`: aprovado.
- `dotnet build Detara.sln -c Release --no-restore`: aprovado, zero erros e zero warnings no build final, com processos de QA encerrados durante a compilação.
- `dotnet test Detara.sln -c Release --no-build`: 989 aprovados (329 unitários e 660 integração), zero falhas e zero ignorados.
- A mesma suíte de persistência do lançamento rápido foi executada separadamente em SQL Server 2022: 3 aprovados, incluindo aplicação de migrations, índice único e estorno.
- `dotnet format Detara.sln --verify-no-changes --no-restore`: aprovado.
- `git diff --check`: aprovado.
- `dotnet ef migrations has-pending-model-changes`: sem alterações pendentes.
- Migration validada em bancos novos do container descartável de QA e, após autorização para teste, aplicada ao banco local Detara com backup `COPY_ONLY` e `RESTORE VERIFYONLY`. A aplicação em produção pertence exclusivamente ao workflow de deploy com backup externo obrigatório.
- Navegador Edge: 1920×1080, 1440×900, 1024×1366 e 390×844 nos temas Claro, Escuro e Sistema. Sem overflow horizontal nas 12 combinações; capturas inspecionadas em desktop, tablet e mobile.
- Fluxo mobile completo com perda proposital da resposta após gravação: o reenvio retornou o mesmo lançamento, mantendo uma OS e um pagamento de R$ 137,50 para a solicitação.
- Histórico do cliente e execução do serviço consultados na API sobre SQL Server; detalhe da OS validado no navegador em 1440×900 e 390×844, sem etapa fictícia de check-in, início ou aviso de retirada.

Limites da validação local: navegador desktop com viewport mobile, sem teste em aparelho físico. Resultados de CI e publicação são informados na entrega de produção. A migration precisa ser aplicada pelo fluxo autorizado do ambiente de destino antes de disponibilizar a funcionalidade.
