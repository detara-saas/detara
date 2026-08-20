# Revisão de segurança pré-produção — Task 10

- Data da revisão: 20/08/2026
- Base: `main` com a Task 09 (PR #16)
- Branch: `security/hardening-pre-launch`

## Resultado

**APROVADA COM PENDÊNCIAS de Production Readiness.** A baseline de aplicação não possui Critical ou High aberto. O isolamento de tenant, autorização, autenticação, XSS, upload, secrets e configuração foram revisados adversarialmente. A primeira empresa real ainda depende dos blockers de infraestrutura listados abaixo.

## Escopo e referências

Revisão manual de controllers, contracts, handlers, repositórios, `DetaraDbContext`, autenticação, autorização, uploads, storage, HTML, PWA, Docker/Nginx, configuração e dependências. Referenciais: OWASP ASVS 5.0, OWASP API Security Top 10 2023 e recomendações oficiais do ASP.NET Core 10.

## Inventário de endpoints

Total: **95** endpoints — 94 ações de controller e `GET /health`.

- Anônimos: **2** — `POST /api/autenticacao/login` e `GET /health`.
- Protegidos: **93** — policy explícita por permissão ou fallback autenticado.
- A whitelist anônima é verificada por teste via `EndpointDataSource`; `health` aceita somente GET.

| Área | Endpoints |
|---|---:|
| Agenda/agendamentos | 12 |
| Autenticação | 1 |
| Clientes | 6 |
| Veículos e fotos | 10 |
| Categorias, serviços e pacotes | 15 |
| Configuração operacional | 3 |
| Orçamentos e PDF | 15 |
| Ordens de serviço e fotos | 14 |
| Financeiro | 7 |
| Notificações | 9 |
| Preferências | 2 |
| Health | 1 |

## Findings

| ID | Severidade | Título | Status |
|---|---|---|---|
| SEC-001 | High | JWT permanecia válido após desativação/revogação | Fixed |
| SEC-002 | Medium | Login de identidade inexistente não executava custo de hash | Fixed |
| SEC-003 | Medium | Baseline de host, CORS e headers de produção incompleta | Fixed |
| SEC-004 | Medium | Consumo do provedor de e-mail sem timeout/limite explícitos | Fixed |
| SEC-005 | Low | Nginx podia manter bootstrap do Service Worker por uma hora | Fixed |
| SEC-006 | Low | `.env.example` continha credenciais fictícias reutilizáveis | Fixed |
| SEC-007 | Low | Imagem não é decodificada para validar dimensão/estrutura completa | Deferred |
| SEC-008 | Low | JWT em `sessionStorage` permanece acessível a XSS same-origin | Accepted |
| SEC-009 | Info | Fonte web depende de terceiro | Deferred |
| SEC-010 | Medium | TLS/trusted proxy/secrets/storage/backup ainda não provisionados | Deferred — Production Readiness |
| SEC-011 | Low | Health não tinha limite próprio nem restrição explícita a GET | Fixed |
| SEC-012 | Low | Ranges de OS/financeiro não tinham teto máximo | Fixed |
| SEC-013 | Info | Scanner de secrets de histórico não disponível localmente | Deferred — CI/Production Readiness |
| SEC-014 | Low | Import map publicado pelo Blazor exige script inline na CSP estática | Accepted |

- **Critical abertos: 0**
- **High abertos: 0**

### SEC-001 — JWT antigo após mudança de identidade

- Área: autenticação/autorização.
- Cenário: claims eram aceitas até a expiração (configuração padrão de 8 horas), mesmo se usuário, empresa ou perfil fosse desativado ou uma permissão fosse revogada.
- Impacto: manutenção de acesso indevido e potencial elevação de privilégio após decisão administrativa.
- Evidência: `JwtBearer` validava apenas criptografia e tempo; não havia consulta ao estado atual.
- Correção: claim de perfil/versão do usuário e revalidação por request de empresa, usuário, perfil e conjunto exato de permissões. Troca de senha atualiza a versão e revoga o token.
- Regressão: testes para estado válido, usuário/empresa/perfil inativo, permissão revogada/forjada, tenant diferente, token expirado/adulterado e algoritmos alternativos.
- Status: Fixed.

### SEC-002 — enumeração temporal no login

- Área: autenticação.
- Cenário: usuário inexistente encerrava o fluxo antes de executar `PasswordHasher`.
- Impacto: amostragem repetida poderia ajudar a distinguir identidades existentes.
- Correção: hash fictício reutilizado e verificação de custo equivalente; mensagem permanece genérica. Usuário inativo também executa a verificação real antes da decisão.
- Regressão: `UsuarioInexistente_ExecutaVerificacaoFicticia` e rate limit HTTP.
- Status: Fixed.

### SEC-003 — configuração defensiva incompleta

- Área: misconfiguration.
- Cenário: `AllowedHosts=*`, ausência de fail-fast Production, HSTS/headers e version disclosure do Kestrel.
- Impacto: host header abuse, clickjacking/MIME sniffing e implantação insegura por omissão.
- Correção: Production rejeita host wildcard e CORS não HTTPS/wildcard; Kestrel remove server header, Nginx oculta a versão e ambos aplicam limites/headers seguros; HSTS foi adicionado.
- Regressão: teste de headers, CORS hostil e whitelist anônima; startup Production deve ser exercitado no QA final.
- Status: Fixed no código da aplicação; edge TLS permanece em SEC-010.

### SEC-004 — resposta externa sem limites explícitos

- Área: integração de e-mail/API10.
- Cenário: o cliente do Resend dependia do timeout e buffering padrão.
- Impacto: retenção excessiva de recursos e propagação inadequada de falha externa.
- Correção: host HTTPS fixo, timeout de 15 segundos, buffer máximo de 64 KiB, cancelamento e tratamento seguro de timeout/JSON inválido.
- Regressão: `ResendEmailProviderSecurityTests`.
- Status: Fixed.

### Findings Low/Info residuais

- SEC-007: o servidor não decodifica a imagem. Mitigações: 10 MiB, JPEG/PNG/WebP por magic bytes, SVG/HTML rejeitados, storage privado, nome aleatório, `nosniff` e nenhuma transformação server-side. Adicionar decoder seguro, limite de dimensões e política EXIF junto ao pipeline de thumbnails.
- SEC-008: `sessionStorage` reduz persistência entre sessões, mas não protege contra XSS. Migração para BFF/cookie HttpOnly não é uma mudança isolada: introduz CSRF e arquitetura server-side. Manter CSP/sanitização e reavaliar com Platform Admin.
- SEC-009: auto-hospedar Inter em Production Readiness para reduzir dependência e metadados enviados ao terceiro.
- SEC-013: busca por padrões e revisão de arquivos rastreados foram executadas sem revelar secret real. Habilitar scanner de histórico no CI; nenhum valor deve ser impresso em logs de pipeline.
- SEC-014: o publish .NET 10 gera um `importmap` inline necessário para resolver assets fingerprintados. O Nginx estático permite `unsafe-inline` em `script-src`; `self`, `wasm-unsafe-eval`, `object-src 'none'`, `frame-ancestors 'none'`, sanitização e encoding permanecem. Automatizar nonce/hash exigirá uma camada dinâmica ou etapa de build dedicada.

## Autenticação e sessão

- JWT: HS256 somente, key >= 32 bytes, issuer/audience/lifetime obrigatórios, clock skew de 1 minuto e error details desabilitados.
- Expiração configurável entre 1 e 1440 minutos; atual padrão 480 minutos.
- Key vem de secret/variável, nunca do browser.
- Token expirado, adulterado, HS384 e HS512 são rejeitados.
- Usuário, empresa, perfil, troca de senha e permissões são revalidados no banco em cada request.
- Login retorna resposta genérica, executa hash fictício quando necessário e limita 10 tentativas/minuto por IP observado.
- O reverse proxy futuro deve preservar IP real apenas via `KnownProxies`/`KnownNetworks`; habilitar forwarded headers sem trust list permitiria spoofing do rate limit.
- Não existem refresh tokens, cookies de autenticação ou logout server-side nesta fase.

## Multi-tenancy e BOLA

Recursos atacados por ID/tenant em testes e revisão: preferências, clientes, veículos, fotos, categorias, serviços, pacotes, agenda, orçamentos, PDF, ordem de serviço, checklist/fotos, contas a receber, pagamentos/estornos, notificações e configurações.

Controles confirmados:

- query filter para todo `EntidadeEmpresaBase`;
- write guard bloqueia create/update/delete sem tenant, tenant diferente e mutação de `EmpresaId`;
- `EmpresaId` é concurrency token;
- DTOs públicos não aceitam tenant como autoridade;
- `IgnoreQueryFilters` foi inventariado e mantém predicado explícito por empresa, salvo worker/login de sistema documentados;
- teste HTTP lê e edita GUID real de outra empresa e recebe 404, sem alterar o registro;
- nenhum acesso indevido foi observado.

## Autorização e ações sensíveis

- fallback global autenticado e policies geradas a partir de `Permissoes.Todas`;
- permission claim é comparada ao perfil atual, impedindo claim antiga após revogação;
- testes diretos cobrem criação/edição/status, PDF, OS/check-in/checklist/transições/fotos/adicionais, pagamentos/estornos/vencimento, templates/preview/teste/reenvio e configurações;
- anônimo recebe 401; autenticado sem permissão recebe 403; recurso de outro tenant não é revelado.

## Mass assignment e property authorization

Controllers recebem records de contrato, mapeiam explicitamente para commands e nunca fazem bind de entidade EF. Inputs não expõem `EmpresaId`, auditoria, provider ID ou status internos. Um probe HTTP adicionando `EmpresaId`, `EhAtivo` e `CriadoEmUtc` confirmou que esses campos não controlam tenant, estado ou auditoria.

## SQL e acesso a dados

- Raw SQL runtime encontrado: **0**.
- Raw SQL em migrations: **2**, ambos updates constantes sem entrada do usuário.
- Usos seguros: queries LINQ parametrizadas pelo EF e ordenações por allowlist.
- Findings de SQL injection: **0**.

## XSS, HTML e CSP

- `MarkupString`/`HtmlString` no código de produto: **0**.
- Rich text: template de e-mail, sanitizado por allowlist com Ganss.Xss; tokens são codificados com `HtmlEncoder`.
- Testes removem `script`, event handlers, SVG/iframe, `javascript:`/`vbscript:` e CR/LF de assunto; valores de token hostis são encoded.
- Preview usa `iframe sandbox=""`; API aplica CSP `default-src 'none'` e o Nginx aplica CSP compatível com Blazor WASM. A exceção `unsafe-inline` de script necessária ao import map está registrada como SEC-014.
- Stored/reflected XSS explorável encontrado: **0**.

## Uploads e storage

- tamanho: 10 MiB por arquivo; request multipart e Kestrel também limitados;
- formatos: somente JPEG, PNG e WebP;
- tipo: magic bytes no servidor, independente de extensão/MIME informado;
- chaves: geradas no servidor com IDs internos e GUID aleatório;
- traversal: caminho canonicalizado e validado sob root explícito;
- storage: privado e proibido sob `wwwroot`;
- download: metadata tenant-scoped, autorização backend, content type conhecido, `nosniff`, `no-store` e nome seguro;
- SVG/HTML/JS e assinatura inválida são rejeitados;
- dívida: decoder/dimensão/EXIF, classificada Low pelas mitigações e ausência de processamento server-side.

## CORS, HTTPS e headers

Implementado na aplicação:

- allowlist explícita; wildcard/URI inválida falha; Production exige HTTPS não local;
- `AllowedHosts` explícito obrigatório em Production;
- HTTPS redirection, HSTS fora de Development;
- API: `nosniff`, `DENY`, referrer policy, permissions policy, CSP, `no-store` e trace id;
- Nginx: CSP, anti-framing, HSTS, COOP e headers equivalentes;
- Kestrel sem server header e Nginx sem número de versão no banner.

Dependente do reverse proxy:

- certificado e domínio reais;
- redirect HTTP→HTTPS na borda e confirmação de HSTS sobre conexão externa HTTPS;
- trusted forwarded headers com allowlist de proxy;
- limites de conexão/borda e política de acesso ao health.

## Secrets e logs

- Revisados: appsettings, launch settings, Docker/Compose, `.env.example`, código, testes, scripts, arquivos rastreados e nomes do histórico Git.
- Busca por padrões não encontrou secret real versionado. `.env` permanece ignorado.
- Valores de exemplo de senha/chave foram removidos; `.env.example` deixa campos sensíveis vazios.
- `gitleaks` não estava instalado; a varredura profunda de histórico deve entrar no CI.
- Logs não incluem senha, JWT, API key, connection string, corpo de e-mail ou resposta externa. Falhas usam código seguro e trace id; stack fica somente no log de servidor para 5xx.

## PWA e armazenamento do browser

- Worker publicado ignora requests cross-origin, `/api/` e qualquer request com `Authorization`.
- JWT, respostas API e dados de negócio não são cacheados.
- Cache contém somente app shell/assets versionados; worker/manifest/index usam revalidação no Nginx.
- JWT fica em `sessionStorage`; preferências não sensíveis usam `localStorage`.
- Scope permanece na origem da aplicação; atualização exige consentimento.

## Resource abuse

- paginação limitada a 10/25/50 e buscas/ordenações limitadas;
- request global 12 MiB, imagens 10 MiB e headers 32 KiB;
- timeout de headers Kestrel 15 segundos;
- login 10/min/IP; teste de e-mail 3/10min/empresa+usuário; health 30/min/IP;
- e-mail com fila, retry bounded, idempotência, timeout e lote limitado;
- períodos de OS e financeiro limitados a dez anos;
- PDF e listas trabalham com dados tenant-scoped e coleções validadas.

## Production config

- Production startup falha com JWT/DB/host críticos ausentes ou `AllowedHosts=*`;
- Swagger, seed e validação automática de migrations executam apenas em Development;
- error details do JWT e exceções não são enviados ao cliente;
- CORS Development não amplia Production;
- configuração final de host, CORS, banco, storage e e-mail deve vir do ambiente/secrets manager.

## Testes de segurança destacados

- anonymous whitelist e fallback policy;
- JWT válido, expirado, adulterado e algoritmo alternativo;
- usuário/empresa/perfil inativo, permissão revogada/forjada e tenant divergente;
- permissões de operações sensíveis diretamente na API;
- BOLA com dois tenants reais no banco;
- mass assignment de campos protegidos;
- XSS/HTML/header injection;
- upload inválido, tamanho, tipo e traversal;
- brute force/login, paginação, ranges, request size e falhas externas.

## BLOCKERS ANTES DA PRIMEIRA EMPRESA REAL

1. provisionar domínio, certificado TLS e reverse proxy com trusted proxies configurados;
2. definir `AllowedHosts` e origens CORS reais; validar headers externamente;
3. armazenar/rotacionar JWT key, senha SQL e API key em secrets manager;
4. usar banco Production com usuário de menor privilégio, política de patch e restore testado;
5. provisionar object storage privado durável, retenção e lifecycle;
6. implantar backup/restore e executar teste de restauração;
7. configurar logs centralizados, métricas, alertas, auditoria operacional e retenção;
8. configurar domínio/DNS do Resend, reputação, limites e alertas;
9. adicionar gitleaks (ou equivalente), SAST e dependency audit no CI;
10. executar DAST/pentest autenticado em staging production-like antes da beta real;
11. revisar auto-hospedagem de fontes e política de privacidade;
12. planejar MFA obrigatório para o futuro Platform Admin, com recovery seguro.

## Security debt e decisões

- Medium SEC-010: aceito apenas porque é infraestrutura ainda inexistente; bloqueia cliente real, não a próxima implementação local.
- Low SEC-007/008/014: mitigados e registrados; reavaliar antes de portal público ou Platform Admin privilegiado.
- Info SEC-009/013: entram no backlog de Production Readiness/CI.
- Recomendações de pentest/DAST foram registradas; nenhum scanner substitui a revisão e os testes adversariais atuais.

## Migrations e dependências

- Migrations criadas na Task 10: **0**.
- Packages novos/atualizados: **0**.
- O hardening reutiliza ASP.NET Core, EF Core, PasswordHasher e sanitizador já existentes.
