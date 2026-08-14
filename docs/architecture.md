# Arquitetura

A Detara começa como monólito modular em uma única solution. Os limites de projeto deixam as responsabilidades visíveis sem introduzir rede, mensageria ou consistência distribuída.

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
