# Fronteiras dos módulos

## Administração da plataforma

A Administração da Plataforma é dona de `AdministradorPlataforma`, MFA/recovery, provisionamento, convites e `AuditoriaPlataforma`. Suas identidades não pertencem a tenants. Integrações com o domínio base usam IDs e o menor write path explícito necessário para criar ou alterar o estado de uma empresa; não autorizam navegação genérica pelo grafo operacional nem um contexto global sem filtros. O Platform Admin pode consultar somente metadados de empresa, usuário inicial e convite. Não há dependência inversa dos módulos tenant para a administração da plataforma.

Este documento é a fonte principal para ownership, dependências e comunicação entre os módulos da Detara. A decisão que o sustenta está no [ADR 001](../adr/001-modular-monolith.md).

## Objetivo

A Detara é um **monólito modular preparado para evolução arquitetural**. Hoje os módulos compartilham processo, deploy e banco SQL Server, mas suas fronteiras de negócio devem permanecer explícitas.

A estratégia é:

```text
Modularização lógica primeiro
        ↓
Modularização comercial quando necessária
        ↓
Distribuição física somente quando justificada
```

Um módulo comercial não é sinônimo de microserviço. Um add-on pode ser contratado separadamente e continuar dentro do mesmo processo e banco.

## Por que monólito modular

O produto e o domínio ainda evoluem rapidamente. O monólito modular preserva transações locais, desenvolvimento simples, um único pipeline e baixo custo operacional. Fronteiras explícitas evitam que essa simplicidade se transforme em um monólito acoplado e preservam uma rota realista de extração futura.

Permanecem os projetos atuais por camada:

```text
Detara.Api
Detara.Application
Detara.Domain
Detara.Infrastructure
Detara.Contracts
Detara.Web
```

Não serão criados assemblies por módulo enquanto a quantidade de módulos, equipes e integrações não justificar esse custo.

## Mapa inicial de módulos

| Módulo | Estado | Responsabilidade e ownership |
|---|---|---|
| Plataforma / Identidade | Atual, base | Empresa, usuário, perfil, permissão, preferência, autenticação e resolução do tenant |
| Clientes | Atual, base | Cadastro e identificação de clientes e veículos, incluindo fotos permanentes do cadastro do veículo |
| Catálogo | Atual, base | O que a empresa oferece: categorias, serviços e pacotes |
| Agenda | Atual, base | Agendamento, itens planejados, snapshots, reagendamento, status e consultas operacionais |
| Atendimento | Atual, base | Orçamento, configuração operacional, ordem de serviço, check-in, execução, fotos transacionais e entrega |
| Financeiro | Atual, base | Contas a receber, pagamentos, estornos e indicadores de recebimento |
| Notificações | Atual, base | Preferências, templates, intenções duráveis, tentativas e providers de e-mail/WhatsApp |
| Estoque | Futuro, add-on candidato | Produto, saldo, movimentação, inventário e consumo |
| CRM | Futuro, add-on candidato | Lead, follow-up, campanhas, relacionamento e pós-venda |
| Autoatendimento / Portal do Cliente | Futuro, add-on candidato | Experiência externa de catálogo, agenda, aprovações e acompanhamento, consumindo capacidades do Core |

Essa lista é uma visão inicial, não um enum fechado. Novos bounded contexts podem surgir e os existentes podem ser reorganizados com aprendizado real de produto.

Plataforma oferece capacidades fundamentais, mas não é um depósito genérico para funcionalidades que ainda não foram classificadas.

### Fluxo conceitual

```text
Plataforma
   ↑
   ├──────── Clientes
   └──────── Catálogo

Clientes ──────┐
               ▼
             Agenda
               ▲
Catálogo ──────┘
               │
               ▼
          Atendimento
          │     │     │
          ▼     ▼     ▼
     Financeiro Estoque CRM
                 opcional opcional
```

As setas mostram fluxo de informação ou reação conceitual. Não representam automaticamente referência entre assemblies, navegação EF ou permissão para consultar tabelas internas.

## Ownership e manipulação

Cada módulo é dono de seu domínio, invariantes e dados. Somente o módulo proprietário pode alterar seus agregados.

Um consumidor de outro módulo deve preferir:

```text
identificador
+
menor contrato explícito necessário
```

Exemplo: Agenda pode armazenar `ClienteId`, `VeiculoId`, `ServicoId` e `PacoteId`, mas não passa a ser dona desses cadastros. Alterar telefone, documento, veículo, preço base ou composição de pacote continua responsabilidade de Clientes ou Catálogo.

Aggregate roots não devem atravessar fronteiras como objetos mutáveis. Uma dependência entre módulos deve responder claramente:

1. Por que o consumidor precisa conhecer o outro módulo?
2. Qual é o menor contrato necessário?

“Porque o `DbContext` permite” não é justificativa arquitetural.

## Comunicação entre módulos

### Consultas e comandos

O `DetaraDbContext` compartilhado é uma decisão de infraestrutura, não uma API global. Um handler não pode consultar livremente tabelas internas de vários módulos.

Quando existir uma integração real, criar um contrato interno estreito, por exemplo uma consulta de cliente ou serviço, definido e implementado na fronteira adequada. Não criar antecipadamente `IClienteService`, `IServicoService` ou service layers genéricos sem consumidor real.

Chamadas locais podem permanecer in-process. Se um módulo for extraído, o contrato interno existente é o ponto natural para introduzir uma chamada remota.

### Eventos

Reações desacopladas podem usar Domain Events ou Application Events quando surgir um caso concreto. O primeiro mecanismo deve ser in-process e simples; esta arquitetura não exige barramento genérico.

Exemplo futuro:

```text
OrdemServicoFinalizada
    ├── Financeiro reage
    ├── Estoque reage, se contratado
    └── CRM reage, se contratado
```

Se houver distribuição física, o conceito de negócio pode evoluir para Integration Event e message broker. Broker, outbox, inbox, saga e consistência eventual só entram quando houver necessidade comprovada.

### Integrações externas

WhatsApp, e-mail, Google Calendar e provedores de pagamento devem ficar atrás de adapters/abstrações na fronteira apropriada. Agenda não deve conter código específico de provedor de WhatsApp; ela publica a intenção ou evento de notificação.

## Dependências

- Dependências circulares entre módulos são proibidas e indicam fronteira incorreta.
- O produto base não depende de add-ons.
- Add-ons estendem comportamento; sua ausência não bloqueia o fluxo essencial.
- Shared Kernel deve ser pequeno e técnico: `EntidadeBase`, `EntidadeEmpresaBase`, `IUsuarioContexto`, paginação, respostas de API e abstrações fundamentais.
- `Common`, `Shared`, `Helpers` ou `Utils` não são destinos padrão para regras de negócio.

Uma dependência comum e estável pode permanecer compartilhada. Uma regra pertencente a um domínio deve ficar no módulo proprietário, mesmo quando outro módulo gostaria de reutilizá-la.

## Banco compartilhado e data ownership

Hoje existe uma aplicação, um SQL Server e um database multi-tenant compartilhado. Não é necessário schema SQL por módulo para existir um bounded context.

| Entidade/tabela | Módulo proprietário |
|---|---|
| `Empresas` | Plataforma |
| `Usuarios` | Plataforma |
| `Perfis` | Plataforma |
| `Permissoes` | Plataforma |
| `PerfisPermissoes` | Plataforma |
| `UsuariosPreferencias` | Plataforma |
| `UsuariosPaginasFavoritas` | Plataforma |
| `Clientes` | Clientes |
| `Veiculos` | Clientes |
| `VeiculosFotos` | Clientes |
| `CategoriasServico` | Catálogo |
| `Servicos` | Catálogo |
| `Pacotes` | Catálogo |
| `PacotesServicos` | Catálogo |
| `Agendamentos` | Agenda |
| `AgendamentosItens` | Agenda |
| `Orcamentos` | Atendimento |
| `OrcamentosItens` | Atendimento |
| `OrcamentosHistoricosStatus` | Atendimento |
| `ConfiguracoesOperacionaisAtendimento` | Atendimento |
| `ChecklistModelos` | Atendimento |
| `ChecklistModeloItens` | Atendimento |
| `OrdensServico` | Atendimento |
| `OrdensServicoItens` | Atendimento |
| `OrdensServicoChecklist` | Atendimento |
| `OrdensServicoChecklistItens` | Atendimento |
| `OrdensServicoFotos` | Atendimento |
| `OrdensServicoHistoricosStatus` | Atendimento |
| `ContasReceber` | Financeiro |
| `Pagamentos` | Financeiro |
| `ConfiguracoesNotificacaoEmpresa` | Notificações |
| `TemplatesEmailEmpresa` | Notificações |
| `NotificacoesEmail` | Notificações |
| `TentativasNotificacaoEmail` | Notificações |
| `ComunicacoesCliente` | Notificações |
| `SessoesWhatsAppEmpresa` | Notificações |

Essa matriz deve ser atualizada quando uma tabela ou agregado for introduzido.

### Integridade e navegações

- Dentro de um módulo, FKs fortes e navegações EF são recomendadas quando protegem invariantes úteis.
- FKs cross-module são decisões deliberadas, avaliadas pelo equilíbrio entre integridade atual e custo de extração.
- Grandes grafos de navegação atravessando módulos devem ser evitados.
- Cascade delete não deve atravessar fronteiras. Preferir `Restrict`, inativação lógica e eventos.
- Add-ons potencialmente extraíveis devem evitar hard coupling com tabelas do produto base quando identificador, validação de aplicação ou evento forem suficientes.

### Transações e consistência

Transações locais podem envolver operações de mais de um módulo enquanto tudo estiver no mesmo banco, mas novos fluxos não devem depender de transações gigantes sem necessidade.

Dentro de um módulo, a regra padrão é consistência forte. Entre módulos distribuídos poderá existir consistência eventual; ela não será implementada preventivamente no monólito atual.

## Documentos transacionais e snapshots

Agenda e Atendimento podem referenciar IDs de Clientes e Catálogo. Agenda preserva snapshots de identificação do cliente/veículo e das referências do catálogo apresentadas no planejamento. Orçamentos e ordens de serviço, porém, deverão preservar também as informações comerciais negociadas — descrição, quantidade e preço praticado — para que alterações posteriores no Catálogo não reescrevam o histórico.

O snapshot pertence ao documento transacional. Ele não transfere ownership do cadastro original.

## Add-ons, entitlement e autorização

Módulo contratado pela empresa e permissão do usuário são conceitos distintos.

Modelo conceitual futuro, criado apenas quando existir o primeiro add-on comercial real:

```text
EmpresaModulo
    EmpresaId
    Modulo
    EhAtivo
    Plano
```

O acesso a um módulo opcional poderá exigir:

```text
empresa possui entitlement ativo
AND
usuário possui permissão
```

Permissões não representam assinatura comercial, e a ausência de uma permissão não prova que a empresa não contratou o módulo. Feature flags também têm outra finalidade: rollout técnico.

Entitlement futuro deve controlar de forma coerente rotas, sidebar, favoritos e acesso da API. Esconder a navegação não substitui autorização no backend.

## Organização do código

O inventário atual está organizado por funcionalidade em Application, Infrastructure e Contracts:

```text
Application/Clientes       Infrastructure/Clientes       Contracts/Clientes
Application/Catalogo       Infrastructure/Catalogo       Contracts/Catalogo
Application/Preferencias   Infrastructure/Preferencias   Contracts/Preferencias
```

Handlers estão em `Application/<Modulo>`, repositórios em `Infrastructure/<Modulo>`, contratos públicos em `Contracts/<Modulo>` e os controllers HTTP finos em `Api/Controllers`. O frontend usa páginas, componentes e clients HTTP próprios de Clientes, Veículos e Catálogo. Projeções de consulta ficam nos repositórios e o mapeamento HTTP permanece nos controllers.

O Domain ainda concentra entidades em `Domain/Entidades`, e controllers/pages permanecem em pastas amplas. Isso não constitui, por si só, violação de fronteira. Não haverá reorganização em massa.

### Auditoria da base atual

- Cliente e Veículo permanecem no mesmo módulo Clientes; a relação entre eles é interna e válida.
- Categoria, Serviço, Pacote e PacoteServico permanecem no Catálogo e não manipulam Clientes.
- Application e Api não usam o `DetaraDbContext` como API global; acesso persistente fica encapsulado em Infrastructure.
- O contrato técnico de ativação/inativação foi movido de `Contracts.Clientes` para `Contracts.Comum`, eliminando a única dependência nominal de Catálogo em Clientes encontrada no inventário.
- Não foram encontradas dependências circulares ou modificações de agregados entre módulos.

Novos módulos devem nascer organizados por ownership, por exemplo:

```text
Domain/Agenda/
Application/Agenda/
Infrastructure/Agenda/
Contracts/Agenda/
```

Commands, queries, handlers e validators podem ser separados gradualmente quando o tamanho justificar. Namespaces devem refletir o módulo sem criar assemblies artificiais.

Testes arquiteturais poderão ser adicionados quando namespaces e fronteiras estiverem estáveis o suficiente para produzir regras robustas. Não usar testes frágeis baseados apenas em strings.

## Agenda implementada

Agenda é dona de `Agendamento`, `AgendamentoItem`, reagendamento, status e consultas por período. Não é dona de Cliente, Veículo, Serviço, Pacote ou Empresa.

Na implementação inicial:

- armazenar os IDs necessários;
- validar e copiar Cliente/Veículo por `IClientesAgendaConsulta`;
- validar e copiar Serviço/Pacote por `ICatalogoAgendaConsulta`;
- obter o fuso IANA da empresa por `IFusoHorarioEmpresaConsulta`;
- consultar apenas projeções mínimas, com snapshots para leituras históricas;
- não alterar agregados de Clientes ou Catálogo;
- não criar navegações EF profundas atravessando esses módulos.

As tabelas da Agenda não possuem FKs cross-module para Clientes ou Catálogo. A integridade de entrada é validada pelos contratos internos e os IDs são mantidos para rastreabilidade. A FK composta entre `AgendamentosItens` e `Agendamentos` é interna, tenant-safe e forte.

Preço no Agendamento é somente snapshot da referência do Catálogo. Agenda não possui preço acordado, total a cobrar ou valor final; esses conceitos pertencerão ao futuro Orçamento.

Atendimento é dono de Orçamento, Ordem de Serviço e Checklist. Ele referencia Clientes e Catálogo sem assumir o cadastro deles.

## Atendimento implementado — Orçamentos

Atendimento é dono de `Orcamento`, `OrcamentoItem` e `HistoricoStatusOrcamento`. O módulo referencia Cliente, Veículo, Agendamento, Serviço, Pacote, Empresa e Usuário somente pelos IDs necessários e por consultas internas estreitas. Não existem FKs cross-module dessas referências; as FKs compostas tenant-safe existem apenas dentro do agregado de Orçamento.

O fluxo consome:

- Clientes por `IClientesAtendimentoConsulta`, validando tenant, atividade e pertencimento do Veículo ao Cliente;
- Agenda por `IAgendaAtendimentoIntegracao`, reutilizando snapshots apresentados no Agendamento e expondo somente as operações necessárias para criar ou sincronizar o atendimento;
- Catálogo por `ICatalogoAtendimentoConsulta`, copiando nome, descrição, tipo e preço de referência sem alterar Serviço/Pacote;
- Plataforma por `IPlataformaAtendimentoConsulta`, obtendo fuso, identificação da Empresa e nomes de usuários para histórico/PDF.

Orçamentos são mutáveis somente enquanto `Rascunho`. Após a emissão, qualquer mudança comercial exige um novo documento com novo ID, código e PDF. A emissão da nova proposta marca o documento de origem como `Substituido` somente se ele estiver `Emitido` ou `Aprovado`; criar ou abandonar um novo rascunho não altera o anterior. `Recusado` significa que o cliente recusou a proposta e nunca é reclassificado como `Substituido` apenas porque outra proposta foi criada.

`Expirado` é um estado efetivo calculado quando o status persistido é `Emitido` e `ValidoAte` é anterior à data local da Empresa. Não existe job de expiração. O PDF oficial é regenerado server-side a partir dos snapshots e permanece disponível para documentos recusados, cancelados, expirados ou substituídos. Se futuramente houver exigência jurídica maior, a evolução prevista é armazenar o arquivo final, hash e data/origem de envio sem alterar o documento comercial existente.

Uma Ordem de Serviço criada a partir de Orçamento aprovado copia itens, descrições, quantidades, valores negociados, desconto, acréscimo e total do Orçamento. Ela não reconstrói preços a partir do Catálogo e não nasce de orçamento recusado, cancelado, substituído ou expirado.

## Fluxo operacional Agenda ↔ Atendimento

Agenda continua dona de `Agendamento`; Atendimento continua dono de `Orcamento` e `OrdemServico`. A composição transversal fica em `Application/FluxoOperacional` quando precisa consultar ambos. Nenhum aggregate root atravessa a fronteira como objeto mutável e não há navegações EF ou FKs cross-module.

`IAgendaAtendimentoIntegracao` é o contrato estreito consumido por Atendimento. Sua implementação de infraestrutura valida explicitamente `EmpresaId` mesmo ao ignorar query filters e permite apenas: consultar o snapshot de um agendamento, criar Agenda a partir de orçamento aprovado, marcar o atendimento iniciado e concluir o atendimento. O mesmo `DetaraDbContext` scoped e um único `SaveChanges` preservam atomicidade local entre orçamento/agenda e OS/agenda.

Toda nova OS exige `AgendamentoOrigemId`. O índice filtrado único `(EmpresaId, AgendamentoOrigemId)` protege a cardinalidade de no máximo uma OS por Agenda; registros históricos continuam aceitando valor nulo. `Orcamento.AgendamentoId` representa o vínculo operacional atual, enquanto `AgendamentoOrigemId` preserva a origem histórica do documento comercial. Não existe cascade delete entre os módulos.

O início da OS move Agenda para o estado persistido `Compareceu`, apresentado ao usuário como **Em atendimento**. A entrega/conclusão da OS move Agenda para `Concluido`; finalizar serviços e aguardar retirada não conclui Agenda. O cancelamento da OS não conclui nem cancela Agenda automaticamente. Transições manuais da Agenda passam pela composição operacional e são bloqueadas quando contradizem a OS vinculada.

## Fundação operacional implementada

Atendimento é dono de `ConfiguracaoOperacionalAtendimento`, `ChecklistModelo` e `ChecklistModeloItem`. Cada empresa pode ter uma configuração e um modelo padrão de checklist de entrada. A ausência de registros representa os defaults desabilitados e não provoca escrita durante criação de empresa ou consulta. Desabilitar a exigência preserva o modelo configurado.

O modelo padrão é configuração mutável. Quando a Ordem de Serviço for implementada, Atendimento deverá copiar os itens para um snapshot pertencente à OS; alterações posteriores no modelo não poderão reescrever atendimentos históricos. Respostas e evidências preenchidas também pertencerão à OS.

Clientes é dono de `VeiculoFoto`, pois a imagem permanente descreve o cadastro e o histórico do veículo, independentemente de um atendimento. Fotos futuras de entrada, durante o serviço ou saída pertencerão à Ordem de Serviço em Atendimento e não reutilizarão `VeiculoFoto` como entidade genérica.

`IArquivoStorage` é uma abstração técnica de infraestrutura, não um módulo de mídia. A implementação local persiste conteúdo privado fora do `wwwroot`; o banco guarda apenas chave lógica e metadados. Um futuro adapter de Object Storage poderá substituir o provider sem mudar o ownership dos arquivos.

A FK composta tenant-safe de `VeiculosFotos` para `Veiculos` é interna ao módulo Clientes e usa exclusão restritiva. Configuração e checklist não recebem FK para `Empresas`: o isolamento e a existência da empresa são protegidos pelo contexto autenticado e pelos índices únicos por `EmpresaId`, evitando acoplamento desnecessário entre Atendimento e Plataforma.

## Onboarding inicial implementado

Onboarding é uma composição transversal de leitura no Dashboard tenant e não possui aggregates próprios. O read model consulta somente estados booleanos através de contratos estreitos implementados pelos módulos Plataforma, Atendimento, Catálogo, Clientes e Agenda. Nenhum handler de onboarding usa o `DetaraDbContext` como API global e nenhum módulo externo é alterado pela composição.

O `EmpresaId` vem exclusivamente de `IUsuarioContexto`. Empresa ativa e configurada, configuração operacional salva, serviço ativo, cliente ativo com veículo ativo e agendamento válido permanecem conceitos dos respectivos módulos. O progresso é recalculado a cada consulta, sem flags redundantes, eventos, cache ou persistência adicional.

Permissões de ação são avaliadas no boundary HTTP com as policies canônicas e entregues ao read model apenas para compor CTAs. Isso não substitui a autorização dos endpoints de destino e não cria permissão específica de onboarding.

## Dashboard operacional implementado

Dashboard é uma composição transversal de leitura, sem aggregate ou tabela próprios. O handler recebe o `EmpresaId` somente de `IUsuarioContexto` e consulta contratos mínimos implementados por Plataforma, Agenda, Atendimento e Financeiro. Cada implementação lê exclusivamente as tabelas do módulo proprietário, com query filters ativos, projeções pequenas e sem alterar qualquer aggregate.

As policies canônicas são avaliadas no boundary HTTP e determinam quais contratos podem ser consultados. Agenda, Orçamentos, Ordem de Serviço e Financeiro são omitidos do read model quando o usuário não possui a respectiva permissão de visualização; em especial, valores financeiros nunca são buscados nem retornados sem `Financeiro.Visualizar`. A composição não cria `Dashboard.Visualizar`, não usa cache e não executa queries paralelas sobre o `DbContext` compartilhado.

## Add-ons e exemplos de evolução

### Autoatendimento / Portal do Cliente

Autoatendimento é candidato a add-on comercial futuro. Ele deverá consumir capacidades de Agenda e Catálogo, sem criar agendas ou catálogos paralelos (`PortalAgendamento`, `PortalServico` ou `PortalPacote`). O módulo opcional será dono da própria configuração de publicação; por isso o Catálogo Core não recebe `DisponivelNoPortal` antecipadamente.

O Portal poderá futuramente agendar, reagendar, cancelar, consultar catálogo, visualizar/aprovar orçamentos e acompanhar atendimentos. Nenhuma API pública, entitlement ou dependência do Core no Portal é criada antes da implementação real do add-on.

### Estoque

Hoje, Atendimento e Estoque podem executar no mesmo processo e banco. A conclusão de uma ordem não depende da existência de Estoque. Se contratado, Estoque reage e gera movimentações.

Futuramente:

```text
Detara Core
    │ Integration Event
    ▼
Detara Estoque
```

O Core continua operando se o serviço de Estoque estiver ausente ou desabilitado.

### Financeiro implementado

Financeiro é dono de `ContaReceber` e `Pagamento`. A conta nasce exatamente quando Atendimento finaliza a execução e a OS passa de `EmExecucao` para `AguardandoRetirada`; total zero não gera cobrança. A origem comercial é sempre `OrdemServico.TotalAutorizado`, preservada com subtotal, desconto, acréscimo e snapshots mínimos de OS, cliente e veículo. Financeiro não consulta Catálogo ou Orçamento para reconstruir valores.

A infraestrutura atual não possui um Unit of Work separado. A integração usa a menor orquestração Application-level: `FinalizarExecucaoHandler` entrega um fato imutável a `IIntegracaoFinanceiroOrdensServico`; os repositórios de Atendimento e Financeiro compartilham o mesmo `DetaraDbContext` scoped, e um único `SaveChanges` confirma a transição da OS, seu histórico e a nova conta atomicamente. A chave única `(EmpresaId, OrdemServicoId)` e a verificação no repositório tornam o consumo idempotente. Não há broker, outbox ou dependência de `Detara.Domain.Atendimento` em Financeiro.

`ContaReceber` referencia OS, Cliente e Veículo somente por IDs e snapshots, sem FKs cross-module. A relação conta → pagamentos é interna ao Financeiro e possui FK composta tenant-safe com delete restritivo. Pagamentos são imutáveis; correções usam estorno auditado. A conta mantém o saldo e uma versão de concorrência incrementada em cada mutação, impedindo overpayment por requests simultâneos.

### Comunicação transacional implementada

Notificações é dono de `ConfiguracaoNotificacaoEmpresa`, `TemplateEmailEmpresa`, `NotificacaoEmail`, `TentativaNotificacaoEmail`, `ComunicacaoCliente` e `SessaoWhatsAppEmpresa`. Atendimento entrega somente o fato mínimo de que uma OS mudou de `EmExecucao` para `AguardandoRetirada`; Clientes e Plataforma são consultados por contratos internos estreitos para obter os contatos atuais do cliente, nome da empresa e e-mail do usuário autenticado. Nenhum agregado externo é modificado e não existem FKs cross-module.

Quando o envio automático está habilitado, `FinalizarExecucaoHandler` prepara a intenção durável pelo contrato `IIntegracaoNotificacoesOrdensServico`. O mesmo `DetaraDbContext` scoped e o mesmo `SaveChanges` confirmam a transição da OS, histórico, conta a receber e intenção de e-mail. A chamada ao Resend nunca ocorre nessa transação. A unicidade `(EmpresaId, Tipo, OrdemServicoId)` torna o gatilho idempotente.

`ComunicacaoCliente` é o histórico de negócio neutro por canal. A configuração aceita exatamente `Nenhum`, `Email` ou `WhatsApp`; portanto, a transição da OS nunca prepara dois canais automáticos. `NotificacaoEmail` e suas tentativas continuam sendo o detalhe técnico durável do provider de e-mail e compartilham o mesmo identificador da comunicação correspondente. WhatsApp usa um provider HTTP único para um gateway Node separado; o tenant vem do contexto autenticado, e `SessaoWhatsAppEmpresa` mantém somente metadados tenant-safe enquanto as credenciais `LocalAuth` ficam no volume exclusivo do gateway.

O worker pertencente a Notificações processa a fila persistente em lotes, usa versão de concorrência para claim e envia com a chave estável `notificacao-email/{id}`. Conteúdo, destinatário, Reply-To e origem do template são snapshots; alterações posteriores em Cliente, configuração ou template não reescrevem o histórico. A única relação EF é Notificação → Tentativas, interna ao módulo e com delete restritivo.

Concluir a OS não cria outra conta e não significa pagamento. Da mesma forma, pagar a conta não altera o estado operacional da OS. O módulo permanece no monólito; uma eventual extração futura depende dos critérios operacionais abaixo, não apenas de sua existência.

## Critérios para extração futura

Um módulo pode virar serviço independente quando existir benefício comprovado, como:

- escala ou carga muito diferente;
- necessidade de deploy, SLA ou tecnologia independente;
- equipe com autonomia operacional;
- segurança ou regulação específica;
- integrações externas intensas;
- estratégia comercial que exija isolamento;
- custo operacional menor que o benefício da separação.

Quantidade de linhas de código, isoladamente, não é critério.

Processo conceitual de extração:

1. Confirmar o módulo e seu ownership.
2. Inventariar contratos consumidos e oferecidos.
3. Eliminar acessos externos diretos às tabelas do módulo.
4. Transformar chamadas internas em contratos remotos.
5. Migrar os dados proprietários.
6. Transformar eventos internos em integration events.
7. Introduzir somente a infraestrutura distribuída necessária.
8. Definir observabilidade, resiliência e estratégia de consistência.

## Anti-patterns

São proibidos ou exigem correção arquitetural:

- **DbContext como API global:** handler consultando tabelas de vários módulos apenas porque estão disponíveis.
- **Agregados mutáveis compartilhados:** um módulo carregando e modificando aggregate root de outro.
- **Grafo EF global:** navegações atravessando Cliente → Agenda → Atendimento → Financeiro.
- **Common gigante:** regras de negócio movidas para uma pasta genérica sem ownership.
- **Dependência circular:** Agenda dependendo de Atendimento que depende novamente de Agenda.
- **Add-on obrigatório:** fluxo essencial do Core falhando porque Estoque ou CRM não existem.
- **Microserviços preventivos:** mensageria, service discovery, API gateway, bancos separados ou containers por módulo sem problema real.

## Checklist para novas funcionalidades

Antes de implementar:

1. Qual módulo é dono do novo conceito?
2. Quais dados ele possui?
3. Ele precisa conhecer outro módulo? Por quê?
4. O menor vínculo possível é um ID, consulta ou evento?
5. Existe acesso direto a tabela interna alheia que deveria virar contrato?
6. O fluxo base passou a depender de add-on?
7. Há cascade, navegação ou transação atravessando fronteiras sem justificativa?
8. A matriz de ownership e este documento precisam ser atualizados?
