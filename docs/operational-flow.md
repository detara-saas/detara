# Fluxo operacional Agenda, Orçamento e Ordem de Serviço

## Invariantes

- Toda nova Ordem de Serviço possui um `AgendamentoOrigemId` válido da empresa autenticada.
- Uma Agenda possui no máximo uma Ordem de Serviço. A regra existe no handler e em índice único filtrado no SQL Server.
- `AgendamentoOrigemId` pode ser nulo apenas em OS históricas criadas antes desta integração.
- Um orçamento principal aprovado pode ser vinculado a uma Agenda. O vínculo não altera seus snapshots, valores, aprovação ou PDF.
- A OS criada de orçamento usa a mesma Agenda, Cliente e Veículo e preserva integralmente os valores comerciais aprovados.
- Agenda não cria cobrança. Financeiro continua reagindo somente à finalização da execução da OS.

## Caminhos suportados

### Agenda primeiro

1. Criar Agenda com Cliente, Veículo, horário, duração e itens planejados.
2. Opcionalmente criar e aprovar orçamento a partir dela.
3. Criar a OS pela Agenda. Se já existir, a interface oferece apenas **Ver OS**.
4. Iniciar a OS para colocar a Agenda em **Em atendimento**.
5. Finalizar serviços para `AguardandoRetirada`; a Agenda permanece em atendimento.
6. Registrar a entrega/conclusão da OS para concluir a Agenda.

### Orçamento primeiro

1. Criar, emitir e aprovar o orçamento.
2. Usar **Agendar atendimento** e escolher data, hora e duração.
3. A operação cria a Agenda e grava `Orcamento.AgendamentoId` no mesmo `SaveChanges`.
4. Criar a OS usando os dois vínculos.

### Atendimento sem horário prévio

1. Na Agenda, usar **Atendimento agora**.
2. O formulário sugere data e hora atuais; o operador confirma Cliente, Veículo, duração e escopo.
3. Após criar a Agenda, criar a OS e seguir o fluxo normal de check-in e execução.

## Estados sincronizados

| Ação na OS | Estado da OS | Estado da Agenda |
|---|---|---|
| Criar | Aberta | Agendado ou Confirmado, sem alteração automática |
| Iniciar execução | Em execução | Em atendimento (`Compareceu` persistido) |
| Finalizar serviços | Aguardando retirada | Em atendimento |
| Entregar/concluir | Concluída | Concluído |
| Cancelar | Cancelada | Sem conclusão ou cancelamento automático |

## Consistência e segurança

As operações derivam o tenant exclusivamente de `IUsuarioContexto`. IDs enviados pelo browser são validados dentro do tenant. Os vínculos usam contratos internos estreitos, sem navegações EF entre Agenda e Atendimento e sem cascade delete. A Agenda pode armazenar zero itens somente no caminho especializado de orçamento aprovado composto exclusivamente por itens personalizados; o orçamento permanece a fonte comercial auditável.

Na migração de bases existentes, vínculos duplicados Agenda→OS não provocam exclusão de documentos. O vínculo é mantido na OS ativa mais recente; na ausência de OS ativa, na histórica mais recente. Somente `AgendamentoOrigemId` das duplicatas excedentes é tornado nulo, classificando-as como legado e permitindo criar o índice único.
