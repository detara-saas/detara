# Runbook — backup, criptografia e recuperação

## Política V1

RPO alvo até 24h; RTO alvo até 8h para VPS perdida, sem SLA. Timer systemd diário 06:00 UTC (03:00 Brasília), configurável via override; backup adicional obrigatório antes de qualquer migration. SQL Express usa BACKUP DATABASE COPY_ONLY, NO_COMPRESSION e CHECKSUM; VERIFYONLY verifica integridade estrutural, mas **não substitui restore**.

`backup-sql.sh` comprime com gzip, cifra com **age** usando chave pública e envia somente .age ao R2. Senha/chave privada age permanecem em cofre externo, não na VPS nem junto do backup. R2 adiciona criptografia de serviço, bucket privado e acesso restrito. Sem criptografia inventada. Testar acesso à identidade de recuperação por dois operadores autorizados.

Credenciais de R2 backups são distintas da mídia, limitadas ao bucket de backups. Rclone é ferramenta de host; configuração via ambiente, sem arquivo de token no Git. Upload é verificado lendo bytes remotos e comparando SHA-256 do cifrado. Só depois registra /var/backups/detara/sql/last-success. Falha retorna exit não-zero e não autoriza migration.

## Retenção R2 (configuração manual obrigatória)

Criar lifecycle por prefixo, sem bucket público:
- daily/: 7 dias;
- weekly/: 28 dias, cópia aos domingos;
- monthly/: 186 dias, cópia no dia 1;
- keyring/: 186 dias;
- **latest/: sem expiração**, nunca apagar o último backup validado.

São janelas aproximadas de 7 diários/4 semanais/6 mensais; deploys extras podem aumentar a quantidade. latest/database.bak.gz.age é promovido somente após upload diário e key ring verificados. latest/keyring.tar.gz.age protege o key ring cumulativo de MFA; nunca apagar chaves antigas. Se backups falharem por longo período, latest não expira. Não aplicar lifecycle genérico que alcance latest. Configurar regras antes de go-live e conferir mensalmente.

Staging local /var/backups/detara/sql não é backup externo. Plaintext desta execução é removido só após confirmação externa; em falha é mantido com acesso restrito para investigação. Cifrados locais têm retenção de aproximadamente dois dias. Não apagar último backup válido para liberar disco. Script não faz prune remoto; lifecycle pertence ao operador.

## Executar e agendar

```bash
sudo env DETARA_RELEASE_FILE=/opt/detara/releases/current.env bash /opt/detara/current/scripts/production/backup-sql.sh
sudo install -m 644 /opt/detara/current/scripts/production/systemd/detara-backup.* /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now detara-backup.timer
sudo systemctl list-timers detara-backup.timer
sudo journalctl -u detara-backup.service --since yesterday
```

Não executar estes comandos na VPS nesta task. Em produção, alertar se last-success tiver >26h ou unit falhar. Logs locais não são notificação proativa: configurar monitor externo/dead-man's-switch ou revisão diária pelo operador antes da beta. Não criar scheduler na API.

## Restore test real

Baixar cópia cifrada com rclone usando acesso read-only temporário ao bucket, verificar checksum de transporte e disponibilizar identidade age por sessão protegida. Não baixar backup real para Codex.

`sudo bash scripts/production/restore-test.sh --confirm-disposable /caminho/backup.bak.gz.age /caminho-seguro/identity.agekey`

O script decifra localmente em mktemp restrito, inicia SQL Express **sem rede/portas**, restaura em banco DetaraRestoreDrill_*, roda DBCC CHECKDB e remove somente container/arquivos que criou, protegidos por nome aleatório/label. Nunca recebe host SQL de produção. Verificar também contagens/amostras autorizadas, schema e login em ambiente isolado antes de confiar numa recuperação real. O helper interno restore-drill.sh rejeita target existente antes de instalar cleanup; backups com mais de dois arquivos lógicos exigem procedimento revisado, sem suposição silenciosa.

Realizar semanalmente e antes de go-live; registrar data UTC, hash, duração, CHECKDB, contagens e operador. Requer capacidade para SQL descartável; preferir máquina separada da pequena VPS. Não prometer drill seguro em host sem memória/disco suficiente.

## Além do SQL

- **Data Protection key ring**: exportado cifrado no backup, restaurar com o PFX original/senha do cofre e ApplicationName estável. Volume ausente é permitido somente no banco vazio ainda sem tabela de migrations; depois disso o script falha fechado. Perda do key ring bloqueia recuperação de MFA e deve ser tratada como incidente.
- PFX, senha SQL/JWT/Resend/gateway/R2, chave privada age: cópias em cofre externo, nunca plaintext no bucket.
- Mídia: R2 privado próprio, não incluída no .bak; política de preservação/versionamento/cópia e teste de download autorizados independentes. Sem mídia no disco efêmero.
- WhatsApp: persistência local por volume/bind. Sem cópia de credenciais de sessão offsite nesta V1; recuperar via QR consentido após desastre.
- Caddy: volumes preservados no deploy; se perder VPS, certificados públicos podem ser reemitidos, respeitando limites ACME.

## Recuperação real

Declarar incidente → bloquear escritas → preservar volume original → recuperar backup em SQL/banco **novo** → CHECKDB + amostras + migrations → restaurar key ring/PFX/secrets/mídia → subir release compatível → validar acesso → trocar destino com autorização → manter original para investigação. Nunca executar Down ou sobrescrever banco automaticamente. Ver [disaster recovery](../disaster-recovery.md).
