# Topologia de produção

A topologia canônica atual está em [Produção V1](../production.md#topologia-e-capacidade).

Monólito modular em VPS única, somente Caddy publica 80/443; Web/API na edge, SQL em rede internal e gateway em rede privada com saída isolada. Landing permanece Cloudflare Pages. Configuração versionada única: compose.production.yml.

As regras de ownership/multi-tenancy permanecem em [module boundaries](module-boundaries.md). Infraestrutura não permite bypass de tenant nem adiciona queries globais de negócio.
