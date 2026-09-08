# Runbook — deploy/rollback V1

Referência: [produção](../production.md). Operação manual na VPS somente com autorização posterior. Shell Linux; nunca dotnet run, DemoBootstrap, down -v ou migration no startup.

## Release e registry

CI de PR/main mantém .NET, auditorias e builds. Após CI verde de push em main, `Production images` publica quatro imagens linux/amd64 no GHCR (api, web, gateway, migrations), tags por SHA e manifesto com **digests**. Anexa bundle e SHA256SUMS. PR não publica; workflow não conhece SSH nem runtime secrets. Packages GHCR devem permanecer privados; conferir visibilidade inicial manualmente.

Configurar variável pública GitHub `DETARA_API_ORIGIN` antes do primeiro build de release se domínio divergir do padrão. Conferir `public-api-origin.txt` contra ambiente. Alterar domínio requer novo build Web, não edição de asset publicado. Arquivar manifesto/checksums fora do runner e preservar imagens current/previous; artefatos Actions expiram após 90 dias.

GitHub usa GITHUB_TOKEN packages:write apenas no job de publicação. Host usa credencial read:packages dedicada via docker login --password-stdin (sem token na linha). Runtime secrets ficam no host/cofre, não no GitHub.

## Usuário e diretórios

Estrutura recomendada:
- /opt/detara/releases/<SHA>/ : checkout exato aprovado;
- /opt/detara/current : symlink da infraestrutura aprovada;
- /opt/detara/releases/current.env, previous.env : manifestos root:root 0600;
- /etc/detara/production.env : root:root 0600;
- /etc/detara/data-protection.pfx : root:1654 0640;
- staging/sessões conforme production.md.

Criar futuramente detaradeploy somente por operador. **Grupo docker equivale a root**; não chamá-lo de usuário limitado. Nesta V1 não há SSH automático: operador autorizado executa scripts via sudo, revisando o checkout antes. Se automatizar SSH depois, usar chave dedicada, host key pinada, environment approval e wrapper root-owned que valide releases; não conceder sudo irrestrito nem acesso de escrita ao script executado como root. Não reutilizar chave pessoal.

## Primeiro provisionamento (somente depois do checklist)

1. Obter checkout e manifesto de release da CI. Conferir checksums e commit; instalar candidate.env root 0600. Nunca copiar exemplo por cima de production.env.
2. Configurar Docker login privado, secrets, PFX e diretórios. Instalar no host bash, curl, jq, openssl, gzip, age, rclone e util-linux (flock).
3. Executar:
   `sudo env DETARA_RELEASE_FILE=/opt/detara/releases/candidate.env bash /opt/detara/current/scripts/production/init-sql.sh --confirm-initialization`
4. Init valida config, prepara ownership de staging/sessões/Caddy, inicia SQL e cria banco vazio/logins. Não aplica migrations nem cria tenant. Senhas existentes não são alteradas.
5. Executar deploy controlado abaixo. Mesmo banco vazio recebe backup antes da primeira migration.
6. Validar backup externo/restore antes de Platform Admin e primeiro tenant.

## Deploy controlado

```bash
sudo bash /opt/detara/current/scripts/production/deploy.sh --confirm-deploy /opt/detara/releases/candidate.env --dry-run
sudo bash /opt/detara/current/scripts/production/deploy.sh --confirm-deploy /opt/detara/releases/candidate.env
```

Dry run só valida env, manifestos e Compose/Docker; não simula saúde externa, backup ou migration.

Fluxo real: lock exclusivo → validar Caddy → backup externo obrigatório → pull digests → parar API → bundle com detara_migrator → iniciar gateway sem torná-lo dependência → API/Web saudáveis → proxy → smoke HTTPS → current/previous. Sem dois deploys simultâneos. SQL não é recriado no deploy normal; atualizações da imagem SQL exigem janela/revisão/backup próprios.

Em cada release, o operador prepara o checkout aprovado e confere que /opt/detara/current aponta para a infraestrutura compatível com o SHA candidato, preservando o checkout anterior. O proxy é recriado explicitamente para aplicar o Caddyfile montado; apenas editar um bind mount não recarrega a configuração. Esta janela pode interromper conexões brevemente.

Se backup/upload falhar, migration não roda. Se migration falhar, API permanece parada até investigação; não há Down automático. Se health/smoke falhar após migration, manifesto current permanece anterior e o operador precisa avaliar estado real dos containers antes de rollback. Não presumir que arquivo current prova que release parcialmente iniciada não existe.

Na primeira implantação, não há release anterior para rollback. Validar bundle em banco vazio e incremental antes. Mudança de Caddy não é revertida pelo rollback de aplicação: recuperar Caddyfile do checkout aprovado, validar e recriar somente proxy.

## Rollback

`sudo bash /opt/detara/current/scripts/production/rollback.sh --confirm-schema-compatible`

Exige análise humana de compatibilidade do schema com previous.env. Volta API/Web/gateway por digest, valida saúde e troca manifestos. **Não reverte banco**, volumes, secrets, TLS ou dados. Se migration incompatível, declarar incidente e recuperar backup em banco novo com aprovação, nunca sobrescrever o único banco.

Migrations futuras: expand/contract (coluna nullable → código compatível → remoção em release posterior), sem destruição junto da primeira versão dependente. SQL migrator db_owner não deve ser usado na API. Ferramentas EF agora exigem ConnectionStrings__DefaultConnection no ambiente; não passar --connection com senha, nem dotnet user-secrets list em logs.

Sem limpeza automática de imagens. Inventariar manualmente e remover apenas IDs comprovadamente fora de current/previous e fora de outros serviços. Nunca docker system prune -a ou remoção de volumes como parte do deploy.

## Smoke

Script verifica Web, live e ready por HTTPS válido (sem -k). Operador confirma login tenant, Platform MFA, convite, orçamento/OS/financeiro/relatórios e mídia autorizada. Email/WhatsApp de teste só com autorização específica e destino consentido; não há senha real em scripts. Observar logs/recursos por 15 minutos.
