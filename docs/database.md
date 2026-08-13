# Banco de dados

SQL Server com EF Core Code First. A migration única `InitialCreate` contém:

- `Empresas`
- `Usuarios`
- `Perfis`
- `Permissoes`
- `PerfisPermissoes`

Índices únicos: `Empresa.Slug`, `Empresa.CpfCnpj`, `Permissao.Codigo`, `Perfil(EmpresaId, Nome)` e `Usuario(EmpresaId, Email)`.

A associação de usuário com perfil usa FK composta `(EmpresaId, PerfilId)`, impedindo associação entre tenants no próprio banco. Novas entidades comerciais devem herdar `EntidadeEmpresaBase` e adotar índices com `EmpresaId` quando a unicidade ou consulta for local à empresa.

## Preferências da interface

A migration `AddUserInterfacePreferences` adiciona:

- `UsuariosPreferencias`, única por `(EmpresaId, UsuarioId)`;
- `UsuariosPaginasFavoritas`, única por `(EmpresaId, UsuarioPreferenciaId, Pagina)`;
- FK composta da preferência para o usuário e do favorito para a preferência.

Tema, idioma, estado da sidebar, página inicial e favoritos pertencem ao usuário autenticado. O browser nunca envia `UsuarioId` ou `EmpresaId` para atualizar esses dados.

Aplicação de migration:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/Detara.Infrastructure --startup-project src/Detara.Api
```
