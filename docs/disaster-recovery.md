# Recuperação de desastre V1

Objetivos iniciais, não SLA: RPO até 24h e RTO até 8h. Reduzir intervalo de backup para 12h/6h quando volume de escritas e perda aceitável justificarem; reavaliar retenção/custo e testar restore antes.

## Cenários

1. **API falhou:** conferir logs seguros/config/recursos, SQL ready e espaço; restart somente da API da release atual. Não apagar volumes. Se release causou falha, avaliar rollback compatível.
2. **Release ruim:** conferir schema aplicado, current/previous e estado real dos containers. Voltar imagens com rollback.sh apenas após confirmar compatibilidade. Caddy/env exigem recuperação separada do arquivo aprovado. Nenhuma migration Down automática.
3. **SQL corrompido:** bloquear escritas e preservar original; restaurar backup verificado em novo servidor/banco, CHECKDB e validação por operador. Nunca restaurar por cima da única cópia. Usar release compatível com schema recuperado.
4. **VPS perdida:** seguir reconstrução abaixo, sem depender de arquivo existente apenas no host perdido.
5. **WhatsApp perdido:** API continua operando; nova sessão por QR consentido. Não há promessa de recuperar LocalAuth do backup SQL.
6. **R2 indisponível:** backup/deploy falham fechados, preservar staging e investigar; não migrar sem backup externo. Mídia pode ficar indisponível, sem transformar bucket em público. Validar credenciais/região/rede, não regenerar secrets cegamente.
7. **DNS/TLS incorreto:** validar registros, AAAA, Cloudflare Full strict, ACME e CIDRs. Recuperar config conhecida, sem desativar validação TLS para usuários. Nunca Flexible para mascarar erro.

## VPS nova

1. Autorizar incidente e selecionar backup/SHA pelo último sucesso, preservando evidências.
2. Provisionar Ubuntu amd64 e hardening equivalente ao checklist (SSH por chave, firewall, Docker, logs, swap, NTP); não há automação remota nesta entrega.
3. Recuperar checkout exato, quatro imagens por digest, manifestos/checksums e configuração pública. Criar diretórios/volumes com ownership documentado.
4. Recuperar do cofre senhas SQL, JWTs, Resend/gateway/R2, PFX/senha e identidade privada age. Não expor em argv/logs.
5. Obter SQL .age, verificar transporte, decifrar em ambiente restrito e restaurar em **SQL novo**. Rodar CHECKDB e amostras comerciais/migrations; não aplicar migrations novas automaticamente durante recuperação.
6. Recriar login runtime no servidor novo e remapear usuário restaurado com `ALTER USER detara_runtime WITH LOGIN=detara_runtime`; restaurar migrator apenas para operações controladas. Senhas/SIDs de login ficam no master e não são recuperados apenas pelo .bak de Detara.
7. Restaurar key ring cifrado em volume da API (UID 1654), montar PFX original e ApplicationName Detara.Platform. Certificar recuperação de MFA. Identidade age temporária volta ao cofre; remover cópias temporárias protegidas após validação.
8. Confirmar bucket de mídia original/restaurado, acesso privado e arquivos referenciados no SQL. Backup SQL não contém binários S3.
9. Subir API/Web da release compatível e Caddy; reemitir certificados públicos se necessário. Testar origem antes de DNS cutover, depois proxy Full strict/CIDRs/HSTS.
10. Configurar gateway vazio com diretório persistente e secret; reautenticar empresas por QR quando autorizadas. Email pode operar independentemente.
11. Validar login/MFA, isolamento tenant, agenda/OS/financeiro/relatórios, mídia, health, backup e monitoramento. Declarar restauração somente após aceite humano; documentar perdas dentro do RPO.

Sem secrets/PFX + key ring + identidade age externa, recuperação pode ser impossível mesmo com .bak íntegro. Realizar drill periódico e revisar acesso ao cofre, não apenas existência dos arquivos.
