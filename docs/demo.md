# Demo Bootstrap local — Prime Detail

O `Detara.DemoBootstrap` prepara um tenant fictício e coerente para demonstrações locais do Detara. Todos os nomes, contatos, documentos, veículos, placas e registros operacionais criados pela ferramenta são sintéticos. O cenário não deve ser usado como fonte de dados reais.

## Proteções

- funciona exclusivamente com `ASPNETCORE_ENVIRONMENT=Development`;
- `create` e `reset` exigem `--confirm-local-demo`;
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

Em `create` e `reset`, a senha do administrador é solicitada duas vezes em terminal interativo, sem echo. A política vigente exige de 10 a 256 caracteres. O login padrão é `demo@detara.local`; esse endereço é local e não recebe e-mail.

## Conteúdo do cenário

O cenário possui a empresa Prime Detail Estética Automotiva, perfis Administrador, Recepção e Operação, três usuários, oito clientes com um veículo cada, cinco categorias, dez serviços, sete agendamentos relativos ao momento da execução, quatro orçamentos, quatro ordens de serviço e três contas a receber. Há exemplos em execução, aguardando retirada e concluído, além de pagamento Pix, pagamento misto e recebível pendente.

As configurações operacionais e o checklist são criados por comandos da aplicação. Clientes, veículos, catálogo, agenda, orçamentos, aprovações, ordens de serviço, check-in, transições e pagamentos também percorrem os comandos reais. A criação direta via EF fica restrita à fundação administrativa do fixture (empresa, perfis e usuários inativos auxiliares) e à limpeza transacional do próprio tenant durante o reset.

A notificação automática de veículo pronto permanece desabilitada. Nenhuma notificação de e-mail é criada ou enviada.

## Preparação da apresentação

1. Garanta que o SQL Server local esteja ativo e com as migrations atuais aplicadas.
2. Execute `reset --confirm-local-demo` e informe uma senha conhecida somente pelo operador.
3. Inicie API e Web pelos perfis Development usuais.
4. Abra `http://localhost:5080/login`.
5. Entre com `demo@detara.local` e a senha escolhida no reset.
6. Verifique Dashboard, Clientes, Veículos, Serviços, Agenda, Orçamentos, Ordens de Serviço, Financeiro, Usuários, Perfis, Empresa e Minha Conta.

O progresso do onboarding não usa flags: ele é derivado dos dados reais criados e deve aparecer como concluído.

## Proibição de outros ambientes

A ferramenta se encerra antes de acessar o banco quando o ambiente não é exatamente `Development`. Não execute nem adapte o Demo Bootstrap para Staging ou Production.
