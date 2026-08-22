# Dashboard operacional

O Dashboard tenant é uma composição de leitura autenticada em `GET /api/dashboard`. Ele não é dono dos dados exibidos e não recebe `EmpresaId` do navegador: o tenant vem exclusivamente de `IUsuarioContexto` e os query filters continuam ativos.

## Indicadores e fontes

| Elemento | Fonte | Período e regra | Permissão |
|---|---|---|---|
| Agendamentos de hoje | Agenda | Dia local da Empresa; inclui Agendado, Confirmado, Compareceu e Concluído; exclui Cancelado e Não compareceu | `Agenda.Visualizar` |
| Agenda de hoje | Agenda | Até cinco compromissos válidos do dia, ordenados por horário | `Agenda.Visualizar` |
| OS em execução | Atendimento | Estado persistido `EmExecucao`, no momento da consulta | `OrdemServico.Visualizar` |
| Aguardando retirada | Atendimento | Estado persistido `AguardandoRetirada`, no momento da consulta | `OrdemServico.Visualizar` |
| Orçamentos em aberto | Atendimento | Rascunhos e Emitidos ainda válidos; exclui aprovados, recusados, cancelados, substituídos e expirados | `Orcamentos.Visualizar` |
| Receita líquida recebida | Financeiro | Pagamentos confirmados recebidos no mês local, menos as taxas registradas; não representa lucro | `Financeiro.Visualizar` |
| Contas pendentes | Financeiro | Contas com saldo atual, somando `ValorOriginal - ValorRecebido` | `Financeiro.Visualizar` |

Cards, alertas e listas sem autorização não são retornados pela API nem renderizados. Isso evita representar ausência de permissão como valor zero e impede acesso financeiro indireto. Usuários sem qualquer permissão de leitura operacional recebem somente um estado informativo.

## Composição e performance

O handler consulta contratos estreitos implementados por Plataforma, Agenda, Atendimento e Financeiro. Com todas as permissões, a composição executa aproximadamente sete queries sequenciais e pequenas: fuso horário, dois resumos de Agenda, dois de Atendimento e dois de Financeiro. As consultas usam projeções, `Count`, `Sum`, agrupamentos, `Take(5)` e `AsNoTracking`; não usam `Include`, raw SQL, `IgnoreQueryFilters`, N+1, cache ou paralelismo sobre o mesmo `DbContext`.

O Dashboard é atualizado ao entrar ou ao usar “Tentar novamente” após uma falha. Não existe polling, SignalR ou cache. Empresa sem dados apresenta zeros e estado vazio, nunca conteúdo fictício.
