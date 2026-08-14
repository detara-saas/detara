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

## Preferências da interface

A migration `AddUserInterfacePreferences` adiciona:

- `UsuariosPreferencias`, única por `(EmpresaId, UsuarioId)`;
- `UsuariosPaginasFavoritas`, única por `(EmpresaId, UsuarioPreferenciaId, Pagina)`;
- FK composta da preferência para o usuário e do favorito para a preferência.

Tema, idioma, estado da sidebar, página inicial e favoritos pertencem ao usuário autenticado. O browser nunca envia `UsuarioId` ou `EmpresaId` para atualizar esses dados.

## Clientes e veículos

A migration `AddClientesEVeiculos` adiciona:

- `Clientes`, com documento opcional e único por `(EmpresaId, CpfCnpj)` quando preenchido;
- `Veiculos`, com placa única por `(EmpresaId, Placa)`;
- FK composta `(EmpresaId, ClienteId)` de veículo para cliente;
- FKs para empresa e relacionamento Cliente → Veículo com exclusão restritiva;
- índices de busca por nome, telefone, documento, placa e cliente.

Clientes e veículos herdam `EntidadeEmpresaBase`, portanto recebem automaticamente filtro global, validação de escrita e token de concorrência por tenant. Documento e placa são armazenados normalizados.

## Catálogo de serviços e pacotes

A migration `AddServicosCategoriasEPacotes` adiciona:

- `CategoriasServico`, com nome único por `(EmpresaId, Nome)` e ordenação própria;
- `Servicos`, com nome único por `(EmpresaId, CategoriaServicoId, Nome)`, preço base e duração opcionais;
- `Pacotes`, com nome único por `(EmpresaId, Nome)` e preço comercial independente;
- `PacotesServicos`, com composição ordenada e vínculo único por `(EmpresaId, PacoteId, ServicoId)`;
- FKs compostas para categoria, pacote e serviço, impedindo associações entre tenants no banco;
- FKs para empresa e exclusões restritivas em todos os relacionamentos.

Categorias, serviços e pacotes usam inativação lógica independente. A soma dos serviços, a duração total e a economia do pacote são calculadas nas consultas e não são persistidas. A economia só é apresentada quando todos os serviços possuem preço e o preço do pacote é menor que a soma individual.

Aplicação de migration:

```powershell
dotnet tool restore
dotnet ef database update --project src/Detara.Infrastructure/Detara.Infrastructure.csproj --startup-project src/Detara.Api/Detara.Api.csproj
```

As migrations das Tasks 02 e 03 devem permanecer pendentes no ambiente local até a aplicação deliberada pelo responsável pelo banco.
