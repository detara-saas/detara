# Operação V1

VPS única não oferece alta disponibilidade. Monitor externo deve checar landing, app, /health/live e /health/ready a cada 1–5 minutos, além de expiração TLS/domínio. Configurar manualmente UptimeRobot/equivalente e contato de alerta, sem API nesta task.

## Rotina diária

```bash
free -h
df -h
uptime
docker stats --no-stream
sudo systemctl status detara-backup.timer detara-backup.service
sudo journalctl -u detara-backup.service --since yesterday
sudo cat /var/backups/detara/sql/last-success
timedatectl status
test ! -f /var/run/reboot-required || echo 'Reboot controlado pendente'
```

Para Compose, exportar imagens do current.env pelo loader seguro de common.sh (sessão root autorizada: source common.sh; export DETARA_RELEASE_FILE=/opt/detara/releases/current.env; load_config; dc ps). **Não executar docker compose config sem --quiet** em terminal/log compartilhado: configuração expandida contém secrets. Logs de incidentes podem ser vistos com dc logs --tail=100 api; não colar logs/dados de clientes em chats.

RAM <70% sustentada é baseline inicial; 70–80% acompanhar, >80% ou swap sustentada investigar. CPU alta sustentada com degradação/load elevado: identificar SQL/Chromium antes de ampliar VPS. Picos isolados não justificam upgrade. Disco 70% atenção, 80% investigar, 90% crítico. Registrar tamanho de banco e backup semanalmente; planejar KVM 4/SQL maior quando carga real justificar. Não deixar retenção/Chromium consumir todo disco.

SQL, em sessão administrativa local autorizada, sem password em argv:

```sql
SELECT DB_NAME(database_id) AS Banco,
       SUM(CASE WHEN type=0 THEN size ELSE 0 END)*8.0/1024 AS DadosMB,
       SUM(CASE WHEN type=1 THEN size ELSE 0 END)*8.0/1024 AS LogMB
FROM sys.master_files GROUP BY database_id;
```

Express 2022 limita dados a 10 GB por banco, não confundir tamanho de log com esse teto. Migrar antes do limite, testando restore e licença/serviço de destino.

Docker driver local limita 10m × 5 arquivos por serviço. API usa JSON Information/Warning e correlation ID, sem body/query/JWT. Caddy não habilita access log detalhado nesta V1. Logs locais podem se perder junto da VPS; durante beta exportar somente evidências necessárias e redigidas. Monitorar 5xx/429, readiness, atraso de backup >26h, falhas de email e desconexão gateway. Healthcheck Docker sozinho não reinicia processo vivo unhealthy nem envia alerta: monitor externo/operador é necessário.

## Manutenção

- Reboot: confirmar backup externo/restore recente → janela/aviso → registrar estado → reboot autorizado → Docker/restart policies → live/ready/smoke → sessões WhatsApp. Host já possui unattended-upgrades; reboot permanece controlado.
- Atualização de imagens: PR/review/CI/digest/deploy. Sem Watchtower, prune global ou down -v. Preservar current/previous e volumes.
- JWT tenant/Platform: alterar chaves distintas no cofre/env e recriar API; sessões existentes serão invalidadas. Não há key ring JWT multi-chave novo.
- Resend: emitir chave substituta, atualizar host, testar consentidamente, revogar anterior. Gateway: trocar secret coordenadamente na API/gateway, sem apagar sessões. R2: credencial paralela mínima, validar upload/download e só então revogar antiga.
- PFX Data Protection: não substituir/remover o único certificado de decrypt! Planejar migração e preservação dos certificados antigos/key ring com ensaio antes de rotacionar. Perda impede recuperar MFA.
- Clock UTC/NTP do Ubuntu preservado; aplicação usa fuso por empresa. Não instalar daemon de hora paralelo.

## Administração SQL sem porta pública

Preferir sqlcmd via docker exec no host com SQLCMDPASSWORD vindo de ambiente protegido. Para ferramenta remota: operador obtém IP **interno atual** do container e cria túnel SSH `ssh -L 11433:<IP_INTERNO_SQL>:1433 <OPERADOR>@<PRODUCTION_HOST>`; conectar ferramenta em localhost:11433. IP é efêmero, nunca versionado nem usado na connection string da API. Fechar túnel após uso; não abrir 1433 no firewall.

Homologação futura pode reutilizar Compose com projeto/subnet/domínios/buckets/secrets separados. Scripts V1 intencionalmente fixam projeto/caminhos de produção; revisar parametrização antes de usar em outro ambiente, não misturar volumes.
