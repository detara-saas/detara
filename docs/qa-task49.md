# Task 49 — relatório de implementação e QA

Data: 08/09/2026. Branch: `feature/production-readiness-v1`, base `main` `c6b5042` (Task 48). Commit/PR e estado final da CI são informados na entrega; não há merge automático.

## Entrega

Infraestrutura existente consolidada, sem nova feature comercial, banco/migration de domínio, contrato, permissão ou regra de negócio. `src/Detara.Api/data/` foi preservado e permanece fora do Git/imagens. Não houve SSH, alteração da VPS, DNS, Cloudflare/R2 real, envio de email/WhatsApp ou criação de tenant.

Topologia: Cloudflare → Caddy → Web/API; API → SQL Express privado e gateway opcional privado. Landing permanece Pages. Cinco serviços permanentes, imagem adicional de migration one-shot. Somente Caddy publica 80/443. Redes edge, data internal, integration internal e saída dedicada do gateway. Volumes SQL/DP/Caddy persistentes; sessões e staging em paths de host documentados.

Tetos: Caddy 0,5 CPU/256 MiB; Web 0,5/256 MiB; API 1,5/1536 MiB; SQL 1,5/2560 MiB (SQL memory limit 2048 MB); gateway 1/1536 MiB. Aproximadamente 2 GiB reservados ao host/overhead; não são garantia de capacidade. Migration 1 CPU/768 MiB, tmpfs 256 MiB, não-root, filesystem read-only.

Imagens multi-stage por SHA/digest: API ASP.NET 10.0.10 (UID 1654), Web nginx-unprivileged 1.30.4 (UID 101), gateway Node 24 (UID 1000), migration self-contained EF 10.0.10 (UID 1654). Build SDK 10.0 pinado por digest (10.0.400). SQL 2022 CU26 Ubuntu 22.04, **Express** (UID 10001), Caddy 2.11.4 (UID 1000). Digests completos nos Dockerfiles/Compose. Não há SDK/DemoBootstrap na imagem API final.

Caddy: hosts app/api configuráveis; ACME e Cloudflare Full strict com cutover DNS only → proxy documentado. HSTS inicialmente 0 e ativado somente após HTTPS comprovado. Trust exclusivamente em CIDRs revisados, IP normalizado e API confiando apenas no Caddy. NET_BIND_SERVICE é a única capability do proxy (exigida pelo file capability da imagem oficial); CHOWN apenas na inicialização descartável dos volumes. Demais serviços de aplicação sem capabilities, read-only e no-new-privileges.

Secrets: arquivo root:root 0600 externo; SQL admin/runtime/migrator distintos, JWT tenant/Platform distintos, gateway, Resend, credenciais separadas de mídia/backup, PFX/senha e age. PFX root:1654 0640; segredo montado no nome efetivamente esperado pela API. Nenhum valor secreto versionado. Runtime SQL sem sa/db_owner. Parser não executa shell, rejeita interpolação/aspas/permissão insegura; manifesto aceita somente SHA e quatro imagens.

Deploy: CI verde em main → GHCR por SHA/digest + manifesto/bundle/checksums → operador revisa/env/dry run → backup externo validado → maintenance → migration separada → health/smoke → current/previous. Falha de backup impede migration; falha de migration mantém API parada. Rollback somente de aplicação, com confirmação de compatibilidade de schema; sem Down, prune ou remoção de volumes. Reaplicar manifesto idêntico preserva previous.

Backup diário + pré-migration: nativo COPY_ONLY/NO_COMPRESSION/CHECKSUM/VERIFYONLY, gzip + age, R2 privado via rclone e SHA-256 remoto. Staging restrito; SQL plaintext desta execução só sai após confirmação externa. Key ring cifrado também é salvo. Retenção manual R2 7 dias/28 dias/186 dias por prefixo e latest sem expiração. Restore isolado, confirmação explícita, banco novo, CHECKDB, cleanup restrito. RPO alvo 24h/RTO alvo 8h, não SLA.

Monitoramento preparado por runbook: live independente de SQL, ready exige SQL, gateway opcional; monitor externo de uptime/TLS, CPU/RAM/swap/disco/tamanho SQL, atraso de backup >26h, logs local 10 MiB × 5 e falhas de integração. Configuração externa/alertas reais permanecem manuais.

## Evidências executadas

| Verificação | Resultado |
|---|---|
| dotnet restore | Aprovado |
| dotnet build --configuration Release | Aprovado, 0 warnings/0 erros |
| dotnet test --configuration Release | 170 unitários + 558 integração = **728 aprovados**, sem ignorados |
| dotnet format --verify-no-changes | Aprovado |
| EF has-pending-model-changes | Nenhuma alteração pendente; nenhuma migration nova |
| NuGet --vulnerable --include-transitive | Nenhuma vulnerabilidade conhecida reportada |
| Gateway npm test / npm audit --omit=dev | **12 aprovados**, zero vulnerabilidades reportadas |
| qs | 6.15.3 → 6.16.0, sem npm audit fix --force; lock opcional bare-events 2.9.1 → 2.9.2 regenerado no Linux |
| Docker API/Web/gateway/migrations | Quatro builds Production aprovados |
| Compose config + asserts | Portas, redes, Express, gateway opcional, secret PFX e capability Caddy aprovados |
| Caddy validate | Configuração válida, execução não-root validada |
| Bash syntax + ShellCheck warning | Aprovados nos scripts operacionais |
| Safety fixtures Linux sem rede/socket | Parser/permissões, manifesto, age, checksum, falha de upload e deploy sem backup aprovados; providers simulados |
| SQL Express real descartável | Migration inicial/incremental/rerun, usuário migrator separado, runtime não-owner, replacement/persistência e restore de dados sintéticos aprovados |
| Restore de .age em outro SQL sem rede | Decifragem + restore real + CHECKDB aprovados |
| Stack completo / HTTP / PWA | API Production + Web via TLS, CORS/Host, gateway offline, SQL offline/recuperação e replacement aprovados; sessão sintética preservada e **95 assets publicados íntegros** |
| git diff --check | Aprovado |

Testes de segurança novos cobrem CORS permitido/arbitrário, SQL offline com resposta segura, secrets inválidos bloqueando startup, HSTS delegado e headers encaminhados de peer confiável versus forjado. Total .NET anterior 719 → 728.

O QA de infraestrutura usa `scripts/production/tests/Test-Production.ps1` no Windows/Docker Desktop com projeto aleatório, SQL/volumes/credenciais sintéticos, TLS local e redes sem egress. Não usa portas 5080/5090, banco local de desenvolvimento ou sessão WhatsApp existente. `-k` é exclusivo do certificado local do teste; smoke de produção exige TLS válido. A fixture Linux de upload NÃO comprova credenciais/R2 reais.

Reprodução local (na raiz do repo; Docker Linux e Node disponíveis):

```powershell
docker build -f src/Detara.Api/Dockerfile -t detara-api:task49 .
docker build -f src/Detara.Web/Dockerfile --build-arg DETARA_API_ORIGIN=https://api.detara.test -t detara-web:task49 .
docker build -f whatsapp-gateway/Dockerfile -t detara-whatsapp-gateway:task49 .
docker build -f deploy/Dockerfile.migrations -t detara-migrations:task49 .
docker build -f scripts/production/tests/Dockerfile.qa -t detara-production-tools:task49 .
./scripts/production/tests/Test-Production.ps1 -ConfirmDisposable
```

As imagens de QA ficam no Docker local para repetição; containers/volumes das execuções completas são removidos por escopo explícito. Uma fixture de tentativa inicial interrompida permaneceu no TEMP local (somente configuração/certificado sintéticos, fora do Git); a limpeza adicional foi bloqueada pelo ambiente. Não executar o QA no host de produção. Avisos informativos de runtime incluem nginx read-only, Caddy HTTP/2/3 no listener de redirect e X-Forwarded-Proto explícito redundante, além da ausência de porta HTTPS na sonda interna da API. TLS público é responsabilidade do Caddy; não foram mascarados erros de build ou auditoria.

## Correções descobertas no ensaio

- SQL Express não suporta compressão nativa de backup: NO_COMPRESSION + gzip externo.
- Nome do secret PFX alinhado ao caminho usado pela API.
- Caddy oficial com file capability exige NET_BIND_SERVICE para executar mesmo em portas altas; volume recebe ownership em init restrito.
- Health Web usa 127.0.0.1, evitando falso negativo de localhost/IPv6 no nginx IPv4.
- Bundle usa extração em /tmp, permitindo execução read-only sem gravar no home.
- SQL totalmente sem rede no restore usa tcp:127.0.0.1,1433 e timeout explícito.
- Configuração pública Web é aplicada antes do publish; Development removido antes da geração do manifesto PWA, não depois.

## Limites e próximos passos manuais

Não foi executado deploy/rollback contra VPS, ACME público, Cloudflare real, upload R2 real, recovery de MFA real, reboot do host, envio real ou smoke autenticado com cliente. Não há benchmark/SLA, HA ou garantia de QR após desastre. Gateway atual usa Chromium sem sandbox: risco contido e documentado, não eliminado. SQL TrustServerCertificate é restrito à rede internal do host único. Auditorias NuGet/npm não substituem revisão contínua das imagens/OS.

A execução pública de release só acontecerá após merge humano e CI verde em main. Confirmar visibilidade privada do GHCR e arquivar manifestos fora dos artefatos temporários de Actions. Validar backup externo/restore/secrets/monitoramento antes do primeiro tenant.

Sequência exata do operador, **não executada nesta task**: [22 passos de primeiro go-live](production-go-live.md). Detalhes: [produção](production.md), [deploy](runbooks/deploy.md), [backup](runbooks/backup-restore.md), [recuperação](disaster-recovery.md), [operação](operations.md), [primeiro tenant](first-tenant.md).
