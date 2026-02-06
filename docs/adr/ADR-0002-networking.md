# ADR-0002 — Biblioteca de Networking (Mirror vs FishNet)

## Status
Proposto (TBD)

## Contexto
Precisamos de networking com suporte a:
- server headless
- PC e mobile (IL2CPP)
- 8–16 players por sala
- comunidade/estabilidade

## Opções
- Mirror
- FishNet

## Decisão (parcial)
Enquanto a escolha não é fechada:
- Criar uma camada de abstração (`INetworkFacade`) para evitar acoplamento.
- Manter mensagens/DTOs independentes do transporte.

## Critérios de escolha
- Licença compatível com o produto
- Estabilidade e manutenção
- Suporte a snapshots / RPC / interest management
- Ferramentas e debug

## Consequências
- Leve trabalho extra inicial (abstração), mas reduz risco de reescrita.
