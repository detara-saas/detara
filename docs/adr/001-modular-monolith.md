# ADR 001 — Monólito modular com fronteiras explícitas

Status: aceito.

## Contexto

A Detara possui Plataforma, Clientes e Catálogo e receberá Agenda, Atendimento, Financeiro e possíveis add-ons como Estoque e CRM. O domínio ainda evolui rapidamente, a equipe não precisa de deploys independentes e não existe carga que justifique distribuição física.

Ao mesmo tempo, um monólito sem ownership permitiria acesso indiscriminado ao banco, agregados compartilhados e dependências circulares, tornando futuras evoluções caras.

## Decisão

A Detara permanecerá um **monólito modular** enquanto distribuição física não trouxer benefício comprovado.

Os módulos executam no mesmo processo, deploy e banco, mas possuem:

- domínio e dados sob ownership explícito;
- dependências direcionais e sem ciclos;
- comunicação cross-module pelo menor contrato necessário;
- Core independente de add-ons;
- possibilidade de eventos internos in-process;
- organização gradual por módulo nos projetos atuais.

Shared database não elimina fronteiras. Não serão criados assemblies, schemas ou bancos por módulo preventivamente. As regras completas estão em [Fronteiras dos módulos](../architecture/module-boundaries.md).

## Consequências positivas

- Operação, deploy, debugging e transações permanecem simples.
- Ownership reduz acoplamento e torna mudanças mais previsíveis.
- Contratos e eventos criados por necessidade formam seams para extrações futuras.
- Módulos opcionais podem existir comercialmente sem exigir microserviços.
- Agenda pode nascer como módulo sem assumir Clientes ou Catálogo.

## Trade-offs

- O compilador não impede todas as dependências entre módulos dentro do mesmo assembly.
- O `DbContext` compartilhado exige disciplina e revisão de código.
- Algumas integrações locais poderão precisar de adaptação se um módulo for extraído.
- Transações locais facilitam acoplamento; fluxos grandes precisam ser avaliados conscientemente.

## Alternativas rejeitadas

### Microserviços desde o início

Rejeitado por introduzir rede, mensageria, consistência distribuída, resiliência, observabilidade, múltiplos deploys e maior custo operacional sem necessidade atual.

### Monólito sem fronteiras

Rejeitado por gerar domínio misturado, acesso global ao banco, dependências circulares e baixa clareza de ownership, dificultando manutenção e extração futura.

### Assembly e banco por módulo agora

Rejeitado porque a quantidade atual de módulos e equipes não justifica dezenas de projetos, migrations coordenadas e composição mais complexa. Organização lógica é suficiente neste estágio.

## Critérios futuros de revisão

Esta decisão deve ser revista quando um módulo demonstrar escala, SLA, segurança, tecnologia, equipe, integrações ou estratégia comercial significativamente diferentes. Tamanho de código isoladamente não justifica microserviço.

Qualquer extração deve partir do ownership e dos contratos existentes, nunca de uma divisão técnica arbitrária.
