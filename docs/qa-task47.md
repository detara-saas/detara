# QA — Task 47: despesas e contas a pagar

Branch: `feature/despesas-contas-pagar`, criada de `origin/main` (`76e27ab`). Escopo e decisões: [documentação funcional/técnica](despesas-contas-pagar.md).

## Backend e qualidade

| Verificação | Resultado local |
| --- | --- |
| `dotnet restore` | Aprovado |
| `dotnet build --configuration Release` | Aprovado, zero erros e zero warnings |
| `dotnet test --configuration Release` | 666 aprovados: 169 unitários + 497 integração; nenhum ignorado |
| `dotnet format --verify-no-changes` | Aprovado |
| `git diff --check` / diff staged | Aprovado |
| EF `has-pending-model-changes` | Nenhuma alteração pendente |
| Dependências | Nenhum package novo ou alteração de versão |

São 68 novos casos de teste executáveis: 17 de domínio e 51 de integração/API. As fixtures da demo também foram ampliadas. A matriz HTTP cobre todas as 15 novas rotas sem autenticação/sem permissão e todas as rotas com IDs contra outro tenant. Testa ainda categoria estrangeira, campos de autoridade ignorados, filtros inválidos e pagamento/estorno/cancelamento com versão.

## SQL Server real

- Migration `20260907232359_AddDespesasContasPagar` aplicada ao banco local existente, sem reset ou alteração dos dados de recebimentos.
- Banco SQL Server temporário exclusivo de QA: todas as migrations executadas desde zero e ausência de model changes verificada.
- Bootstrap real da Prime Detail no banco temporário: seis contas, quatro regras e R$ 2.000 pagos, com resumo SUM e paginação traduzidos/executados pelo SQL Server.
- Duas tarefas concorrentes, com DbContexts distintos, materializaram três competências atrasadas da mesma regra: exatamente três novas contas; nenhuma duplicada.
- Ciclo completo do worker executado no SQL Server e repetido sem gerar novas contas indevidas.
- Banco temporário eliminado ao fim da validação; o banco local Detara foi preservado. A demo local já existente não foi resetada.

## Interface

Navegador Chromium/Edge local, build Release da Web em Development. A ferramenta de navegação interativa falhou ao inicializar o kernel; a alternativa foi Playwright local com revisão das capturas. Não foram instalados packages no repositório.

| Resolução | Claro | Escuro | Sistema (SO escuro) |
| --- | --- | --- | --- |
| 1920×1080 | Revisado | Revisado | Revisado |
| 1440×900 | Revisado | Revisado | Revisado |
| 1024×1366 | Revisado | Revisado | Revisado |
| 390×844 | Revisado | Revisado | Revisado |

A matriz navegou por Despesas, Nova despesa, Detalhe, Recorrências e Categorias: 60 combinações de página/resolução/tema. Incluiu também a mudança do formulário para mensal recorrente e a abertura/fechamento do diálogo de pagamento, totalizando 84 capturas. Nenhum overflow horizontal do documento ou erro JavaScript/Blazor não tratado foi encontrado na execução final. Capturas inspecionadas levaram à correção de fornecedor colado à descrição, filtros apertados e espaçamento após o alerta de recorrências.

**Limite da evidência visual:** esta matriz usa respostas sintéticas interceptadas no navegador, identificadas como QA visual; não representa login manual nem comandos financeiros no banco do usuário. A persistência, autorização e concorrência reais foram validadas separadamente pelos testes HTTP/SQLite e pelo cenário SQL Server descrito acima. Não houve envio de e-mails ou WhatsApp pelos cenários novos.

## Entrega

A migration e os dados de demo são parte da implementação. Não há novo módulo, scheduler externo, alterações de indicadores, PWA, recebimentos, integração bancária ou relatórios. O PR deve ser revisado e mesclado manualmente. Número/URL, commit e estado corrente do CI são informados na entrega, pois pertencem ao estado do GitHub após o commit deste relatório.
