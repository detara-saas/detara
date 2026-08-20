# Arquitetura

A Detara é um monólito modular em uma única solution. Os limites de projeto deixam as responsabilidades técnicas visíveis; as fronteiras de negócio, ownership e regras cross-module estão formalizadas em [Fronteiras dos módulos](architecture/module-boundaries.md).

## Dependências

```text
Api ──> Application ──> Domain
 │             ▲
 ├──> Contracts│
 └──> Infrastructure ──> Domain

Web ──> Contracts
```

- `Domain`: entidades e invariantes, sem dependência de infraestrutura.
- `Application`: casos de uso MediatR, validação e portas.
- `Infrastructure`: EF Core, SQL Server, senha e repositórios.
- `Api`: HTTP, JWT, tratamento global, Swagger e composição de dependências.
- `Contracts`: requests, responses e envelope compartilhado.
- `Web`: Blazor WebAssembly, MudBlazor e estado da interface.

Controllers apenas traduzem HTTP para comandos e contratos. Não há repository genérico nem Unit of Work custom; o `DbContext` cumpre essa responsabilidade.

A API exige autenticação por padrão por meio de fallback policy; endpoints públicos precisam de `[AllowAnonymous]` explícito. Permissões específicas continuam sendo responsabilidade de policies/endpoints de cada módulo, pois autenticação não equivale a autorização.

Veja também os ADRs [001](adr/001-modular-monolith.md) e [002](adr/002-shared-database-multitenancy.md).

## Plano de controle da plataforma

A Administração da Plataforma permanece no mesmo monólito, mas forma um plano de controle com identidade e autorização independentes. Ela provisiona o grafo inicial de um tenant por um write path transacional explícito e consulta somente metadados de empresa/convite. Não existe contexto global operacional, impersonation ou dependência dos módulos tenant para a plataforma. Decisões e operação estão em [Administração da plataforma](platform-admin.md).

## Evolução modular

Plataforma, Clientes e Catálogo são os módulos atuais. Agenda será o próximo módulo e deverá referenciar Clientes e Catálogo por IDs e contratos mínimos, sem assumir ownership desses agregados.

A estratégia oficial é modularização lógica primeiro, modularização comercial quando necessária e distribuição física somente quando justificada. Não há mensageria, bancos separados ou infraestrutura de microserviços preventiva.
