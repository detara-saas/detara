# Deploy de produção pelo GitHub Actions

## Arquitetura e limites

O workflow manual **Deploy Production** valida e implanta uma release já construída. Um merge ou push em `main` **não faz deploy automático**. A publicação existente `Production images` continua responsável por construir `api`, `web`, `whatsapp-gateway` e `migrations` em linux/amd64, publicar tags pelo SHA e gerar `release.env`, `detara-migrate`, `public-api-origin.txt` e `SHA256SUMS`.

O workflow recebe um SHA Git completo, prova que ele pertence à história de `origin/main`, encontra a execução bem-sucedida de `Production images` para o mesmo SHA, valida checksums e confere os quatro manifests GHCR por digest. Nenhuma imagem é construída no deploy. Só depois configura SSH com host key pinada, transfere os quatro arquivos de metadata/bundle para um staging sem secrets e chama o entrypoint privilegiado no host.

No servidor, `/usr/local/sbin/detara-deploy-release` é root-owned e é o único comando permitido por sudo ao usuário `detaradeploy`. O wrapper valida SHA, ID da execução, ownership, paths, conteúdo e digests novamente. O código da release não é aceito do usuário SSH: um mirror Git público root-owned busca `main`, confirma ancestry e produz `/opt/detara/releases/<SHA>/` via `git archive`. Só então o wrapper chama o `scripts/production/deploy.sh` daquela release. Assim, o script executado como root corresponde ao commit aprovado, e não a um payload gravável pelo usuário de automação.

`deploy.sh` permanece a fonte de verdade: lock → Caddy candidato em container isolado/sem rede → backup externo obrigatório → pull por digest → parada controlada da API → migration → gateway independente → API/Web → proxy → HTTPS smoke → promoção de `current.env`/`previous.env`. Após sucesso, o wrapper promove atomicamente `/opt/detara/current` e `/opt/detara/previous`. Em falha, não há rollback automático, restore, `compose down`, remoção de volume, prune ou mudança dos pointers de checkout.

Secrets de aplicação, PFX e autenticação GHCR permanecem exclusivamente na VPS. O GitHub guarda apenas a chave SSH dedicada. O artefato transferido não contém segredo. O acesso GHCR root já configurado no host continua sendo usado; não copie `GITHUB_TOKEN` para a VPS.

## Bootstrap único no servidor

Faça estes passos em uma estação administrativa segura e na VPS por seu acesso humano atual. Eles **não** são executados automaticamente por esta task.

1. Gere uma chave exclusiva para Actions, sem reutilizar chave pessoal:

   ```bash
   ssh-keygen -t ed25519 -a 100 -f detara-actions-deploy -C detara-github-actions-production
   ```

   Guarde `detara-actions-deploy` somente no GitHub Environment. Transfira `detara-actions-deploy.pub` à VPS pelo canal administrativo existente.

2. Na VPS, use um checkout revisado da versão da OPS-01 e execute:

   ```bash
   sudo bash /caminho/do/checkout/scripts/production/automation/bootstrap-deploy-user.sh /caminho/detara-actions-deploy.pub
   ```

   O script exige root, valida uma única chave Ed25519, cria/reutiliza `detaradeploy`, recusa associação ao grupo `docker`, mantém home e `authorized_keys` root-owned, instala staging root-owned com grupo dedicado/sticky bit, chave com opção OpenSSH `restrict`, mirror Git root-owned, helpers root-owned e sudoers mínimo. Valida o sudoers com `visudo -cf`. Não altera `sshd_config`, usuário administrativo, firewall, Docker, secrets ou login GHCR. Reexecução atualiza explicitamente a chave autorizada e os binários versionados.

3. Confirme localmente na VPS:

   ```bash
   id detaradeploy
   sudo -u detaradeploy sudo -n -l
   sudo stat -c '%U:%G %a %n' \
     /usr/local/sbin/detara-deploy-release \
     /usr/local/bin/detara-stage-release \
     /usr/local/lib/detara-deploy-validation.sh \
     /etc/sudoers.d/detara-deploy-release \
     /var/lib/detara-deploy-trust/repository.git
   ```

   `detaradeploy` não pode aparecer no grupo `docker`. O sudo permitido deve ser somente `/usr/local/sbin/detara-deploy-release`. Preserve o login humano para recuperação manual.

4. Confirme que o Docker root da VPS já consegue ler os quatro packages privados pelo mecanismo `read:packages` dedicado existente. Não salve essa credencial na release nem no usuário SSH. Confirme também `git`, `docker buildx`, `flock`, `tar`, `sha256sum`, `bash`, `curl`, `age` e `rclone` instalados — as dependências normais do deploy continuam válidas.

## Pin confiável da host key

Não use `StrictHostKeyChecking=no` e não confie cegamente em `ssh-keyscan`. Pelo console do provedor ou sessão administrativa já autenticada, obtenha e confira a fingerprint diretamente na VPS:

```bash
sudo ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub
sudo cat /etc/ssh/ssh_host_ed25519_key.pub
```

Compare a fingerprint por um segundo canal confiável. Monte a linha known_hosts com o hostname/IP usado pelo Actions, o algoritmo e a chave pública exibida. Para porta 22: `host ssh-ed25519 BASE64`. Para outra porta: `[host]:porta ssh-ed25519 BASE64`. O comentário final da `.pub` não é necessário.

## GitHub Environment `production`

Em Settings → Environments → `production`, configure required reviewers, limite deployment branches a `main` e impeça bypass conforme a política da organização. O próprio workflow também recusa dispatch originado de outra ref. Adicione:

| Tipo | Nome | Conteúdo |
|---|---|---|
| Environment variable | `PROD_SSH_HOST` | DNS ou IPv4 da VPS, exatamente como no known_hosts. |
| Environment variable | `PROD_SSH_PORT` | Porta SSH, normalmente `22`. |
| Environment variable | `PROD_SSH_USER` | Obrigatoriamente `detaradeploy`. |
| Environment variable | `PROD_SSH_KNOWN_HOSTS` | Linha pinada obtida por canal confiável; é material público. |
| Environment secret | `PROD_SSH_PRIVATE_KEY` | Conteúdo completo da chave privada dedicada. |

Não adicione `production.env`, PFX, JWT, SQL, R2/S3, Resend, gateway ou credencial GHCR ao GitHub. O workflow usa apenas o `GITHUB_TOKEN` efêmero, com `actions:read`, `contents:read` e `packages:read`, para baixar/verificar artefatos no próprio repositório.

## Primeiro deploy automatizado

1. Faça merge da OPS-01 e aguarde `CI` e `Production images` verdes para um novo commit de `main`. Releases anteriores à OPS-01 não contêm os validadores e não são candidatas ao primeiro teste.
2. Copie o SHA completo de 40 caracteres do commit de `main`. Confira que o artefato `production-release-<SHA>` existe e que `DETARA_API_ORIGIN` usado no build corresponde ao host de produção.
3. Registre baseline: `current`, `previous`, containers, backup mais recente, espaço em disco e janela aprovada. Não execute se backup/restore ou incidente estiver pendente.
4. Actions → Deploy Production → Run workflow → informe o SHA completo. Um reviewer do Environment `production` aprova conscientemente a execução.
5. Acompanhe as fases. O workflow deve validar release/artefatos antes do primeiro SSH. No host, espere validação de main, digests, Caddy, backup, migration, gateway, API/Web, proxy, HTTPS smoke e promoção.
6. Confirme o Step Summary `SUCCESS`, SHA correto, `current.env`, symlink `/opt/detara/current`, health HTTPS, `RestartCount`, recursos e backup externo. Faça smoke autenticado manual sem colocar credenciais nos logs. WhatsApp/email somente com destinatário consentido e autorização específica.
7. Observe produção por 15–30 minutos. O workflow não faz merge, tag, deploy automático futuro ou limpeza de imagens.

## Falha e recuperação manual

Uma etapa crítica falha o job e o summary registra a última fase. Preserve logs sanitizados e avalie o estado real. Falha de backup impede migration. Falha de migration deixa a API parada conforme `deploy.sh`; não restaure banco nem improvise rollback automático. Falha posterior à migration pode ter alterado schema mesmo sem promoção de `current`; revisão de compatibilidade é obrigatória.

O caminho manual permanece independente do GitHub Actions:

```bash
sudo bash /opt/detara/current/scripts/production/deploy.sh --confirm-deploy /opt/detara/releases/candidate.env --dry-run
sudo bash /opt/detara/current/scripts/production/deploy.sh --confirm-deploy /opt/detara/releases/candidate.env
```

Prepare antes o checkout/manifesto aprovado como já descrito no runbook. Rollback de aplicação continua exclusivamente manual:

```bash
sudo bash /opt/detara/current/scripts/production/rollback.sh --confirm-schema-compatible
```

Ele não reverte schema, dados, volumes, secrets ou TLS. GitHub Actions indisponível não bloqueia deploy, diagnóstico ou recuperação pelo administrador.

## O que não é automatizado

Provisionamento inicial do SQL, criação/rotação de credenciais, alteração do VPS, rollback, restore do banco, schema down, limpeza de imagens/releases, prune, deploy por push, smoke autenticado e validações humanas de email/WhatsApp permanecem fora deste workflow. Nenhum deploy deve ser declarado concluído apenas pelo status do Actions sem conferir saúde e observação operacional proporcionais ao risco.
