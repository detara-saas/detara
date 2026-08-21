# Threat model da Detara

## Escopo e método

Este modelo cobre a aplicação existente até a Task 13.1: Blazor WebAssembly, API ASP.NET Core, SQL Server, storage privado, integração transacional com Resend, Platform Admin, provisionamento e login tenant por e-mail. Billing e infraestrutura definitiva de produção não fazem parte deste escopo.

A revisão usa STRIDE como guia e prioriza OWASP ASVS 5.0 e OWASP API Security Top 10 2023, sobretudo API1 (BOLA), API2 (Broken Authentication), API3 (Broken Object Property Level Authorization), API4 (Unrestricted Resource Consumption), API5 (Broken Function Level Authorization), API8 (Security Misconfiguration) e API10 (Unsafe Consumption of APIs).

## Ativos

- isolamento lógico e dados comerciais de cada empresa;
- identidades, hashes de senha, JWTs, perfis e permissões;
- clientes, veículos, agenda, orçamentos, ordens de serviço e fotografias;
- contas a receber, pagamentos, estornos e indicadores financeiros;
- templates e fila de notificações;
- credenciais de banco, assinatura JWT e provedor de e-mail;
- integridade do código, migrations, logs e configuração de produção;
- disponibilidade da API, banco, storage e fila.

## Atores

- usuário legítimo de uma empresa, com permissões limitadas;
- administrador da própria empresa;
- usuário desativado que conserva um JWT antigo;
- usuário da Empresa A tentando acessar a Empresa B;
- atacante não autenticado tentando enumerar contas ou forçar login;
- atacante autenticado manipulando GUIDs, propriedades, status e valores;
- conteúdo malicioso submetido em nome, observação, HTML ou upload;
- provedor externo indisponível ou retornando conteúdo hostil;
- operador de infraestrutura com configuração incorreta;
- dependência ou cadeia de build comprometida.

## Trust boundaries

```text
Browser/PWA não confiável
        |
        | HTTPS + JSON/multipart + JWT
        v
Reverse proxy futuro / borda       (fora do processo da aplicação)
        |
        v
API ASP.NET Core                   (autenticação, autorização, limites)
        |
        +----> Application/Domain  (validação e invariantes)
        |
        +----> EF Core/SQL Server  (fronteira de persistência multi-tenant)
        |
        +----> storage privado     (bytes não públicos)
        |
        +----> Resend HTTPS        (sistema externo não confiável)
```

O browser nunca é autoridade sobre `EmpresaId`, usuário, permissão, preço calculado, status interno ou identificador do provedor. O JWT é uma credencial assinada, mas seu estado é revalidado contra o banco em cada requisição autenticada.

## Entry points

- `POST /api/autenticacao/login` (anônimo, limitado por origem);
- `POST /api/autenticacao/selecionar-empresa` (anônimo, exige challenge protegido e é limitado por origem);
- `GET /health/live` e `GET /health/ready` (anônimos, somente GET e limitados por origem);
- 103 endpoints protegidos e dez endpoints anônimos na whitelist auditada;
- uploads multipart de fotos de veículo e ordem de serviço;
- parâmetros de rota e query, especialmente GUIDs, busca, ordenação, paginação e períodos;
- corpo JSON de comandos de criação, edição e transição;
- HTML rico do template de e-mail e preview em iframe sandboxed;
- respostas e falhas do Resend;
- variáveis de ambiente, Docker, appsettings, migrations e pipeline de publicação;
- cache do Service Worker e armazenamento do browser.

## Fluxos sensíveis

1. Login normaliza o e-mail e executa uma consulta cross-tenant encapsulada, projetando somente usuário, empresa, perfil e permissões necessários. Todos os hashes candidatos são verificados antes da decisão.
2. Uma única membership válida recebe JWT tenant HS256 diretamente. Duas ou mais recebem um challenge Data Protection com purpose exclusivo e validade de cinco minutos; a escolha deve pertencer à lista protegida e toda a membership é revalidada antes do JWT.
3. Em cada requisição, a API valida assinatura, emissor, audiência, expiração e algoritmo; depois confirma empresa, usuário, perfil e permissões atuais no banco.
4. O tenant vem de claims validadas e alimenta filtros globais EF e o write guard do `DbContext`.
5. IDs recebidos são resolvidos dentro do tenant; ausência e tentativa cross-tenant convergem para não encontrado ou acesso negado.
6. Upload é limitado, identificado por magic bytes, recebe chave aleatória server-side e é armazenado fora do `wwwroot`.
7. HTML de e-mail é sanitizado e tokens são codificados; envio externo usa destino derivado do domínio, chave idempotente e endpoint fixo HTTPS.
8. O Service Worker cacheia somente app shell/assets estáticos e desvia API, requisições autenticadas e origens externas diretamente para a rede.

## Ameaças e controles

| Ameaça | Cenário | Controles principais | Evidência |
|---|---|---|---|
| Spoofing | JWT adulterado, expirado ou com algoritmo alternativo | validação estrita HS256, issuer, audience, lifetime e chave >= 32 bytes | testes `JwtEEndpointsSecurityTests` |
| Spoofing | JWT antigo após desativação/revogação | revalidação do estado autenticado em cada requisição | `ValidadorIdentidadeAutenticadaTests` |
| Spoofing | escolha de empresa forjada, alterada ou expirada | challenge Data Protection isolado, allowlist de memberships e revalidação antes do JWT | `ChallengeSelecaoEmpresaTenantTests` e testes HTTP multiempresa |
| Tampering | cliente envia `EmpresaId`, `EhAtivo` ou auditoria | contratos de entrada mínimos, mapeamento explícito e tenant do contexto | teste de mass assignment HTTP |
| Tampering | alteração de valor/status financeiro | permissões backend, comandos semânticos e invariantes de domínio | suíte Financeiro e autorização HTTP |
| Repudiation | erro sem correlação | trace id em resposta e logs estruturados; sem payload/secrets | `TratadorGlobalExcecoes` |
| Information disclosure | Empresa A usa GUID da Empresa B | query filters, predicados explícitos e write guard | suíte multi-tenancy + BOLA HTTP |
| Information disclosure | stack trace ou detalhe do provedor chega ao cliente | erro global seguro, JWT sem error details e mensagens externas normalizadas | testes de API e Resend |
| Information disclosure | login revela se o e-mail existe ou se a empresa está inativa | mesmo status/corpo genérico, hash fictício quando não há candidato e verificação de todos os hashes candidatos | testes de enumeração e `AutenticarCommandTests` |
| Denial of service | brute force, health, e-mail, uploads e ranges | rate limit, 12 MiB global, 10 MiB por imagem, paginação e range máximo | testes de limite e validadores |
| Elevation of privilege | endpoint sem policy ou permissão forjada | fallback autenticado, policies por claim, comparação da permissão com o banco e whitelist anônima testada | inventário automático de endpoints |
| Stored XSS | HTML/event handler/URL perigosa em template | HtmlSanitizer allowlist, HtmlEncoder, CSP e iframe sandbox | `RenderizadorTemplateEmailTests` |
| Unsafe external consumption | timeout/JSON hostil do Resend | host HTTPS fixo, timeout, resposta limitada, erro seguro e retry bounded | `ResendEmailProviderSecurityTests` |
| Cache disclosure | Service Worker armazena API/JWT/dados | bypass explícito de API, Authorization e cross-origin; cache apenas de assets | testes PWA e revisão do worker |

## Riscos residuais

- O JWT permanece em `sessionStorage`; uma futura vulnerabilidade XSS na mesma origem poderia acessá-lo. Migrar para BFF/cookie HttpOnly exige decisão arquitetural e proteção CSRF correspondente.
- O challenge de seleção é stateless e pode ser reapresentado durante seus cinco minutos de validade. Ele não autentica requisições operacionais, não é um Bearer token e sempre revalida usuário, empresa, perfil e versões antes de emitir JWT. O frontend o mantém somente em memória e o perde no refresh.
- A validação de imagem confirma assinatura e tipo permitido, mas não decodifica dimensões nem remove EXIF. O servidor não processa a imagem e a entrega com `nosniff`; transformação segura permanece no backlog.
- TLS, trusted proxies, proteção de borda, secrets manager, backup/restore, storage de produção, monitoramento e DNS do Resend dependem da fase Production Readiness.
- Google Fonts é uma dependência de disponibilidade/privacidade de terceiro; auto-hospedagem permanece recomendada antes de maior exigência de privacidade.

## Extensão Task 11 — Platform Admin e provisionamento

Novos ativos: chave JWT exclusiva da plataforma, segredo TOTP protegido, recovery codes, challenge MFA, token de convite, key ring, trilha de auditoria e capacidade de suspender/provisionar tenants.

| Ameaça | Cenário | Controle |
|---|---|---|
| Elevação de privilégio | token tenant usado na API global ou token platform usado em dados tenant | schemes, audiences, signing keys e handlers separados; testes cruzados |
| Bypass MFA | senha/challenge usado como sessão ou JWT sem `amr=mfa` | challenge Data Protection opaco, policy e revalidação obrigatória |
| Replay/brute force | repetição de timestep, recovery code ou tentativas de challenge | timestep persistido, hashes single-use, cinco erros/challenge e rate limits |
| Vazamento de segredo | QR/TOTP enviado a terceiro, key ring efêmero ou token no log/cache | QR local, purpose isolado, volume persistente, fragment URL, `no-store` e bypass PWA |
| Abuso de provisionamento | mass assignment, CPF/CNPJ duplicado ou grafo parcial | DTO mínimo, IDs/slug server-side, unique indexes e transação única |
| Cross-tenant global | `IgnoreQueryFilters` vira acesso operacional irrestrito | consultas explícitas somente a metadados e predicados EmpresaId/UsuarioId; sem impersonation |
| Convite adulterado | token em claro, reuso, expiração, empresa suspensa ou usuário ativo | SHA-256, comparação fixa, single-use, 72h, reenvio invalida anterior e estado revalidado |
| Falha externa | Resend falha ou worker cai após obter lease | commit tenant anterior ao e-mail, retry bounded, lease recuperável e idempotência |
| XSS/header injection | nome malicioso entra no e-mail ou auditoria/UI | HTML encoding, Razor encoding e rejeição de CR/LF no e-mail |
| Repudiation | operação global sem rastro ou rastro alterado | auditoria append-only com ator, alvo, UTC, trace ID e descrição segura |

Riscos residuais específicos: limite por challenge é local ao processo e precisa de cache distribuído antes de múltiplas réplicas; key ring depende de KMS/volume seguro da infraestrutura; e-mail e confirmação no banco não são uma transação distribuída.

## Referências

- [OWASP ASVS 5.0](https://owasp.org/www-project-application-security-verification-standard/)
- [OWASP API Security Top 10 2023](https://owasp.org/API-Security/editions/2023/en/0x11-t10/)
- [ASP.NET Core Kestrel security considerations](https://learn.microsoft.com/aspnet/core/fundamentals/servers/kestrel/security-considerations?view=aspnetcore-10.0)
