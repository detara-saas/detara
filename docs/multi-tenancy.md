# Multi-tenancy

O modelo atual compartilha aplicação e banco, isolando dados por `EmpresaId`.

## Leitura

`DetaraDbContext` aplica automaticamente um Global Query Filter a todo tipo derivado de `EntidadeEmpresaBase`. Assim, novas entidades tenant entram protegidas sem depender de uma chamada manual a `HasQueryFilter`. O identificador vem de `IUsuarioContexto`, preenchido exclusivamente pelos claims `sub` e `empresa_id` de um JWT validado. Identidade sem ambos os GUIDs válidos é tratada como anônima e não enxerga dados tenant.

## Escrita

Antes de qualquer `SaveChanges`, o contexto inspeciona inclusões, alterações e exclusões de `EntidadeEmpresaBase`. A operação é rejeitada quando:

- não há usuário autenticado;
- o `EmpresaId` original difere do claim;
- o `EmpresaId` atual difere do claim;
- houve tentativa de trocar o tenant da entidade.

Os filtros não são considerados uma barreira suficiente: a validação de escrita, FKs para `Empresa`, FK composta de `Usuario` para `Perfil` e `EmpresaId` como token de concorrência compõem a defesa em profundidade. O token de concorrência inclui o tenant no `WHERE` de updates e deletes, impedindo que uma entidade desconectada com `EmpresaId` forjado altere uma linha de outra empresa.

## Exceção controlada

O login tenant é a única consulta de identidade que ignora filtros antes da autenticação. Ela está encapsulada em `IConsultaIdentidadeLoginTenant`, parte do e-mail normalizado e projeta somente os candidatos e metadados necessários para validar a identidade. O contrato público não recebe slug nem `EmpresaId`.

E-mails iguais permanecem permitidos em empresas diferentes. O handler verifica todos os hashes candidatos: uma membership válida emite o JWT daquele tenant; duas ou mais produzem um challenge protegido de curta duração. A empresa escolhida deve constar no challenge e a consulta final usa simultaneamente `UsuarioId` e `EmpresaId`, revalidando usuário, empresa, perfil e versões antes de emitir o token. Nenhuma empresa é selecionada por ordem, nome ou primeiro resultado.

A consulta inicial é única e não usa `Include` nem N+1. O custo variável é a verificação de senha para cada membership encontrada, uma escolha deliberada para evitar enumeração e parada temporal antecipada. O cadastro de memberships continua sendo um fluxo administrativo controlado e o endpoint conserva limite de 10 tentativas por minuto por origem.

Testes relacionais SQLite validam consulta, criação, edição e exclusão entre empresas.

Preferências e favoritos derivam de `EntidadeEmpresaBase`, recebem filtros globais e passam pela mesma validação de escrita. Os endpoints `/api/preferencias/me` resolvem usuário e empresa exclusivamente pelos claims autenticados; IDs de usuário/tenant não fazem parte dos contratos públicos.

## Operações que exigem revisão explícita

`IgnoreQueryFilters`, `ExecuteUpdate`, `ExecuteDelete` e SQL bruto podem contornar parte das proteções do `SaveChanges`. No login pré-tenant, a exceção parte exclusivamente do e-mail e retorna projeção mínima; na seleção, o predicado exige `UsuarioId` e `EmpresaId` autorizados pelo challenge. Qualquer uso futuro deve permanecer encapsulado em infraestrutura, aplicar o menor predicado possível e receber teste de isolamento.

Operações administrativas da plataforma e provisionamento de produção ainda não possuem bypass genérico. Devem usar um fluxo separado, explícito e inacessível a usuários comuns quando forem implementadas.
