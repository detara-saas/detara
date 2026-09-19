# Capacidades por empresa

## Decisão

A ARCH-03 introduz capacidades persistidas por empresa como configuração de domínio do tenant. Elas não são feature flags de deploy, planos comerciais nem permissões de usuário.

- **Segmento** é metadata e escolhe apenas o preset na criação.
- **Capacidade** define quais módulos/comportamentos pertencem à empresa.
- **Permissão** define o que o usuário pode fazer dentro de uma capacidade disponível.

O acesso efetivo exige capacidade habilitada e permissão concedida. Segmento nunca é consultado para autorizar ou compor comportamento.

## Catálogo e preset atuais

O catálogo central fica em `Detara.Domain.Capacidades` e contém código estável, nome, categoria, dependências, possibilidade de configuração e ordem.

| Categoria | Código | Dependência | Estado nesta fase |
|---|---|---|---|
| Core | `clientes` | — | habilitada e bloqueada |
| Core | `servicos` | — | habilitada e bloqueada |
| Core | `pacotes` | — | habilitada e bloqueada |
| Core | `agenda` | — | habilitada e bloqueada |
| Core | `orcamentos` | — | habilitada e bloqueada |
| Core | `ordem-servico` | — | habilitada e bloqueada |
| Core | `financeiro` | — | habilitada e bloqueada |
| Core | `despesas` | — | habilitada e bloqueada |
| Automotivo | `veiculos` | — | habilitada e bloqueada |
| Automotivo | `check-in` | `veiculos` | habilitada e bloqueada |

O único segmento e preset produtivo é `estetica-automotiva`, que habilita todo o catálogo e reproduz integralmente o comportamento anterior. Aplicar o preset é uma ação de criação explícita; mudar segmento futuramente não deve sobrescrever customizações.

## Persistência e isolamento

`Empresa.SegmentoCodigo` armazena metadata limitada. `EmpresaCapacidade` é tenant-owned e registra `EmpresaId`, código, estado, auditoria padrão e versão de concorrência. A chave única `(EmpresaId, Codigo)` impede duplicidade e a FK restritiva para `Empresas` impede órfãos.

O filtro global de `EntidadeEmpresaBase` e a proteção de escrita do `DetaraDbContext` isolam leituras e mutações. O Platform Admin consulta outro tenant apenas em seu fluxo explícito, com `IgnoreQueryFilters` acompanhado por `EmpresaId == alvo` e MFA já obrigatório. Não existe endpoint produtivo de alteração na ARCH-03; a tela administrativa é somente leitura. Uma futura escrita deverá validar dependências, usar `Versao` e registrar auditoria administrativa append-only.

## Backend enforcement

`IEmpresaCapacidadesServico` oferece snapshot, `Possui`, validação e criação do preset. A primeira consulta carrega o snapshot lazy no serviço scoped; checagens seguintes na mesma request reutilizam a mesma tarefa. Não há Redis e as capacidades não são autoridade no JWT.

Policies de autorização compõem capacidade com o RBAC já existente. Veículos e seus endpoints de foto exigem `veiculos`; operações diretas de check-in e checklist exigem `check-in`. Capacidade desabilitada retorna `403` com código `capability_disabled`, sem refresh ou logout. `401` continua representando autenticação, e `402` continua reservado à suspensão comercial.

O endpoint tenant `GET /api/configuracoes/capacidades` obtém a empresa exclusivamente do contexto autenticado. Parâmetros de `EmpresaId` do browser não são usados como autoridade.

## Frontend

`EmpresaCapacidadesState` carrega o snapshot uma vez por sessão, compartilha `Possui`, permite recarga futura e é limpo em login/logout. Os estados `NaoCarregado`, `Carregando`, `Carregado`, `FalhaRede` e `SessaoInvalida` são distintos. Falhas são fechadas: módulos condicionais não são liberados sem snapshot válido.

O menu de Veículos exige capacidade e permissão. Favoritos respeitam a mesma capacidade. As rotas diretas de lista, detalhe e edição não iniciam a operação quando Veículos está desligado. A ação de check-in da OS é substituída por estado informativo quando a capacidade está desligada. O backend permanece a autoridade final.

## Migration e compatibilidade

`AddCompanyCapabilities` adiciona o segmento, cria `EmpresasCapacidades`, a FK e o índice único, e faz backfill determinístico de todas as empresas existentes com as dez capacidades habilitadas. Novas empresas provisionadas pela plataforma, seed de desenvolvimento e bootstrap demo recebem o mesmo preset.

A ARCH-03 não altera nullability de `VeiculoId`, a máquina de estados da OS, PDFs, comunicação, dashboards ou documentos. Agenda, Orçamento, OS e Financeiro continuam automotivos nesta fase.

## Convenção do Core

Novas funcionalidades do Detara Core não devem introduzir dependência obrigatória de `VeiculoId`, check-in ou outro conceito automotivo sem justificativa explícita de domínio registrada na documentação arquitetural.

## Limitações e roadmap

- Veículos e Check-in permanecem bloqueados para alteração produtiva.
- Não existe preset nem suporte declarado para outros nichos.
- ARCH-04 tornará veículo opcional no backbone transacional.
- ARCH-05 tornará o check-in automotivo opcional na máquina de estados.
- ARCH-06 fará a composição ampla de UI, documentos e comunicação.
