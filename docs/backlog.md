# Backlog

## MVP

- Fundação, autenticação, empresa e usuários
- Clientes e veículos — concluído na Task 02
- Categorias de serviço, serviços e pacotes — concluído na Task 03
- Agenda e agendamentos — concluído na Task 04
- Orçamentos, valor negociado, aprovação e PDF profissional — concluído na Task 05
- Fundação operacional, checklist configurável, storage privado e fotos permanentes do veículo — concluído na Task 06.1
- Ordens de serviço, snapshots do checklist e fotos de entrada/saída — Task 06.2
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
- Adapter de Object Storage para produção, com Azure Blob Storage como candidato inicial
- Limpeza/reconciliação de arquivos órfãos quando existir necessidade operacional comprovada
- Thumbnails, compressão, correção de orientação e sanitização de EXIF para imagens, sem alterar os originais antes de decisão de produto
- Avaliar suporte a HEIC após definição de conversão e compatibilidade dos clientes

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
- Checklist por serviço, categoria ou tipo de veículo somente após validar a necessidade; a versão atual mantém um modelo padrão por empresa
- Snapshots imutáveis de checklist e respostas pertencentes à Ordem de Serviço
- Fotos de entrada, durante o serviço e saída pertencentes à Ordem de Serviço, separadas das fotos permanentes do cadastro do veículo
- Categorias e finalidade das fotos de Ordem de Serviço
- Respostas simples de checklist (conforme/não conforme, observação e evidência) na Task 06.2
- Integração futura de consumo com Estoque e geração financeira apenas quando esses módulos forem implementados

Cada item exige validação de produto antes de introduzir infraestrutura externa.
