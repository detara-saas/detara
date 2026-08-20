# Baseline permanente de segurança

Esta baseline é obrigatória para código novo e manutenção. Exceções exigem decisão arquitetural registrada e análise de risco.

## Identidade e autenticação

- A API valida assinatura, emissor, audiência, expiração e algoritmo JWT; `alg=none` e algoritmos fora da allowlist são rejeitados.
- Chaves JWT, senhas, API keys e connection strings reais nunca pertencem ao repositório, frontend ou logs.
- Configuração crítica ausente ou insegura deve impedir o startup em Production.
- Usuário, empresa, perfil e permissões atuais são revalidados no backend. Desativação, troca de perfil, troca de senha e revogação de permissão invalidam o token existente.
- Login inválido usa mensagem genérica, custo de hash também para identidade inexistente e rate limit.
- Claims de autorização não são aceitas apenas porque estão assinadas: devem continuar compatíveis com o estado atual persistido.

## Autorização

- A fallback policy exige usuário autenticado. Toda exceção anônima entra em whitelist revisada e testada.
- Toda ação sensível exige policy no backend. Ocultar botão ou rota na UI nunca autoriza a operação.
- Toda nova rota protegida recebe teste de usuário anônimo e de permissão ausente.
- Mudanças financeiras, transições de status, downloads, uploads, reenvio de e-mail e configurações são sempre autorizados server-side.

## Multi-tenancy e BOLA

- `EmpresaId` nunca é aceito do frontend como fonte de autoridade quando o tenant pode vir de `IUsuarioContexto`.
- Toda entidade comercial multi-tenant mantém query filter e proteção de escrita por tenant.
- Todo uso de `IgnoreQueryFilters` inclui predicado explícito de tenant, salvo operação de sistema documentada e de menor privilégio.
- Toda nova rota que recebe ID de recurso tenant-owned recebe teste adversarial Empresa A versus Empresa B.
- Referências cross-module e IDs relacionados são revalidados dentro do tenant antes de persistir.
- Respostas para GUID de outro tenant não revelam existência nem dados do recurso.

## Contratos, validação e dados

- DTOs de entrada expõem somente propriedades editáveis pelo caso de uso. IDs de tenant, auditoria, status interno, provider ID e valores derivados não são bindáveis.
- Mapeamento request → command → domínio é explícito; entidades EF não são modelos de entrada.
- Busca, paginação, ordenação, datas, coleções, texto e valores numéricos possuem limites server-side.
- Ordenação dinâmica usa allowlist. SQL parametrizado pelo EF é o padrão; raw SQL com entrada do usuário é proibido.
- Erros de validação usam o envelope padrão; falhas 5xx não expõem stack, SQL, paths, credenciais ou detalhes externos.

## Upload e storage

- Arquivos são privados, limitados e validados no servidor por conteúdo; extensão e MIME do cliente não são autoridade.
- Somente JPEG, PNG e WebP são aceitos no fluxo atual; SVG, HTML, JavaScript e demais formatos são rejeitados.
- Chaves de storage são geradas no servidor e caminhos são canonicalizados. Storage local nunca fica sob `wwwroot`.
- Metadados e download são autorizados por tenant antes de ler bytes.
- Downloads usam content type conhecido, `nosniff`, cache privado/no-store e nome seguro.
- Novo formato ou provider exige threat model, testes de path traversal, autorização e limites.

## HTML, browser e PWA

- HTML configurável é sanitizado por allowlist antes de persistência/renderização; tokens de negócio são HTML-encoded.
- URLs, atributos de evento, scripts, SVG ativo e cabeçalhos CR/LF recebem testes adversariais.
- Preview rico usa iframe sandboxed. Novo `MarkupString`, `innerHTML`, `srcdoc` ou bypass de encoding exige revisão de segurança.
- CSP e headers de defesa em profundidade devem permanecer compatíveis com o build publicado.
- Service Worker pode cachear somente app shell e assets estáticos versionados. API, Authorization, JWT e dados comerciais/financeiros nunca entram no cache.
- JWT não é persistido em cache offline. Alteração do modelo de sessão exige revisão de XSS e CSRF.

## Integrações externas

- Host e esquema do provedor são definidos pelo servidor e HTTPS; usuário não controla URL.
- Toda chamada externa possui timeout, limite de resposta, cancelamento, erro seguro e política de retry bounded.
- Chaves idempotentes protegem reenvios quando o provedor suporta.
- Payload, resposta do provedor, token e API key não são registrados.

## Operação e produção

- Production exige `AllowedHosts` explícito e CORS somente com origens HTTPS explícitas. Wildcards falham no startup.
- Swagger, seed e detalhes de desenvolvimento não ficam ativos em Production.
- Kestrel não anuncia versão, limita corpo/headers e usa timeout de headers.
- API retorna `nosniff`, anti-framing, CSP restritiva, `no-store`, permissions policy e trace id.
- Forwarded headers só podem ser habilitados com proxies/redes conhecidos; nunca confiar em `X-Forwarded-For` arbitrário.
- HSTS, TLS, rate limit de borda, WAF opcional, backup/restore, observabilidade e rotação de secrets pertencem ao checklist de Production Readiness.

## Gate de entrega

- Vulnerabilidade corrigida recebe teste que falharia antes, quando tecnicamente viável.
- Nenhum Critical ou High conhecido pode permanecer aberto.
- `dotnet restore`, build Release, testes, publish, format, auditoria de pacotes e verificação de migrations devem passar.
- Antes da primeira empresa real, todos os blockers de infraestrutura em `security-review.md` precisam estar fechados e validados em ambiente production-like.
