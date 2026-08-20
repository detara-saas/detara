# PWA Detara

## Visão geral

A Detara Web é a experiência instalável inicial oficial para desktop, celular e tablet. A PWA reutiliza a mesma aplicação Blazor WebAssembly, API, autenticação, permissões, Design System e regras de negócio. Não existe frontend, backend ou banco separado para o modo instalado.

A implementação segue a orientação oficial do [ASP.NET Core Blazor PWA para .NET 10](https://learn.microsoft.com/aspnet/core/blazor/progressive-web-app/?view=aspnetcore-10.0).

## Escopo offline

O Service Worker publicado mantém uma versão atômica do app shell:

- HTML, CSS e JavaScript necessários à aplicação;
- runtime e assemblies WebAssembly gerados pelo Blazor;
- fontes, ícones e assets oficiais de marca;
- manifest e configuração estática necessária ao bootstrap.

Offline não significa operação de negócio offline. Sem acesso à API, não funcionam login, consultas ou gravações de Clientes, Veículos, Agenda, Orçamentos, Ordens de Serviço, Financeiro, Notificações e Configurações. A aplicação não exibe respostas antigas como se fossem atuais, não cria dados simulados e não enfileira comandos.

O primeiro acesso exige rede. Depois que o build publicado foi visitado e o app shell foi armazenado corretamente, o shell pode abrir offline e apresenta `Sem conexão com o servidor`. A reconexão não recarrega a aplicação nem apaga formulários; a operação deve ser tentada novamente pelo usuário.

## Cache e segurança

O cache usa o namespace `detara-app-shell-` e a versão gerada em `service-worker-assets.js`. Uma ativação remove somente caches antigos com esse prefixo.

O worker ignora explicitamente:

- qualquer método diferente de `GET`;
- qualquer request cross-origin;
- rotas `api/` dentro do scope da aplicação;
- requests com header `Authorization`.

Respostas da API, JWT, claims, login, dados pessoais, operacionais e financeiros não entram no Cache Storage. O token continua na estratégia existente de `sessionStorage`; o Service Worker não lê nem copia esse conteúdo. Não existe IndexedDB comercial, Background Sync ou fila offline.

Uma resposta HTTP `401` mantém o fluxo atual de remoção da sessão. `HttpRequestException`, timeout sem cancelamento do usuário, DNS e perda de rede apenas sinalizam indisponibilidade e não removem o token.

## Instalação

O manifest usa identidade estável, `display: standalone`, start URL e scope relativos ao base path. Os ícones oficiais de 192 e 512 px são usados no manifest, e o asset de 180 px permanece como `apple-touch-icon`.

Quando o browser fornece `beforeinstallprompt`, a Detara guarda o evento sem abrir prompt automaticamente. A ação `Instalar Detara` aparece no menu da conta e discretamente no login. `appinstalled` e a detecção de `display-mode: standalone` removem a ação. Browsers sem instalação programática não recebem um botão inoperante.

Os ícones atuais usam `purpose: any`. Embora tenham fundo e margem próprios, não foram marcados como `maskable` sem uma validação específica de safe zone para launchers variados.

## Atualizações

O registro usa `updateViaCache: 'none'`. O browser verifica o worker no início e quando a aplicação volta a ficar visível após pelo menos 30 minutos.

Quando um novo worker termina de instalar e fica em espera, a interface mostra `Nova versão da Detara disponível`. A versão antiga continua ativa até o usuário escolher `Atualizar agora`. Essa ação envia `SKIP_WAITING`; depois de `controllerchange`, a página recarrega uma única vez. Não há reload automático durante trabalho não salvo.

## Temas, layout e safe areas

O manifest usa as cores de marca como fallback. Em runtime, a meta `theme-color` acompanha o tema efetivo: superfície clara em Claro e superfície escura em Escuro ou Sistema escuro.

O mesmo shell, sidebar, drawer e estratégias `Fluid`, `Wide` e `Focused` são usados no browser e em standalone. O viewport habilita `viewport-fit=cover`, e topbar, drawer, conteúdo, login e mensagens inferiores respeitam `env(safe-area-inset-*)`.

## Desenvolvimento e publicação

`service-worker.js` é usado em Development e sempre busca na rede, sem oferecer cache offline. O comportamento PWA real está em `service-worker.published.js` e deve ser validado com:

```bash
dotnet publish src/Detara.Web/Detara.Web.csproj --configuration Release
```

Produção precisa de:

- HTTPS;
- fallback SPA de rotas desconhecidas para `index.html`;
- `service-worker.js`, `service-worker-assets.js` e `index.html` sem cache HTTP prolongado;
- assets com hash servidos com cache longo e `immutable` quando o host suportar;
- MIME types corretos para manifest, JavaScript e WebAssembly.

O repositório ainda não define o host final de produção. Essas regras devem ser aplicadas na infraestrutura escolhida, sem presumir Nginx, IIS ou outro servidor antecipadamente.

## QA recomendado

O QA PWA deve servir o conteúdo de `bin/Release/net10.0/publish/wwwroot` em `localhost` ou HTTPS e verificar:

1. manifest sem erros e ícones carregados;
2. Service Worker ativo no scope da aplicação;
3. instalação e abertura standalone quando o browser suportar;
4. shell abrindo offline, sem respostas de API no Cache Storage;
5. sessão preservada em falha de rede e removida em `401`;
6. retorno online sem reload automático;
7. atualização Build A → Build B, banner, clique e um único reload;
8. rotas principais, temas e larguras previstas no Design System.

## Troubleshooting

- **A PWA não instala:** confirme HTTPS ou localhost, manifest válido, ícones acessíveis, Service Worker ativo e suporte do browser ao fluxo de instalação.
- **O worker não atualiza:** confirme headers sem cache longo para o worker e o manifest de assets, volte à aplicação após o intervalo de verificação ou use `registration.update()` apenas durante diagnóstico.
- **A versão antiga continua aberta:** verifique se o novo worker está `waiting` e use `Atualizar agora`; não é necessário limpar todo o cache no fluxo normal.
- **Manifest com erro:** valide JSON, paths relativos, MIME type e presença dos ícones no publish.
- **API offline:** o banner deve permanecer até uma resposta válida da API. Reconecte e tente a consulta ou gravação novamente.
