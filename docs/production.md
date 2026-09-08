# Produção V1 — Task 49

Esta entrega prepara o repositório. **Não executa SSH, DNS, Cloudflare, R2, Resend, WhatsApp ou deploy na VPS real.** Nenhuma feature comercial foi adicionada. Primeiro go-live: [checklist ordenado](production-go-live.md).

## Topologia e capacidade

```text
Internet → Cloudflare (Full strict) → Caddy :80/:443
                                      ├→ Web estático (app.DOMINIO)
                                      └→ API privada (api.DOMINIO)
                                           ├→ SQL Express [rede data, internal]
                                           ├→ Gateway [rede integration, internal] → WhatsApp
                                           ├→ R2 mídia privada (S3 existente)
                                           └→ Resend
Backup host → gzip → age → R2 privado separado
Landing → Cloudflare Pages (fora da VPS)
```

Reutilizamos `compose.production.yml`, `deploy/` e `scripts/production/`; não há segunda infraestrutura concorrente. Host alvo: Ubuntu 22.04 amd64, Hostinger KVM 2, 2 vCPU/8 GB/100 GB, swap de 2 GB já preparada. A task não refaz o host.

| Serviço | CPU teto | RAM teto | Persistência / saúde |
|---|---:|---:|---|
| reverse-proxy (Caddy) | 0,5 | 256 MiB | volumes data/config; valida config |
| web (nginx não-root) | 0,5 | 256 MiB | estático, sem estado; HTTP raiz |
| api (UID APP_UID) | 1,5 | 1536 MiB | key ring protegido; live |
| sqlserver (mssql UID 10001) | 1,5 | 2560 MiB | volume de dados; SELECT 1 |
| whatsapp-gateway (node UID 1000) | 1 | 1536 MiB | `/var/lib/detara/whatsapp`; healthz autenticado |

Tetos somam 6 GiB, deixando aproximadamente 2 GiB para host/overhead. CPU é teto compartilhado, não reserva exclusiva. SQL tem `MSSQL_MEMORY_LIMIT_MB=2048`; buffer pool da edição Express possui limite próprio menor. Valores iniciais defensivos, não capacidade garantida. Medir sessões Chromium antes de ampliar tenants; swap sustentada não é operação normal.

Somente Caddy publica 80/443 (8080/8443 internos, usuário 1000). Sua imagem oficial possui file capability no binário: manter exclusivamente NET_BIND_SERVICE no bounding set evita falha de exec; demais capabilities removidas. CHOWN é usado apenas pelo init descartável de ownership. Edge usa subnet privada 172.30.0.0/24 e Caddy .2; verificar conflito antes do go-live. API confia somente nesse IP, um salto. SQL está em `data` internal; gateway só em `integration` internal + rede de saída própria. API usa saída edge para S3/Resend. Nenhuma porta 1433/3000/8080 é publicada. Containers possuem limites de PID, `no-new-privileges`, restart unless-stopped; API/Web/gateway/proxy read-only, API/Web/gateway sem capabilities. SQL mantém filesystem requerido pelo produto.

Gateway não é dependência de startup/readiness da API. SQL é aguardado com timeout no Compose; readiness falha se o banco não responder, liveness não depende dele. Health público retorna status mínimo, sem credenciais, schema ou stack trace. Health não é autorização para endpoints comerciais.

## Imagens e versões

Bases verificadas no registry oficial e pinadas por digest em Dockerfiles/Compose:

- SQL Server **2022 CU26 Ubuntu 22.04**, digest `ba4c8329…`, edição **Express fixa**, nunca Developer;
- Caddy 2.11.4-alpine;
- .NET SDK canal 10.0 pinado por digest (resolvido 10.0.400), ASP.NET runtime 10.0.10;
- nginx-unprivileged 1.30.4-alpine-slim;
- Node 24-bookworm-slim pinado por digest.

API/Web/gateway usam multi-stage. API final inclui apenas runtime, binários e a ferramenta **PlatformBootstrap** necessária; não inclui DemoBootstrap/SDK. Migration tem imagem one-shot própria com bundle linux-x64. Pacotes de SO/Chromium resolvem durante build: re-build não é garantia de bytes idênticos; rollback usa o digest da imagem **já construída**. Atualizações de bases/Chromium passam por review/CI, nunca Watchtower.

SQL 2022 Express: até 10 GB de dados por banco, menor de 1 socket/4 cores e buffer pool de 1.410 MB. Não cria backup nativo comprimido: usamos NO_COMPRESSION + gzip externo. Planejar mudança de edição/serviço ao aproximar 70–80% do limite, sem troca silenciosa de licença. Fontes oficiais: [edições](https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022?view=sql-server-ver17), [compressão](https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/backup-compression-sql-server?view=sql-server-ver17), [containers amd64](https://learn.microsoft.com/en-us/sql/linux/containers/deploy?view=sql-server-ver17).

## TLS, Cloudflare e confiança de IP

1. Landing/domínio raiz continuam Pages. Criar app/api A para `<PRODUCTION_HOST>`, inicialmente **DNS only**; não criar AAAA sem IPv6 operacional.
2. Caddy emite certificado público ACME usando 80/443. Conferir certificado e redirecionamento diretamente na origem.
3. Habilitar proxy Cloudflare e **Full (strict)**. Não usar Flexible. Permitir HTTP-01 sem cache/challenge/redirect de borda que impeça renovação em `/.well-known/acme-challenge/*`; validar renovação após cutover.
4. Revisar a lista pública [Cloudflare IPs](https://www.cloudflare.com/ips/) e preencher `DETARA_TRUSTED_CIDRS` somente com essas redes. Não confiar em 0.0.0.0/0, redes privadas genéricas ou headers sem verificar o peer. Sem Cloudflare usar o sentinel 127.0.0.254/32 (ninguém externo confiável).
5. Caddy interpreta CF-Connecting-IP **somente de peers confiáveis**, descarta o header recebido e escreve X-Forwarded-For com `{client_ip}` e proto com o esquema real. API só confia no Caddy. Chamadas diretas à origem não podem forjar IP com esse header. Revalidar lista ao atualizar infra, sem plugin/API/token Cloudflare no Caddy. [Documentação Caddy](https://caddyserver.com/docs/caddyfile/options#servers).
6. Desabilitar cache de API/health/autenticação e HTML/config/manifest/service-worker no Cloudflare. Assets versionados podem ter cache. Não habilitar Cache Everything na aplicação.
7. Após HTTPS validado, mudar `DETARA_HSTS_MAX_AGE=31536000`. Inicialmente 0. HSTS pertence ao Caddy; sem preload/includeSubDomains automático.

CORS API permite exclusivamente `https://DETARA_APP_HOST`; AllowedHosts recebe DETARA_API_HOST. Web recebe somente origem pública de API por build-arg `DETARA_API_ORIGIN`, validada e aplicada **antes** de publish/hash PWA; CSP connect-src recebe a mesma origem. Não editar appsettings publicado em runtime, pois isso invalidaria o manifesto de assets. Se domínio mudar, reconstruir Web e registrar nova release. Nenhum secret vai para WASM. Swagger só Development; erros e logs seguros existentes permanecem.

## Secrets e persistência

`/etc/detara/production.env`: root:root 0600, exemplo versionado sem valores secretos. Parser aceita KEY=value literal, não executa shell, rejeita aspas/interpolação. Senhas SQL: 24–128 caracteres aleatórios, letras maiúsculas/minúsculas/números, alfabeto `A-Za-z0-9_!@%+=.-`. Pode gerar 32 bytes aleatórios em base64 com adaptação ao alfabeto, garantindo complexidade; nunca registrar valores.

Necessários: três senhas SQL distintas (sa, runtime, migrator), duas signing keys distintas (mínimo 32 bytes), secret gateway, Resend, credenciais R2 **separadas** para mídia e backup, senha PFX e chave pública age. Endpoint/host/bucket não são secrets, mas nunca inserir IDs de conta ou IP real no Git. R2 bucket privado, tokens limitados ao bucket; nenhuma credencial global Cloudflare.

PFX `/etc/detara/data-protection.pfx`: root:1654 **0640**, diretório pai acessível somente conforme necessário ao bind mount. UID 1654 da API precisa ler o arquivo; não usar root:root 0600 num bind legível apenas por root. Certificado e senha têm cópia no cofre externo; key ring é volume distinto e também precisa de backup. `DataProtection:ApplicationName=Detara.Platform` não muda. Recuperar PFX sem o key ring não recupera TOTP.

SQL usa volume `detara-production_detara-sql-data`. Staging `/var/backups/detara/sql`: 10001:0 0700. Sessões WhatsApp: 1000:1000 0700. Caddy volumes têm owner 1000 preparado no init. API key ring herdará ownership da imagem. Não usar chmod 777 ou down -v. Production usa S3 para todos os uploads, já suportado e validado; não há upload persistente no layer efêmero. `/tmp` é temporário e limitado. Data local `src/Detara.Api/data/` não é copiada para imagem nem alterada.

Credencial runtime só db_datareader/db_datawriter; sem sa/db_owner. `detara_migrator` tem db_owner apenas em Detara, usado no bundle, sem API/worker. Primeiro init cria banco vazio e usuários sem alterar senhas existentes. Para rotacionar SQL, alterar login em sessão administrativa segura e atualizar secret/recriar serviço em janela; init não é rotação.

Tráfego SQL usa Encrypt=True/TrustServerCertificate=True exclusivamente na rede internal do host único. Criptografa, mas não autentica a cadeia do servidor: risco explicitamente aceito nesta topologia; migrar para certificado validável ao separar hosts. Não usar essa configuração em rede pública.

## Operação e limites

- [Deploy/rollback](runbooks/deploy.md), [backup/restore](runbooks/backup-restore.md), [recuperação](disaster-recovery.md), [monitoramento](operations.md), [primeiro tenant](first-tenant.md).
- WhatsApp é integração não oficial; pode desconectar independentemente da API. O gateway existente usa `--no-sandbox` no Chromium: risco contido por usuário não-root, isolamento de rede, caps removidas, sessão privada e atualizações revisadas; nunca expor o gateway. Não ampliar privilégio para mascarar falhas. Sessões não são copiadas automaticamente para R2; desastre exige novo QR. Nenhum envio real no QA.
- Não há zero downtime/HA numa VPS única. Deploy tem janela de manutenção, especialmente migrations. RPO alvo 24h e RTO alvo até 8h são objetivos iniciais, não SLA.
- Repositório + imagens por digest + config + cofre de secrets/PFX/age + SQL/key ring externos + mídia R2 são necessários à reconstrução.
