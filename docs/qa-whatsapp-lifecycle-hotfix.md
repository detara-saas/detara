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

1. Obter autorização explícita e validar o artefato aprovado para a janela.
2. Confirmar SHA, tag e digest; registrar a imagem anterior para rollback.
3. Instalar/selecionar no host exatamente o checkout correspondente ao SHA aprovado.
4. Gerar e revisar o `candidate.env` sem imprimir seus secrets.
5. Executar o dry-run do deploy e resolver qualquer bloqueio antes da mudança.
6. Executar o deploy sem recriar volumes e sem ampliar o escopo de serviços.
7. Confirmar live/ready da aplicação e `/healthz` interno autenticado do gateway.
8. Registrar estado e `RestartCount` do gateway imediatamente após estabilização.
9. Abrir a configuração WhatsApp somente no tenant e usuário autorizados.
10. Decidir, a partir do estado exibido, se a sessão existente pode ser reutilizada.
11. Gerar QR somente se a sessão persistida realmente não restaurar.
12. Ler o QR uma única vez com o aparelho autorizado.
13. Observar a transição do QR sem registrar seu conteúdo em logs/capturas.
14. Confirmar um único marco `authenticated` por ciclo.
15. Aguardar obrigatoriamente o evento `ready` e o estado backend `Connected`.
16. Confirmar que a UI apresenta **Conectado** somente depois de `ready`.
17. Confirmar que o container não reiniciou durante QR/autenticação/ready.
18. Confirmar apenas uma árvore Chromium para cada sessão/tenant esperado.
19. Enviar uma mensagem manual somente ao número de teste consentido.
20. Confirmar entrega e idempotência no histórico da aplicação.
21. Testar uma comunicação automática WhatsApp consentida e controlada.
22. Confirmar que o fluxo automático não enviou e-mail simultaneamente.
23. Reiniciar/recriar somente o gateway de forma controlada, preservando o volume.
24. Confirmar `Reconnecting` → `Connected` via `LocalAuth`, sem novo QR.
25. Enviar novamente ao destino consentido após a restauração.
26. Revisar logs sanitizados, sem QR, bearer, telefone ou mensagem.
27. Revisar RAM, CPU, PIDs e árvores Chromium após a restauração.
28. Confirmar nenhuma ocorrência de `Execution context was destroyed` que provoque crash.
29. Confirmar nenhuma ocorrência de `onQRChangedEvent already exists`.
30. Confirmar que `RestartCount` não aumentou; caso contrário, executar rollback e preservar evidências.
