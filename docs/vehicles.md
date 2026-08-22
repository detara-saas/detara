# Veículos

O cadastro de veículos atende itens emplacados e não emplacados sem inferir regras a partir do tipo. Os campos mínimos são cliente, tipo, marca e modelo.

## Tipos suportados

| Valor | Tipo |
| ---: | --- |
| 1 | Carro |
| 2 | Moto |
| 3 | Caminhonete |
| 4 | Van |
| 5 | Caminhão |
| 6 | Embarcação |
| 7 | Moto aquática |
| 8 | Quadriciclo / UTV |
| 99 | Outro |

Na migration que introduz o campo, os veículos existentes recebem `Carro`, que corresponde ao escopo automotivo anterior do produto. O tipo é obrigatório nos novos cadastros e pode ser corrigido na edição.

## Identificação

- A placa é opcional para qualquer tipo de veículo. Quando informada, é normalizada para letras maiúsculas e caracteres alfanuméricos e deve seguir `ABC1234` ou `ABC1D23`.
- Placa vazia ou composta somente por espaços é armazenada como `null`.
- Uma placa não nula é única dentro da empresa. A mesma placa pode existir em empresas diferentes e vários veículos sem placa podem existir na mesma empresa. A garantia física usa o índice único filtrado `(EmpresaId, Placa) WHERE Placa IS NOT NULL` no SQL Server.
- `IdentificacaoAlternativa` é opcional, tem no máximo 120 caracteres, recebe apenas `Trim`, preserva a capitalização informada e não é única. Pode representar inscrição, chassi ou referência interna.
- Nenhum tipo exige placa ou identificação alternativa. Isso permite, por exemplo, carro novo ainda não emplacado e embarcação sem inscrição disponível no momento do cadastro.

## Exibição e pesquisa

A descrição operacional usa a primeira identificação disponível, sem separador solto:

- `Honda Civic · ABC1D23`
- `Sea-Doo GTX 300 · DEMO-JET-01`
- `Sea-Doo GTX 300`

As buscas de veículos aceitam placa, identificação alternativa, marca, modelo, cliente e telefone. A busca da listagem de clientes também encontra o proprietário por placa, identificação alternativa, marca ou modelo, sempre respeitando o filtro de tenant.

Agenda, Orçamento, Ordem de Serviço e Conta a Receber guardam a descrição e a placa como snapshots históricos. Para novos registros sem placa, a descrição já contém a identificação alternativa quando disponível e o snapshot de placa permanece `null`. Registros antigos não são recalculados.

## Reversão da migration

O `Down` não fabrica placas. Caso já existam veículos ou snapshots operacionais com placa nula, a reversão é interrompida com erro explícito. Em ambiente pré-produção, remova responsavelmente esses dados ou restaure um backup compatível antes de reverter.
