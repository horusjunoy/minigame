# ADR-0003 — Modelo de Autoridade (Server Authoritative)

## Status
Aceito

## Contexto
Jogos multiplayer precisam evitar trapaça e divergência de estado.

## Decisão
- O servidor é fonte de verdade para:
  - posições/estado de entidades
  - score
  - timers e vitórias
- O client pode:
  - prever input local (opcional)
  - interpolar snapshots
  - renderizar e tocar FX

## Consequências
- Menos cheaters e bugs difíceis.
- Requer atenção a latência (interpolação/predição).
