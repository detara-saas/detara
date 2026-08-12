# ADR 002 — Multi-tenancy em banco compartilhado

Status: aceito.

## Decisão

Compartilhar aplicação e banco SQL Server. Entidades comerciais carregam `EmpresaId`, resolvido do JWT e nunca aceito como autoridade a partir da URL ou body.

## Consequências

Global Query Filters reduzem risco em leituras; validação central de `SaveChanges`, FKs e índices compostos protegem escritas e relações. Consultas que ignoram filtros são excepcionais, explícitas, documentadas e devem sempre aplicar o tenant de forma verificável.
