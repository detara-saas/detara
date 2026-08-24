# Landing page V2 do Detara

Landing institucional estática e independente da aplicação Blazor. Não utiliza API, banco de dados, Node.js, npm, frameworks, CDNs, fontes externas, cookies ou analytics.

## Direção da experiência

A V2 traduz o posicionamento “precisão operacional encontra o cuidado automotivo premium” com superfícies azul-marinho, linhas de inspeção e reflexos controlados inspirados no acabamento automotivo. A narrativa deixa de ser uma sequência uniforme de cards e passa a apresentar o produto em quatro momentos: command center interativo, fluxo operacional progressivo, cenas amplas do produto e estrutura de controle.

Os dados exibidos nas composições são cenários ilustrativos e não pertencem a clientes. As interações usam JavaScript vanilla como aprimoramento progressivo:

- navegação móvel com suporte a Escape;
- abas acessíveis no hero, incluindo setas, Home e End;
- revelação discreta de conteúdo em viewport;
- progressão visual do fluxo Agenda → Orçamento opcional → OS → Entrega → Financeiro;
- respeito a `prefers-reduced-motion` e funcionamento do conteúdo sem JavaScript.

## Estrutura

```text
landing/
├── index.html
├── 404.html
├── _headers
├── robots.txt
├── README.md
├── css/
│   └── styles.css
├── js/
│   └── main.js
└── assets/
    └── brand/
        ├── detara-logo-on-dark.png
        ├── detara-icon.png
        └── favicon.ico
```

Os arquivos de marca foram copiados, sem modificação, de `src/Detara.Web/wwwroot/brand/`. Os originais continuam sendo a fonte oficial. O preview do produto é uma composição HTML/CSS ilustrativa e não contém dados de clientes.

## Visualização local

Com Python disponível, execute na raiz do repositório:

```powershell
python -m http.server 4173 --directory landing
```

Depois abra `http://localhost:4173/`. Para conferir a página de erro diretamente, abra `http://localhost:4173/404.html`.

O servidor simples do Python não aplica o arquivo `_headers`; esses headers são interpretados pelo Cloudflare Pages após a publicação.

## Publicação no Cloudflare Pages

No painel do Cloudflare:

1. Acesse **Workers & Pages**.
2. Escolha **Create application** e depois **Pages**.
3. Conecte o GitHub e selecione `detara-saas/detara`.
4. Configure:

| Campo | Valor |
|---|---|
| Project name | `detara` |
| Production branch | `main` |
| Framework preset | `None` |
| Build command | `exit 0` |
| Build output directory | `.` |
| Root directory | `landing` |
| Environment variables | nenhuma |

Depois do primeiro deploy, em **Settings → Builds & deployments → Build watch paths** (a nomenclatura pode variar no painel), recomenda-se incluir apenas:

```text
landing/**
```

Isso evita novos builds da landing quando somente a aplicação ou o backend forem alterados.

## Custom domain

O domínio próprio será configurado posteriormente. Até essa definição, a landing não possui URL canonical, sitemap com hostname ou links absolutos para um domínio presumido.

## Pendências deliberadas

- Adicionar CTA comercial somente quando o canal público oficial for definido.
- Avaliar analytics posteriormente, por decisão explícita e com a revisão de privacidade correspondente.
- Configurar domínio próprio somente depois de validar o primeiro deploy em `*.pages.dev`.

Nenhuma dessas pendências impede a publicação institucional inicial.
