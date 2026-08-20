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

Antes de criar uma nova página, identifique o arquétipo definido em `docs/design-system.md` e reutilize os padrões existentes. Não crie layouts isolados de feature sem justificativa explícita.

Prefira componentes e padrões Detara existentes antes de introduzir novas estruturas visuais. MudBlazor continua sendo a base; componentes compartilhados representam padrões de produto, não wrappers genéricos de HTML.

Não introduza spacing, cores, `max-width` ou layouts específicos de feature quando existir token ou padrão equivalente no Design System.

Não utilize um único `max-width` para todas as páginas. Antes de criar uma tela, escolha a estratégia de largura definida pelo Design System (`Fluid`, `Wide` ou `Focused`) com base no arquétipo e na densidade da página.

Dashboards, listagens, analytics, calendários e telas operacionais densas devem priorizar a área útil do shell e não devem nascer artificialmente centralizadas em containers estreitos.

## PWA

O Service Worker da Detara pode cachear somente o app shell e assets estáticos versionados. Não cacheie respostas da API, autenticação, JWT, dados comerciais ou financeiros para uso offline sem uma decisão arquitetural explícita.

Funcionalidades PWA devem ser testadas com o build publicado. O Service Worker de Development não oferece suporte offline deliberadamente, para que alterações locais não fiquem presas em cache.

Atualizações da PWA não devem recarregar automaticamente a aplicação enquanto o usuário pode estar executando trabalho não salvo. A ativação de uma nova versão exige ação explícita do usuário.

Não introduza fila offline, Background Sync, IndexedDB de negócio ou reenvio automático de comandos. Depois de reconectar, o usuário deve tentar a operação novamente.

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

Toda nova rota que recebe um ID pertencente a tenant deve possuir teste adversarial Empresa A versus Empresa B. Todo uso de `IgnoreQueryFilters` deve restringir explicitamente o tenant ou documentar por que é uma operação de sistema de menor privilégio.

Toda nova ação protegida deve possuir autorização no backend e teste de usuário anônimo/sem permissão; esconder UI nunca é suficiente. Exceções `[AllowAnonymous]` pertencem a uma whitelist mínima e revisada.

Nenhum input deve aceitar `EmpresaId`, auditoria, status interno ou valor derivado como fonte de autoridade quando esses dados puderem ser obtidos do contexto ou calculados no servidor. Use DTOs mínimos e mapeamento explícito; não faça bind de entidades EF.

Uploads, HTML configurável, novas integrações externas, `MarkupString`, `innerHTML`, `srcdoc`, raw SQL e mudanças no armazenamento de token exigem revisão de segurança e testes adversariais proporcionais ao risco.

Vulnerabilidade corrigida deve receber teste de regressão quando tecnicamente viável. Nenhum Critical ou High conhecido pode permanecer aberto para iniciar feature privilegiada.

## Git e qualidade

Execute `git status` antes de modificar e preserve alterações locais do usuário. Prefira commits pequenos e semânticos. Antes de concluir tarefa relevante, execute:

```bash
dotnet restore
dotnet build
dotnet test
```

Priorize, nesta ordem: segurança, correção, simplicidade, legibilidade, manutenibilidade e escalabilidade. Não faça uma grande refatoração quando uma pequena correção resolver corretamente.
