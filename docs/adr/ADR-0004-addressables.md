# ADR-0004 — Conteúdo Hot-Load com Addressables

## Status
Proposto

## Contexto
Queremos preparar o projeto para mobile e para adicionar mini-games sem rebuild completo do app (conteúdo).

## Decisão
- Adotar Addressables para carregar:
  - cenas de mini-games
  - prefabs e assets grandes
- Versionar conteúdo por mini-game.

## Consequências
- Pipeline de build de conteúdo mais complexo.
- Muito ganho em flexibilidade e redução de tamanho inicial do app.
