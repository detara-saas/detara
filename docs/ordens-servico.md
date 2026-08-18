# Ordens de Serviço

`OrdemServico` pertence a Atendimento e representa o escopo autorizado e a execução em
um veículo. Ela pode nascer de orçamento aprovado, agendamento ou atendimento direto.

No orçamento aprovado, todos os snapshots e valores são copiados exatamente. Agenda é
somente sugestão de escopo; sem orçamento, o operador confirma os valores e a aplicação
registra data, usuário e observação da autorização direta. Alterações futuras em Cliente,
Veículo, Catálogo, Agenda ou configuração não reescrevem a OS.

O fluxo operacional é `Aberta → EmExecucao → AguardandoRetirada → Concluida`, com
cancelamento permitido enquanto aberta ou em execução. Cada transição possui comando e
histórico explícitos. `Concluida` significa veículo entregue, não pagamento recebido.

O check-in captura os níveis atuais de checklist, fotos de entrada e fotos de saída.
Checklist obrigatório exige todas as respostas, inclusive `NaoConforme` ou
`NaoAplicavel`; fotos obrigatórias exigem uma evidência na etapa correspondente. Fotos de
entrada, durante e saída são privadas, usam `IArquivoStorage` e tornam-se imutáveis após
conclusão ou cancelamento.

Um adicional cobrado cria outro Orçamento com `OrdemServicoOrigemId`. Enquanto rascunho,
emitido, recusado, cancelado ou expirado, ele não compõe a OS. Ao ser aprovado, seus itens
são incorporados uma vez, identificados por `OrcamentoItemOrigemId`; o orçamento original
permanece aprovado e não é substituído. Cortesia de valor zero pode ser registrada
diretamente com auditoria.

O total autorizado é a soma dos escopos aprovados, com descontos e acréscimos de cada
documento. O futuro Financeiro deve consumir esse total sem consultar preços atuais do
Catálogo. O futuro Estoque deve reagir ao consumo real da execução, não ao mero cadastro
do serviço.
