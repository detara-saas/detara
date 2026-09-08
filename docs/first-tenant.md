# Primeiro tenant real

Pré-condições: go-live técnico, domínio Resend verificado, SQL/backup/restore validados, cofre e monitoramento ativos. Nunca DemoBootstrap, Prime Detail, senha fixa ou INSERT de empresa/usuário no SQL.

1. Operador autorizado carrega a configuração externa e manifesto current conforme runbook de deploy e executa `dc exec api dotnet /tools/platform-bootstrap/Detara.PlatformBootstrap.dll create-admin` no shell com `common.sh` carregado. O console interativo solicita nome, email e senha (oculta, confirmada); usar `--help` para ajuda. A API já utiliza configuração Production e key ring/PFX persistentes. Não passar senha em argumento nem criar endpoint de bootstrap.
2. Concluir cadastro de MFA TOTP antes do token administrativo e guardar recovery codes de forma segura. Identidade Platform é global, sem EmpresaId/impersonation.
3. Fazer backup após geração do key ring e confirmar recuperação do PFX no cofre.
4. Abrir Platform Admin com MFA, cadastrar empresa real pelo fluxo existente e enviar convite somente com autorização para email correto.
5. Responsável da empresa aceita convite single-use e define sua própria senha; Platform Admin não define senha de cliente.
6. Concluir onboarding/configurações/fuso/perfis. Conferir isolamento e permissões com usuário de menor privilégio, sem compartilhar token administrativo.
7. Cadastrar mínimo necessário pelo sistema e executar smoke operacional consentido. Não inserir dados fictícios na empresa real.
8. Manter comunicação automática Nenhum/Email até gateway e consentimento WhatsApp estarem configurados. Falha de canal não impede fluxo de negócio.
9. Registrar aceite do responsável, contatos de suporte e horário do primeiro backup após provisionamento.

Esta task apenas documenta o procedimento; não cria administrador, tenant ou convite real.
