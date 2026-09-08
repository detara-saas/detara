# Production Readiness

A preparação inicial da beta foi consolidada pela Task 49 para Hostinger KVM 2 (2 vCPU/8 GB), SQL Express, Caddy/Cloudflare e domínios Web/API separados.

- [Arquitetura e configuração V1](production.md)
- [Checklist exato de go-live](production-go-live.md)
- [Deploy e rollback](runbooks/deploy.md)
- [Backup e restore](runbooks/backup-restore.md)
- [Operação](operations.md)
- [Recuperação](disaster-recovery.md)

Nenhum ambiente real foi provisionado nesta task. Go-live exige evidência de restore, configuração de monitoramento externo, secrets e revisão humana. Não usar instruções antigas de backup com COMPRESSION nativo na edição Express.
