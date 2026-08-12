# Multi-tenancy

O modelo atual compartilha aplicação e banco, isolando dados por `EmpresaId`.

## Leitura

`DetaraDbContext` aplica Global Query Filters em todas as entidades tenant existentes (`Usuario` e `Perfil`). O identificador vem de `IUsuarioContexto`, preenchido exclusivamente pelos claims validados do JWT. Requisições anônimas recebem `Guid.Empty` e não enxergam dados tenant.

## Escrita

Antes de qualquer `SaveChanges`, o contexto inspeciona inclusões, alterações e exclusões de `EntidadeEmpresaBase`. A operação é rejeitada quando:

- não há usuário autenticado;
- o `EmpresaId` original difere do claim;
- o `EmpresaId` atual difere do claim;
- houve tentativa de trocar o tenant da entidade.

Os filtros não são considerados uma barreira suficiente: a validação de escrita e constraints relacionais compõem a defesa em profundidade.

## Exceção controlada

O login é a única consulta que ignora filtros. Antes da autenticação, ela resolve uma empresa ativa pelo slug e limita explicitamente a consulta de usuário ao `EmpresaId` obtido. Isso permite e-mails iguais em empresas diferentes sem aceitar `EmpresaId` do cliente.

Testes relacionais SQLite validam consulta, criação, edição e exclusão entre empresas.
