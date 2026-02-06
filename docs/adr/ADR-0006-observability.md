# ADR-0006 — Observabilidade mínima desde o início

## Status
Aceito

## Contexto
Sem logs/métricas, bugs em multiplayer viram “fantasmas”.

## Decisão
- Logs estruturados (JSON) em server e client.
- Métricas mínimas: tick_ms, rtt, players/match, crashes.
- IDs de correlação: match_id, player_id, minigame_id.

## Consequências
- Pequeno custo de implementação.
- Grande ganho de diagnósticos e velocidade de correção.
