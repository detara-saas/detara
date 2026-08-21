# Banco de dados

SQL Server com EF Core Code First. `InitialCreate` cria:

- `Empresas`
- `Usuarios`
- `Perfis`
- `Permissoes`
- `PerfisPermissoes`

O banco é compartilhado entre os módulos, mas cada tabela possui um módulo proprietário. Shared database é uma decisão de infraestrutura e não autoriza acesso indiscriminado entre módulos. A matriz completa de ownership e as regras para FKs cross-module estão em [Fronteiras dos módulos](architecture/module-boundaries.md).

Índices únicos: `Empresa.Slug`, `Empresa.CpfCnpj`, `Permissao.Codigo`, `Perfil(EmpresaId, NomeNormalizado)` e `Usuario(EmpresaId, Email)`. `Usuario.Email` também possui índice não exclusivo para a busca de login pré-tenant.

`AdministracaoBasicaTenant` adiciona versões de concorrência separadas para cadastro da empresa, usuário e perfil; versão de segurança explícita do usuário; descrição/flag de sistema/nome normalizado do perfil; e origem/criador Tenant opcional no convite compartilhado. O índice de convite inclui `(EmpresaId, UsuarioId, Origem)` e a FK composta do criador Tenant impede referência entre empresas.

A associação de usuário com perfil usa FK composta `(EmpresaId, PerfilId)`, impedindo associação entre tenants no próprio banco. Novas entidades comerciais devem herdar `EntidadeEmpresaBase` e adotar índices com `EmpresaId` quando a unicidade ou consulta for local à empresa.

`StrengthenFoundation` adiciona FKs restritivas de `Perfil.EmpresaId` e `Usuario.EmpresaId` para `Empresa.Id`. A unicidade de e-mail é deliberadamente por empresa (`EmpresaId + Email`), permitindo o mesmo endereço em tenants diferentes. `EmpresaId` também é token de concorrência no modelo EF; essa marca não exige coluna adicional.

## Preferências da interface

A migration `AddUserInterfacePreferences` adiciona:

- `UsuariosPreferencias`, única por `(EmpresaId, UsuarioId)`;
- `UsuariosPaginasFavoritas`, única por `(EmpresaId, UsuarioPreferenciaId, Pagina)`;
- FK composta da preferência para o usuário e do favorito para a preferência.

Tema, idioma, estado da sidebar, página inicial e favoritos pertencem ao usuário autenticado. O browser nunca envia `UsuarioId` ou `EmpresaId` para atualizar esses dados.

## Clientes e veículos

A migration `AddClientesEVeiculos` adiciona:

- `Clientes`, com documento opcional e único por `(EmpresaId, CpfCnpj)` quando preenchido;
- `Veiculos`, com placa única por `(EmpresaId, Placa)`;
- FK composta `(EmpresaId, ClienteId)` de veículo para cliente;
- FKs para empresa e relacionamento Cliente → Veículo com exclusão restritiva;
- índices de busca por nome, telefone, documento, placa e cliente.

Clientes e veículos herdam `EntidadeEmpresaBase`, portanto recebem automaticamente filtro global, validação de escrita e token de concorrência por tenant. Documento e placa são armazenados normalizados.

## Catálogo de serviços e pacotes

A migration `AddServicosCategoriasEPacotes` adiciona:

- `CategoriasServico`, com nome único por `(EmpresaId, Nome)` e ordenação própria;
- `Servicos`, com nome único por `(EmpresaId, CategoriaServicoId, Nome)`, tipo/preço de referência e duração opcional;
- `Pacotes`, com nome único por `(EmpresaId, Nome)` e tipo/preço de referência independente;
- `PacotesServicos`, com composição ordenada e vínculo único por `(EmpresaId, PacoteId, ServicoId)`;
- FKs compostas para categoria, pacote e serviço, impedindo associações entre tenants no banco;
- FKs para empresa e exclusões restritivas em todos os relacionamentos.

Categorias, serviços e pacotes usam inativação lógica independente. A soma dos serviços, a duração total e a economia do pacote são calculadas nas consultas e não são persistidas. A economia só é apresentada quando todos os serviços possuem preço e o preço do pacote é menor que a soma individual.

`AddCatalogPricingType` adiciona `TipoPrecificacao` a Serviços e Pacotes. O backfill classifica registros com preço como `Fixo` e sem preço como `SobConsulta`; nenhum dado existente é inferido como `APartirDe`.

## Timezone da empresa

`AddCompanyTimeZone` adiciona `Empresa.FusoHorario`, usando identificador IANA e `America/Sao_Paulo` como default para dados existentes. Agenda persiste instantes em UTC e converte entradas/saídas pelo fuso da empresa.

## Agenda

`AddAgenda` adiciona:

- `Agendamentos`, com snapshots de Cliente e Veículo, início UTC, duração planejada, status e observações separadas;
- `AgendamentosItens`, com snapshots de nome, descrição, tipo/preço e duração de referência de Serviço/Pacote;
- FK composta `(EmpresaId, AgendamentoId)` interna à Agenda, com cascade apenas para a composição do agregado;
- índices por tenant/início, tenant/status/início, cliente, veículo e recuperação de itens do catálogo;
- unicidade tenant-safe da ordem e do item dentro do Agendamento.

Não existem FKs cross-module de Agenda para Clientes ou Catálogo. Os vínculos são validados por contratos internos antes da gravação e preservados por ID + snapshot. O Agendamento não possui preço acordado ou total comercial.

## Atendimento — Orçamentos

`AddOrcamentos` adiciona:

- `Orcamentos`, com código oficial único por empresa, snapshots de Cliente/Veículo, origem opcional por Agendamento, origem opcional por outro Orçamento, validade comercial, valores e timestamps de transição;
- `OrcamentosItens`, com snapshots de Serviço/Pacote ou item personalizado, referência interna do Catálogo, quantidade e valor unitário negociado;
- `OrcamentosHistoricosStatus`, com status, instante UTC, usuário responsável e observação opcional;
- FKs compostas `(EmpresaId, OrcamentoId)` apenas dentro do módulo Atendimento, protegendo Itens e Histórico;
- índices de código, status, criação, cliente, veículo, Agendamento de origem e Orçamento de origem, sempre iniciados por `EmpresaId`.

Não existem FKs cross-module para Cliente, Veículo, Agendamento, Serviço, Pacote, Empresa ou Usuário. A integridade de entrada é validada pelos contratos internos e o histórico é preservado por ID + snapshot. `Subtotal` e `Total` são derivados de quantidade, valor unitário, desconto e acréscimo; não são colunas redundantes.

O código é criado uma única vez na emissão no formato `ORC-AAAA-XXXXXXXXXXXX`, derivado do GUID do documento e protegido por índice único `(EmpresaId, Codigo)`. Rascunhos mantêm código nulo.

## Fundação operacional e checklist

A migration `AddOperationalSettingsAndChecklist` adiciona:

- `ConfiguracoesOperacionaisAtendimento`, única por `EmpresaId`, com os níveis desabilitado, opcional ou obrigatório para checklist de entrada, fotos de entrada e fotos de saída;
- `ChecklistModelos`, único por `EmpresaId` nesta versão inicial;
- `ChecklistModeloItens`, com FK composta `(EmpresaId, ChecklistModeloId)`, cascade somente dentro do agregado e ordem única por modelo;
- validação de domínio case-insensitive após trim para bloquear duplicidades de itens.

A ausência da configuração ou do modelo é válida e representa defaults desabilitados. Nenhum registro é criado ao cadastrar uma empresa ou apenas consultar a configuração. Não existem FKs dessas tabelas para `Empresas`, pois a referência é cross-module com Plataforma; os filtros e interceptações tenant-safe do `DetaraDbContext`, os repositórios e os índices por empresa protegem leitura e escrita.

## Fotos permanentes de veículos

A migration `AddVehiclePhotos` adiciona:

- chave alternativa tenant-safe `(EmpresaId, Id)` em `Veiculos`;
- `VeiculosFotos`, com metadados, chave lógica privada de storage e indicação de foto principal;
- FK composta `(EmpresaId, VeiculoId)` para `Veiculos`, interna ao módulo Clientes e com delete `Restrict`;
- chave de storage única e índice filtrado que permite no máximo uma foto principal por veículo.

O conteúdo binário e caminhos físicos não são armazenados no SQL Server. O banco contém somente `ChaveStorage`, nome original saneado, content type detectado, tamanho, principal e auditoria. A inativação do veículo não remove fotos.

## Atendimento — Ordens de Serviço

A migration `AddOrdensServico` adiciona `OrdensServico`, `OrdensServicoItens`,
`OrdensServicoChecklist`, `OrdensServicoChecklistItens`, `OrdensServicoFotos` e
`OrdensServicoHistoricosStatus`, além de `Orcamentos.OrdemServicoOrigemId` para
orçamentos complementares.

O código da OS é imutável e único por `(EmpresaId, Codigo)`. A origem por orçamento
principal também é única por tenant, impedindo duas OS para o mesmo documento aprovado.
Os índices de status, criação, cliente, veículo e origens começam por `EmpresaId`.
`OrcamentoItemOrigemId` é único por tenant nos itens da OS, tornando a incorporação de
adicionais aprovados idempotente.

Cliente, Veículo, Agenda e Catálogo são referenciados por IDs e snapshots, sem FKs
cross-module. As FKs compostas existem somente entre a OS e suas composições internas.
Não há FK destrutiva entre OS e Orçamento: cancelar uma OS não altera ou apaga o documento
comercial. Fotos transacionais usam o mesmo storage privado da aplicação, mas possuem
metadados próprios e não se relacionam com `VeiculosFotos`.

O total autorizado é derivado dos itens incorporados, descontos e acréscimos aprovados.
Financeiro consome esse total e nunca o reconstrói a partir do Catálogo. Estoque deverá
reagir ao consumo efetivamente executado quando o módulo existir.

## Financeiro — Contas a Receber e Pagamentos

A migration `AddContasReceberEPagamentos` adiciona `ContasReceber` e `Pagamentos`.
Uma conta é criada no mesmo `SaveChanges` que move a OS para `AguardandoRetirada`,
desde que o total autorizado seja maior que zero. `(EmpresaId, OrdemServicoId)` é único,
garantindo uma única cobrança por OS mesmo em reprocessamento.

`ContasReceber` preserva código da OS, cliente, veículo, subtotal, desconto, acréscimo e
valor original. Competência e vencimento inicial usam a data local da empresa na
finalização. `Vencido` não é persistido: é calculado por saldo, vencimento e timezone.
OS, Cliente e Veículo não possuem FKs cross-module; os snapshots tornam consultas
financeiras independentes desses agregados.

`Pagamentos` possui FK composta `(EmpresaId, ContaReceberId)` para `ContasReceber`, com
delete `Restrict`. Valor, taxa, forma e parcelas são imutáveis depois do registro. Estorno
preserva o pagamento original, responsável, data e motivo. `ContaReceber.Versao` é token
de concorrência otimista incrementado em pagamentos, estornos e alteração de vencimento,
impedindo que recebimentos simultâneos ultrapassem o saldo.

Os índices de status, competência, vencimento, cliente, veículo, criação e recebimento
começam por `EmpresaId`. O dashboard agrega no SQL Server faturamento por competência e
pagamentos confirmados por data de recebimento; pagamentos estornados são excluídos.

## Notificações — e-mail transacional

A migration `AddNotificacoesEmail` adiciona:

- `ConfiguracoesNotificacaoEmpresa`, única por `EmpresaId`, com opt-in explícito para o envio automático e Reply-To opcional;
- `TemplatesEmailEmpresa`, único por `(EmpresaId, Tipo)`, contendo somente assunto e HTML já sanitizado;
- `NotificacoesEmail`, com destinatário, nome, assunto, corpo HTML completo, Reply-To e origem do template preservados como snapshots;
- `TentativasNotificacaoEmail`, com número, origem automática/manual, responsável opcional, resultado, instante, ID do provedor e erro seguro;
- unicidade `(EmpresaId, Tipo, OrdemServicoId)` e índice de fila `(EmpresaId, Status, ProximaTentativaEmUtc)`;
- FK composta tenant-safe de Tentativa para Notificação, interna ao módulo e com delete `Restrict`.

A ausência de configuração significa envio automático desabilitado; GET não cria registros. O template padrão também não é seed: é materializado dinamicamente pela aplicação, e restaurar o padrão remove a customização do tenant. Não existem FKs para Empresa, OS, Cliente ou Usuário. `NotificacaoEmail.Versao` protege o claim otimista da fila, enquanto a idempotência externa usa `notificacao-email/{Id}`.

Aplicação de migration:

```powershell
dotnet tool restore
dotnet ef database update --project src/Detara.Infrastructure/Detara.Infrastructure.csproj --startup-project src/Detara.Api/Detara.Api.csproj
```

As migrations permanecem pendentes até aplicação deliberada pelo responsável pelo banco.
Em Development, a API valida migrations pendentes e informa o comando necessário; seed
de dados e evolução de schema são operações separadas. Production nunca executa migration
ou seed automaticamente.
