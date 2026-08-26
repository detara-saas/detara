# Comunicação transacional com o cliente

## Escopo atual

O módulo implementa somente o aviso `VeiculoProntoRetirada`, disparado na transição da Ordem de Serviço de `EmExecucao` para `AguardandoRetirada`. Não há campanhas, tracking, webhooks, anexos, imagens, unsubscribe ou central global de notificações.

Cada empresa escolhe exatamente um canal automático: `Nenhum`, `Email` ou `WhatsApp`. O operador pode escolher Email ou WhatsApp em um envio manual, sempre uma opção por solicitação. `ComunicacaoCliente` preserva o histórico unificado; a fila de e-mail existente continua responsável pelo Resend.

## Configuração de infraestrutura

O provider atual é Resend, acessado por um `HttpClient` tipado sobre `POST /emails`. A escolha preserva controle explícito sobre classificação de falhas e o header oficial `Idempotency-Key`. A API key nunca pertence ao tenant e não deve ser commitada.

WhatsApp usa o adapter `WhatsAppGatewayClienteProvider` para o serviço Node separado documentado em [Gateway WhatsApp multi-tenant](whatsapp.md). A fila e o histórico continuam no módulo Notificações; credenciais `LocalAuth` e clientes `whatsapp-web.js` permanecem fora da API .NET.

Variáveis de ambiente esperadas:

```text
Email__Provider=Resend
Email__ApiKey=<secret>
Email__FromAddress=notificacoes@dominio-verificado.com
Email__FromName=Detara
```

Sem essas configurações, a aplicação compila, os testes usam provider fake e a fila registra falha terminal segura. Para envio real, o domínio do `FromAddress` deve estar verificado no Resend. Cada empresa configura apenas o Reply-To opcional.

Parâmetros da fila:

```text
Notificacoes__Fila__TamanhoLote=20
Notificacoes__Fila__IntervaloSegundos=15
Notificacoes__Fila__MaximoTentativas=4
Notificacoes__Fila__ProcessamentoExpiraMinutos=10
```

## Segurança do template

O corpo é analisado pelo `HtmlSanitizer` 9.2.995 (MIT), com allowlist explícita:

- tags: `p`, `br`, `strong`, `b`, `em`, `i`, `u`, `ul`, `ol`, `li`, `a`, `span`, `div`;
- atributos: `href`, `title`, `target`, `rel`, `style`;
- CSS: somente `text-align` e `color`;
- protocolos: `http`, `https` e `mailto`.

Scripts, iframes, imagens, eventos `on*`, estilos arbitrários e URLs `javascript:`, `data:` ou `vbscript:` são removidos. Variáveis de domínio são HTML-encoded antes da substituição. Assunto não aceita CR/LF e tem de 1 a 200 caracteres; o HTML customizado sanitizado tem limite de 50 KB. Preview e envio percorrem o mesmo sanitizador, renderizador e shell responsivo.

Variáveis aceitas:

- `{{EmpresaNome}}`
- `{{ClienteNome}}`
- `{{ClientePrimeiroNome}}`
- `{{VeiculoDescricao}}`
- `{{Placa}}`
- `{{OrdemServicoCodigo}}`

Qualquer variável desconhecida ou incompleta gera erro de validação compreensível.

## Persistência e processamento

Se o envio automático estiver desativado, nenhuma intenção é criada durante a transição da OS. Isso não proíbe e-mail: enquanto a OS estiver em `AguardandoRetirada`, um operador com `Notificacoes.Reenviar` pode criar o primeiro envio manualmente. Se o automático estiver ativo, a intenção inicial nasce no mesmo `SaveChanges` da transição da OS e da conta a receber. Em ambos os casos, a chamada externa ocorre somente no `NotificacoesWorker`.

Estados: `Pendente`, `Processando`, `Enviada`, `Falhou` e `SemDestinatario`. `Enviada` significa aceita pelo Resend, não entregue. Falhas temporárias usam até quatro tentativas com intervalos de aproximadamente 1, 5 e 30 minutos; erros terminais não entram em loop. Claims abandonados em `Processando` voltam à fila após o timeout configurado.

Cada tentativa usa a chave idempotente estável `notificacao-email/{NotificacaoId}`. As ações têm semânticas distintas:

- **Manual:** cria a primeira `NotificacaoEmail` quando nenhuma comunicação existe e o automático não criou uma intenção.
- **Retry / tentar novamente:** recoloca a mesma notificação `Falhou` ou `SemDestinatario` na fila, preservando assunto, corpo e histórico de tentativas. Quando nasceu sem destinatário, captura o e-mail atual válido do cliente.
- **Reenvio:** depois de uma notificação `Enviada`, cria uma nova `NotificacaoEmail` com o e-mail e o template vigentes. O registro enviado anteriormente permanece imutável.

O ID da intenção inicial é determinístico por OS. Cada reenvio recebe um ID de solicitação gerado uma vez pelo cliente Web e reutilizado em retries HTTP. A chave primária impede double submit concorrente, enquanto a consulta do estado mais recente bloqueia duplicidade entre envio automático, primeiro envio manual, pendências e processamento. A migration `PermiteReenvioNotificacaoVeiculoPronto` remove somente a unicidade antiga por OS/tipo, necessária para preservar múltiplos envios históricos; não adiciona colunas.

Novos envios e reenvios exigem que a OS ainda esteja em `AguardandoRetirada` e que o cliente possua e-mail válido. `Aberta`, `EmExecucao`, `Cancelada` e `Concluida` são rejeitadas no backend. Alterar ou remover o e-mail do cliente não modifica snapshots históricos. `Enviada` continua significando apenas que o provider aceitou a mensagem, não que ela foi entregue à caixa de entrada.

## Operação

- Configuração de canal e template de e-mail: `/configuracoes`, permissões `Configuracoes.Visualizar` e `Configuracoes.Editar`.
- Histórico na OS: `/ordens-servico/{id}`, permissão `OrdemServico.Visualizar`.
- Envio manual por Email/WhatsApp, retry e reenvio: `Notificacoes.Reenviar`.
- Teste: enviado somente ao e-mail do usuário autenticado, limitado a 3 solicitações por 10 minutos por empresa/usuário.

Logs não incluem API key, corpo HTML, destinatário ou resposta bruta do provider. IDs técnicos de notificação/empresa podem ser usados para correlação.
