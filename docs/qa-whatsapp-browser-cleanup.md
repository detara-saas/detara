# QA — cleanup determinístico do Chromium e LocalAuth

Data: 2026-09-09. Base: `40822f291dfe0bb5b4e7e2a1ef03f7221bbe9ce2` (PR #54 mesclado).

## A. Resumo

Hotfix restrito ao gateway, testes, documentação e `init: true` do seu container. A geração seguinte passa a depender do encerramento físico do browser anterior. DELETE só retorna sucesso depois da remoção persistente do LocalAuth e do registro/contexto. Nenhuma mudança de versão, user-agent, limites de memória/PIDs, regras de negócio, contratos, UI ou banco.

## B. Causa raiz confirmada

- **Primária, reproduzida com o Chromium da imagem:** `whatsapp-web.js` 1.34.7 usa `browser?.isConnected?.()` no `destroy()`. Puppeteer 25.8.0 tem `connected` como propriedade e não esse método. O resultado é undefined e o fechamento é pulado. Uma Promise resolvida não comprovava a morte do browser.
- **Primária em falhas parciais, reproduzida:** `Client.initialize()` upstream mantém `browser` numa variável local durante `pages()/newPage()`, autenticação e `setUserAgent()`. Só depois atribui `pupBrowser/pupPage`. Exceções nesse intervalo deixam o browser inacessível pelo cliente original.
- **Contribuintes:** o gateway marcava o Client num WeakSet antes do destroy, absorvia a falha, não confirmava exit e liberava `cleanupPromise`. Além disso, apagava incondicionalmente SingletonLock/Socket/Cookie ao criar outro cliente, retirando a proteção do profile ocupado.
- **Processos filhos:** no experimento local sem Docker init, permaneceram entradas zombie em `/proc`. Com `--init`, inclusive nos fallbacks forçados, desapareceram todos os PIDs capturados. Isso justifica exclusivamente `init: true` no gateway; não justifica aumento de recursos.
- **Hipóteses não sustentadas pelo incidente:** falta global de RAM, OOM como causa primária, restart do container ou necessidade de aumentar limites. Os dados fornecidos pelo operador mostram RestartCount=0, pressão no cgroup e duas árvores para o mesmo profile, não OOM. Não houve nova inspeção da VPS nesta tarefa.

Fontes da investigação: código efetivamente instalado de `whatsapp-web.js/src/Client.js`, `authStrategies/LocalAuth.js`, Puppeteer `cdp/Browser.js` e `@puppeteer/browsers/lib/launch.js`, mais os testes executados. Não se atribui um ponto exato da falha inicial de produção sem o stack completo: os dois caminhos locais acima explicam e reproduzem a deficiência de cleanup.

## C. Por que A permanecia vivo

O upstream podia não guardar seu handle ou ignorar o fechamento por ausência de `isConnected()`. O gateway não mantinha um handle independente nem observava o exit. Retirar A do mapa, desregistrar eventos ou marcar destroy como realizado não encerrava o recurso físico. PPID=1, por si só, não comprova a inexistência de um browser vivo.

## D. Por que B conseguia iniciar

`prepareClient()` aguardava `cleanupPromise`, mas essa Promise representava somente a tentativa de destroy com erro absorvido. Quando ela resolvia, criava B; a remoção dos locks permitia reutilizar o mesmo profile ainda ocupado. Agora uma falha de cleanup rejeita a barreira e mantém `retiringClient`; nenhuma nova factory é chamada enquanto a barreira não for concluída com sucesso. DELETE pode repetir a limpeza retendo esse handle.

## E. TypeError de logout

Reprodução real: após fechar o browser, `Client.logout()` avalia `this.pupBrowser.isConnected()` e lança `TypeError: this.pupBrowser.isConnected is not a function`, antes de `authStrategy.logout()`. O profile permanece. Em inicialização parcial também existe TypeError no acesso a `pupPage.evaluate` quando `pupPage` ainda é null. Ambos foram testados. O log sanitizado do incidente informa apenas TypeError; sem stack não se afirma qual linha foi atingida naquela chamada específica.

## F. Estratégia de remoção persistente

Após teardown físico e término das operações antigas, o adapter delega a `LocalAuth.logout()` da classe base, pelo método explícito `removeProfile()`, no profile exato `session-tenant-<32 hex>`. Valida sessionKey, raiz real, parent real, symlink/junction e correspondência de `userDataDir`; confirma ENOENT depois da remoção. Não remove raiz, volumes, profiles por wildcard ou dados de outros tenants. Não usa shell nem limpeza manual como solução do produto.

O adapter de LocalAuth não remove arquivos no hook `logout()` chamado pelo upstream: a remoção pertence exclusivamente ao DELETE após a barreira. Isso impede que o callback assíncrono `framenavigated` inicie outra remoção fora do cleanup rastreado. O hook `beforeBrowserInitialized()` também não recria o profile quando o cliente já está encerrando. Uma regressão simula a continuação desse callback depois do DELETE e confirma que o diretório continua inexistente.

Operações destrutivas pendentes são retidas por cliente em WeakMaps: timeout não equivale a cancelamento. Repetir DELETE não cria outra remoção concorrente nem permite que uma remoção tardia alcance um profile novo. Erro persistente de I/O retorna 503 e exige repetir a ação, sem ocultar a falha.

## G. Arquivos alterados

- `whatsapp-gateway/src/client-cleanup.js`: teardown verificável, timeouts, fallbacks e single-flight das operações destrutivas.
- `whatsapp-gateway/src/whatsapp-client-factory.js`: browser próprio antes de setup, adapter de initialize e LocalAuth restrito; preservação de locks.
- `whatsapp-gateway/src/gateway-service.js`: barreira física, handle aposentado, serialização por tenant e DELETE retryable.
- `whatsapp-gateway/src/logger.js`: estágio de fallback permitido no log sanitizado.
- `whatsapp-gateway/test/client-cleanup.test.js`: regressões de cleanup, concorrência, segurança de paths e shutdown.
- `whatsapp-gateway/test/gateway.test.js`: HTTP 503/isolamento e correção do teste que anteriormente exigia apagar locks.
- `whatsapp-gateway/qa/chromium-cleanup.mjs`: testes reais de browser sem rede externa.
- `.github/workflows/ci.yml`: execução desses testes na imagem construída.
- `compose.production.yml`, `docker-compose.yml`: `init: true` somente no gateway.
- `scripts/production/tests/compose-fixture.sh`: proteção do init no Compose.
- `docs/whatsapp.md`, `docs/qa-whatsapp-lifecycle-hotfix.md` e este relatório: operação, evidências e limites.

## H. Lifecycle antes/depois

| Momento | Antes | Depois |
|---|---|---|
| Launch/setup | Browser possivelmente só local no upstream | Handle próprio capturado imediatamente após launch |
| Falha | Client descartado logicamente | Client aposentado retido até cleanup físico |
| Destroy | Promise resolvida interpretada como sucesso | Exit do processo + conexão encerrada verificados |
| Retry | B permitido mesmo com A vivo | B aguarda cleanup de A; falha bloqueia criação |
| DELETE | Logout falhava e registro podia desaparecer | Browser + LocalAuth + registro/contexto concluídos ou 503 |
| Transitório/shutdown | Intenção de preservar sessão | Preserva LocalAuth e encerra recursos |

## I. Barreira cleanupPromise

Single-flight de inicialização, proteções de geração, ready obrigatório, recuperação limitada de inject e rejeição de eventos tardios do PR #54 permanecem. A Promise de teardown não absorve falhas; uma nova inicialização passa por ela antes de criar cliente. Connect/DELETE são serializados por tenant, não globalmente. Uma limpeza lenta de A não impede B (outra empresa) de operar.

## J. Encerramento físico

O adapter lança o Chromium com as opções existentes e conecta o Client upstream a esse mesmo browser por `browserWSEndpoint` (outra conexão CDP/página, não outro processo). Guarda a Promise de launch para capturar resultados tardios. Cleanup observa `exitCode/signalCode` e o evento `exit` do ChildProcess capturado, verifica `connected`/`isConnected()` e aguarda o initialize original terminar. Não usa sleep fixo como prova de morte.

## K. Fallback

Ordem: destroy → close → SIGTERM → SIGKILL, com prazo por etapa de 5 segundos, somente se necessário. No Linux, o último recurso atinge o grupo detached lançado pelo próprio Puppeteer, identificado pelo handle próprio; sem esse ownership usa apenas ChildProcess.kill. Não existe `pkill`, `killall`, busca por nome ou enumeração de processos no produto. O teste real mantém B vivo enquanto A é encerrado por SIGTERM e por SIGKILL. A enumeração de `/proc` é exclusivamente evidência de teste.

## L–M. DELETE, transitórios e persistência

DELETE invalida geração/eventos e aguarda operações anteriores. Só remove registry/context após teardown e LocalAuth. Uma segunda chamada sem sessão é idempotente. Falha de cleanup retorna `whatsapp_cleanup_pendente`, HTTP 503, com mensagem segura/traceId, mantendo estado Error e impedindo Connect até DELETE concluir. Falhas transitórias, destroy e shutdown não chamam a remoção explícita de LocalAuth. Restart sem logout e logout são cenários distintos; não houve prova com autenticação real nesta tarefa.

## N. Testes gateway

26 testes antes; **44/44 depois** (+18). A regressão antiga de locks foi ajustada deliberadamente para exigir preservação, não excluída. Os demais cenários anteriores permanecem. Novos testes cobrem destroy no-op/rejeitado/travado, launch tardio, sinais e falha definitiva, fila de gerações, timeout de logout/remoção, callback upstream tardio, shutdown, concorrência DELETE/Connect, LocalAuth real em diretório temporário, symlink/traversal, isolamento A/B e resposta HTTP segura. `npm run check` e modo `--unhandled-rejections=strict` aprovados.

## O. Container e smoke

Imagem `detara-whatsapp-gateway:cleanup`, construída localmente sem alteração do Dockerfile/pins. Chromium `152.0.7977.82`, Node da imagem pinada, Puppeteer `25.8.0`, whatsapp-web.js `1.34.7`.

**5/5 testes reais aprovados**, executados na imagem final sem mount substituindo código do produto, com `--init --network none --read-only`, `/tmp` temporário e profiles sintéticos:

1. falha de setup antes da atribuição upstream: árvore encerrada, LocalAuth preservado e removido somente no logout;
2. SIGTERM de A sem encerrar browser de B;
3. destroy no-op e logout TypeError reais da combinação instalada;
4. SIGKILL do grupo de A com todos seus processos removidos de `/proc`, B preservado;
5. geração B aguarda A; DELETE remove profile/registry/context e é idempotente.

O experimento negativo sem init deixou processos zombie e falhou na verificação de `/proc`; a execução com init passou. Container de smoke isolado, sem rede externa/portas publicadas: `/healthz` autenticado retornou HTTP 200 `Healthy`, running, RestartCount=0. Shutdown por SIGTERM terminou com ExitCode=0 e RestartCount=0. Somente esse container descartável foi removido; os serviços e volumes locais existentes foram preservados. Nenhum QR ou envio real.

## P–R. Validações gerais

- `dotnet restore`: aprovado.
- `dotnet build --configuration Release --no-restore`: 0 erros, 0 avisos.
- `dotnet test --configuration Release --no-build --no-restore`: **170 unitários + 559 integrados = 729**.
- `dotnet format --verify-no-changes --no-restore`: aprovado.
- `npm audit --omit=dev`: 0 vulnerabilidades.
- `dotnet list Detara.sln package --vulnerable --include-transitive`: nenhum pacote vulnerável nas fontes consultadas.
- Compose fixture: aprovado, incluindo init e isolamento existente.
- `git diff --check`: aprovado antes do commit.

## S–T. Banco e produção

Nenhuma migration, package, contrato ou mudança de modelo. EF `has-pending-model-changes`: nenhuma alteração pendente. **Nenhum deploy, merge automático, acesso à VPS, QR real, envio de mensagem ou alteração de sessão de produção.** Os dados do incidente acima vieram do relato fornecido.

## U. Git

Branch `fix/whatsapp-browser-cleanup`, originada da main atualizada `40822f2`, preservando o histórico do PR #54 e hotfixes anteriores. Commit local informado na entrega. Push e abertura de PR contra main aguardam autorização, conforme a condição da seção 128 da solicitação. CI remoto ainda não executado nesta entrega local; depois da autorização, aguardar os checks do commit publicado, sem auto-merge. Este relatório registra evidências locais, não substitui o status do CI.

## V. Riscos residuais

- O protocolo WhatsApp Web e o adapter dependem do comportamento upstream: mudanças de versão exigem repetir testes reais e smoke consentido. Não foi feita alegação de compatibilidade completa do override.
- Sessão autenticada, envio manual/automático, restauração sem QR e memória em uso real permanecem para pós-release autorizado. A redução da pressão de memória é esperada por eliminar browsers duplicados, não um benchmark medido de produção.
- A posse e a barreira são do processo atual e do tenant. Não recuperam arbitrariamente processos herdados de uma versão antiga, nem coordenam duas réplicas usando o mesmo volume. Não executar gateways concorrentes sobre os mesmos profiles.
- Depois de crash abrupto, locks persistentes podem impedir abertura: o sistema deixa de apagá-los cegamente. Investigar ownership em uma janela autorizada, não usar limpeza global. Restart normal executa fechamento preservando LocalAuth.
- Filesystem indisponível ou processo que não encerra falham de modo fechado: 503, sem afirmar desconexão completa, sem reutilizar profile e sem exigir restart como fluxo normal.

## Checklist pós-release — somente após aprovação e deploy autorizado

1. Validar artefato aprovado e hashes; registrar rollback.
2. Confirmar SHA exato.
3. Instalar checkout correspondente no host.
4. Preparar/revisar candidate.env sem expor secrets.
5. Executar dry-run e resolver bloqueios.
6. Fazer deploy autorizado, sem recriar volumes ou ampliar serviços.
7. Confirmar health geral e gateway interno autenticado; confirmar init ativo.
8. Registrar RestartCount=0 como baseline do novo container.
9. No tenant interno de teste, confirmar ausência de sessão/browser antes do teste; não alterar tenants reais.
10. Confirmar LocalAuth inexistente para esse tenant interno.
11. Clicar Conectar uma única vez.
12. Gerar um QR, sem registrar seu conteúdo.
13. Ler esse QR uma vez com aparelho autorizado.
14. Acompanhar lifecycle e logs sanitizados.
15. Confirmar apenas um Chromium principal para o user-data-dir.
16. Confirmar um marco authenticated por ciclo.
17. Aguardar ready/Connected; authenticated não basta.
18. Confirmar RestartCount=0.
19. Medir RAM do gateway e do host separadamente.
20. Verificar memory.events e pressão do cgroup, sem ampliar limites para mascarar leaks.
21. Enviar WhatsApp manual ao destino de teste consentido.
22. Confirmar entrega e registro idempotente.
23. Testar automático com consentimento e escopo controlado.
24. Confirmar que não houve email simultâneo no fluxo exclusivo.
25. Executar disconnect explícito pelo DELETE oficial.
26. Confirmar Chromium encerrado, incluindo filhos; sem intervenção manual.
27. Confirmar LocalAuth removido e registro/status desconectado.
28. Conectar novamente, sem apagar diretórios manualmente.
29. Confirmar um único browser e QR único.
30. Chegar a ready novamente; em erro/reconnect controlado, comprovar cleanup de A antes de B.
31. Testar persistência separadamente: conectar → ready → NÃO fazer logout → recriar somente gateway preservando volume → restaurar LocalAuth → ready sem novo QR.
32. Enviar novamente ao destino consentido.
33. Confirmar RestartCount sem aumento não planejado, distinguindo a recriação autorizada.
34. Revisar logs, sem QR, bearer, telefone, mensagens ou arquivos de auth.
35. Confirmar ausência de browser órfão e processos zombie acumulados.
36. Confirmar ausência de segunda árvore para o mesmo profile; interromper e preservar evidências se qualquer critério falhar.
