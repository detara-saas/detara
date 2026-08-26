# Gateway WhatsApp multi-tenant

## Escopo e arquitetura

O gateway envia somente a comunicação transacional `VeiculoProntoRetirada`. Não recebe mensagens, não responde clientes, não acessa grupos e não implementa campanhas, marketing ou disparos em massa.

```text
Detara Web → Detara API (.NET) → WhatsApp Gateway (Node.js) → whatsapp-web.js → WhatsApp
```

O browser nunca acessa o gateway diretamente. A API resolve `EmpresaId` a partir do usuário autenticado, chama o contrato único `IWhatsAppClienteProvider` e envia ao gateway o mesmo tenant no path/payload e no header `X-Detara-Tenant-Id`. O gateway exige ambos idênticos e autenticação `Bearer` interna.

## Isolamento e persistência

Cada empresa recebe a chave determinística `tenant-{EmpresaId:N}`. O `LocalAuth` do `whatsapp-web.js` usa essa chave como `clientId`, criando credenciais de sessão separadas no volume `detara-whatsapp-sessions`. O registro do gateway guarda somente metadados de sessão; QR Code, telefone e mensagem não são persistidos nele.

O SQL Server mantém `SessaoWhatsAppEmpresa`, com `EmpresaId`, `SessionKey`, status, datas, erro seguro e versão de concorrência. O filtro global de tenant e os índices únicos por empresa/chave impedem leitura cruzada no monólito. A credencial efetiva do WhatsApp permanece exclusivamente no volume do gateway.

Após reinício, o gateway carrega todas as sessões conhecidas e inicializa um cliente separado para cada empresa. Durante a restauração o estado é `Disconnected`; somente o evento `ready` libera envios novamente. Assim, metadados antigos não produzem um falso estado conectado.

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

O QR Code é temporário, recebe `Cache-Control: no-store`, não é salvo pela API e só é devolvido nos endpoints protegidos de conexão. Consultas informativas da OS retornam apenas status.

## Envio e idempotência

Ao preparar uma comunicação WhatsApp, o worker envia `EmpresaId`, telefone, mensagem renderizada e uma chave idempotente estável. O gateway:

1. exige que a sessão daquele tenant esteja `Connected`;
2. normaliza e valida o telefone;
3. confirma que o número está registrado no WhatsApp;
4. envia usando somente o cliente indexado pelo `EmpresaId`;
5. persiste o resultado da chave idempotente.

Uma chave já enviada retorna o mesmo ID sem novo disparo. Se a conexão cair depois do início e o resultado ficar incerto, o registro permanece `InProgress` e o gateway bloqueia repetição automática para evitar mensagem duplicada. O operador recebe erro seguro e deve reconciliar antes de tentar outra solicitação.

Endpoints internos autenticados:

- `POST /sessions/{empresaId}/connect`
- `GET /sessions/{empresaId}/status`
- `POST /messages/send`
- `GET /healthz`

## Logs e segurança operacional

São registrados somente eventos e IDs técnicos de empresa: criação, QR gerado, autenticação, conexão, início/conclusão de envio e erros tipados. Nunca são registrados QR Code, chave Bearer, telefone, mensagem ou credenciais da sessão.

O gateway executa como usuário não root. A imagem usa Chromium do Debian, filesystem somente leitura em produção, capabilities removidas, `/tmp` temporário e volume gravável apenas para sessões. O override de Puppeteer deve acompanhar os testes de compatibilidade do `whatsapp-web.js`; execute `npm audit --omit=dev`, os testes Node e um vínculo real controlado a cada atualização.

Na versão atual, `whatsapp-web.js` ainda traz `fluent-ffmpeg` e `glob` como dependências transitivas marcadas como deprecated. O audit não aponta vulnerabilidades conhecidas, mas esses avisos devem ser acompanhados e não podem ser silenciados por fork local sem teste de compatibilidade upstream.

`whatsapp-web.js` depende do protocolo do WhatsApp Web e não é uma API oficial da Meta. Mudanças externas podem exigir atualização emergencial ou nova leitura de QR. Para SLA formal, templates aprovados, webhooks de entrega ou escala maior, reavalie a migração para a WhatsApp Business Platform oficial.

## Validação

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
