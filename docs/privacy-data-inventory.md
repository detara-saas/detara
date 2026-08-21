# Inventário inicial de dados e privacidade

Este documento é um mapa técnico inicial, não parecer jurídico.

| Categoria | Exemplos | Finalidade | Local principal | Retenção inicial a definir |
|---|---|---|---|---|
| Identidade tenant | nome, e-mail, hash de senha, permissões | acesso e operação do SaaS | SQL | vínculo + prazo legal/contratual |
| Platform Admin | e-mail, hash, MFA, recovery codes em hash | administração global separada | SQL + Data Protection | enquanto ativo + auditoria definida |
| Clientes | nome, contato e observações | prestação do serviço automotivo | SQL | política da empresa/controladora |
| Veículos | placa, modelo, fotos | identificação e evidência do atendimento | SQL + bucket privado | política da empresa/controladora |
| Agenda/OS/orçamento | datas, serviços, valores, observações | operação comercial | SQL | obrigação contratual/fiscal aplicável |
| Financeiro | contas, pagamento, valores | controle financeiro | SQL | obrigação fiscal/contábil aplicável |
| Comunicações | destinatário, template, status/tentativas | convite e notificações | SQL + Resend | período operacional/auditoria definido |
| Auditoria e logs | usuário, ação, correlation ID, IP técnico | segurança e diagnóstico | SQL/logs | janela curta e justificada |
| Backups | cópia das categorias anteriores | continuidade | storage offsite criptografado | conforme política de retenção |

## Princípios técnicos já aplicados

- isolamento por tenant e identidade Platform separada;
- senha, recovery code e convite armazenados somente em hash quando aplicável;
- mídia privada mediada pela API;
- logs sem corpo/query string e sem secrets;
- backup com retenção definida, sem exclusão improvisada;
- nenhum cache offline de dados comerciais/financeiros.

## Pendências organizacionais antes da beta

- definir controlador/operador, bases legais, termos e política pública;
- definir prazos por categoria e procedimento de descarte em banco, bucket, logs e backups;
- canal para acesso, correção e exclusão, considerando obrigações de retenção;
- subprocessadores e regiões de SQL/VPS, S3 e Resend;
- procedimento de incidente e comunicação conforme risco;
- minimizar campos livres e orientar empresas a não inserirem dados sensíveis desnecessários.
