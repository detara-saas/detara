# Runbook — deploy e rollback

## Pré-requisitos

- VPS Linux atualizado, Docker Engine/Compose e firewall liberando apenas SSH controlado, 80 e 443.
- DNS do host público apontando para o VPS.
- `.env.production` criado a partir do exemplo, permissão `0600`.
- PFX de Data Protection em `secrets/detara-data-protection.pfx`, permissão `0600`, e cópia segura externa.
- Bucket S3-compatible privado, sem public access, credencial limitada ao bucket/prefixo.
- imagens API/Web/WhatsApp Gateway construídas pelo commit aprovado e publicadas com tag imutável ou digest.
- login `detara_runtime` provisionado no SQL com somente permissões de runtime; migrations usam credencial separada e temporária.

## Primeiro provisionamento

1. Copie somente repositório/release, Compose, Caddyfile e secrets para o VPS.
2. Execute `scripts/production/validate-compose.sh .env.production`.
3. Suba somente SQL: `docker compose --env-file .env.production -f compose.production.yml up -d sqlserver`.
4. Crie login/user runtime com senha do secret. Não use `sa` na API.
5. Gere o bundle com `scripts/production/build-migration-bundle.sh` no CI e confira o SHA-256.
6. Faça backup pré-migração, mesmo no primeiro ambiente se já houver dados.
7. Execute o migration bundle com a credencial de migração. O bundle é uma etapa explícita e deve terminar antes da API.
8. Suba `whatsapp-gateway`, `api`, `web` e `reverse-proxy`.
9. Valide a saúde autenticada do gateway na rede interna, `https://HOST/health/live`, `https://HOST/health/ready`, login tenant, login Platform com MFA e envio de convite.

## Deploy recorrente

1. Confirme CI verde, revisão aprovada, imagem imutável e notas de migration.
2. Registre versões atuais: `docker compose ... images`.
3. Execute backup e guarde o nome/horário.
4. Baixe imagens: `docker compose ... pull whatsapp-gateway api web`.
5. Se houver migration, aplique o bundle antes da nova API. Migrations destrutivas exigem procedimento específico e aprovação humana.
6. Recrie gateway/API/Web: `docker compose ... up -d --no-deps whatsapp-gateway api web`.
7. Recrie o proxy somente se sua configuração/imagem mudou.
8. Observe logs, live e ready por pelo menos 15 minutos; execute smoke tests.

## Rollback

- Sem migration incompatível: restaure os digests anteriores no `.env.production`, valide Compose e execute `up -d --no-deps api web`.
- Com migration compatível para trás: volte imagens e mantenha schema.
- Com migration incompatível/destrutiva: interrompa escrita, documente incidente e restaure o backup em novo banco. Não improvise down migration em produção.
- Nunca apague volumes para “corrigir” um deploy.
- O volume `detara-whatsapp-sessions` contém credenciais de sessão por tenant. Inclua-o em backup criptografado e restrito; restaurá-lo em outro host exige os mesmos cuidados de um secret operacional.

## Validação pós-deploy

- `live` e `ready` retornam apenas `healthy`.
- redirecionamento HTTP→HTTPS e certificado válido;
- nenhum container além do Caddy publica porta;
- logs carregam correlation ID sem secrets;
- fluxo principal de orçamento/OS e onboarding abre;
- upload/download autorizado de mídia funciona;
- fila de e-mail não apresenta acúmulo inesperado.
- gateway WhatsApp volta a `Connected` para uma empresa de teste após restart, sem novo QR.
