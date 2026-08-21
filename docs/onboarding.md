# Onboarding inicial da empresa

## Objetivo

O onboarding orienta uma empresa recém-provisionada até o primeiro uso operacional sem bloquear a navegação e sem criar dados demonstrativos. Ele aparece no Dashboard tenant e compõe um checklist a partir dos dados reais dos módulos existentes.

Não existe comando para concluir uma etapa, tabela de etapas ou flag de primeiro login. Uma alteração válida no módulo proprietário aparece na próxima consulta a `GET /api/onboarding`.

## Etapas

| Código | Etapa | Fonte de verdade | Critério de conclusão | Permissão da ação | Destino |
|---|---|---|---|---|---|
| `empresa` | Empresa provisionada | Plataforma / `Empresa` | empresa ativa com nome fantasia, razão social, CPF/CNPJ, slug e fuso horário | sem ação tenant | — |
| `operacao` | Configure sua operação | Atendimento / `ConfiguracaoOperacionalAtendimento` | configuração operacional salva, inclusive quando as opções escolhidas permanecem desabilitadas | `Configuracoes.Editar` | `/configuracoes` |
| `catalogo` | Cadastre seu primeiro serviço | Catálogo / `Servico` | ao menos um serviço ativo | `Servicos.Criar` | `/servicos/novo` |
| `cliente_veiculo` | Cadastre seu primeiro cliente e veículo | Clientes / `Cliente` e `Veiculo` | ao menos um cliente ativo com veículo ativo associado | `Clientes.Criar` ou `Veiculos.Criar`, conforme o próximo passo | `/clientes/novo` ou `/veiculos/novo` |
| `agenda` | Faça seu primeiro agendamento | Agenda / `Agendamento` | ao menos um agendamento cujo status não seja `Cancelado` nem `NaoCompareceu` | `Agenda.Criar` | `/agenda/novo` |

Serviço inativo não representa catálogo operacional. Um único agendamento cancelado ou sem comparecimento não representa operação iniciada.

## Permissões

O endpoint de leitura exige identidade tenant autenticada, mas não introduz uma permissão própria. Cada etapa pendente informa se o usuário pode executar a ação com base nas permissões canônicas do módulo proprietário. Sem permissão, a etapa permanece visível como informação e não oferece CTA.

As rotas de destino continuam protegidas no backend. Ocultar um botão nunca substitui autorização.

## Dashboard

- Empresa nova ou parcial: o checklist recebe prioridade e os indicadores demonstrativos não são apresentados como operação real.
- Empresa completa: o checklist fica compacto e o Dashboard volta a priorizar a visão operacional.
- Ocultar por enquanto: o card fica compacto e pode ser reaberto no próprio Dashboard.
- A preferência de recolhimento contém somente um booleano local, é identificada pelo usuário e reutiliza o serviço de preferências de interface. Nenhum dado comercial é armazenado no browser e nenhuma alteração de schema é necessária.
- Falha de carregamento: apresenta erro controlado com tentativa novamente, sem bloquear os demais módulos.

## Arquitetura e ownership

Onboarding é uma composição de leitura em `Detara.Application/Onboarding`. Ele não possui aggregates e não altera Empresa, Configuração Operacional, Serviço, Cliente, Veículo ou Agendamento.

Cada módulo implementa uma consulta mínima sobre suas próprias tabelas:

- `IPlataformaOnboardingConsulta`;
- `IAtendimentoOnboardingConsulta`;
- `ICatalogoOnboardingConsulta`;
- `IClientesOnboardingConsulta`;
- `IAgendaOnboardingConsulta`.

As consultas recebem o `EmpresaId` resolvido de `IUsuarioContexto`; a API não aceita `EmpresaId` do cliente. As verificações usam `AnyAsync`, projeções booleanas e filtros explícitos de tenant, sem listas completas, `Include` ou N+1.

## Multi-tenancy e segurança

- O endpoint é tenant autenticado e não possui `AllowAnonymous`.
- JWT de Platform Admin não autentica no scheme tenant.
- Empresa suspensa continua sujeita à revalidação de identidade existente.
- Nenhum identificador, dado pessoal, financeiro ou comercial é retornado.
- A preferência local de recolhimento não contém estado do onboarding nem fonte de autoridade.

## Empty states

Clientes, Veículos, Serviços, Agenda, Orçamentos, Ordens de Serviço e Contas a Receber distinguem:

- ausência real de registros, com explicação e CTA permissionado;
- ausência de correspondência para filtros, com orientação para revisar os filtros e sem CTA de criação enganoso.

Orçamento é apresentado como fluxo opcional. Conta a receber é explicada como consequência de uma OS com valor autorizado finalizada; nenhum recebível fictício é criado.

## Não escopo

Não fazem parte deste onboarding: dados demo, e-mail de lembrete, tour obrigatório, analytics, billing, entitlement, WhatsApp, PWA offline, cache distribuído, filas, microserviço, impersonation ou deploy.
