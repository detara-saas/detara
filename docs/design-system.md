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

## Componentes

- **Botões:** um primário por região; secundário, outline, text e destrutivo conforme hierarquia.
- **Inputs:** label real, estados hover/focus/erro/disabled e mensagem de erro textual.
- **Cards:** apenas para unidades reais de informação, com borda discreta, padding consistente e ação clara.
- **Tabelas:** densidade operacional, busca/filtros/ordenação quando úteis, paginação backend e ações por linha. Em mobile, prefira lista/card adaptado a overflow horizontal puro.
- **Status:** sempre texto + cor. Execução/verde, agendado/azul, confirmado/teal, aguardando/âmbar, cancelado/vermelho e pausado/neutro.
- **Toast:** serviço global para sucesso, informação, alerta e erro.
- **Modais:** decisões focadas; grandes CRUDs não cabem em modal por padrão.
- **Ícones:** MudBlazor/Material Icons, preferencialmente outlined; uma família consistente e sem emoji funcional.

## Shell e navegação

Desktop: sidebar de 248 px, recolhível para 72 px; no modo compacto, exibe somente símbolo, ícones e tooltips. Tablet usa rail compacto quando necessário. Mobile usa drawer sobreposto e topbar compacta.

Seções previstas: Favoritos, Principal, Atendimento, Cadastros, Financeiro e Administração. Favoritos usam identificadores conhecidos e persistem por usuário; não aceite URL arbitrária.

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
