# Detara

SaaS multi-tenant para gestão de empresas de estética e cuidado automotivo. Esta entrega contém somente a fundação técnica do produto: autenticação, isolamento por empresa, shell web, persistência, testes e ambiente local.

## Pré-requisitos

- .NET SDK 10
- Docker Desktop (opcional para SQL Server e execução em containers)

## Executar com Docker

```powershell
Copy-Item .env.example .env
# Ajuste os valores locais do .env antes de continuar.
docker compose up --build
```

- Web: `http://localhost:5080`
- API: `http://localhost:5090`
- Swagger: `http://localhost:5090/swagger`
- Health check: `http://localhost:5090/health`

Quando `DETARA_SEED_ENABLED=true`, o seed roda somente em `Development`. Os valores padrão de empresa e usuário são `empresa-demo` e `admin@detara.local`; a senha vem exclusivamente de `DETARA_SEED_PASSWORD`.

## Executar sem Docker

Configure `ConnectionStrings__DefaultConnection` e `Jwt__ChaveAssinatura` por variável de ambiente ou user-secrets. Depois:

```powershell
dotnet tool restore
dotnet ef database update --project src/Detara.Infrastructure/Detara.Infrastructure.csproj --startup-project src/Detara.Api/Detara.Api.csproj
dotnet run --project src/Detara.Api
dotnet run --project src/Detara.Web
```

## Qualidade

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build --no-restore
```

Decisões importantes estão resumidas em [docs/architecture.md](docs/architecture.md) e [docs/multi-tenancy.md](docs/multi-tenancy.md).
