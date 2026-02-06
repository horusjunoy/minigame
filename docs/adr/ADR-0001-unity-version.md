# ADR-0001 — Versão do Unity e Render Pipeline

## Status
Proposto

## Contexto
Precisamos de uma base moderna e estável, compatível com PC agora e mobile no futuro.
URP é o pipeline alvo por performance e portabilidade.

## Decisão
- Adotar **Unity LTS moderna** como baseline (evitar prender em patch).
- Usar **URP** com perfis de qualidade por plataforma (PC/Mobile).

## Consequências
- Melhor portabilidade e performance.
- Necessário manter guias de upgrade e testes de regressão em upgrades LTS.
