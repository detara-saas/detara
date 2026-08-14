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

O Web mantém o JWT somente em `sessionStorage`. Antes de produção, avaliar um BFF com cookie HttpOnly para reduzir ainda mais a exposição do token a código executado no navegador.

## Fluxo

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build --no-restore
```

Para novas migrations, use o `dotnet-ef` 10.0.10 registrado em `.config/dotnet-tools.json` e mantenha uma migration por mudança coerente de modelo.

Não desenvolva diretamente na `main`.
