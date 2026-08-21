# Runbook — resposta a incidentes

## Severidade

- **SEV-1:** vazamento/risco entre tenants, credencial comprometida, perda de dados ou indisponibilidade total.
- **SEV-2:** degradação relevante, readiness instável, fila parada ou falha de função crítica com contorno.
- **SEV-3:** erro localizado sem risco de dados ou impacto amplo.

## Primeiros 15 minutos

1. Nomeie responsável e registre horário UTC, sintomas e correlation IDs.
2. Preserve evidências. Não cole JWT, senha, API key, connection string ou conteúdo pessoal no ticket.
3. Em risco de isolamento/credencial, suspenda o caminho afetado e rotacione o secret comprometido.
4. Verifique `docker compose ps`, live/ready, uso de disco/memória e logs JSON pelo correlation ID.
5. Se deploy recente for causa provável e schema for compatível, aplique rollback de imagem.

## Cenários

### API indisponível

- live falha: verificar processo/OOM/configuração; recriar apenas API após preservar logs.
- live passa e ready falha: verificar SQL/rede/migrations; não reiniciar tudo indiscriminadamente.

### Disco crítico

- pare deploys/uploads, identifique volumes e logs; rotacione logs conforme política.
- não exclua volume SQL, Data Protection ou único backup.

### Suspeita de vazamento entre tenants

- tratar como SEV-1, preservar requests/correlation IDs e desabilitar rota afetada.
- não oferecer impersonation nem consulta manual ampla.
- avaliar notificação e obrigações legais com responsável por privacidade.

### Secret comprometido

- revogar/rotacionar no provedor e ambiente; reiniciar somente consumidores.
- JWT tenant e Platform são independentes. Rotacionar uma chave invalida sua classe de sessões.
- preservar o key ring/certificado Data Protection durante rotação planejada; perda desses itens invalida tokens protegidos.

## Encerramento

Documentar linha do tempo, causa, impacto, tenants potencialmente afetados, dados envolvidos, correção, teste de regressão e ações com responsável/prazo. Incidente Critical/High aberto bloqueia novas features privilegiadas e expansão da beta.
