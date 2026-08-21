# Runbook — backup, retenção e restore drill

## Objetivos iniciais

- RPO alvo: até 6 horas.
- RTO alvo: até 8 horas.
- Esses números são objetivos operacionais da beta, não SLA contratual.

## Política

1. Backup full com `CHECKSUM`, `COMPRESSION` e `COPY_ONLY` a cada 6 horas.
2. `RESTORE VERIFYONLY WITH CHECKSUM` imediatamente após cada backup.
3. Copiar o arquivo para destino externo criptografado, com credencial diferente da aplicação.
4. Retenção mínima: 48 horas dos intervalos de 6h, 14 diários e 8 semanais.
5. Um job de retenção externo remove apenas backups fora da política; o script da aplicação não realiza exclusão automática.
6. Alerta se backup ou cópia offsite não ocorrer no intervalo.

O volume `detara-sql-backups` é apenas staging local e não protege contra perda do VPS.

## Executar backup

No diretório da release, carregue o secret sem colocá-lo na linha de comando persistida:

```bash
read -s SQL_ADMIN_PASSWORD
export SQL_ADMIN_PASSWORD
docker compose --env-file .env.production -f compose.production.yml exec -T \
  -e SQLCMDPASSWORD="$SQL_ADMIN_PASSWORD" sqlserver /opt/detara/scripts/backup.sh
unset SQL_ADMIN_PASSWORD
```

Registre o arquivo retornado, SHA-256 após a cópia e destino offsite.

## Restore drill

Execute mensalmente e obrigatoriamente antes de abrir a beta. O script aceita apenas basename `.bak`, restaura em banco cujo nome começa com `DetaraRestoreDrill_`, roda `DBCC CHECKDB` e remove o banco temporário ao sair.

```bash
read -s SQL_ADMIN_PASSWORD
export SQL_ADMIN_PASSWORD
docker compose --env-file .env.production -f compose.production.yml exec -T \
  -e SQLCMDPASSWORD="$SQL_ADMIN_PASSWORD" sqlserver \
  /opt/detara/scripts/restore-drill.sh Detara_full_YYYYMMDDTHHMMSSZ.bak
unset SQL_ADMIN_PASSWORD
```

Evidência obrigatória: data UTC, nome/hash do backup, duração, resultado do `DBCC CHECKDB`, operador e observações. Falha no drill bloqueia go-live.

## Recuperação real

1. Declare incidente e impeça novas escritas.
2. Preserve banco e logs atuais; não sobrescreva o único volume.
3. Escolha backup pelo RPO, valide hash e `RESTORE VERIFYONLY`.
4. Restaure em banco novo e execute `DBCC CHECKDB`.
5. Valide schema/migrations e smoke tests com API isolada.
6. Troque a connection string somente após aprovação humana.
7. Mantenha o banco anterior até encerrar a investigação.
