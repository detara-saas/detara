# Fundação de SEO da landing Detara

## Estado atual

| Sinal | Valor |
|---|---|
| URL pública | `https://detara.pages.dev/` |
| Canonical | `https://detara.pages.dev/` |
| Sitemap | `https://detara.pages.dev/sitemap.xml` |
| Robots | `https://detara.pages.dev/robots.txt` |
| Consulta primária | `sistema para estética automotiva` |
| Idioma | `pt-BR` |

O endereço `pages.dev` é a URL pública real enquanto não existe domínio próprio oficial configurado. Canonical, `og:url`, sitemap e Search Console usam a mesma base. Previews do Cloudflare continuam declarando a URL de produção como canonical.

Esta fundação facilita descoberta, rastreamento, indexação e compreensão sem prometer posição, primeira página ou prazo. Um site novo pode levar tempo para ser descoberto, indexado e ganhar autoridade.

## Decisões de implementação

- a headline comercial do hero foi preservada;
- a consulta primária aparece uma vez no conteúdo visível, em uma frase que explica o produto;
- variações semânticas aparecem apenas onde esclarecem a operação;
- não existe `meta keywords`, texto oculto, doorway page ou conteúdo duplicado;
- `SoftwareApplication`, `WebApplication`, `Product`, FAQ e reviews não foram marcados;
- não existem preço público, oferta, avaliação ou review reais suficientes para esses schemas;
- JSON-LD foi omitido para não adicionar dados pouco úteis nem relaxar a CSP para script inline;
- não foram adicionados analytics, cookies, tracking, fontes, CDN ou conexões externas;
- a imagem social 1200×630 permanece como evolução futura, pois ainda não existe um asset oficial aprovado nesse formato.

Isso significa: zero ofertas fictícias, zero `aggregateRating`, zero reviews inventados e nenhuma alteração de `script-src`.

## Pesquisa leve de intenção

Pesquisa realizada em 24/08/2026, sem copiar textos, estrutura ou claims de concorrentes.

| Termo | Intenção percebida | O que a landing responde | Gap futuro legítimo |
|---|---|---|---|
| sistema para estética automotiva | Comercial; comparar uma solução específica | Explica público, fluxo operacional, produto real e contato | Dados reais do Search Console e prova social somente quando existirem |
| software para estética automotiva | Comercial; entender recursos e adequação | Agenda, clientes, veículos, orçamento, ordem de serviço, execução e financeiro | Conteúdo útil baseado em dúvidas reais de usuários |
| gestão para estética automotiva | Comercial/informacional; organizar a operação | Mostra continuidade do primeiro contato ao recebimento | Guias people-first somente após observar queries reais |

Os resultados pesquisados são predominantemente páginas de produto. Os temas recorrentes são centralização da operação, agenda, clientes/veículos, orçamento, ordem de serviço e financeiro. O Detara responde a essa intenção com seu fluxo real e evita claims de estoque, lucro, WhatsApp, preço, teste grátis ou automações que não fazem parte da oferta atual.

## Google Search Console — procedimento manual

Enquanto a URL pública for `pages.dev`:

1. abrir o Google Search Console;
2. adicionar uma propriedade do tipo **URL-prefix**;
3. informar `https://detara.pages.dev/`;
4. escolher um método de verificação oferecido pelo Google;
5. concluir a verificação manualmente, sem armazenar credenciais no repositório;
6. abrir **URL Inspection**;
7. informar `https://detara.pages.dev/`;
8. executar **Test Live URL**;
9. usar **Request indexing**;
10. abrir **Sitemaps** e enviar `sitemap.xml`.

Solicitar indexação não garante inclusão imediata, primeira página ou posição específica.

### Verificação futura

Se o Google fornecer uma meta tag `google-site-verification`, inserir a tag real no `<head>` de `landing/index.html`. Não adicionar placeholder.

Se o Google oferecer um arquivo HTML de verificação, colocar o arquivo real diretamente em `landing/`. Não gerar arquivo fictício.

## Checklist pós-merge e deploy

1. aguardar o deploy de produção do Cloudflare Pages;
2. abrir `https://detara.pages.dev/` e confirmar HTTP 200;
3. usar **View Source** e confirmar canonical, title, description e Open Graph;
4. abrir `https://detara.pages.dev/robots.txt` e confirmar HTTP 200;
5. abrir `https://detara.pages.dev/sitemap.xml` e confirmar HTTP 200 e XML válido;
6. confirmar que canonical, `og:url`, robots e sitemap usam a mesma base;
7. confirmar que `/404.html` não consta no sitemap;
8. configurar manualmente o Search Console;
9. enviar `sitemap.xml`;
10. solicitar indexação da homepage;
11. aguardar coleta de dados antes de criar novos conteúdos.

## Acompanhamento

Depois que houver dados, monitorar impressões, cliques, CTR, posição média, consultas e páginas. Consultas iniciais de interesse:

- `detara`;
- `detara estética automotiva`;
- `sistema para estética automotiva`;
- `software para estética automotiva`;
- `gestão estética automotiva`.

Os dados reais devem orientar as próximas páginas. Não criar conteúdo novo no escuro.

## Quando o domínio oficial for configurado

Não presumir nem publicar um domínio antes de ele existir. Quando estiver adquirido, configurado e efetivamente publicado:

1. definir a variante HTTPS canônica;
2. atualizar canonical e `og:url` em `index.html`;
3. atualizar as URLs absolutas de `sitemap.xml`;
4. atualizar a linha `Sitemap` em `robots.txt`;
5. atualizar este `SEO.md` e eventual metadata de imagem social;
6. criar uma propriedade **Domain** no Search Console, preferencialmente com verificação DNS;
7. planejar redirect permanente de `detara.pages.dev` para o domínio oficial;
8. garantir que as duas versões não concorram como páginas principais.

## Roadmap, não implementado nesta task

1. fundação técnica e semântica;
2. Search Console e solicitação de indexação;
3. conteúdo útil orientado por queries reais;
4. autoridade legítima por perfis oficiais, parceiros e clientes beta autorizados;
5. otimizações baseadas em dados.

Possíveis temas futuros incluem organização da agenda, orçamento, ordem de serviço, check-in e checklist de atendimento. Cada página futura deve responder a uma intenção real; não criar variações duplicadas para capturar palavras-chave. Não comprar backlinks, publicar spam ou simular uma presença local inexistente.
