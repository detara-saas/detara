# QA — hotfix do ciclo de vida do gateway WhatsApp

Data da validação local: 2026-09-09.

## Escopo e causa raiz

O hotfix trata exclusivamente o gateway Node e sua documentação. Não altera API/contratos .NET, domínio, frontend, banco, migrations, permissões ou o formato persistido da sessão.

A falha era a combinação de dois comportamentos:

1. `whatsapp-web.js` 1.34.7 executa `inject()` durante navegações de frame. A espera baseada em `page.evaluate()` pode atravessar uma navegação e rejeitar com `Execution context was destroyed`. Chamadas concorrentes também podem passar juntas pela verificação de um binding e tentar expor `onQRChangedEvent` duas vezes.
2. O gateway Detara encerrava seu single-flight quando a Promise de `client.initialize()` retornava, antes do evento `ready`. Uma nova ação de conectar podia chamar `initialize()` novamente na mesma instância parcialmente inicializada. Falhas mantinham esse cliente no mapa e eventos tardios não possuíam proteção de geração.

O diagnóstico coincide com a correção já mesclada no branch principal upstream, mas ainda ausente de uma release stable: [issue 127082](https://github.com/wwebjs/whatsapp-web.js/issues/127082), [PR 201653](https://github.com/wwebjs/whatsapp-web.js/pull/201653), [release stable 1.34.7](https://github.com/wwebjs/whatsapp-web.js/releases/tag/v1.34.7).

## Correção aplicada

- Um coordenador single-flight envolve `Client.inject()` no adapter Detara.
- `Execution context was destroyed` aguarda um contexto válido com `waitForFunction` antes de uma nova tentativa.
- Binding duplicado é removido com `Page.removeExposedFunction`, API oficial do Puppeteer, antes da reinjeção.
- São permitidas no máximo três tentativas por ciclo. A falha terminal vira evento controlado, sem `uncaughtException` ou `unhandledRejection` global.
- O single-flight de `initialize()` permanece ativo até `ready`, erro, desconexão ou encerramento.
- Cada cliente possui geração. Eventos de instâncias descartadas são ignorados.
- Erro transitório destrói o browser/cliente, preserva `LocalAuth` e exige nova instância na próxima conexão; não existe loop automático infinito.
- Somente `ready` produz `Connected`; `authenticated` duplicado é deduplicado.
- `GET /status` é somente leitura e não inicializa sessão.
- Atualizações de estado são serializadas para impedir regressão de `WaitingQRCode` para `Connecting` em requisições concorrentes.

## Matriz inspecionada

| Item | Resultado |
|---|---|
| Node.js | 24.20.0 |
| `whatsapp-web.js` | 1.34.7, pin exato |
| Puppeteer / Core | 25.8.0, override explícito |
| Chrome esperado pelo Puppeteer | 152.0.7977.42 |
| Chromium da imagem | 152.0.7977.82, Debian 12 |
| User-Agent | Chrome 101.0.4951.67, default upstream |

O upstream 1.34.7 declara Puppeteer 24.38.0. O override 25.8.0 já existia desde a criação do gateway e acompanha a linha Chromium 152 da imagem observada. Não há evidência de que esse override causou os dois stack traces; ambos decorrem da concorrência/reinjeção do `Client.js`. Como não existe stable posterior a 1.34.7, o hotfix mantém todas as versões, não usa beta, não edita `node_modules` e contém a falha no adapter versionado.

## Evidências locais

- `npm ci`: concluído; somente depreciações transitivas já documentadas (`glob` e `fluent-ffmpeg`).
- `npm run check`: 26/26 testes aprovados.
- `node --unhandled-rejections=strict --test`: 26/26 testes aprovados.
- `npm audit --omit=dev`: 0 vulnerabilidades.
- Imagem `detara-whatsapp-gateway:hotfix-lifecycle`: build aprovado.
- Smoke isolado da imagem: `/healthz` retornou `Healthy`, processo permaneceu `running`, exit code 0 e `RestartCount=0`.
- `dotnet restore`: aprovado.
- `dotnet build --configuration Release`: aprovado, 0 avisos e 0 erros.
- `dotnet test --configuration Release`: 170 unitários + 559 integrados = 729 aprovados.
- `dotnet format --verify-no-changes`: aprovado.
- `dotnet list package --vulnerable --include-transitive`: nenhum pacote vulnerável conhecido.
- EF Core `has-pending-model-changes`: nenhum pending model change.
- `git diff --check`: aprovado.

Não houve QR real, envio, acesso a produção/VPS, alteração de sessão existente, deploy ou reinício do gateway em produção. A persistência real após restart precisa ser confirmada no pós-deploy autorizado.

## Checklist pós-release — 30 passos

1. Obter autorização explícita para a janela, tenant e número de teste consentido.
2. Registrar commit, tag e digest da imagem anterior para rollback.
3. Registrar `RestartCount`, saúde e consumo do gateway antes da mudança.
4. Confirmar que o volume de sessões está montado no caminho esperado.
5. Confirmar owner/permissões do volume sem exibir seu conteúdo.
6. Preservar backup operacional conforme o runbook; não copiar sessão entre tenants.
7. Construir ou obter a imagem exclusivamente do commit aprovado.
8. Conferir Node, `whatsapp-web.js`, Puppeteer e Chromium dentro da imagem.
9. Validar assinatura/digest da imagem que será implantada.
10. Implantar somente o serviço `whatsapp-gateway`, sem recriar volumes.
11. Confirmar `/healthz` pela rede interna autenticada.
12. Confirmar processo `running` e `RestartCount=0` após estabilização.
13. Observar logs sanitizados; não coletar QR, telefone, mensagem ou bearer.
14. Abrir a configuração do tenant autorizado.
15. Solicitar conexão uma única vez e confirmar estado `Connecting`/`Reconnecting`.
16. Confirmar que polling de status não repete o log de inicialização.
17. Se necessário, ler um único QR com o aparelho autorizado.
18. Confirmar um único log de autenticação para o ciclo.
19. Aguardar obrigatoriamente o evento/estado `Connected`.
20. Confirmar que não ocorreu `Execution context was destroyed`.
21. Confirmar que não ocorreu binding duplicado `onQRChangedEvent`.
22. Enviar uma mensagem transacional apenas ao número consentido.
23. Confirmar o recebimento e a idempotência registrada pela aplicação.
24. Registrar novamente saúde, memória, CPU e `RestartCount`.
25. Reiniciar somente o gateway uma vez, sem remover/recriar o volume.
26. Observar `Reconnecting` → `Connected` sem novo QR.
27. Confirmar que a mesma empresa possui apenas um ciclo de inicialização ativo.
28. Repetir um envio controlado com nova solicitação consentida após a restauração.
29. Monitorar por 15 minutos; rollback se houver crash loop, sessão perdida ou falha de envio.
30. Registrar evidência final e encerrar a janela sem apagar `LocalAuth`.
