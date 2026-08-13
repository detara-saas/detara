# Banco de dados

SQL Server com EF Core Code First. `InitialCreate` cria:

- `Empresas`
- `Usuarios`
- `Perfis`
- `Permissoes`
- `PerfisPermissoes`

Índices únicos: `Empresa.Slug`, `Empresa.CpfCnpj`, `Permissao.Codigo`, `Perfil(EmpresaId, Nome)` e `Usuario(EmpresaId, Email)`.

A associação de usuário com perfil usa FK composta `(EmpresaId, PerfilId)`, impedindo associação entre tenants no próprio banco. Novas entidades comerciais devem herdar `EntidadeEmpresaBase` e adotar índices com `EmpresaId` quando a unicidade ou consulta for local à empresa.

`StrengthenFoundation` adiciona FKs restritivas de `Perfil.EmpresaId` e `Usuario.EmpresaId` para `Empresa.Id`. A unicidade de e-mail é deliberadamente por empresa (`EmpresaId + Email`), permitindo o mesmo endereço em tenants diferentes. `EmpresaId` também é token de concorrência no modelo EF; essa marca não exige coluna adicional.

Aplicação de migration:

```powershell
dotnet tool restore
dotnet ef database update --project src/Detara.Infrastructure/Detara.Infrastructure.csproj --startup-project src/Detara.Api/Detara.Api.csproj
```
