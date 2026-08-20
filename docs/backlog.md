# Backlog

## MVP

- Fundação, autenticação, empresa e usuários
- Clientes e veículos — concluído na Task 02
- Categorias de serviço, serviços e pacotes — concluído na Task 03
- Agenda e agendamentos — concluído na Task 04
- Orçamentos, valor negociado, aprovação e PDF profissional — concluído na Task 05
- Fundação operacional, checklist configurável, storage privado e fotos permanentes do veículo — concluído na Task 06.1
- Ordens de serviço, check-in, execução, adicionais e fotos transacionais — concluído na Task 06.2
- Contas a receber, pagamentos, estornos e dashboard financeiro com dados reais — concluído na Task 07

## Próxima versão

- Pós-venda por e-mail/WhatsApp
- Comissões e estoque básico
- Melhorias de agenda e relatórios
- Entitlements comerciais por empresa (`EmpresaModulo`) quando surgir o primeiro add-on real
- Avaliar promoção da integração Application-level OS → Financeiro para eventos internos somente quando houver outro consumidor real e um transaction behavior explícito
- Taxa padrão configurável por forma de pagamento
- Testes arquiteturais quando namespaces e fronteiras estiverem estáveis
- Branding empresarial no PDF (logo, endereço e demais dados) quando existir perfil/storage empresarial
- Armazenamento auditável do PDF final, hash e data de envio se surgir exigência jurídica
- Adapter de Object Storage para produção, com Azure Blob Storage como candidato inicial
- Limpeza/reconciliação de arquivos órfãos quando existir necessidade operacional comprovada
- Thumbnails, compressão, correção de orientação e sanitização de EXIF para imagens, sem alterar os originais antes de decisão de produto
- Avaliar suporte a HEIC após definição de conversão e compatibilidade dos clientes

## Futuro

- CRM, campanhas e fidelidade
- Autoatendimento / Portal do Cliente como add-on comercial: catálogo público, agendar/reagendar/cancelar, aprovar orçamento e acompanhar atendimento
- Link público da OS e avaliações
- Google Calendar e financeiro avançado
- Contas a pagar, fornecedores, despesas, centros de custo e DRE
- Conciliação bancária/cartão e integrações opcionais com gateways para pagamentos dos clientes da estética
- Indicadores de margem apenas após Estoque e custos operacionais fornecerem dados confiáveis
- Avaliar Estoque e CRM como módulos adicionais independentes do produto base
- Definir publicação do Catálogo dentro do futuro módulo Autoatendimento, sem `DisponivelNoPortal` no Core
- Projetar API pública, segurança e entitlement somente quando Autoatendimento for efetivamente implementado
- Portal do Cliente poderá visualizar, baixar, aprovar e recusar Orçamentos; não criar link/token público antes do módulo existir
- Reavaliar extração de microserviço apenas pelos critérios operacionais do ADR 001
- Checklist por serviço, categoria ou tipo de veículo somente após validar a necessidade; a versão atual mantém um modelo padrão por empresa
- Integração futura de consumo com Estoque para permitir custos variáveis e indicadores de rentabilidade confiáveis

Cada item exige validação de produto antes de introduzir infraestrutura externa.
