# Detara — regras permanentes de desenvolvimento

## Projeto

Detara é um SaaS multi-tenant de gestão para empresas de estética e cuidado automotivo. A stack oficial é .NET 10, ASP.NET Core Web API, EF Core, SQL Server, MediatR, Blazor WebAssembly, MudBlazor e Docker. Não troque a stack sem decisão arquitetural explícita.

## Frontend e identidade

Antes de qualquer trabalho de interface, leia `docs/design-system.md` e consulte `docs/design/`. Utilize exclusivamente os assets oficiais de `src/Detara.Web/wwwroot/brand/`; não redesenhe, distorça, recolora, rotacione, aplique glow/sombra ou recrie a marca.

Toda interface deve:

- suportar os modos Sistema, Claro e Escuro;
- funcionar em desktop, tablet e mobile, mudando a composição quando necessário;
- manter a identidade Detara e evitar aparência de template administrativo genérico;
- usar tokens centralizados e componentes consistentes, sem cores hardcoded espalhadas;
- priorizar informação operacional, acessibilidade e alvos de toque adequados;
- usar MudBlazor/Material Icons, sem emojis como ícones funcionais.

## Multi-tenancy

Segurança entre tenants é crítica. Nunca confie em `EmpresaId` vindo do frontend quando o tenant puder ser obtido do usuário autenticado. Toda entidade comercial multi-tenant deve respeitar isolamento de leitura e escrita. Preferências e favoritos pertencem ao usuário autenticado; URLs ou identificadores arbitrários enviados pelo browser devem ser rejeitados.

## Arquitetura

Mantenha o monólito modular e, preferencialmente, o fluxo `Controller → MediatR → Handler → Domain/Infrastructure`. Domain não depende de Infrastructure, Api ou Web. Não crie abstrações sem necessidade.

## Module Boundaries

Consulte `docs/architecture/module-boundaries.md` antes de criar ou integrar módulos.

- Cada módulo é dono do próprio domínio, invariantes e dados.
- Não modifique entidades de outro módulo diretamente.
- Referências cross-module devem preferir IDs e o menor contrato explícito necessário.
- Não consulte tabelas internas de outro módulo indiscriminadamente só porque o `DbContext` é compartilhado.
- Crie contratos internos somente quando existir uma integração real; não adicione service layers genéricos preventivos.
- Dependências circulares entre módulos são proibidas.
- O produto base não depende de add-ons.
- Add-ons estendem comportamento sem serem obrigatórios para o fluxo base.
- Considere eventos internos in-process para reações entre módulos quando houver caso real.
- Não adicione mensageria ou infraestrutura distribuída sem necessidade comprovada.
- Shared database não elimina data ownership nem autoriza grafos EF atravessando módulos.
- Evite cascade delete entre módulos; prefira `Restrict`, inativação e eventos conforme o contexto.
- Novos módulos devem nascer organizados pela fronteira de negócio nos projetos atuais.
- Mantenha o Shared Kernel pequeno e técnico; `Common`, `Shared`, `Helpers` e `Utils` não são depósitos de regras de negócio.
- Microserviço é uma decisão operacional futura, não o padrão inicial.

## Segurança

Nunca commite senha, JWT signing key, API key, connection string real, certificado/chave privada ou `.env` real. Nunca registre senha, JWT, secret ou connection string em logs.

## Git e qualidade

Execute `git status` antes de modificar e preserve alterações locais do usuário. Prefira commits pequenos e semânticos. Antes de concluir tarefa relevante, execute:

```bash
dotnet restore
dotnet build
dotnet test
```

Priorize, nesta ordem: segurança, correção, simplicidade, legibilidade, manutenibilidade e escalabilidade. Não faça uma grande refatoração quando uma pequena correção resolver corretamente.
