# E-mail transacional

## Escopo atual

A Task 08 implementa somente o aviso `VeiculoProntoRetirada`, disparado na transição da Ordem de Serviço de `EmExecucao` para `AguardandoRetirada`. Não há campanhas, tracking, webhooks, anexos, imagens, unsubscribe, WhatsApp ou central global de notificações.

## Configuração de infraestrutura

O provider atual é Resend, acessado por um `HttpClient` tipado sobre `POST /emails`. A escolha preserva controle explícito sobre classificação de falhas e o header oficial `Idempotency-Key`. A API key nunca pertence ao tenant e não deve ser commitada.

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

Se o envio automático estiver desativado, nenhuma intenção é criada. Se estiver ativo, a intenção nasce no mesmo `SaveChanges` da transição da OS e da conta a receber. A chamada externa ocorre somente no `NotificacoesWorker`.

Estados: `Pendente`, `Processando`, `Enviada`, `Falhou` e `SemDestinatario`. `Enviada` significa aceita pelo Resend, não entregue. Falhas temporárias usam até quatro tentativas com intervalos de aproximadamente 1, 5 e 30 minutos; erros terminais não entram em loop. Claims abandonados em `Processando` voltam à fila após o timeout configurado.

Cada tentativa usa a chave idempotente estável `notificacao-email/{NotificacaoId}`. O reenvio manual é permitido apenas para falha/sem destinatário, nunca para uma mensagem já aceita, e reutiliza assunto/corpo originais. Quando a intenção nasceu sem destinatário, o primeiro reenvio pode capturar o e-mail atual do cliente.

## Operação

- Configuração e template: `/configuracoes`, permissões `Configuracoes.Visualizar` e `Configuracoes.Editar`.
- Histórico na OS: `/ordens-servico/{id}`, permissão `OrdemServico.Visualizar`.
- Reenvio manual: `Notificacoes.Reenviar`.
- Teste: enviado somente ao e-mail do usuário autenticado, limitado a 3 solicitações por 10 minutos por empresa/usuário.

Logs não incluem API key, corpo HTML, destinatário ou resposta bruta do provider. IDs técnicos de notificação/empresa podem ser usados para correlação.
