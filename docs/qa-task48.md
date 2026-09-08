# QA — Task 48: Central de Relatórios e Insights

## Base e escopo

Branch `feature/relatorios-insights`, criada de `origin/main` em `083d932`, já com a Task 47 e `20260907232359_AddDespesasContasPagar`. A central é exclusivamente de leitura; a única escrita adicional pertence ao comando local explícito `presentation` do DemoBootstrap.

Definições de todas as métricas, fontes, contratos, permissões, gráficos, thresholds e divergências estão em [relatorios-insights.md](relatorios-insights.md).

## Verificações automatizadas

| Verificação | Resultado |
|---|---|
| `dotnet restore` | Sucesso |
| `dotnet build --configuration Release` | 0 erros, 0 avisos |
| `dotnet test --configuration Release` | 170 unitários + 549 integração = **719 aprovados**, nenhum ignorado |
| `dotnet format --verify-no-changes` | Sucesso |
| `git diff --check` | Sucesso |
| EF `migrations has-pending-model-changes` | Nenhuma alteração pendente |
| Migration, índice, package novo | Nenhum |

Cobertura acrescentada: períodos corridos e calendário, comparação equivalente, base zero, DST, intervalo invertido/excessivo, fontes financeiras reais, pagamento parcial/estorno, status e data de conclusão da OS, cortesias, valores históricos, recorrência, conversão por decisões, rankings determinísticos e Top 10/Outras categorias. Formatação pt-BR não contamina coordenadas SVG/CSS.

Os testes HTTP executam endpoints diretamente com autenticação de teste: anônimo (401), sem policy (403), tenant A/B com valores deliberadamente diferentes, `empresaId` arbitrário sem autoridade, usuário sem Financeiro sem payload/insights financeiros, usuário sem Clientes sem rankings nominais. Não há novo endpoint recebendo um ID arbitrário de cliente; o link usa o Cliente 360 existente e seu isolamento.

## SQL Server real

Docker SQL Server saudável. Foi criado um banco **descartável separado**, com todas as migrations existentes e somente dados sintéticos. Nenhum reset/presentation foi executado no banco local do usuário.

Consultas reais de Financeiro, Atendimento e Agenda foram executadas, incluindo séries com `AT TIME ZONE`, soma correlacionada dos itens, Top Serviços, Top Clientes, recorrência e decisões de orçamento. O cenário de apresentação, nos últimos 30 dias, confirmou:

- Receita recebida: R$ 2.990,00;
- Despesas pagas: R$ 2.000,00;
- OS concluídas: 5;
- Valor em atendimentos: R$ 1.790,00;
- Ticket médio: R$ 358,00;
- Clientes atendidos: 4, incluindo 1 recorrente.

Tempo observado do conjunto de consultas frias: aproximadamente 573 ms no ambiente local. Isso é um smoke test funcional de tradução SQL, **não** benchmark ou garantia de latência em produção. Agregações ficam no banco, sem N+1 ou join multiplicativo entre pagamentos e itens. Não houve tuning de índices nem teste de carga em escala de produção.

Em seguida, somente nesse banco, foram adicionados dados extremos: mais de dez itens/categorias, nomes extensos, cortesia, valores de R$ 999.999,99 e resultado negativo superior a dez milhões. Totais, rankings e gráficos permaneceram legíveis. O banco descartável foi removido ao final e os processos próprios de QA nas portas 5080/5090 foram encerrados; os dados locais foram preservados.

## Matriz visual

Revisão no navegador integrado, com viewport controlado; cinco perspectivas em cada combinação abaixo (**60 combinações**). Verificações de largura do documento, ausência de NaN/Infinity em SVG, presença da seção correta, além de capturas inspecionadas nos diferentes layouts.

| Resolução | Claro | Escuro | Sistema |
|---|---|---|---|
| 1920 × 1080 | Aprovado | Aprovado | Aprovado |
| 1440 × 900 | Aprovado | Aprovado | Aprovado |
| 1024 × 1366 | Aprovado | Aprovado | Aprovado |
| 390 × 844 | Aprovado | Aprovado | Aprovado |

Sistema acompanhou a preferência escura do sistema operacional durante a sessão. Claro foi conferido separadamente. Esta matriz não equivale a testes em dispositivos físicos ou em todos os navegadores.

Também verificados:

- Troca de perspectivas preservando período, skeleton transitório e resultado atualizado;
- Datas digitadas 01/01/2025–02/01/2025 realmente aplicadas, período anterior 30/12/2024–31/12/2024 e estado vazio sem conclusões inventadas;
- Período invertido apresenta erro e permite corrigir os filtros;
- Este ano muda o gráfico para granularidade mensal;
- Alternância Quantidade/Valor no Top 10;
- Link de Fernanda Ribeiro abre o Cliente 360 correto, com consumo de R$ 450,00 consistente entre as telas;
- Nomes extensos, resultado negativo, moeda pt-BR e Top 10 + Outras categorias;
- Após desligar deliberadamente a API de QA, mensagem de indisponibilidade e botão Tentar novamente, sem loading infinito ou dados antigos apresentados como atuais;
- Nenhum erro/aviso de console nas navegações normais antes da desconexão intencional.

## Ajustes encontrados durante QA

1. Projeção das séries ajustada para inicialização de propriedades, garantindo tradução do agrupamento pelo EF/SQL Server.
2. Datas sintéticas da apresentação ajustadas após a auditoria do SaveChanges, exclusivamente no seed validado, para preceder as transações históricas.
3. Gráfico de caixa com altura controlada e métricas de quatro indicadores em quatro colunas no desktop.
4. Campos de data personalizados com `ImmediateText`, evitando consultar o valor anterior após digitação.
5. Cabeçalho e ações do ranking empilhados no mobile, sem comprimir título/descrição.

## Limites e observações

- Não houve mudança de regras financeiras, domínio, permissões existentes, dashboards, PWA, banco ou packages.
- O aviso global de versão da PWA apareceu nas sessões de Development após recompilar. Sua mecânica não foi alterada nesta task; não é um alerta do relatório.
- Não foi realizada auditoria completa de leitor de tela, load test ou teste físico mobile. Gráficos têm legenda, rótulos e dados acessíveis em HTML/SVG; isso não substitui uma auditoria formal de acessibilidade.
- Saldos a receber/a pagar representam a situação **atual** dos vencimentos do período; não há reconstrução contábil histórica, exportação ou IA.
- CI e mergeabilidade devem ser consultados no PR, pois são estados externos posteriores ao commit.
