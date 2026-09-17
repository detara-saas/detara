# Sessões persistentes de autenticação

## Decisão arquitetural

O Detara mantém o access token JWT somente em `sessionStorage` e usa um refresh token opaco em cookie `HttpOnly`. O refresh token nunca é retornado no JSON, exposto ao JavaScript, gravado em Web Storage ou persistido em texto puro. No banco existe apenas o hash SHA-256 do token.

O access token tenant expira em 15 minutos. A sessão de refresh não persistente expira em 12 horas e usa cookie de sessão. Quando o usuário escolhe **Manter-me conectado**, a sessão e o cookie expiram em 30 dias. A sessão de Platform Admin expira em 8 horas, não é persistente e somente é criada depois da conclusão válida do MFA. Esses valores são centralizados em `SessaoAutenticacao` na configuração da API.

## Topologia, cookie, CORS e CSRF

Em produção, o frontend usa `https://app.detara.com.br` e a API usa `https://api.detara.com.br`. São origens diferentes, mas pertencem ao mesmo site. Por isso o cookie é host-only, restrito ao caminho de autenticação correspondente, `HttpOnly`, `Secure` em ambientes não Development e `SameSite=Lax`. Não há atributo `Domain` e o cookie tenant não é enviado às rotas do Platform Admin, nem o inverso.

O Web envia credenciais somente à origem configurada da API. O CORS usa uma origem HTTPS explícita com credentials; wildcard não é permitido. `refresh` e `logout` validam o header `Origin` quando ele existe. A combinação de cookie host-only, `SameSite=Lax`, CORS restrito, validação de origem e métodos POST mitiga CSRF sem transformar as APIs operacionais em autenticação por cookie. As APIs operacionais continuam exigindo Bearer.

## Rotação, replay e concorrência

Cada refresh bem-sucedido invalida o token atual e cria outro token na mesma família, preservando a expiração absoluta da sessão original. O banco usa controle otimista de concorrência para impedir duas rotações válidas do mesmo registro.

O frontend aplica single-flight por escopo WASM: requisições simultâneas na mesma aba aguardam uma única renovação e cada request pode ser repetido apenas uma vez. Como abas e PWA compartilham o cookie, o servidor tolera por 30 segundos a reapresentação de um token recém-rotacionado sem revogar a família. Fora dessa janela, a reutilização é tratada como replay e revoga toda a família. Nenhum segredo é usado para coordenar abas.

## Validação da identidade

O refresh tenant não recebe `EmpresaId` do browser. Usuário, empresa, perfil, permissões e versões de segurança são reconstruídos do estado atual no servidor. Usuário, empresa ou perfil inativo e alteração de versão de segurança invalidam a família.

O refresh administrativo usa entidade, cookie e chave JWT separados. Ele só existe após MFA válido e exige que o administrador continue ativo, com MFA habilitado e com a mesma versão de segurança. O JWT renovado mantém `amr=mfa`; o refresh não substitui MFA nem step-up de ações críticas.

Troca de senha, redefinição de credencial e desativação de usuário revogam as sessões tenant ativas. Logout revoga a família, limpa o cookie e é idempotente.

## Assinatura, falhas de rede e PWA

Login, refresh e logout permanecem fora do bloqueio comercial. Uma empresa suspensa pode restaurar a autenticação e continua recebendo HTTP 402 nas APIs operacionais. O frontend só tenta refresh em 401 elegível; 402 e 403 nunca iniciam renovação.

Um 401 definitivo remove o access token local. Falhas transitórias de rede não revogam a sessão nem apagam o cookie. O Service Worker mantém toda chamada de API e autenticação como network-only, e as respostas da API usam `Cache-Control: no-store`.

## Limpeza e observabilidade

A limpeza é oportunística: sessões expiradas ou revogadas há mais de sete dias são removidas durante a criação de sessões tenant. Logs estruturados registram criação, renovação, revogação e replay sem senha, access token, refresh token ou hash completo.
