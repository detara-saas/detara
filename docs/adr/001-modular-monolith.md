# ADR 001 — Monólito modular

Status: aceito.

## Decisão

Usar um monólito modular .NET, separado por projetos e funcionalidades, com uma API e um frontend Blazor.

## Motivo

O domínio ainda mudará bastante. Limites locais preservam clareza e testes sem o custo operacional e transacional de microserviços. Extrações futuras permanecem possíveis quando houver evidência concreta.
