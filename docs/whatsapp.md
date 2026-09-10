# Gateway WhatsApp multi-tenant

## Escopo e arquitetura

O gateway envia somente a comunicação transacional `VeiculoProntoRetirada`. Não recebe mensagens, não responde clientes, não acessa grupos e não implementa campanhas, marketing ou disparos em massa.

```text
Detara Web → Detara API (.NET) → WhatsApp Gateway (Node.js) → whatsapp-web.js → WhatsApp
```

O browser nunca acessa o gateway diretamente. A API resolve `EmpresaId` a partir do usuário autenticado, chama o contrato único `IWhatsAppClienteProvider` e envia ao gateway o mesmo tenant no path/payload e no header `X-Detara-Tenant-Id`. O gateway exige ambos idênticos e autenticação `Bearer` interna.

## Isolamento e persistência

Cada empresa recebe a chave determinística `tenant-{EmpresaId:N}`. O `LocalAuth` do `whatsapp-web.js` usa essa chave como `clientId`, criando credenciais de sessão separadas no volume `detara-whatsapp-sessions`. O registro do gateway guarda somente metadados de sessão e o número da conta conectada; QR Code, destinatários e mensagens não são persistidos nele.

O SQL Server mantém `SessaoWhatsAppEmpresa`, com `EmpresaId`, `SessionKey`, status, número conectado, datas, erro seguro e versão de concorrência. O filtro global de tenant e os índices únicos por empresa/chave impedem leitura cruzada no monólito. A credencial efetiva do WhatsApp permanece exclusivamente no volume do gateway.

Após reinício, o gateway carrega todas as sessões conhecidas e inicializa um cliente separado para cada empresa. Durante a restauração o estado é `Reconnecting`; somente o evento `ready` libera envios novamente. Assim, metadados antigos não produzem um falso estado conectado.

O ciclo de vida é single-flight por `EmpresaId`: existe no máximo um `Client` e uma chamada de `initialize()` ativos por empresa. `authenticated` é apenas um marco intermediário e não libera envio; chamadas repetidas de conexão reutilizam a inicialização até `ready`, erro ou desconexão. Polling de `GET /status` é somente leitura e nunca cria cliente. Eventos de uma geração descartada são ignorados, impedindo que um browser antigo altere o estado de uma sessão substituta.

Falhas durante a injeção do WhatsApp Web são tratadas no adapter do cliente, e não por handlers globais do processo. Navegação destruindo o execution context aguarda um novo contexto com `waitForFunction`; binding Puppeteer duplicado é removido pela API oficial `removeExposedFunction`; ambas as recuperações são single-flight e limitadas a três tentativas. Esgotado o limite, a empresa vai para `Error`, a instância é destruída e uma próxima conexão cria outro cliente. O diretório `LocalAuth` é preservado em erros transitórios e só há logout na desconexão explícita.

Faça backup criptografado do volume de sessões junto com o plano de recuperação. Perder esse volume exige nova leitura do QR Code. Não copie uma pasta de sessão entre empresas.

## Configuração

Use uma chave aleatória exclusiva, com pelo menos 32 caracteres, igual nos dois processos:

```text
DETARA_WHATSAPP_GATEWAY_API_KEY=<secret>
WhatsAppGateway__Enabled=true
WhatsAppGateway__BaseUrl=http://whatsapp-gateway:3000/
WhatsAppGateway__ApiKey=<mesmo secret>
WhatsAppGateway__TimeoutSeconds=30
```

Para desenvolvimento via Docker:

```powershell
Copy-Item .env.example .env
# preencha DETARA_SQL_PASSWORD, chaves JWT e DETARA_WHATSAPP_GATEWAY_API_KEY
docker compose up -d --build sqlserver whatsapp-gateway api web
```

Execução isolada do gateway, para diagnóstico local:

```powershell
Set-Location whatsapp-gateway
npm ci
$env:DETARA_WHATSAPP_GATEWAY_API_KEY = '<secret-com-32-ou-mais-caracteres>'
npm start
```

O default local escuta apenas `127.0.0.1:3000`. No Compose, a porta de diagnóstico é publicada somente em `127.0.0.1:3001`; em produção não há porta pública e API/gateway compartilham uma rede Docker interna. O gateway também recebe uma rede dedicada somente para saída à internet, necessária ao WhatsApp Web, sem publicar portas de entrada. Se o gateway for externalizado, adicione TLS/mTLS e uma política de rede equivalente antes da mudança.

## Conectar uma empresa

1. Entre no tenant com `Configuracoes.Editar`.
2. Abra `/configuracoes` e selecione **Conectar WhatsApp**.
3. No celular da empresa, use **Aparelhos conectados** e leia o QR Code.
4. Aguarde o estado **Conectado**.

Os estados apresentados são **Desconectado**, **Conectando**, **Conectado**, **Erro** e **Reconectando**. Desconectar encerra somente a sessão do tenant atual e exige um novo QR Code na próxima conexão.

## Consentimento e teste

A preferência **Ativar envio de avisos operacionais via WhatsApp** controla somente o fluxo automático. Sua ativação registra data e usuário responsável; desativá-la não remove o histórico e não altera as permissões do envio manual.

O teste de conexão exige número, confirmação explícita e `SolicitacaoId` idempotente. A mensagem usa o template fixo de teste, entra em `ComunicacoesCliente` com o tipo `TesteWhatsApp` e passa pelo mesmo worker tenant-safe do envio operacional.

O QR Code é temporário, recebe `Cache-Control: no-store`, não é salvo pela API e só é devolvido nos endpoints protegidos de conexão. Consultas informativas da OS retornam apenas status.

## Template operacional

O aviso `VeiculoProntoRetirada` usa um template textual próprio do canal WhatsApp, persistido em `TemplatesComunicacaoEmpresa` somente quando a empresa o personaliza. O padrão é materializado dinamicamente e aceita apenas `{ClienteNome}`, `{VeiculoDescricao}` e `{EmpresaNome}`. Variáveis desconhecidas ou incompletas são rejeitadas no backend; a prévia percorre o mesmo renderizador do envio real. O template de teste de conexão continua fixo e separado do aviso operacional.

## Envio e idempotência

Ao preparar uma comunicação WhatsApp, o worker envia `EmpresaId`, telefone, mensagem renderizada e uma chave idempotente estável. O gateway:

1. exige que a sessão daquele tenant esteja `Connected`;
2. normaliza e valida o telefone;
3. confirma que o número está registrado no WhatsApp;
4. envia usando somente o cliente indexado pelo `EmpresaId`;
5. persiste o resultado da chave idempotente.

Uma chave já enviada retorna o mesmo ID sem novo disparo. Se a conexão cair depois do início e o resultado ficar incerto, o registro permanece `InProgress` e o gateway bloqueia repetição automática para evitar mensagem duplicada. O operador recebe erro seguro e deve reconciliar antes de tentar outra solicitação.

Além da idempotência técnica, uma OS bloqueia outra comunicação idêntica de veículo pronto para o mesmo canal, destinatário e mensagem durante cinco minutos após um envio confirmado. A resposta padrão é `Já existe uma comunicação enviada recentemente para este cliente.`

Endpoints internos autenticados:

- `POST /sessions/{empresaId}/connect`
- `GET /sessions/{empresaId}/status`
- `DELETE /sessions/{empresaId}`
- `POST /messages/send`
- `GET /healthz`

## Logs e segurança operacional

São registrados somente eventos e IDs técnicos de empresa: criação, QR gerado, autenticação, conexão, início/conclusão de envio e erros tipados. Nunca são registrados QR Code, chave Bearer, telefone, mensagem ou credenciais da sessão.

O gateway executa como usuário não root. A imagem usa Chromium do Debian, filesystem somente leitura em produção, capabilities removidas, `/tmp` temporário e volume gravável apenas para sessões. O override de Puppeteer deve acompanhar os testes de compatibilidade do `whatsapp-web.js`; execute `npm audit --omit=dev`, os testes Node e um vínculo real controlado a cada atualização.

Os Composes habilitam `init: true` somente para coletar processos filhos do gateway. O teardown usa o handle do browser capturado antes do setup de páginas: tenta `Client.destroy()`, confirma a saída real, recorre a `Browser.close()` e, se necessário, a SIGTERM/SIGKILL exclusivamente do processo/grupo que ele próprio lançou. Não há busca global de PIDs nem remoção de locks na criação de uma sessão.

A próxima geração aguarda o cleanup físico da anterior. Erros transitórios e shutdown preservam LocalAuth. DELETE explícito só conclui depois de encerrar o browser, remover o profile validado do tenant por `LocalAuth.logout()` e remover o registro/contexto. Uma falha final retorna HTTP 503 seguro, mantém o contexto para nova tentativa de DELETE e bloqueia reconexão. Timeout não cancela uma operação: logout/remoção pendentes continuam associados ao cliente antigo, sem liberar um profile para outra geração.

Foi reproduzida uma incompatibilidade específica: Puppeteer 25.8 expõe `browser.connected`, mas não `browser.isConnected()`. O `destroy()` upstream 1.34.7 pode ignorar o fechamento, e `logout()` lança `TypeError` antes de chegar ao LocalAuth. O adapter verifica ambos os formatos e realiza cleanup independente, mantendo os pins. Isso não equivale a afirmar compatibilidade integral do upstream com o override; consulte a [evidência e os limites do hotfix](qa-whatsapp-browser-cleanup.md).

Matriz verificada no hotfix de 2026-09-09:

| Componente | Versão/valor | Decisão |
|---|---|---|
| Node.js | `24.20.0` | Mantido pelo digest da imagem Node |
| `whatsapp-web.js` | `1.34.7` | Última stable disponível; pin exato mantido |
| Puppeteer / Core | `25.8.0` | Override explícito mantido |
| Chrome esperado pelo Puppeteer | `152.0.7977.42` | Compatível com a mesma linha major do browser |
| Chromium da imagem inspecionada | `152.0.7977.82` Debian 12 | Pacote de SO; registrar novamente a cada rebuild |
| User-Agent do cliente | `Mozilla/5.0 (Macintosh; Intel Mac OS X 10_14_0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/101.0.4951.67 Safari/537.36` | Default do `whatsapp-web.js`; não alterado pelo hotfix |

O pacote upstream `1.34.7` declara Puppeteer `24.38.0`, mas o override `25.8.0` existe desde a introdução do gateway e corresponde ao Chromium 152 usado pela imagem inspecionada. A stable ainda não contém as correções de reinjeção já mescladas no branch principal upstream. O hotfix mantém os pins atuais e aplica a contenção mínima no adapter Detara, sem editar `node_modules`, instalar beta ou incorporar mudanças upstream não relacionadas. Reavaliar a remoção do adapter quando uma release stable incluir a correção e passar pelo smoke real controlado.

Na versão atual, `whatsapp-web.js` ainda traz `fluent-ffmpeg` e `glob` como dependências transitivas marcadas como deprecated. O audit não aponta vulnerabilidades conhecidas, mas esses avisos devem ser acompanhados e não podem ser silenciados por fork local sem teste de compatibilidade upstream.

`whatsapp-web.js` depende do protocolo do WhatsApp Web e não é uma API oficial da Meta. Mudanças externas podem exigir atualização emergencial ou nova leitura de QR. Para SLA formal, templates aprovados, webhooks de entrega ou escala maior, reavalie a migração para a WhatsApp Business Platform oficial.

## Validação

### Preparação pós-autenticação (Task 55.1)

O gateway desabilita somente o **cache HTML** via `webVersionCache.type=none`: carrega HTML first-party e não escreve `.wwebjs_cache` em `/app` read-only. Isso não desabilita LocalAuth nem sua persistência no volume de sessões. Foi reproduzida uma falha em `LocalWebCache.persist`, anterior a LoadUtils, que deixava authenticated sem WWebJS/ready; a rejeição do callback exposto não chegava ao initialize já resolvido.

O adapter envolve apenas esse callback pós-auth, reutilizando LoadUtils, ClientInfo e listeners upstream. Há single-flight por cliente, deduplicação por documento, verificação de contexto entre awaits e deadline terminal de 30s. Uma única recuperação antes de listeners pode usar o novo contexto no mesmo browser; falha de listeners/contexto posterior aposenta a geração com cleanup existente e preserva LocalAuth. Stop/timeout bloqueiam ready tardio. Navegação após ready exige preparar os listeners do novo documento novamente.

O caminho observável é `POST_AUTH_STARTED` → `POST_AUTH_CACHE_SKIPPED` → `POST_AUTH_LOAD_UTILS_STARTED/DONE` → `POST_AUTH_WWEBJS_READY` → `POST_AUTH_CLIENT_INFO_DONE` → `POST_AUTH_LISTENERS_STARTED/DONE` → `READY_EMITTED` → Connected. Erros incluem somente `stage`, `errorType` limitado e ID técnico de empresa. Socket CONNECTED, hasSynced ou progresso 100 não substituem WWebJS e listeners operacionais. Nenhum QR, telefone, WID ou conteúdo de página é registrado.

Não houve alteração de versões, UA, browser, recursos ou regras de envio. Os testes isolados com Chromium real não substituem vínculo e envio reais autorizados. Consulte [investigação, matriz A/B/C, resultados e checklist pós-release](qa-whatsapp-post-auth-ready.md). Ao atualizar WWebJS, revisar o adapter vinculado ao fluxo de 1.34.7; não carregar HTML antigo de terceiros como workaround.

### Comandos locais

```powershell
Set-Location whatsapp-gateway
npm ci
npm run check
npm audit --omit=dev

Set-Location ..
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
dotnet format --verify-no-changes
```

O smoke test real exige um número de teste: conectar por QR, enviar a uma pessoa que consentiu em receber o aviso, reiniciar apenas o gateway, aguardar `Connected` e confirmar que um segundo envio controlado não exige novo QR.

Para acompanhar a recuperação sem dados sensíveis, correlacione somente `EmpresaId`, mensagem operacional e `errorType`. O fluxo esperado é `Inicialização ... iniciada` → `QR Code ... gerado` → `Sessão ... autenticada` (uma vez) → `Sessão ... conectada`. `authenticated` repetido sem `ready`, `Error` após três reinjeções ou novo `RestartCount` exige interromper o smoke e preservar os logs; não apague a sessão como primeira resposta.
