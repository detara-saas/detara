# Backlog

## MVP

- Fundação, autenticação, empresa e usuários
- Clientes e veículos — concluído na Task 02
- Categorias de serviço, serviços e pacotes — concluído na Task 03
- Agenda e agendamentos — concluído na Task 04
- Orçamentos, valor negociado, aprovação e PDF profissional — concluído na Task 05
- Ordens de serviço, checklist e fotos
- Pagamento básico e dashboard com dados reais

## Próxima versão

- Pós-venda por e-mail/WhatsApp
- Comissões e estoque básico
- Melhorias de agenda e relatórios
- Entitlements comerciais por empresa (`EmpresaModulo`) quando surgir o primeiro add-on real
- Eventos internos entre módulos quando houver o primeiro fluxo reativo real
- Testes arquiteturais quando namespaces e fronteiras estiverem estáveis
- Branding empresarial no PDF (logo, endereço e demais dados) quando existir perfil/storage empresarial
- Armazenamento auditável do PDF final, hash e data de envio se surgir exigência jurídica

## Futuro

- CRM, campanhas e fidelidade
- Autoatendimento / Portal do Cliente como add-on comercial: catálogo público, agendar/reagendar/cancelar, aprovar orçamento e acompanhar atendimento
- Link público da OS e avaliações
- Google Calendar e financeiro avançado
- Avaliar Estoque e CRM como módulos adicionais independentes do produto base
- Definir publicação do Catálogo dentro do futuro módulo Autoatendimento, sem `DisponivelNoPortal` no Core
- Projetar API pública, segurança e entitlement somente quando Autoatendimento for efetivamente implementado
- Portal do Cliente poderá visualizar, baixar, aprovar e recusar Orçamentos; não criar link/token público antes do módulo existir
- Ordem de Serviço originada de Orçamento aprovado deverá copiar os valores negociados e nunca recalculá-los pelo Catálogo
- Reavaliar extração de microserviço apenas pelos critérios operacionais do ADR 001

Cada item exige validação de produto antes de introduzir infraestrutura externa.
