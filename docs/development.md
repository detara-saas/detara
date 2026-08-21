# Desenvolvimento

## Configuração segura

Use variáveis de ambiente, user-secrets ou um `.env` local ignorado pelo Git:

- `ConnectionStrings__DefaultConnection`
- `Jwt__ChaveAssinatura` (mínimo de 32 bytes)
- `Seed__Enabled`
- `Seed__SenhaAdministrador`

O arquivo `.env` da raiz é consumido pelo Docker Compose. O Visual Studio e `dotnet run` não carregam esse arquivo automaticamente; para esses fluxos, configure os mesmos valores em User Secrets (`Jwt:ChaveAssinatura`, por exemplo). Nunca copie a chave real para `appsettings*.json`.

O seed é opcional e recusado fora do ambiente `Development`. Quando habilitado, aplica migrations e cria uma empresa/administrador demo de forma idempotente.

Para habilitar localmente sem versionar senha:

```powershell
dotnet user-secrets --project src/Detara.Api set Seed:Enabled true
dotnet user-secrets --project src/Detara.Api set Seed:SenhaAdministrador "uma-senha-local-forte"
```

## Resend em Development

Os profiles `http` e `https` de `src/Detara.Api/Properties/launchSettings.json` já fornecem `DETARA_EMAIL_FROM_ADDRESS=onboarding@resend.dev` e `Web__PublicBaseUrl=http://localhost:5080`. O ASP.NET Core resolve a segunda variável como `Web:PublicBaseUrl`.

A API key permanece fora do repositório e precisa ser configurada uma única vez no User Secrets da API:

```powershell
dotnet user-secrets set "DETARA_RESEND_API_KEY" "<API_KEY>" --project .\src\Detara.Api
```

Para confirmar que o secret existe:

```powershell
dotnet user-secrets list --project .\src\Detara.Api
```

Não compartilhe a saída desse comando, pois ela contém os valores reais armazenados. Depois da configuração inicial, pare e inicie novamente a solução pelo Visual Studio para que o profile de Development seja aplicado.

O Web mantém o JWT somente em `sessionStorage`. Antes de produção, avaliar um BFF com cookie HttpOnly para reduzir ainda mais a exposição do token a código executado no navegador.

## Fluxo

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build --no-restore
```

Para novas migrations, use o `dotnet-ef` 10.0.10 registrado em `.config/dotnet-tools.json` e mantenha uma migration por mudança coerente de modelo.

Não desenvolva diretamente na `main`.
