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

O login é a única consulta que ignora filtros. Antes da autenticação, ela resolve uma empresa ativa pelo slug e limita explicitamente a consulta de usuário ao `EmpresaId` obtido. Isso permite e-mails iguais em empresas diferentes sem aceitar `EmpresaId` do cliente.

Testes relacionais SQLite validam consulta, criação, edição e exclusão entre empresas.

Preferências e favoritos derivam de `EntidadeEmpresaBase`, recebem filtros globais e passam pela mesma validação de escrita. Os endpoints `/api/preferencias/me` resolvem usuário e empresa exclusivamente pelos claims autenticados; IDs de usuário/tenant não fazem parte dos contratos públicos.

## Operações que exigem revisão explícita

`IgnoreQueryFilters`, `ExecuteUpdate`, `ExecuteDelete` e SQL bruto podem contornar parte das proteções do `SaveChanges`. Não são usados nos módulos atuais, exceto o `IgnoreQueryFilters` limitado do login. Qualquer uso futuro deve filtrar `EmpresaId` explicitamente, permanecer encapsulado em infraestrutura e receber teste de isolamento.

Operações administrativas da plataforma e provisionamento de produção ainda não possuem bypass genérico. Devem usar um fluxo separado, explícito e inacessível a usuários comuns quando forem implementadas.
