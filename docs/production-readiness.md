# Production Readiness — beta privada

Status desta entrega: preparação técnica implementada e validável localmente. Nenhum VPS, DNS, bucket, serviço pago ou ambiente real foi criado.

## Matriz de prontidão

| Área | Estado | Evidência / ação antes do go-live |
|---|---|---|
| Build, testes e formatação | Preparado | workflow `.github/workflows/ci.yml` e gates locais |
| Containers | Preparado | `compose.production.yml`, somente Caddy exposto |
| TLS e proxy | Preparado | `deploy/Caddyfile`; validar DNS e emissão ACME no VPS |
| Configuração e secrets | Preparado | `.env.production.example`; preencher fora do Git |
| Data Protection | Preparado | volume persistente + PFX obrigatório; criar e guardar cópia segura |
| Banco | Preparado | login runtime dedicado e migration bundle documentados; provisionar no ambiente |
| Mídia privada | Preparado | provider S3-compatible; criar bucket privado e credencial mínima |
| Health checks | Preparado | `/health/live` e `/health/ready`, sem detalhes internos |
| Logs e correlação | Preparado | JSON em Production, `X-Correlation-ID`, rotação Docker |
| Backup | Preparado | script com checksum e `RESTORE VERIFYONLY`; agendar e copiar offsite |
| Restore drill | Preparado | script com banco temporário e `DBCC CHECKDB`; executar antes da beta |
| Deploy/rollback | Documentado | runbook; ensaiar em host de homologação |
| Observabilidade externa | Pendente operacional | configurar uptime de live/ready, alerta de disco e agregação de logs |
| Segurança operacional | Pendente operacional | firewall, atualizações do host, acesso SSH e rotação de credenciais |
| Privacidade/LGPD | Mapeado | inventário inicial; validar bases legais e política com responsável |

## Variáveis e secrets obrigatórios

Nunca reutilize valores entre tenant JWT, Platform JWT, SQL ou Data Protection.

- `DETARA_PUBLIC_HOST`, `DETARA_ACME_EMAIL`
- `DETARA_API_IMAGE`, `DETARA_WEB_IMAGE` com tag imutável/digest
- `DETARA_SQL_ADMIN_PASSWORD`, `DETARA_SQL_RUNTIME_PASSWORD`
- `DETARA_JWT_KEY`, `DETARA_PLATFORM_JWT_KEY`
- `DETARA_DATA_PROTECTION_PASSWORD` e `secrets/detara-data-protection.pfx`
- `DETARA_S3_ENDPOINT`, bucket, região, access key e secret key
- `DETARA_RESEND_API_KEY`, remetente e nome

O startup em Production falha cedo se host, URL pública, CORS, certificado/key ring, usuário SQL, storage, remetente ou proxy confiável estiverem inseguros/incompletos.

## Capacidade inicial e sinais de alerta

A capacidade real deve ser medida com uso da beta. Como ponto de partida, reservar no VPS ao menos 4 vCPU, 8 GiB de RAM e SSD com folga de três vezes o banco atual. Não tratar isso como dimensionamento final.

Alertas mínimos:

- disco acima de 70% (aviso) e 85% (crítico);
- banco se aproximando do limite da edição SQL escolhida (Express tem teto baixo e exige upgrade antes de alcançá-lo);
- memória sustentada acima de 80%;
- readiness indisponível por 2 minutos;
- liveness indisponível em duas verificações;
- crescimento anormal de erros 5xx/429;
- backup não produzido no intervalo esperado;
- certificado com menos de 21 dias para expirar;
- fila de notificações acumulando ou Resend falhando repetidamente.

## Revisão de performance

- Listagens paginadas permanecem limitadas; Platform preserva 10/25/50 e padrão 25.
- EF usa consultas assíncronas e filtros de tenant existentes; nenhuma nova consulta cross-module foi criada.
- Uploads continuam limitados a 10 MiB e o Kestrel a 12 MiB.
- S3 usa timeout de 15 segundos e até duas novas tentativas do SDK; a API não cria URL pública.
- Antes de ampliar a beta, observar consultas lentas no SQL e adicionar índice somente com evidência e migration revisada.

## Critério de go/no-go

Go somente quando todos os itens obrigatórios de `docs/beta-launch-checklist.md` estiverem marcados, o restore drill tiver evidência recente e não houver vulnerabilidade Critical/High conhecida aberta. Ausência de evidência equivale a no-go.
