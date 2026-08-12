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

Veja também os ADRs [001](adr/001-modular-monolith.md) e [002](adr/002-shared-database-multitenancy.md).
