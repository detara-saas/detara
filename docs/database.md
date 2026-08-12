# Banco de dados

SQL Server com EF Core Code First. A migration única `InitialCreate` contém:

- `Empresas`
- `Usuarios`
- `Perfis`
- `Permissoes`
- `PerfisPermissoes`

Índices únicos: `Empresa.Slug`, `Empresa.CpfCnpj`, `Permissao.Codigo`, `Perfil(EmpresaId, Nome)` e `Usuario(EmpresaId, Email)`.

A associação de usuário com perfil usa FK composta `(EmpresaId, PerfilId)`, impedindo associação entre tenants no próprio banco. Novas entidades comerciais devem herdar `EntidadeEmpresaBase` e adotar índices com `EmpresaId` quando a unicidade ou consulta for local à empresa.

Aplicação de migration:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/Detara.Infrastructure --startup-project src/Detara.Api
```
