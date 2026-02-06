# AssistantContext — Fonte de Verdade (para Codex/IA)

**Projeto:** Plataforma multiplayer de mini-games (hub/lobby + salas/instâncias) com runtime carregável.  
**Data do pack:** 2026-02-05

> Este documento é o “cartão de memória” do assistente.  
> Antes de implementar qualquer coisa, leia: ProductVision, Architecture, MinigameContract e ADRs.

---

## 0) Objetivo (1 frase)
Construir uma plataforma onde jogadores entram num **hub**, criam/entram em **salas**, jogam um **mini-game** (5–15 min), e voltam ao hub; com arquitetura preparada para UGC no futuro.

## 1) Regras do projeto (não-negociáveis)
1. **Servidor é autoritativo** para estado do jogo e validações.
2. **Sala = instância isolada** (processo headless dedicado por sala) no MVP.
3. **Mini-game é plugável**: adicionar novo mini-game deve exigir **mudanças mínimas ou nenhuma** no core.
4. **Compatível com mobile no futuro**: evitar decisões que dependam de “carregar DLL com código novo” em runtime.
5. **Observabilidade mínima** desde cedo: logs estruturados + métricas básicas + ids de correlação.
6. **Performance-first**: metas e budgets definidos em `PerformanceBudgets.md`.

## 2) Escopo MVP (resumo)
- Plataforma inicial: **PC (Windows)**, preparando terreno para mobile.
- Multiplayer: **8–16 jogadores por sala**.
- Sem mundo persistente, sem MMO.
- 1 mini-game exemplo completo (ex.: arena simples) + loop end-to-end.

Detalhes: `ProductVision.md`.

## 3) Decisões técnicas (estado atual)
Algumas decisões são “TBD” e estão registradas como ADRs. Enquanto isso, siga as regras abaixo:

- **Unity/Render:** Unity LTS moderna + URP (sem travar em patch específico). (ADR-0001)
- **Networking:** escolher entre Mirror e FishNet (TBD). Enquanto isso, **não acople** o core à API específica: crie uma camada `INetworkTransport` / `INetworkFacade`. (ADR-0002)
- **Matchmaking:** começar com serviço simples (HTTP) no MVP e planejar caminho para solução mais completa (ex.: Nakama) sem quebrar contratos. (ADR-0007)
- **UGC/Scripting:** MVP em C# interno; preparar interface `IScriptEngine` para futuro (Lua/JS interpretado, sem JIT). (ADR-0005)

## 4) Padrões de implementação (como o assistente deve agir)
- Sempre escrever código em módulos claros:
  - `Game.Core` (domínio / entidades / regras puras)
  - `Game.Runtime` (lifecycle, carregamento, contratos)
  - `Game.Network` (rede, replicação, RPC, serialização)
  - `Game.Client` (UI, input, câmera, FX)
  - `Game.Server` (autoridade, validações, simulação)
  - `Game.Minigames.*` (conteúdo e regras por mini-game)
- Usar **asmdefs** para isolamento e compilação mais rápida.
- Preferir **composition over inheritance**.
- Preferir **eventos/commands** com payloads simples e versionáveis.
- **Não** duplicar lógica crítica no client; usar previsão/interpolação apenas onde necessário.
- Escrever documentação incremental nos docs + ADR quando houver decisão irreversível.

## 5) Links internos essenciais
- Visão do produto: `ProductVision.md`
- Arquitetura: `Architecture.md`
- Contrato do Mini-game: `MinigameContract.md`
- Budgets e performance: `PerformanceBudgets.md`
- Matchmaking: `MatchmakingSpec.md`
- Telemetria: `Telemetry.md`
- Estilo e organização: `StyleGuide.md`, `RepoStructure.md`
- Build e release: `BuildAndRelease.md`, `Infrastructure.md`
- ADRs: `adr/`

## 6) Definition of Done (DoD) para qualquer entrega
Uma entrega está “pronta” quando:
- Compila (Client + Server headless) e roda localmente.
- Passa nos checks de qualidade (lint/format/testes definidos).
- Logs estruturados incluem `match_id`, `session_id`, `player_id` quando aplicável.
- Não viola budgets críticos (FPS/memória/rede) definidos.
- Documentação atualizada (ao menos README/ADR se mudou contrato).

---

## 7) Glossário rápido
- **Hub/Lobby:** UI/experiência fora da partida, onde cria/entra em salas.
- **Sala/Instância:** processo de servidor dedicado para um match.
- **Runtime:** camada que carrega mini-games e fornece APIs (spawn, score…).
- **Mini-game:** pacote isolado com assets + regras + manifesto, executado pelo runtime.
