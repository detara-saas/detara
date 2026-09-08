# Primeiro go-live — execução futura pelo operador

**Não executado pela Task 49.** Não começar sem CI verde, revisão aprovada, restore sintético comprovado e cópia recuperável dos secrets. Esta sequência é manual; parar na primeira falha.

1. Confirmar domínio sob controle, acesso administrativo com MFA aos provedores e cofre externo. Não comprar/registrar domínio por script.
2. Conferir host já preparado: Ubuntu 22.04 amd64, 2 vCPU/8 GB, disco livre, SSH por chave, root/password login desabilitados, UFW/firewall 22/80/443, Docker/Compose, logs local rotacionados, live-restore, swap e atualizações. Não refazer firewall remotamente sem janela/acesso de recuperação.
3. Definir domínios Web/API. Preservar landing no Cloudflare Pages e www redirect. Criar app/api A para o IP da VPS inicialmente DNS only; remover AAAA inválido.
4. Criar buckets R2 privados e separados para mídia e backup, credenciais mínimas distintas. Configurar lifecycle por prefixo e **excluir latest de expiração**, conforme backup runbook. Guardar credenciais fora da VPS.
5. Configurar domínio de envio Resend. Inserir SPF/DKIM exatamente fornecidos pelo painel e DMARC revisado; não inventar registros. Conferir domínio verificado antes do primeiro convite.
6. Gerar três senhas SQL e duas chaves JWT distintas, secret gateway, PFX/senha Data Protection e par age. Guardar todos no cofre; a identidade privada age não precisa permanecer na VPS. Nunca usar senha de usuário final definida pelo operador.
7. Configurar variável pública DETARA_API_ORIGIN no GitHub se necessário; conferir CI verde e workflow Production images. Confirmar que packages GHCR são privados. Baixar manifesto/bundle/SHA256SUMS e public-api-origin.txt da release por SHA; verificar hashes e domínio do build.
8. Preparar credencial GHCR read:packages dedicada no host, sem root SSH ou chave pessoal em pipeline. Revisar política do futuro usuário de deploy: acesso docker é equivalente a root. Nesta V1 deploy é via operador autorizado/sudo, não SSH de Actions.
9. Instalar checkout **exato** aprovado em /opt/detara/releases/<SHA>, sem .env local, backups ou src/Detara.Api/data. Apontar /opt/detara/current para ele. Instalar bash/curl/openssl/gzip/age/rclone/util-linux e validar Docker Compose.
10. Criar manualmente /etc/detara/production.env root:root 0600 a partir do exemplo, preencher sem interpolação/aspas. Instalar PFX root:1654 0640. Instalar candidate.env root:root 0600. Nunca sobrescrever secrets existentes. Conferir paths, UID e redes privadas sem conflito.
11. Rodar validate-env.sh e deploy --dry-run. Conferir digests, hosts, disponibilidade de disco e ferramentas; dry run não prova TLS/backup.
12. Rodar init-sql.sh --confirm-initialization com DETARA_RELEASE_FILE=candidate.env. Confirmar Express, banco vazio, runtime restrito, migrator db_owner apenas de Detara e diretórios/volumes com ownership correto. Não inserir tenants via SQL.
13. Executar deploy.sh --confirm-deploy candidate.env em janela. Backup externo do banco vazio deve passar **antes** de migrations. Bundle falhou: abortar/investigar; nunca Down automático. Confirmar API/Web/Caddy saudáveis.
14. Validar TLS público ACME na origem e HTTP→HTTPS para ambos os hosts. Depois revisar CIDRs oficiais Cloudflare, configurar trust no Caddy e habilitar proxy + Full (strict). Validar preservação de IP/rate limit, emissão/renovação ACME, cache bypass da API/health/service worker e CSP/CORS. Nenhuma porta SQL/gateway/API pública.
15. Somente após HTTPS confirmado, habilitar HSTS (31536000, sem preload). Atualizar/validar/recriar Caddy em janela; não editar configuração ativa sem backup do arquivo aprovado.
16. Rodar smoke.sh (TLS validado, sem -k). Confirmar Web, live, ready, ausência de Swagger público, erro seguro de origem/host não permitidos e API BaseUrl HTTPS correta no artefato Web.
17. Executar backup-sql.sh, confirmar upload/checksum remoto/last-success, recuperar arquivo cifrado e executar restore-test.sh --confirm-disposable em máquina com capacidade. Registrar CHECKDB, contagens/migrations e capacidade real de recuperar a chave age. Falha bloqueia primeiro tenant.
18. Instalar units systemd de backup e habilitar timer. Configurar monitor externo para landing/app/live/ready/SSL e rotina de alerta de backup atrasado/falha; conferir recursos e logs. Logs sem operador/alerta não equivalem a monitoramento.
19. Executar bootstrap seguro do primeiro Platform Admin pelo console existente, sem senha em argv, HTTP seed ou DemoBootstrap. Completar MFA e guardar recovery codes de forma segura. Fazer novo backup para incluir key ring persistente/PFX recuperável.
20. Provisionar primeira empresa pelo Platform Admin e convite/onboarding existentes. Ver [primeiro tenant](first-tenant.md). Não copiar Prime Detail nem criar senha de cliente no Platform.
21. Com consentimento específico, validar email de convite e operação mínima: login, agenda, orçamento, OS, recebimento/despesa, relatórios e upload/download isolado. WhatsApp pode continuar Nenhum/Email; se desejado, conectar QR e enviar somente para número autorizado.
22. Observar 15–30 minutos, registrar CPU/RAM/disco/tamanho SQL, validar current/previous e acesso aos backups/cofre. Planejar primeiro restore semanal e teste de reboot controlado futuro. Só então declarar go-live.

Não executar esta lista automaticamente. Riscos aceitos/limites estão em production.md; ausência de evidência de backup externo/restore ou High/Critical conhecido é **no-go**.
