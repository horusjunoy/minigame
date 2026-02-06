# ADR-0007 — Estratégia de Matchmaking (MVP simples → evolução)

## Status
Proposto

## Contexto
Precisamos entregar MVP rápido, mas sem travar o caminho de escala.

## Decisão
- MVP: matchmaker simples (HTTP) que cria/encerra salas e entrega endpoint/token.
- Evolução: migrar para solução mais completa (ex.: Nakama) mantendo contratos (`match_id`, `join_token`, `endpoint`).

## Consequências
- Velocidade no MVP.
- Evita lock-in e reescrita total na escala.
