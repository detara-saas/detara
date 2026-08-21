# Administração básica do tenant

As rotas `/empresa`, `/usuarios`, `/perfis` e `/minha-conta` pertencem ao plano Tenant. A identidade Platform continua em scheme, audience e chave separados e não pode usar esses endpoints. Não existe troca de empresa, impersonation ou administração de dados operacionais pelo Platform Admin.

## Empresa

`GET /api/empresa` e `PUT /api/empresa` resolvem a empresa exclusivamente por `IUsuarioContexto`. O contrato editável contém nome fantasia, razão social, CPF/CNPJ, e-mail comercial, telefone e fuso horário. Slug, status SaaS, versões de segurança, identificadores e auditoria não são bindáveis. O e-mail comercial não altera o login. `VersaoCadastro` oferece concorrência otimista independente de `VersaoSeguranca`, portanto uma atualização cadastral não revoga sessões.

## Usuários e convites

`Administracao.Usuario` autoriza listar, convidar, alterar perfil, inativar/reativar e reenviar convite. E-mail é único em `(EmpresaId, Email)` e pode repetir em outra empresa. Um administrador nunca define a senha de outra pessoa.

O convite reutiliza a fila durável do convite inicial. O registro distingue origem Platform ou Tenant, guarda somente SHA-256 do token aleatório, expira, é single-use e invalida o token anterior no reenvio. O link usa fragmento, o usuário define a própria senha e o aceite não faz login automático. A criação do usuário e do convite ocorre no mesmo `SaveChanges`; Resend é efeito posterior, retryable e não desfaz os dados em caso de falha.

Inativação, reativação, alteração de perfil, e-mail ou senha incrementam `Usuario.VersaoSeguranca`. O validador de identidade consulta o estado corrente em cada requisição protegida; JWT antigo deixa de funcionar. Alterar somente o nome incrementa a versão de edição, não a de segurança.

O backend bloqueia auto-inativação, alteração do próprio perfil e a remoção/inativação do último usuário ativo cujo perfil ativo possui `Administracao.Usuario`.

## Perfis e permissões

Perfis são tenant-scoped e têm nome normalizado único por empresa. O perfil `Administrador` criado pelo provisionamento é marcado `EhSistema`, não podendo ser alterado nem inativado. Perfis customizados podem ser criados, editados, ativados e inativados; não há hard delete.

Permissões vêm exclusivamente do catálogo canônico. Código desconhecido retorna validação controlada. Ao criar, editar ou atribuir perfil, o caller só pode conceder um subconjunto de suas próprias permissões efetivas. A alteração do próprio perfil não é oferecida. Mudanças no permission set são percebidas pela revalidação corrente, sem cache com TTL.

## Minha Conta

Qualquer usuário Tenant autenticado pode consultar os próprios dados e alterar o nome. Alterar e-mail ou senha exige a senha atual, incrementa a versão de segurança e encerra a sessão no frontend. E-mail novo é validado apenas dentro do tenant atual e pode existir em outra empresa. Não há confirmação do novo endereço nesta etapa; ele não é marcado como verificado.

## Limites atuais

Recuperação de senha Tenant, verificação do novo e-mail e auditoria Tenant completa permanecem pendências explícitas antes de uma beta mais ampla. MFA Tenant, SSO, passkeys, RH e troca de tenant não fazem parte deste escopo.
