# Central de Relatórios e Insights — Task 48

## Superfície e ownership

`/relatorios` é uma página Analytics/Finance **Fluid**, com cinco perspectivas e uma entrada na navegação. `GET /api/relatorios/{perspectiva}` aceita Geral (1), Serviços (2), Clientes (3), Financeiro (4) e Operação (5). Cada chamada consulta somente a perspectiva selecionada; não existe resposta global contendo dados de todas as abas. O Dashboard permanece inalterado.

Controller avalia policies canônicas → MediatR/`ObterRelatorioHandler` compõe → consultas de leitura de Atendimento, Financeiro e Agenda acessam apenas dados de seu ownership. Plataforma fornece apenas o fuso pelo contrato existente. Não há novas entidades, tabelas, índices, migrations, packages, cache ou escrita de relatórios.

## Datas e comparações

- Hoje, últimos 7/30 dias e este mês/ano terminam na data atual da empresa, obtida por `TimeProvider` + fuso da Plataforma. Mês anterior usa o mês fechado.
- Personalizado exige ambas as datas, fim ≥ início, anos 2000–9998 e até 1.096 dias inclusivos (aproximadamente três anos). O backend valida independentemente da UI.
- Intervalos UTC são `[início local convertido, dia posterior ao fim convertido)`, inclusive no último dia e respeitando DST.
- Hoje, janelas móveis e personalizado comparam com a janela imediatamente anterior de igual número de dias.
- Este mês compara do dia 1 ao mesmo dia do mês anterior, limitado ao último dia existente. Mês anterior compara com o mês fechado precedente. Este ano compara de 1/jan ao mesmo dia/mês do ano anterior (29/fev é limitado a 28/fev).
- Variação = `(atual − anterior) / abs(anterior) × 100`, uma casa decimal. Base zero retorna `null`, exibido como “Sem base de comparação”.
- Período é preservado ao trocar perspectiva. A Web cancela consultas anteriores e ignora respostas obsoletas; nenhuma preferência nova é persistida.

## Semântica das métricas

| Métrica | Fonte e definição |
|---|---|
| Receita recebida | Soma decimal de `Pagamento.Valor`, somente Confirmado, por `RecebidoEmUtc` no período. **Bruta**, antes de taxas. Estornado não entra. Não é valor de OS ou de catálogo. |
| Despesas pagas | Soma de `ContaPagar.ValorPago` vigente, status Pago, por `DataPagamento`. Competência, valor previsto e regras recorrentes não contam como pagamentos. Estorno limpa o snapshot e remove o valor do relatório. |
| Resultado operacional | Receita recebida − despesas pagas. Pode ser negativo. Não é lucro, DRE ou EBITDA; taxas de recebimento não são descontadas neste indicador bruto. |
| Atendimentos concluídos | Contagem de OS com status Concluída e `ConcluidaEmUtc` no período. Aberta, cancelada, em execução e aguardando retirada não entram. |
| Valor em atendimentos | Soma dos itens autorizados (quantidade × valor unitário histórico) − desconto autorizado + acréscimo autorizado de cada OS concluída. Inclui adicionais e cortesias. |
| Ticket médio | Valor em atendimentos / OS concluídas, duas casas decimais. Zero quando não há OS; não deriva de caixa. |
| Clientes atendidos | `DISTINCT ClienteId` entre OS concluídas no período. |
| Clientes novos | Primeira OS concluída em toda a história ocorre no período. Cadastro novo não implica cliente novo atendido. |
| Clientes recorrentes | Atendidos no período que também possuem conclusão anterior ao início. Novos + recorrentes = clientes atendidos. |
| Serviços/itens realizados | Soma das quantidades das linhas autorizadas de OS concluídas. Pacote é uma linha/quantidade, não explode seus componentes; cortesias contam quantidade e valor zero. |
| Top serviços | Top 10 separado por quantidade e por valor histórico dos itens, **antes** de desconto/acréscimo global da OS. Chave por tipo + ID do catálogo, nunca preço atual. Linhas personalizadas sem ID ficam distintas para não fundir serviços homônimos. |
| Top clientes/consumo | Soma do valor comercial final de OS concluídas por ClienteId. Frequência ordena quantidade de OS; desempata consumo e ID. Nome é snapshot histórico, sem documento, telefone ou email. |
| Orçamentos criados | Data de criação no período, em todos os status. Não é o denominador da conversão. |
| Aprovações/recusas | Data real da respectiva decisão no período **e estado atual** Aprovado/Recusado. Substituído não é aprovação vigente nem recusa. |
| Conversão | Aprovados / (aprovados + recusados), uma casa decimal. Pendentes, cancelados e substituídos excluídos. São decisões no período, não uma coorte de documentos criados no período. |
| A receber | Saldo atual `ValorOriginal − ValorRecebido` das contas não pagas com vencimento no período. Não reconstrói saldo histórico em uma data passada. |
| A pagar | Valor previsto de contas Pendentes com vencimento no período, incluindo vencidas. |
| Vencido a pagar | Subconjunto de A pagar com vencimento antes de hoje no fuso da empresa. Não mistura estoque vencido fora do período. |
| Categorias | Valores efetivamente pagos, agrupados pelo ID, nome histórico, inclusive categoria hoje inativa. Top 10 + Outras categorias; percentual sobre todas as despesas pagas. |
| Agenda | Início agendado no período. Total inclui todos os status; cancelado e não compareceu dependem desses estados explícitos, nunca de ausência de OS. |
| Dia da semana | Conclusão da OS no dia civil do tenant, segunda a domingo; dias sem conclusão são zero. |

Nomes históricos de um mesmo ID renomeado são representados pelo maior nome de snapshot na ordenação do banco (determinístico); valores nunca consultam o catálogo atual. É uma limitação explícita da leitura histórica, não identificação por nome.

### Diferenças conscientes do Dashboard existente

O Dashboard atual chama de receita líquida o recebido menos taxas e calcula alguns indicadores por início/finalização da execução. Esta central usa **receita recebida bruta**, ticket comercial de **conclusão/entrega** e conversão por **decisões vigentes**, com labels e tooltips próprios. Não se alterou o Dashboard nem o Financeiro para igualar números de semânticas distintas.

## Permissões e isolamento

- Geral: OrdemServico.Visualizar ou Financeiro.Visualizar; cada bloco é omitido quando falta sua policy. Sem Financeiro não se consulta nem retorna caixa, comparações financeiras, categorias, séries financeiras ou insights correspondentes.
- Serviços: OrdemServico.Visualizar ou Orcamentos.Visualizar; cada conjunto depende de sua policy.
- Clientes: **Clientes.Visualizar e OrdemServico.Visualizar**. Rankings nominais e links Cliente 360 somente aqui. O endpoint de Cliente 360 preserva sua própria autorização/isolamento.
- Financeiro: Financeiro.Visualizar obrigatório; acesso direto sem policy é 403.
- Operação: Agenda.Visualizar ou OrdemServico.Visualizar; campos indisponíveis são omitidos, não zerados.
- Tenant deriva exclusivamente de `IUsuarioContexto`. Query filters permanecem ativos e as consultas também restringem explicitamente EmpresaId. Nenhum input de tenant, entidade EF ou filtro arbitrário é aceito como autoridade.
- Nenhum dado comercial é cacheado offline; service worker, tokens e notificações não foram alterados.

## Gráficos e apresentação

Reutilizam o Design System e a abordagem de gráficos locais do Dashboard, sem biblioteca nova:

- `RelatorioCaixaChart`: duas linhas (receita contínua verde, despesa tracejada azul), escala/legenda, pontos com descrição acessível e valores por data; dia até 63 dias, mês acima disso. Mesmo componente em Geral e Financeiro.
- `RelatorioRanking`: barras horizontais para serviços, clientes por consumo/frequência e categorias. Valores e nomes ficam acessíveis em HTML, com links somente para clientes autorizados.
- `RelatorioSemanaChart`: sete barras verticais, segunda a domingo, com quantidades legíveis.
- `RelatorioIndicador` usa `DetaraMetricCard` e tooltip de definição. Cards de seção, skeleton e empty state são os componentes canônicos.
- Formatação monetária/percentual pt-BR; coordenadas SVG e percentuais CSS sempre invariant. Operações monetárias permanecem decimal, inclusive agregações SQL.
- Estados sem registros não geram séries fictícias; zeros em dias vazios de um período que possui movimentos apenas completam o eixo temporal real.

## Insights determinísticos

Não existe IA generativa, chamada externa, previsão ou persistência de insights. Ordem estável, até cinco observações:

1. Resultado negativo: despesas excedem receita, informa diferença. Caso contrário, variação de receita exige ≥3 recebimentos em **ambos** os períodos e base anterior positiva.
2. Variação de ticket: ≥3 conclusões em ambos os períodos e ticket anterior positivo.
3. Serviço/item líder: ≥3 execuções do primeiro colocado por quantidade.
4. Recorrência: ≥3 clientes atendidos; informa percentual com conclusão anterior.
5. Categoria líder: despesas >0; informa valor/participação.
6. Dia de maior volume: ≥7 conclusões; empate usa ordem segunda–domingo. Entra quando há espaço entre os cinco insights.

Sem amostra suficiente há empty state, não frases causais ou avaliações sobre a saúde do negócio.

## SQL e performance

Consultas `AsNoTracking`, projeções, SUM/COUNT/DISTINCT/GroupBy e Top 10 no banco. Totais de OS usam soma correlacionada dos próprios itens, independente de pagamentos. Não há join Financeiro × itens de OS, N+1 de rankings nem Task.WhenAll sobre o DbContext compartilhado.

SQL Server usa `AT TIME ZONE` com identificação Windows derivada do fuso IANA para agrupar séries por dia local. O adapter SQLite de testes agrega por minuto UTC antes de converter fuso (incluindo DST e offsets fracionários); não transfere entidades completas. EF 10 suporta somas decimais no provider SQLite usado pelos testes. Produção usa exclusivamente SQL Server.

## Demonstração

`presentation` mantém a confirmação local existente e reconstrói somente Prime Detail (`prime-detail-demo`). Além da base, inclui cinco OS concluídas e pagas, com datas relativas, três clientes e serviços variados, um atendimento no mês anterior e repetições recentes. `create`/`reset` permanecem com o cenário anterior. As datas sintéticas de fundação precedem transações; o ajuste está restrito ao seed validado por tenant/slug e não abre setters nem altera a auditoria de produção.

O QA usa banco SQL temporário próprio; nunca executa presentation em uma empresa real. Evidências e limitações de validação estão em `qa-task48.md`.
