# Administração da plataforma

## Escopo e princípio de segurança

O Platform Admin é uma identidade global separada das identidades de empresas. Ele não possui `EmpresaId`, não reutiliza usuário, perfil ou permissões tenant e não oferece impersonation. Sua API expõe somente metadados de provisionamento, status, convite e auditoria; dados operacionais permanecem inacessíveis.

Os tokens também são separados:

- tenant: scheme `DetaraTenantBearer`, audience `Detara.Web` e chave `Jwt__ChaveAssinatura`;
- plataforma: scheme `DetaraPlatformBearer`, audience `detara-platform` e chave exclusiva `PlatformJwt__ChaveAssinatura`;
- o token de plataforma dura 45 minutos, exige `identidade=platform_admin` e `amr=mfa`, é revalidado contra status e versão de segurança em toda requisição e não possui refresh token;
- o frontend mantém o token em `sessionStorage`, sob `detara.platform.token`, e usa um `HttpClient`/handler dedicado. O handler tenant exclui explicitamente as rotas de plataforma e convite.

Não existe “entrar como empresa”, bypass de tenant ou endpoint de bootstrap.

## Configuração

Valores sensíveis devem vir de secret store ou variáveis de ambiente; nunca do appsettings versionado.

| Chave | Regra |
|---|---|
| `PlatformJwt__ChaveAssinatura` | segredo exclusivo com pelo menos 32 bytes; diferente do JWT tenant |
| `PlatformJwt__Emissor` | emissor esperado pelo scheme da plataforma |
| `PlatformJwt__Audiencia` | `detara-platform` em produção |
| `PlatformJwt__ExpiracaoMinutos` | entre 30 e 60; padrão 45 |
| `DataProtection__KeyRingPath` | caminho absoluto, persistente e gravável em produção |
| `Web__PublicBaseUrl` | URL HTTPS pública do frontend em produção |

Em produção, a API falha no startup se a chave de plataforma estiver ausente/fraca, se coincidir com a chave tenant, se o key ring não for absoluto ou se a URL pública não for HTTPS. O Docker Compose local persiste o key ring no volume `detara-data-protection-keys`; em produção, o volume também deve ser protegido pela infraestrutura.

## Bootstrap e break-glass

O primeiro administrador é criado exclusivamente pela ferramenta `Detara.PlatformBootstrap`. A senha é lida interativamente, sem eco, nunca é aceita como argumento e deve ter de 12 a 256 caracteres. A primeira execução só funciona quando a tabela está vazia; uma segunda execução de `create-admin` é recusada.

Local:

```powershell
$env:ConnectionStrings__DefaultConnection = "<obter do secret store>"
dotnet run --project tools/Detara.PlatformBootstrap -- create-admin --nome "Operação Detara" --email "admin@exemplo.com"
```

No container da API, usando as variáveis já injetadas e terminal interativo:

```powershell
docker compose run --rm -it api dotnet /tools/platform-bootstrap/Detara.PlatformBootstrap.dll create-admin --nome "Operação Detara" --email "admin@exemplo.com"
```

Break-glass:

```powershell
dotnet run --project tools/Detara.PlatformBootstrap -- reset-password --email "admin@exemplo.com"
dotnet run --project tools/Detara.PlatformBootstrap -- reset-mfa --email "admin@exemplo.com"
```

Ambos incrementam a versão de segurança, revogam sessões anteriores e geram auditoria. O reset de MFA também remove todos os recovery codes. O próximo login exige novo enrollment.

## MFA TOTP e recuperação

MFA é obrigatório. O primeiro fator produz apenas um challenge opaco protegido por Data Protection, com purpose `Detara.Platform.MfaChallenge.v1` e validade de cinco minutos; ele não é um JWT administrativo. O segredo TOTP usa purpose independente `Detara.Platform.TotpSecret.v1`, permanece cifrado no banco e nunca é logado.

O TOTP segue RFC 6238, SHA-1, seis dígitos, passo de 30 segundos e janela de um passo anterior/futuro. O último timestep aceito é persistido para impedir replay. Há no máximo cinco erros por challenge, além dos rate limits por origem: login 5/minuto e MFA 8/5 minutos.

O QR code é gerado localmente com QRCoder; nenhuma chave é enviada a serviço externo. O enrollment só fica ativo após validar o primeiro código. São gerados dez recovery codes com 80 bits cada, exibidos uma única vez e persistidos somente como SHA-256. Cada código é single-use. Regeneração exige senha atual e TOTP e invalida todos os anteriores.

Dependências adicionais: Otp.NET 1.4.1 e QRCoder 1.8.0, ambas MIT. O pacote completo ASP.NET Core Identity não foi introduzido.

## Provisionamento de empresa

`POST /api/plataforma/empresas` aceita somente nome fantasia, razão social, CPF/CNPJ, contatos opcionais, timezone e nome/e-mail do administrador inicial. IDs, slug, status, perfil, permissões e senha não fazem parte do DTO, evitando mass assignment.

Uma única transação cria:

1. empresa com ID e slug gerados pelo servidor;
2. perfil Administrador;
3. vínculos com o catálogo canônico `Permissoes.Definicoes`;
4. usuário administrador inativo, com hash aleatório inutilizável;
5. convite pendente, ainda sem token;
6. evento append-only de auditoria.

O envio de e-mail ocorre depois do commit, em worker durável. Falha do Resend nunca desfaz o tenant. O worker usa tentativas limitadas (1, 5 e 30 minutos), chave de idempotência por tentativa, recupera processamento interrompido e armazena somente SHA-256 do token. O convite padrão expira em 72 horas.

O link usa `/ativar-conta#token=...`: o fragment não vai ao servidor em requisições HTTP, é lido em memória e removido imediatamente da barra. Ele não é salvo no browser. O e-mail escapa nome da empresa, nome do usuário e URL. O aceite exige empresa ativa, usuário inativo, token válido, não expirado e não utilizado; define a senha escolhida pelo próprio tenant e não faz login automático.

Reenvio invalida o hash anterior antes de colocar a mensagem na fila. Empresa suspensa e usuário já ativo impedem o aceite. Não há hard delete de empresa.

## Suspensão, reativação e auditoria

Suspender ou reativar exige motivo e incrementa `Empresa.VersaoSeguranca`. Tokens tenant emitidos antes de qualquer transição deixam de ser válidos; reativar não restaura sessões antigas. As ações ficam em auditoria com ator, alvo, timestamp UTC, trace ID e descrição segura.

`AuditoriasPlataforma` é append-only: update e delete são recusados. A tela usa paginação server-side e filtros. Auditoria não contém senha, JWT, segredo TOTP, recovery code, token de convite, connection string ou conteúdo operacional de tenant.

## Endpoints e PWA

Anônimos, justificados e inventariados:

- `POST /api/plataforma/autenticacao/login`;
- `POST /api/plataforma/autenticacao/mfa/configuracao`;
- `POST /api/plataforma/autenticacao/mfa/ativar`;
- `POST /api/plataforma/autenticacao/mfa/verificar`;
- `POST /api/convites/administrador/validar`;
- `POST /api/convites/administrador/aceitar`.

Convites têm 10 requisições por cinco minutos por origem. As respostas são `no-store`; service workers ignoram toda rota `/api/`, portanto dados e tokens de plataforma não entram no cache PWA. Não há fluxo offline para login, MFA, provisionamento ou convite.

## Limitações deliberadas

- somente o primeiro Platform Admin é criado nesta versão; o segundo administrador fica para backlog;
- não há refresh token, self-signup, billing, impersonation ou support access;
- o limite por challenge usa cache de processo; antes de escalar réplicas, deve migrar para cache distribuído;
- e-mail e commit de conclusão não formam transação distribuída. Idempotência reduz duplicidade, e leases interrompidos são recuperados;
- proteção criptográfica do key ring em produção depende do secret/KMS e volume seguro da infraestrutura.

## Operação e diagnóstico

- `Pendente`: aguardando worker ou retry;
- `Processando`: lease em andamento; após dez minutos é recuperado;
- `Enviado`: token vigente até `ExpiraEmUtc`;
- `FalhaEnvio`: tentativas encerradas, disponível para reenvio manual;
- `Aceito`: usuário ativado, token removido;
- `Expirado`/`Invalidado`: não aceitam uso.

Erros de login, MFA e convite usam mensagens genéricas. Tokens e segredos não devem ser adicionados a logs, telemetria, screenshots ou tickets.
