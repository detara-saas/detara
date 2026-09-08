# Despesas e contas a pagar — Task 47

## Uso

Financeiro → Despesas (`/financeiro/despesas`) reúne os compromissos de uma competência. A página de recebimentos continua separada: receita líquida recebida **não é lucro** e não desconta automaticamente estas despesas.

- **Avulsa:** uma conta com descrição, categoria, valor previsto, competência e vencimento. Fornecedor e observação são opcionais.
- **Mensal recorrente:** uma regra com dia de vencimento, início e fim opcional. Cada ocorrência gera uma conta real, independente da regra.
- **Pagamento:** quitação integral em uma única operação, com data e valor efetivamente pago. O valor real pode diferir do previsto; não existe saldo parcial ou conciliação nesta etapa.
- **Estorno:** exige motivo e permissão própria. Preserva o pagamento anterior e devolve a conta a Pendente; um novo pagamento gera outro registro.
- **Cancelamento:** somente conta pendente, preservada no histórico e excluída dos totais. Não inativa a regra que a originou.
- **Categorias:** criar, renomear, inativar/reativar. Não há exclusão. Inativação de categoria usada por regra ativa exige primeiro inativar ou reclassificar a regra, evitando interromper silenciosamente sua geração.

O filtro de competência sempre trabalha com o primeiro dia do mês. Os cards consideram toda a competência, independentemente dos filtros adicionais da lista: total previsto sem canceladas; pago efetivo; pendentes não vencidas; pendentes vencidas. “Vencido” é derivado de Pendente + vencimento anterior ao dia local da empresa; não é um status persistido. O filtro Pendente inclui vencidas.

As telas usam os padrões Fluid (listas), Wide (detalhe/categorias) e Focused (formulário), componentes Detara/MudBlazor e os temas Claro, Escuro e Sistema. Tablet/mobile apresentam cards em vez da tabela densa.

## Domínio e aplicação

Tudo pertence ao módulo Financeiro, sem scheduler externo ou novo módulo:

- `ContaPagar` é o agregado da obrigação e possui seu histórico `PagamentoContaPagar`.
- `DespesaRecorrente` é o agregado da regra mensal, com cursor e versão independentes.
- `CategoriaDespesa` é o cadastro tenant-owned, com nome normalizado único.
- `DespesasController → MediatR → DespesasHandlers → IDespesasRepositorio` segue o fluxo existente. DTOs de entrada não contêm tenant, auditoria, origem, status interno ou valor pago derivado.
- Queries de despesas e recorrências são paginadas (padrão 25; whitelist 10/25/50), ordenadas antes da projeção e sem N+1. O resumo usa SUM no SQL Server.

## Recorrência e concorrência

1. Na criação, a primeira competência é `max(início, mês atual)`: não cria retroativos anteriores à criação.
2. A conta do mês atual é materializada no mesmo SaveChanges da criação da regra, se elegível.
3. `DespesasWorker` executa no startup e a cada hora. Regras ativas recuperam competências não geradas após uma indisponibilidade, respeitando o fim inclusivo.
4. Dia 29/30/31 é limitado ao último dia do mês, incluindo fevereiro bissexto.
5. Inativação pausa a geração. Reativação avança o cursor para pelo menos o mês atual; meses inativos não voltam como dívida.
6. Edição da regra só afeta ocorrências ainda não materializadas. Contas guardam descrição, valor, categoria (ID + nome snapshot), fornecedor, observação e vencimento próprios. A competência de uma conta recorrente não é editável, pois identifica a ocorrência.

O banco tem índice único filtrado `(EmpresaId, DespesaRecorrenteId, Competencia)`. Uma transação implícita do SaveChanges persiste conta e avanço de cursor juntos. `Versao` é token de concorrência em regras, contas e categorias; comandos também exigem a versão lida pela UI. Duas instâncias não conseguem sobrescrever edição/inativação nem gerar duas contas para o mesmo mês. O materializador repete até três vezes conflitos de concorrência/duplicidade SQL (2601/2627), relendo estado. Falhas não benignas são registradas com IDs e não impedem outras regras/empresas de serem processadas.

## Fronteira interna e segurança

O worker **não simula um usuário autenticado**. A descoberta interna projeta apenas ID e fuso de empresas ativas. Cada unidade de trabalho utiliza `DetaraDbContext.ParaProcessamentoFinanceiro`, escopado explicitamente a uma empresa e sem identidade de usuário. O filtro global continua ativo. O write guard limita esse contexto a contas, regras e categorias da empresa escolhida, sem deletes, pagamentos ou alterações em entidades globais/outros módulos. Não há endpoint que aceite esse contexto ou permita escolher o tenant.

Toda rota exige `Financeiro.Visualizar`. Escritas exigem adicionalmente `Financeiro.Editar`, pagamentos `Financeiro.RegistrarPagamento` e estornos `Financeiro.EstornarPagamento`. O significado da permissão Editar foi ampliado na descrição, sem criar permissões paralelas. DTOs seguem mapeamento explícito e erros usam o envelope padrão da API (400/403/404/409).

Não há envio de e-mail/WhatsApp, cache offline de negócio, alteração em PWA, integração bancária, pagamentos parciais, rateios, fornecedores como módulo, relatórios novos ou alteração nos indicadores existentes.

## Banco e implantação

Migration: `20260907232359_AddDespesasContasPagar`.

Tabelas: `CategoriasDespesa`, `DespesasRecorrentes`, `ContasPagar`, `PagamentosContasPagar`. Valores usam `decimal(18,2)`, competências/vencimentos/data de pagamento usam `date`. FKs entre estes agregados são compostas pelo tenant e ID, com `Restrict`. Além da ocorrência única, há unicidade de nome por empresa e de pagamento confirmado por conta, índices por competência/status/vencimento, categoria/competência, processamento de regras e data/status de pagamento.

Aplicar migration antes de iniciar a nova API. Categorias padrão são inicializadas em lote na fundação de novas empresas e no primeiro ciclo do worker para empresas existentes ainda sem categorias. A inicialização não repõe categorias renomeadas ou inativadas. Não modifica dados de recebimentos.

```powershell
dotnet ef database update --project src/Detara.Infrastructure --startup-project src/Detara.Api --configuration Release
dotnet ef migrations has-pending-model-changes --project src/Detara.Infrastructure --startup-project src/Detara.Api --configuration Release
```

## Demo

O bootstrap Prime Detail passa a criar quatro regras: aluguel R$ 4.500, internet R$ 189,90, contabilidade R$ 650 e software R$ 149,90. Acrescenta compra de produtos R$ 2.000 paga e manutenção R$ 480 vencida, com competência relativa ao relógio da demo. Reset elimina esses dados em ordem de FK, somente no tenant demo, dentro da transação existente. Não dispara notificações. Uma demo já existente não é resetada automaticamente para instalar esta feature.

## Validação

Testes de domínio cobrem valores, competência, vencimento, fim de mês, ativação, snapshots e pagamento/estorno. Testes de persistência cobrem unique/FKs, atomicidade, concorrência de edição/inativação, materialização repetida, ciclo com múltiplos tenants e fronteira interna. A matriz da API cobre as 15 rotas anônimas/sem permissão, IDs de outra empresa, injeção de categoria/tenant e ciclo de pagamento com versão.

Mudanças colaterais restritas: seed/provisionamento de categorias, demo, navegação financeira e contagem revisada de endpoints protegidos. A fixture SQLite de autorização desabilita somente o novo worker, testado separadamente, pois sua conexão única não suporta concorrência. O relógio inicial dos testes de demo deixa de usar uma data fixa expirada, necessária para executar os cenários completos existentes junto dos novos dados.
