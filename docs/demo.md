# Demo Bootstrap local — Prime Detail

O `Detara.DemoBootstrap` prepara um tenant fictício e coerente para demonstrações locais do Detara. Todos os nomes, contatos, documentos, veículos, placas e registros operacionais criados pela ferramenta são sintéticos. O cenário não deve ser usado como fonte de dados reais.

## Proteções

- funciona exclusivamente com `ASPNETCORE_ENVIRONMENT=Development`;
- `create`, `reset` e `presentation` exigem `--confirm-local-demo`;
- não possui endpoint HTTP, botão no produto, seed de startup ou bypass de ambiente;
- não aceita senha por argumento e não grava nem repete a senha no terminal;
- não inicia workers, não chama Resend, não acessa S3 e não depende de Internet;
- identifica o tenant pelo slug estável `prime-detail-demo`;
- o reset remove e reconstrói somente os dados desse tenant, respeitando os query filters;
- não cria migrations, pacotes NuGet nem regras especiais no domínio.

## Configuração local

A ferramenta reutiliza o mesmo `UserSecretsId` da API para ler `ConnectionStrings:DefaultConnection`. A connection string também pode ser fornecida por `ConnectionStrings__DefaultConnection`. Não coloque credenciais em arquivos versionados ou argumentos de linha de comando.

No PowerShell, defina o ambiente da sessão:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
```

## Comandos

Consultar o estado sem alterar dados:

```powershell
dotnet run --project .\tools\Detara.DemoBootstrap -- status
```

Criar o cenário, de forma idempotente:

```powershell
dotnet run --project .\tools\Detara.DemoBootstrap -- create --confirm-local-demo
```

Reconstruir exclusivamente o cenário Prime Detail:

```powershell
dotnet run --project .\tools\Detara.DemoBootstrap -- reset --confirm-local-demo
```

Preparar uma apresentação comercial, reconstruindo o mesmo tenant com datas relativas ao momento da execução e exibindo o roteiro sugerido:

```powershell
dotnet run --project .\tools\Detara.DemoBootstrap -- presentation --confirm-local-demo
```

Em `create`, `reset` e `presentation`, a senha do administrador é solicitada duas vezes em terminal interativo, sem echo. A política vigente exige de 10 a 256 caracteres. O login padrão é `demo@detara.local`; esse endereço é local e não recebe e-mail.

## Conteúdo do cenário

O cenário possui a empresa Prime Detail Estética Automotiva, perfis Administrador, Recepção e Operação, três usuários, nove clientes com um veículo cada, cinco categorias, dez serviços, sete agendamentos relativos ao momento da execução, quatro orçamentos, quatro ordens de serviço e três contas a receber. Entre os dados sintéticos estão Mariana Oliveira com Honda Civic Touring, João Mendes com Toyota Corolla, Carlos Henrique com Jeep Compass e Isabela Martins com BMW 320i. Um dos veículos é a moto aquática `Sea-Doo GTX 300`, sem placa e identificada por `DEMO-JET-01`. Há orçamento emitido aguardando decisão, atendimentos em execução, veículos aguardando retirada e atendimento concluído, além de pagamento Pix, pagamento misto e recebível pendente.

As configurações operacionais e o checklist são criados por comandos da aplicação. Clientes, veículos, catálogo, agenda, orçamentos, aprovações, ordens de serviço, check-in, transições e pagamentos também percorrem os comandos reais. A criação direta via EF fica restrita à fundação administrativa do fixture (empresa, perfis e usuários inativos auxiliares) e à limpeza transacional do próprio tenant durante o reset.

A notificação automática de veículo pronto permanece desabilitada. Nenhuma notificação de e-mail é criada ou enviada.

## Preparação automatizada da apresentação

O script abaixo verifica .NET e Docker, sobe somente o SQL Server necessário, aplica migrations, executa o comando `presentation`, compila a solução, inicia API e Web e abre o navegador:

```powershell
.\scripts\demo\Iniciar-Detara-Demo.ps1
```

A senha continua sendo informada interativamente ao `DemoBootstrap`; o script não recebe, persiste ou repete senha, token ou secret. Para reutilizar um cenário já preparado sem executar o bootstrap novamente, use `-PularPreparacao`. Para não abrir o navegador, use `-NaoAbrirNavegador`.

API e Web são executadas em segundo plano, com PIDs e logs armazenados somente no diretório temporário do usuário. Portas já ocupadas não são encerradas automaticamente.

Para encerrar somente API e Web iniciadas pelo script:

```powershell
.\scripts\demo\Parar-Detara-Demo.ps1
```

O SQL Server é preservado por padrão. Para interrompê-lo junto com os processos da aplicação:

```powershell
.\scripts\demo\Parar-Detara-Demo.ps1 -IncluirInfraestrutura
```

## Roteiro comercial sugerido

1. Entre em `http://localhost:5080/login` com `demo@detara.local` e a senha escolhida durante a preparação.
2. Comece no Dashboard Operação para apresentar agenda, atendimento em execução, retirada e orçamento pendente.
3. Abra Mariana Oliveira em Clientes e percorra Cliente 360° → Honda Civic Touring → histórico.
4. Use os atalhos contextuais do veículo para mostrar Agendamento, Orçamento e início de OS sem perder cliente/veículo.
5. Abra uma OS em execução para explicar check-in, checklist, fotos, escopo e evolução operacional.
6. Abra uma OS aguardando retirada para apresentar comunicação manual por Email/WhatsApp e Financeiro.
7. Retorne ao Dashboard Empresa para fechar com receita, ticket médio, clientes atendidos, serviços, funil e insights reais.

O Dashboard reflete os registros reais gerados pelo bootstrap, sem números especiais ou tratamento condicional para a Prime Detail.

O progresso do onboarding não usa flags: ele é derivado dos dados reais criados e deve aparecer como concluído.

## Proibição de outros ambientes

A ferramenta se encerra antes de acessar o banco quando o ambiente não é exatamente `Development`. Não execute nem adapte o Demo Bootstrap para Staging ou Production.
