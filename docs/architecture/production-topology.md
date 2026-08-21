# Topologia de produção — beta privada

## Decisão inicial

A primeira produção cabe em um único VPS Linux com Docker Compose. Essa é uma decisão enxuta para a beta, não uma promessa de escala ilimitada. O desenho preserva fronteiras que permitem mover SQL, arquivos ou containers sem mudar o domínio.

```text
Internet
   |
   | 80/443
   v
Caddy (TLS e proxy) ---- rede edge ---- Web estático
   |                         |
   +-------------------------+---- API
                                      |
                                  rede data (internal)
                                      |
                                  SQL Server

API ---- HTTPS ---- storage S3-compatible privado
API ---- HTTPS ---- Resend
```

Somente o Caddy publica portas no host. Web e API usam a rede `edge`; API e SQL usam a rede isolada `data`. SQL não publica `1433`. O IP interno fixo do Caddy (`172.30.0.2`) é a única origem aceita para `X-Forwarded-For` e `X-Forwarded-Proto`.

## Responsabilidades

| Componente | Responsabilidade | Persistência |
|---|---|---|
| Caddy | TLS automático, redirecionamento HTTP→HTTPS e proxy | estado ACME em volume |
| Web | assets Blazor/PWA e headers do frontend | nenhuma |
| API | autenticação, domínio, integrações e health checks | key ring de Data Protection em volume protegido por certificado |
| SQL Server | dados transacionais | volume SQL e volume separado de staging de backups |
| S3-compatible externo | mídia privada | bucket privado, fora do VPS |

## Limites e evolução

- Escala vertical é a primeira ação durante a beta.
- API e Web permanecem stateless, exceto o key ring compartilhável.
- Arquivos já usam `IArquivoStorage`; migrar entre provedores não altera handlers.
- SQL pode ser movido para serviço gerenciado depois, mantendo a connection string e o fluxo de migrations.
- Não há Kubernetes, mensageria, cache distribuído ou CDN nesta fase.

## Controles de segurança

- Imagens de aplicação devem usar tag imutável ou digest.
- Containers de aplicação usam usuário não-root, filesystem somente leitura e `cap_drop: ALL`.
- Secrets existem apenas no `.env.production`, certificado PFX ou cofre do operador; nenhum deles entra na imagem ou no Git.
- O bucket não é público. Download continua mediado por endpoints autorizados da API.
- Data Protection mantém `ApplicationName=Detara.Platform`, key ring persistente e criptografia em repouso com PFX.
- Forwarded headers são aceitos apenas do proxy explicitamente conhecido e com um salto.
- Logs de produção são JSON, incluem correlation ID e não registram query string ou corpo.
- Liveness não acessa dependências; readiness testa o banco e nunca retorna detalhes internos.

## Riscos aceitos na beta

| Risco | Nível | Tratamento inicial |
|---|---:|---|
| VPS único é um ponto único de falha | Médio | backup externo, restore drill e imagens reproduzíveis |
| SQL e aplicação compartilham recursos do host | Médio | limites monitorados e capacidade revisada semanalmente |
| SQL Express limita o tamanho do banco | Médio | monitorar crescimento e trocar `MSSQL_PID`/licença antes do limite |
| Certificado SQL interno não possui cadeia pública | Médio | tráfego restrito à rede Docker `internal`, `Encrypt=True`; planejar certificado interno validável ou SQL gerenciado |
| Logs locais podem ser perdidos com perda total do VPS | Médio | rotação local agora; exportação para destino externo antes de ampliar a beta |
