# Desenvolvimento

## Configuração segura

Use variáveis de ambiente, user-secrets ou um `.env` local ignorado pelo Git:

- `ConnectionStrings__DefaultConnection`
- `Jwt__ChaveAssinatura` (mínimo de 32 bytes)
- `Seed__Enabled`
- `Seed__SenhaAdministrador`

O seed é opcional e recusado fora do ambiente `Development`. Quando habilitado, aplica migrations e cria uma empresa/administrador demo de forma idempotente.

O Web mantém o JWT somente em `sessionStorage`. Antes de produção, avaliar um BFF com cookie HttpOnly para reduzir ainda mais a exposição do token a código executado no navegador.

## Fluxo

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build --no-restore
```

Para novas migrations, use a ferramenta local registrada em `dotnet-tools.json` e mantenha uma migration por mudança coerente de modelo.

Não desenvolva na `main`. A branch desta entrega é `feature/estrutura-inicial`.
