# Detara Design System v1

Este documento é a fonte oficial de verdade visual da Detara. As referências aprovadas estão em `docs/design/` e os assets consumíveis em `src/Detara.Web/wwwroot/brand/`.

## Direção visual

A Detara deve parecer profissional, premium, tecnológica, moderna, automotiva, precisa, confiável e eficiente. Não deve parecer ERP antigo, template administrativo genérico, dashboard gratuito, interface vazia, excessivamente arredondada, cheia de gradientes ou gamer.

Prioridades de UX: utilidade operacional, clareza, velocidade, refinamento, consistência, responsividade, acessibilidade e personalização. Beleza nunca deve reduzir produtividade.

## Tokens

### Marca

| Token | Valor |
|---|---|
| Primary | `#00C996` |
| PrimaryDark | `#00A67E` |
| PrimaryLight | `#2EE6B8` |
| Secondary | `#2563EB` |
| Purple | `#7C3AED` |
| Teal | `#14B8A6` |

### Superfícies

| Token | Claro | Escuro |
|---|---|---|
| Background | `#F8FAFC` | `#0B1220` |
| Surface | `#FFFFFF` | `#111827` |
| SurfaceAlt | `#F1F5F9` | `#1F2937` |
| TextPrimary | `#111827` | `#F8FAFC` |
| TextSecondary | `#64748B` | `#94A3B8` |
| Border | `#CBD5E1` | `#334155` |

Semânticas: Success `#22C55E`, Warning `#F59E0B`, Error `#EF4444`, Info `#3B82F6`. Cores semânticas comunicam estado; não substituem a marca.

Os valores vivem em `DetaraTokens`, `DetaraTema` e variáveis CSS. Componentes não devem repetir hexadecimais.

## Temas

Há três escolhas: Sistema, Claro e Escuro. Sistema respeita `prefers-color-scheme`; escolha manual prevalece e é aplicada imediatamente no browser. A preferência persistente final pertence ao usuário no backend.

## Tipografia

Fonte: `Inter, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif`.

| Estilo | Tamanho/linha | Peso |
|---|---:|---:|
| Display | 40/48 | 800 |
| H1 | 32/40 | 800 |
| H2 | 24/32 | 700 |
| H3 | 20/28 | 600 |
| H4 | 18/26 | 600 |
| Body | 16/24 | 400 |
| Small | 14/20 | 400 |
| Caption | 12/16 | 500 |

## Espaçamento, radius e elevação

- Escala: 4, 8, 12, 16, 24, 32, 40 e 48 px.
- Label/campo: 8 px; campos relacionados: 16 px; grupos: 24–32 px.
- Cards: 20–24 px; página desktop: 24–32 px; mobile: 16 px.
- Radius: XS 4, SM 6, MD 10, LG 14 e XL 20 px. Pills somente quando semanticamente adequadas.
- Sombras são discretas; borda, superfície, espaço e hierarquia vêm primeiro. Dark mode não depende de sombras fortes.

## Arquitetura de páginas

Toda página autenticada deve começar pela escolha de um arquétipo. A composição pode variar conforme o domínio, mas largura, ritmo, hierarquia e comportamento responsivo vêm dos padrões compartilhados. Não crie um layout isolado para uma feature quando um arquétipo existente resolver o caso.

### Tokens estruturais

| Token CSS | Uso |
|---|---|
| `--detara-page-gutter` | gutter lateral responsivo do conteúdo autenticado (`24–32px` no desktop, `24px` no tablet e `16px` no mobile) |
| `--detara-page-wide-max-width` | limite de fluxos comerciais, detalhes densos e composições main + aside (`1720px`) |
| `--detara-page-focused-max-width` | limite de formulários simples e conteúdo de leitura concentrada (`1040px`) |
| `--detara-page-gap` | distância entre regiões principais da página (`24px`) |
| `--detara-section-gap` | ritmo entre seções relacionadas (`20px`) |
| `--detara-card-padding` | preenchimento padrão de card de seção (`24px`) |
| `--detara-form-gap` | distância entre grupos de formulário (`20px`) |
| `--detara-summary-width` | largura do resumo lateral no desktop (`350px`) |

Use `.page-container` em toda página autenticada e escolha explicitamente uma estratégia de largura. O default estrutural é fluido: telas operacionais novas não devem nascer artificialmente estreitas. Valores monetários usam `.currency-value` para preservar valor e símbolo na mesma linha quando houver espaço.

### Page Width Strategy

Largura de página e largura interna de leitura são decisões diferentes. Uma página fluida pode conter helper text ou empty state com largura legível; uma página ampla pode manter campos curtos em grids controlados.

| Variante | Classe | Quando usar | Comportamento |
|---|---|---|---|
| **Fluid** | `.page-container-fluid` | dashboards, analytics, financeiro, calendário, listagens e telas operacionais densas | ocupa toda a largura útil do shell, sem `max-width`, respeitando somente o gutter responsivo |
| **Wide** | `.page-container-wide` | formulários comerciais complexos, detalhes densos e composição main + summary aside | cresce até `1720px`; em Full HD acompanha praticamente toda a área útil e, em ultrawide, preserva linhas e campos confortáveis |
| **Focused** | `.page-container-focused` | formulários simples, configurações textuais isoladas, estados administrativos e edição concentrada | centraliza o fluxo em até `1040px`, sem alterar o gutter do shell |

Mapa de arquétipos:

- Dashboard / Operational Page, List Page, Calendar / Agenda e Analytics / Finance: **Fluid**.
- Complex Form, Commercial Form + Summary Aside e Detail Page densa: **Wide**.
- Simple Form, edição curta e conteúdo predominantemente vertical: **Focused**.
- Settings Page: **Wide** quando combina painéis ou editor + preview; **Focused** apenas para uma preferência textual isolada.
- Authentication: composição própria fora do shell autenticado.

Não use `width: 100vw`, offsets calculados da sidebar nem tokens específicos por feature. A sidebar expandida ou recolhida altera naturalmente a largura disponível do `MudMainContent`. A variante define apenas o comportamento do conteúdo dentro dessa área.

### Cabeçalho de página

`CabecalhoPagina` é o padrão para contexto, título, descrição e ações. A composição segue:

1. eyebrow estável do domínio, como ATENDIMENTO, AGENDA, CADASTROS ou ADMINISTRAÇÃO;
2. título curto e orientado à tarefa;
3. descrição que esclarece objetivo ou contexto;
4. uma ação principal opcional, com badges ou ações secundárias sem competir visualmente.

Em mobile, título, badge e ações devem recompor em linhas próprias, sem overflow ou sobreposição.

### Form Page

Use em cadastros e operações, como orçamento, ordem de serviço e agendamento. A estrutura padrão é `CabecalhoPagina`, contexto opcional e `.form-page-grid`, com `.page-main-column` e `ResumoLateral` quando existir um resumo relevante. O conteúdo principal é dividido em `.section-card`; `CabecalhoSecao` fornece eyebrow, título, helper e número de etapa opcional.

Etapas `01`, `02`, `03` só representam uma sequência real. Campos relacionados ficam juntos; condições e observações formam uma seção própria quando isso melhora a leitura. No desktop, o resumo pode ser sticky abaixo da topbar. Entre 768 e 1199 px, e no mobile, conteúdo e resumo são empilhados e as ações permanecem acessíveis.

### Detail Page

Use em entidades já criadas. A sequência recomendada é:

1. `CabecalhoPagina` com identidade e status;
2. `.detail-metrics` com poucos `CardMetrica` realmente prioritários;
3. alerts contextuais;
4. `.detail-stack` com cards de conteúdo;
5. histórico, dados relacionados e ações operacionais.

Orçamento e Ordem de Serviço são as referências desse arquétipo. Métricas não substituem conteúdo e não devem proliferar. Alertas usam `.page-alert`; histórico equivalente deve compartilhar a mesma linguagem de timeline.

### List Page

Use `CabecalhoPagina` com uma ação principal, barra `.module-toolbar`, tabela em desktop, lista recomposta em mobile e paginação. Busca, filtros, limpar e buscar devem conservar altura, espaçamento e hierarquia. Não comprima tabela desktop em telas estreitas. Estados sem dados usam `EstadoVazio` ou o padrão compartilhado de empty state apropriado ao contexto.

### Settings Page

Configurações usam `CabecalhoPagina`, container padrão, grupos claros e cards de configuração. Não recebem etapas ou resumo lateral apenas por estética. Helper text deve explicar impacto e escopo da preferência, preservando contraste nos três temas.

### Dashboard / Operational Page

Dashboard e Agenda usam a estratégia **Fluid** e preservam sua composição operacional. Eles compartilham header, tipografia, spacing, cards, status e regras responsivas, mas não são forçados ao layout de formulário. O espaço adicional deve melhorar grids, calendário e leitura de tabelas, sem aumentar artificialmente fonte, ícones ou altura de cards.

### Cards de seção e resumo lateral

`.section-card` representa uma unidade real de trabalho, normalmente com `CabecalhoSecao`, descrição auxiliar e conteúdo. Evite cards aninhados sem função. Use número de etapa apenas em fluxos sequenciais.

`ResumoLateral` apresenta síntese e ações quando o usuário precisa conferir totais ou contexto durante uma operação. Ele pode ser sticky no desktop, respeitando a topbar; deve perder sticky e ficar abaixo do conteúdo em tablet/mobile. Nunca use quando o bloco não acrescenta uma síntese operacional.

### Responsividade dos arquétipos

- Desktop (`>=1200px`): Fluid acompanha a área útil do shell; Wide limita fluxos complexos; Form Page pode usar conteúdo + resumo lateral; métricas recompõem conforme densidade.
- Tablet (`768–1199px`): grids principais empilham; métricas recompõem em duas colunas; Agenda mantém área maior quando necessário.
- Mobile (`<768px`): uma coluna, cards com padding reduzido, ações com alvo mínimo de 44 px, métricas empilhadas e listas recompostas.
- Em todas as larguras: nenhum scroll horizontal acidental, sticky sem sobreposição, textos longos com quebra segura e valores monetários sem separação inadequada.

Os padrões são implementados em `app.css` e nos componentes `CabecalhoPagina`, `CabecalhoSecao`, `CardMetrica`, `ResumoLateral` e `EstadoVazio`. MudBlazor continua sendo a base; componentes Detara representam padrões de produto, não wrappers genéricos de HTML.

## Componentes

- **Botões:** um primário por região; secundário, outline, text e destrutivo conforme hierarquia.
- **Inputs:** label real, estados hover/focus/erro/disabled e mensagem de erro textual.
- **Cards:** apenas para unidades reais de informação, com borda discreta, padding consistente e ação clara.
- **Tabelas:** densidade operacional, busca/filtros/ordenação quando úteis, paginação backend e ações por linha. Em mobile, prefira lista/card adaptado a overflow horizontal puro.
- **Status:** sempre texto + cor. Execução/verde, agendado/azul, confirmado/teal, aguardando/âmbar, cancelado/vermelho e pausado/neutro.
- **Toast:** serviço global para sucesso, informação, alerta e erro.
- **Modais:** decisões focadas; grandes CRUDs não cabem em modal por padrão.
- **Ícones:** MudBlazor/Material Icons, preferencialmente outlined; uma família consistente e sem emoji funcional.

### Componentes de produto compartilhados

Os componentes canônicos abaixo ficam em `src/Detara.Web/Components/Shared/DesignSystem/` e devem ser preferidos a marcação local equivalente:

- `DetaraMetricCard`: título, valor, contexto, descrição ou tendência e tom semântico (`Neutral`, `Positive`, `Warning`, `Info`).
- `DetaraStatusBadge`: texto e tom semântico (`Neutral`, `Positive`, `Warning`, `Info`, `Critical`), sem alterar enums ou regras de domínio.
- `DetaraCard`: container de seção com título, sobretítulo, descrição, ações e conteúdo opcionais.
- `DetaraEmptyState`: ausência de dados com ícone, orientação e ação contextual; use `Compacto` dentro de cards.
- `DetaraSkeleton`: carregamento de cards, tabelas e detalhes sem texto ou spinner solto.
- `DetaraDialog`: título, descrição, erro, ações primária/secundária e estado de processamento consistentes.

Componentes legados podem delegar para estes padrões durante a migração gradual. O tom visual é sempre decidido na camada de apresentação; contratos e enums de negócio não devem conhecer o Design System.

## Shell e navegação

Desktop: sidebar de 248 px, recolhível para 72 px; no modo compacto, exibe somente símbolo, ícones e tooltips. Tablet usa rail compacto quando necessário. Mobile usa drawer sobreposto e topbar compacta.

Seções previstas: Favoritos, Principal, Atendimento, Cadastros, Financeiro e Administração. Favoritos usam identificadores conhecidos e persistem por usuário; não aceite URL arbitrária.

### Experiência PWA

A versão instalada reutiliza o mesmo shell responsivo, temas e estratégias `Fluid`, `Wide` e `Focused`. Não existe layout alternativo para PWA.

- **Instalação:** a ação `Instalar Detara` é discreta, usa ícone Material e só aparece quando o browser oferece `beforeinstallprompt`. No shell autenticado, pertence ao menu da conta; no login, aparece como ação secundária.
- **Conectividade:** perda de rede ou indisponibilidade real da API usa banner global não bloqueante, com texto claro e sem prometer dados offline. Reconexão não recarrega a página nem apaga formulários.
- **Atualização:** nova versão usa banner persistente com a ação `Atualizar agora`. O reload só acontece depois da escolha do usuário e no máximo uma vez após o novo worker assumir.
- **Standalone e safe areas:** topbar, drawer, conteúdo, login, mensagens globais e áreas inferiores respeitam `safe-area-inset-*`. Alvos de toque e composição responsiva permanecem idênticos aos do navegador.
- **Temas:** o manifest define fallback de marca; a meta `theme-color` acompanha o tema efetivo Claro, Escuro ou Sistema.

## Dashboard

O dashboard responde: o que acontece hoje, o que exige atenção, quais veículos estão em atendimento/prontos, qual valor está previsto e o que está atrasado. Use poucos indicadores, agenda e operação; dados temporários devem ser marcados como demonstrativos. Evite excesso de gráficos.

## Login

Preserve o fluxo Empresa/Slug, Email e Senha. Use a marca oficial, tokens, foco visível, responsividade e temas. Em mobile, priorize o formulário e mantenha assinatura de marca compacta.

## Responsividade e acessibilidade

- Mobile: `<768px`; tablet: `768–1199px`; desktop: `>=1200px`.
- Responsividade muda composição, não apenas tamanho.
- Garanta contraste, foco visível, labels, teclado, `aria-label`, alvos mínimos de 44 px e status não dependente apenas de cor.

## Marca

Não altere proporção, tipografia, cores, orientação ou efeitos. Não use wordmark comprimido. O símbolo oficial pode aparecer isolado em sidebar compacta, favicon e app icon. As imagens de `docs/design/` documentam a direção; não são usadas diretamente na UI.
