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
