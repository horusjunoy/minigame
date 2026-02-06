# Architecture — Visão Geral

## 1) Princípios
- **Server authoritative**: regras e estado “verdadeiro” no servidor.
- **Client prediction** apenas para suavidade, nunca como fonte de verdade.
- **Isolamento**: mini-games não conhecem detalhes do transporte de rede nem do UI do lobby.
- **Versionamento**: eventos e manifestos versionáveis para suportar evolução.
- **Mobile-ready**: evitar dependência de “código carregado em runtime” (plugins/dll). Preferir data-driven + scripting interpretado futuro.

---

## 2) Componentes (macro)

### 2.1 Client (Unity)
- Input, câmera, UI (hub e HUD)
- Interpolação/predição (quando necessário)
- Render, VFX, animações, audio
- Carregamento de assets (Addressables)

### 2.2 Server (Unity headless por sala)
- Autoridade do estado do jogo
- Regras e validações
- Spawn/Despawn
- Score, timer, vitória/derrota
- Anticheat mínimo (validações server-side)

### 2.3 Runtime (dentro do server e do client)
- Carregamento do mini-game (manifest + assets)
- Lifecycle: onLoad/onGameStart/.../onGameEnd
- Roteamento de eventos e APIs expostas ao mini-game
- “Sandbox boundary” (futuro): ScriptEngine

### 2.4 Matchmaker (serviço externo no MVP)
- Cria sala, retorna endpoint do servidor, tokens.
- Mantém lista de salas e estados simples.
- Pode evoluir para solução mais completa (ex.: Nakama) sem quebrar contratos.

---

## 3) Fluxos principais

### 3.1 Criar sala
1. Client chama Matchmaker: `POST /matches`
2. Matchmaker aloca/lança servidor headless com `match_id`
3. Matchmaker retorna `{match_id, endpoint, join_token, minigame_id}`
4. Client conecta no servidor usando endpoint + token.

### 3.2 Entrar na sala
1. Client: `POST /matches/{id}/join` (ou `GET` se simples)
2. Recebe endpoint/token
3. Conecta no server; server valida token e registra player.

### 3.3 Rodar partida
- Runtime carrega mini-game.
- Server dispara `onGameStart`.
- Loop `onTick(dt)` roda no server.
- Server replica snapshots e eventos para clients.

### 3.4 Encerrar e voltar ao lobby
- Server chama `endGame(reason)` via runtime.
- Runtime dispara `onGameEnd(result)`.
- Server envia resultado final.
- Client mostra tela de resultados e desconecta → volta ao hub.

---

## 4) Módulos no código (sugestão)
- `Game.Core`: tipos puros (ids, structs, regras sem Unity).
- `Game.Runtime`: lifecycle + carregamento + API do mini-game.
- `Game.Network`: abstração de rede, serialização, replicação.
- `Game.Server`: simulação e autoridade.
- `Game.Client`: UI, input, camera, FX.
- `Game.Minigames.*`: implementações de mini-games.

---

## 5) Autoridade e replicação (resumo)
- Server valida: movimento (limites), ações (cooldowns), pontuação.
- Server replica:
  - **Snapshots** (estado compacto em intervalos fixos)
  - **Events** (ex.: “score+1”, “roundStart”, “roundEnd”)
- Client:
  - interpola snapshots
  - aplica eventos e animações
  - pode prever input local (opcional)

Detalhes: `PerformanceBudgets.md` (rede) + ADR-0003.
