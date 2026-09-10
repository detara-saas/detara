# Task 55.1 — WhatsApp pós-autenticação

## Investigation before changes

2026-09-10. Base `1539f5e` (PR #55 confirmada por ancestry). Nenhum acesso a produção, secrets ou conta WhatsApp. Esta seção foi registrada antes de qualquer alteração de comportamento. Primeiro serão adicionadas reproduções locais isoladas.

### Documentação e source revisados

- [Authentication oficial](https://wwebjs.dev/guide/creating-your-bot/authentication.html): LocalAuth exige persistência do profile, não do HTML do WebVersionCache.
- [Client](https://docs.wwebjs.dev/Client.html), [source exato v1.34.7](https://github.com/wwebjs/whatsapp-web.js/blob/v1.34.7/src/Client.js): initialize, inject, attachEventListeners, initWebVersionCache, getWWebVersion, setDeviceName, destroy e logout.
- Source instalado/tag: LocalAuth, BaseAuthStrategy (getAuthEventPayload/afterAuthReady são no-op para LocalAuth), LocalWebCache/RemoteWebCache/WebCacheFactory, AuthStore, util/Puppeteer, DefaultOptions e LoadUtils.
- Puppeteer: [exposeFunction](https://pptr.dev/api/puppeteer.page.exposefunction), [evaluate](https://pptr.dev/api/puppeteer.page.evaluate), [launch/executablePath](https://pptr.dev/api/puppeteer.launchoptions), [navegação](https://pptr.dev/api/puppeteer.page.waitfornavigation), [close](https://pptr.dev/api/puppeteer.browser.close), [process](https://pptr.dev/api/puppeteer.browser.process), [compatibilidade](https://pptr.dev/supported-browsers), [Chrome for Testing](https://developer.chrome.com/docs/automation-and-testing/chrome-for-testing).

### Fluxo exato e lacuna

`inject()` registra a exposed function de hasSynced. Seu callback posterior aguarda payload, emite authenticated, consulta WWebJS e, se ausente, persiste currentIndexHtml em cache local ANTES de evaluate(LoadUtils). Depois verifica WWebJS por até 30s, cria ClientInfo/InterfaceController, registra listeners, emite ready e chama afterAuthReady. O listener de página chama hasSynced sem await. A Promise do initialize cobre a instalação dos callbacks, não a execução posterior deles. O coordinator Detara cobre inject, não esse callback. initializationPromise do serviço é uma barreira lógica resolvida por eventos terminais; não recebe automaticamente a rejeição da exposed function. Não havia watchdog pós-auth.

LoadUtils começa atribuindo `window.WWebJS = {}`; requires dentro das funções definidas só rodam quando invocadas. O probe fornecido (socket CONNECTED, hasSynced true, offline 100, AuthStore presente, WWebJS ausente) localiza uma falha anterior à atribuição ou uma substituição de documento, não prova prontidão operacional. O timeout upstream não é alcançado se cache.persist/evaluate já rejeitaram. Mesmo quando alcançado, sua rejeição volta à Promise da página, não ao initialize já resolvido.

### Hipóteses antes da reprodução

1. **Forte e verificável:** LocalWebCache default `./.wwebjs_cache`, cwd `/app`, com filesystem read-only. resolve ignora leitura falhada e usa first-party; persist chama mkdirSync/writeFileSync sem catch antes de LoadUtils. Nenhum volume cobre `/app/.wwebjs_cache`.
2. Navegação/contexto destruído durante callback: risco real upstream, ainda sem evidência específica de produção.
3. Módulo lazy/A-B: precisa distinguir requires imediatos de funções diferidas; não inferir a partir do nome do evento ausente.
4. Runtime/UA/WA version: variáveis a comparar, não justificam downgrade ou troca às cegas.

### Histórico e matriz

`556a779` introduziu WWebJS 1.34.7, override Puppeteer 25.8, UA default Chrome101, Chromium apt e Node24. Não há justificativa explícita versionada para o override: não inventar razão. `a31b180` pinou a imagem base e introduziu Compose Production com read_only; compose local não possui essa restrição. Os quatro launch args permanecem. A PR #55 passou a possuir o browser e conectar o upstream nele; mantém UA default em args e setUserAgent, como o launch upstream anterior. O Chromium apt NÃO tem pin e rebuild futuro pode trocar browser. Não há metadados suficientes para afirmar versão exata da imagem/HTML usados na homologação antiga.

| Matriz de mecânica local planejada | WWebJS | Puppeteer | Browser | UA |
|---|---|---|---|---|
| A | 1.34.7 | 25.8 | Chromium 152.0.7977.82 | default Chrome101 |
| B | 1.34.7 | 25.8 | mesmo Chromium | UA nativo do browser |
| C | 1.34.7 | 24.38 | CfT 146.0.7680.31 | default upstream |
| D, se necessário | 1.34.7 | 25.8 | CfT 152.0.7977.42 | nativo |

Mapeamentos confirmados em revisions.ts das tags oficiais [24.38](https://github.com/puppeteer/puppeteer/blob/puppeteer-v24.38.0/packages/puppeteer-core/src/revisions.ts) e [25.8](https://github.com/puppeteer/puppeteer/blob/puppeteer-v25.8.0/packages/puppeteer-core/src/revisions.ts); a tabela web consultada não expôs essas duas entradas. Testes sem conta provam mecânica CDP/evaluate/bindings, não compatibilidade completa WhatsApp.

Default webVersion é `2.3000.1017054665`, cache local strict=false, sem HTML incorporado à imagem. Na ausência desse arquivo usa first-party. `Debug.VERSION=2.3000.1047198186` é versão observada no documento de produção, não prova de pin de HTML. currentIndexHtml vem de response.text da URL first-party; seu tamanho/conteúdo em produção não foi coletado. Cache e LocalAuth são independentes.

### Issues/PRs oficiais revisados

| Referência | Estado consultado | Comparação e influência |
|---|---|---|
| [#3971](https://github.com/wwebjs/whatsapp-web.js/issues/3971) | fechado/completed | 1.34.2, Store exigia WAWebSetPushnameConnAction. Esse require de startup não existe no LoadUtils instalado. Risco A/B genérico, não causa comprovada aqui. PRs 3972/3975 fechados sem merge; não aplicar forks. |
| [#127084](https://github.com/wwebjs/whatsapp-web.js/issues/127084) | fechado/completed | Docker/Node24/LocalAuth semelhantes; 1.34.6 e cache remoto/alpha diferentes. Comentários sugerem bundled browser ou HTML antigo; restauração-only difere da conexão fresh Detara. Nenhum fix universal comprovado; fechamento não prova causa. |
| [#5685](https://github.com/wwebjs/whatsapp-web.js/issues/5685) | fechado/completed | PM2/restore seletivo, fork setPushname; difere de fresh/cache read-only. |
| [#5758](https://github.com/wwebjs/whatsapp-web.js/issues/5758) | fechado/completed | 1.34.4, Chrome144, Node24.12, 99%; comentários tratam corrida hasSynced já true. Aqui authenticated já ocorreu, logo não basta chamar evento novamente. |
| [#5768](https://github.com/wwebjs/whatsapp-web.js/issues/5768) | fechado/completed | alpha/Edge/Windows; mesmo sintoma, sem probe equivalente. |
| [#5773](https://github.com/wwebjs/whatsapp-web.js/issues/5773) | fechado/completed | alpha/iOS/Node22; sugestões de patches de eventos, sem prova da falha Detara. |
| [#5792](https://github.com/wwebjs/whatsapp-web.js/issues/5792) | fechado/completed | RemoteAuth/forks/muitos flags; não transferir configuração. |
| [#5815](https://github.com/wwebjs/whatsapp-web.js/issues/5815) | fechado/completed | alpha, dados incompletos; recomendações main/fork sem equivalência. |
| [#201734](https://github.com/wwebjs/whatsapp-web.js/issues/201734) | fechado/not_planned | ghost depois de ready/inatividade, Socket OPENING/RemoteAuth; distinto de WWebJS ausente no fresh. |
| [#201821](https://github.com/wwebjs/whatsapp-web.js/issues/201821) | fechado/completed, comentários revisados | Relato de TargetCloseError durante attachEventListeners; relevante à captura de erros e navegação, mas fase posterior à ausência de WWebJS. Não copiar espera de input/sleep sugerida. |
| [#201653](https://github.com/wwebjs/whatsapp-web.js/pull/201653) | merged `1780711` | main-frame, abort de inject, listeners e hasSynced atômico. Não contém solução de cache read-only; preservar correções Detara e não importar diff amplo. |
| [#201853](https://github.com/wwebjs/whatsapp-web.js/pull/201853) | aberto | Stream model depois de pronto; não resolve persist antes de LoadUtils. |
| [#201907](https://github.com/wwebjs/whatsapp-web.js/pull/201907) | aberto | bump Puppeteer24.38→25.9, não prova estabilidade; #201876 anterior fechado. |

Pesquisa incluiu abertos/fechados por authenticated/ready, LoadUtils e EROFS; ocorrências recentes incluem #201845 e #201852, sem evidência suficiente de mesma causa. A release estável consultada continua [1.34.7](https://github.com/wwebjs/whatsapp-web.js/releases/tag/v1.34.7). Changelog 1.34.6→1.34.7 inclui remoção de Store (#127077), auth timeout (#127048) e dependências; #5785 removeu módulo indisponível já em 1.34.6. main consultada `942d236` altera Client e adiciona utilitários de media em Utils, sem retirar a escrita de cache anterior a LoadUtils. Nenhum fork/patch externo instalado.

### Evidência experimental

Reprodução ANTES de modificar produto: imagem da PR #55 com --read-only, --network none, Chromium152 real, Client upstream puro, LocalAuth temporário e modelos WA sintéticos. O callback real de inject foi chamado após inject resolver. authenticated ocorreu; `LocalWebCache.persist` (linha37) lançou `ENOENT: no such file or directory, mkdir './.wwebjs_cache/'`, a partir de Client.js:323. WWebJS ficou undefined e ready não ocorreu. O errno pode variar entre filesystems (EROFS/EACCES); o erro concreto desta imagem foi ENOENT em mkdir recursivo sobre a camada read-only.

Chamado sem await pela página, o erro apareceu como pageerror, não como rejeição de inject/initialize; Node strict permaneceu vivo. Com `type: none`, mesmos browser/versões, o fluxo real de LoadUtils/ClientInfo/attachEventListeners/ready concluiu com módulos sintéticos, tanto UA101 quanto nativo. LoadUtils executado com window.require que sempre lança concluiu sem invocar require: **zero requires imediatos** nessa função instalada. Depois de navegação, WWebJS desaparece como esperado. Essas experiências provam uma falha determinística de configuração equivalente à produção e sua propagação oculta; sem stack da VPS não se afirma exclusividade dessa causa naquele processo.

Decisão: desabilitar apenas cache HTML via opção oficial `webVersionCache.type=none` (first-party já era fallback), manter LocalAuth/versões/browser/UA e adicionar boundary pequeno para pós-auth com fases, single-flight, validação de documento e falha terminal observável. Não adotar HTML de terceiros, downgrade ou copiar inject inteiro. Persistir HTML num novo volume não é necessário ao produto. A matriz C será testada isoladamente, sem alterar packages do repo.

## Resultado da implementação e validação local

### Causa, contribuintes e limites

A operação exata que falha na reprodução é `await webCache.persist(currentIndexHtml, version)`, antes de `evaluate(LoadUtils)`. `authenticated` já foi emitido; por isso autenticação, Socket CONNECTED e hasSynced podem estar corretos enquanto WWebJS/listeners nunca foram instalados. O erro real foi:

```text
ENOENT: no such file or directory, mkdir './.wwebjs_cache/'
LocalWebCache.persist (LocalWebCache.js:37)
Binding.<anonymous> (Client.js:323)
Binding.run (Puppeteer Binding.js:131)
```

Contribuinte confirmado: callback exposto assíncrono sem boundary Node após `inject()` resolver. Na chamada sem await pelo JavaScript da página, a rejeição foi um `pageerror`; não chegou ao lifecycle que esperava ready/auth_failure/disconnected. O watchdog upstream sequer foi alcançado. A reprodução usa o source upstream puro, sem o novo coordinator; não depende de um mock do método persist.

**#3971 não se aplica como causa desta reprodução.** O require de startup foi removido nas versões anteriores. O nome ainda existe em `Client.setDisplayName`, sob demanda, não no LoadUtils executado após autenticação. A/B testing e alterações de módulos continuam riscos externos, mas não foram comprovados no incidente. Tampouco há evidência de que a versão de WhatsApp Web observada seja a causa. Não foi coletada stack de produção: a falha reproduzida explica exatamente o estado relatado, sem provar que era a única falha possível naquele processo.

### Matriz executada — somente mecânica, sem conta/rede WhatsApp

| Caso | WWebJS | Puppeteer | Browser efetivamente executado | UA | Resultado |
|---|---|---|---|---|---|
| A | 1.34.7 | 25.8.0 | Chromium Debian 152.0.7977.82 | default Chrome101 | Cache local read-only falha antes de LoadUtils; none completa LoadUtils, ClientInfo, listeners e ready. |
| B | 1.34.7 | 25.8.0 | Chromium Debian 152.0.7977.82 | nativo | Mesmo resultado de A. Trocar UA não corrige persist. |
| C | 1.34.7 | 24.38.0 | Chrome for Testing 146.0.7680.31 | default e nativo | 7/7 testes mecânicos; mesma falha e mesma solução. Alinhamento à dependência upstream não resolve escrita read-only. |
| D | — | — | — | — | Não necessário: A/B/C já isolam a falha de filesystem; nenhuma troca de runtime no produto. |

A combinação declarada pelo upstream é WWebJS 1.34.7 + Puppeteer 24.38; seu browser de referência é CfT146. O override Detara usa Puppeteer25.8, cujo browser de referência é CfT152.0.7977.42. Chromium152 do Debian não é esse binário exato. Passar nesta matriz não certifica compatibilidade integral com WhatsApp real nem elimina o acoplamento runtime/browser. A incompatibilidade de `isConnected()` já tratada pela PR #55 continua compensada pelo cleanup Detara.

Diferença histórica concreta: compose local permite escrita em `/app`; Production não. Também há Chromium apt sem pin, fallback de HTML first-party e mudanças externas do WhatsApp. Não existe evidência suficiente para atribuir o sucesso da homologação antiga a um browser/HTML exato. Node, WWebJS, Puppeteer, UA, quatro launch args, RAM e Dockerfile de produto **não foram alterados**. Nenhum pacote novo no produto ou lockfile; a imagem C é exclusivamente QA.

### Correção limitada ao adapter

1. Opção oficial `webVersionCache: { type: 'none' }`. Sem cache HTML remoto, pin arbitrário ou volume adicional. LocalAuth continua persistente e independente. O adapter também deixa de instalar o listener de response HTML desnecessário quando cache=none.
2. Substituição somente do callback `onAppStateHasSyncedEvent` no cliente derivado. Não copia `initialize`, `inject` ou `attachEventListeners`; reutiliza os reais LoadUtils, ClientInfo, InterfaceController e listeners da versão pinada. Se o reparo de colisão remover o binding na mesma Page, a reinjeção reinstala o boundary Detara antes do callback upstream.
3. Single-flight por cliente, deduplicação no mesmo documento, identificação do documento entre awaits e deadline total de 30s. Não é aumento de timeout para esconder problema: é limite terminal para a Promise que antes não tinha boundary. Erro/timeout gera Error no lifecycle existente e aposenta o cliente com cleanup físico.
4. Uma recuperação de contexto, no mesmo browser, somente antes de iniciar listeners. Nada de sleeps, refresh loop, callbacks sintéticos de ready ou novo browser durante recovery. Falha de listeners ou contexto após attachment é terminal, evitando repetição parcial. Novo documento após ready precisa preparar novamente seus listeners; exposed functions sobrevivem à navegação, listeners de módulos não.
5. Ready só depois de LoadUtils real, WWebJS.sendMessage disponível, Socket CONNECTED, ClientInfo e conclusão do attachment upstream no documento atual. `Connected`, hasSynced ou progresso 100 isoladamente nunca promovem ready.
6. Timeout/stop bloqueiam conclusões tardias; generation token, cleanupPromise, isolamento A/B e semântica DELETE/shutdown permanecem. Erro transitório não remove LocalAuth. Novo connect espera browser anterior sair; sem restart automático do container.

Fases sem conteúdo sensível: `POST_AUTH_STARTED`, `POST_AUTH_CACHE_SKIPPED`, `POST_AUTH_LOAD_UTILS_STARTED`, `POST_AUTH_LOAD_UTILS_DONE`, `POST_AUTH_WWEBJS_READY`, `POST_AUTH_CLIENT_INFO_DONE`, `POST_AUTH_LISTENERS_STARTED`, `POST_AUTH_LISTENERS_DONE`, `READY_EMITTED`; quando necessário, `POST_AUTH_CONTEXT_RETRY`. Falhas registram apenas fase, empresa técnica e tipo limitado. Sem erro bruto, HTML, QR, WID, telefone, cookies, mensagens ou tokens.

O helper `qa/post-auth-probe.mjs` aceita uma Page local já pertencente ao harness e retorna somente flags, estado enumerado, versão e origem sanitizada. Não expõe porta CDP, endpoint HTTP nem procura sessões. Em produção, preferir fases dos logs; qualquer inspeção adicional exige acesso autorizado separado, sem publicar CDP.

### Evidências de validação

| Verificação | Resultado local em 2026-09-10 |
|---|---|
| `dotnet restore` | Aprovado. |
| `dotnet build --configuration Release --no-restore` | Aprovado, 0 warnings/0 errors. |
| `dotnet test --configuration Release --no-build --no-restore` | **729/729**, 170 unitários + 559 integração. |
| `dotnet format --verify-no-changes --no-restore` | Aprovado. |
| EF `migrations has-pending-model-changes`, Release/Testing | Nenhuma alteração pendente no modelo. |
| `npm run check` e Node strict | **58/58**, preservados os 44 anteriores + 14 novos. |
| Chromium real na imagem final, read-only, init, network none | **18/18**: 5 cleanup anteriores + 7 mecânica upstream + 6 adapter. |
| Matriz C isolada | **7/7** mecânicos adicionais, CfT146 real. |
| `npm audit --omit=dev` | 0 vulnerabilidades conhecidas. |
| NuGet `--vulnerable --include-transitive` | Nenhum pacote vulnerável reportado. |
| Docker build gateway final | Aprovado, manifest local `sha256:f7ad6fe6fd9ae21ee68000f9b2a015f455e21f21f30658ebf842b3477b06a68f`. |
| HTTP smoke isolado | Health autorizado 200/Healthy, sem autenticação 401; RestartCount=0, OOMKilled=false; SIGTERM→ExitCode=0. |
| `git diff --check` | Aprovado, incluindo novos arquivos no índice antes do commit. |

No teste real pós-auth, exatamente **1 processo browser raiz para o profile sintético**, cgroup memory.current=202952704 bytes (~194 MiB), memory.events com oom/oom_kill=0. São amostras de harness offline, não estimativa de memória de uma conta real. O smoke HTTP ocioso consumiu 78344192 bytes (~75 MiB). Containers temporários encerrados/removidos; nenhuma sessão local do usuário foi tocada. A CI passou a executar os 18 testes reais, mas **execução remota desta branch depende de push autorizado**; não declarar CI remota aprovada antes disso.

Reprodução A/B e adapter, a partir da raiz do repo (PowerShell):

```powershell
docker build -f whatsapp-gateway/Dockerfile -t detara-whatsapp-gateway:post-auth .
docker run --rm --init --network none --read-only --tmpfs /tmp:rw,nosuid,size=512m -v "${PWD}/whatsapp-gateway/qa:/app/qa:ro" detara-whatsapp-gateway:post-auth node --unhandled-rejections=strict --test --test-concurrency=1 qa/chromium-cleanup.mjs qa/post-auth-mechanics.mjs qa/post-auth-adapter.mjs
```

Para repetir C, `qa/Dockerfile.matrix` requer uma imagem local `detara-whatsapp-gateway:cleanup` construída da base PR #55 (ou a imagem atual etiquetada para essa finalidade). A construção baixa dependências oficiais apenas nessa imagem QA; a execução é sem rede:

```powershell
docker build -f whatsapp-gateway/qa/Dockerfile.matrix -t detara-whatsapp-matrix:24 .
docker run --rm --init --network none --read-only --tmpfs /tmp:rw,nosuid,size=512m -v "${PWD}/whatsapp-gateway/qa:/app/qa:ro" detara-whatsapp-matrix:24 node --unhandled-rejections=strict --test qa/post-auth-mechanics.mjs
```

### Arquivos e escopo

- `whatsapp-gateway/src/post-auth.js`: boundary pós-auth limitado à versão atual.
- `whatsapp-gateway/src/whatsapp-client-factory.js`: cache none e conexão do callback ao coordinator.
- `whatsapp-gateway/src/gateway-service.js`: observabilidade e falha no lifecycle existente.
- `whatsapp-gateway/test/post-auth.test.js`: regressões unitárias/serviço, incluindo isolamento A/B.
- `whatsapp-gateway/qa/post-auth-mechanics.mjs`: reprodução do source upstream puro.
- `whatsapp-gateway/qa/post-auth-adapter.mjs`: Chromium real, callback, contexto, listeners, memória e profile.
- `whatsapp-gateway/qa/synthetic-wa.mjs`: fixture mínima sem WhatsApp real.
- `whatsapp-gateway/qa/post-auth-probe.mjs`: diagnóstico allowlisted apenas no harness.
- `whatsapp-gateway/qa/Dockerfile.matrix`: runtime C temporário para investigação.
- `.github/workflows/ci.yml`: inclusão dos testes reais pós-auth.
- `docs/whatsapp.md` e este documento: operação, evidências e limites.

**Nenhuma migration criada. Nenhum deploy realizado.** Sem alteração de .NET, contratos, frontend, regras de comunicação, infraestrutura Production ou dependências de produto. PR #55 preservada: base `1539f5e`, ancestry de `1ff618e` confirmada. Branch `fix/whatsapp-post-auth-ready`; commit local na entrega, sem push/PR/merge até autorização.

### Pendências e riscos

- Não houve QR, conta WhatsApp, envio ou confirmação de recebimento nesta tarefa. Ready foi validado com Chromium/CDP e código upstream reais, módulos WA sintéticos. **Aceitação ponta a ponta em produção continua pendente** do checklist abaixo, com autorização.
- WhatsApp Web é integração não oficial; mudanças remotas, A/B testing, módulos lazy, página navegada e coupling Puppeteer/browser podem quebrar além da fase corrigida. Não existe garantia de SLA da Meta.
- O callback é um adapter version-specific. Reavaliar/remover quando uma release estável upstream oferecer boundary equivalente e passar pelas reproduções e smoke controlado; não atualizar pacote sem revisar os imports internos.
- Status visual continua usando os contratos atuais; após authenticated pode mostrar WaitingQRCode durante a preparação limitada. Não foi criado novo status/UI nesta tarefa; falha pós-auth agora termina em Error, sem espera infinita por esse callback.
- Chromium apt flutuante é risco de rebuild; sem alteração neste hotfix. Nenhum Critical/High conhecido reportado pelos audits executados.

### Checklist pós-release — somente após autorização operacional

1. Registrar baseline: SHA/image digest, versões, RestartCount, RAM e memory.events; logs sanitizados do tenant de teste. Não imprimir env/secrets, QR, telefone ou conteúdo.
2. Para teste **fresh**, obter autorização explícita de logout do tenant de teste. Usar DELETE autenticado normal e confirmar zero LocalAuth desse tenant e zero browser associado antes de conectar. Não apagar volumes/perfis manualmente nem encerrar browsers de outras empresas. Se não autorizado, executar somente teste de restauração e registrar limitação.
3. Connect uma vez → exibir/escanear QR uma vez → authenticated → confirmar exatamente um browser para essa empresa, sem processos antigos remanescentes.
4. Conferir as fases até LOAD_UTILS_DONE → WWEBJS_READY → CLIENT_INFO_DONE → LISTENERS_DONE → READY_EMITTED → Connected. Flags de diagnóstico, se autorizadas, devem mostrar WWebJS e listeners presentes no documento atual. Socket/hasSynced/progresso isolados não aprovam o teste.
5. Verificar RestartCount=0, ausência de OOM/oom_kill, evolução RAM/memory.events e ausência de nova geração paralela. Se erro/timeout: preservar logs seguros, aguardar cleanup, interromper repetição e investigar fase/errorType; não aumentar RAM/timeout nem apagar sessão como resposta automática.
6. Com consentimento do destinatário, executar **um envio manual autorizado**, confirmar recebimento real e registrar somente resultado técnico. Depois validar comunicação automática WhatsApp em cenário previamente autorizado e confirmar ausência de e-mail duplicado. Não reutilizar uma OS real sem autorização nem contornar idempotência.
7. DELETE explícito → browser sai → LocalAuth desse tenant some → registry/context removidos. Confirmar outra empresa inalterada.
8. Reconectar uma vez e autenticar → READY/Connected. Reiniciar/recriar apenas gateway **sem logout** → LocalAuth preservado → READY sem novo QR → novo envio autorizado confirmado.
9. Coletar logs finais sanitizados, contagem de processos, RestartCount e memory.events. Reportar separadamente fresh, restauração, manual, automático e cleanup; somente com esses resultados aprovar ponta a ponta. Não realizar merge/deploy automático.
