# MinigameContract — Lifecycle + APIs

## 1) Objetivo
Garantir que qualquer mini-game possa rodar no runtime sem acoplamento ao core, com contratos estáveis e versionáveis.

---

## 2) Lifecycle (mínimo)

### Interface conceitual
```csharp
public interface IMinigame
{
    void OnLoad(IMinigameContext context);      // preparar recursos, registrar handlers
    void OnGameStart();                          // iniciar rodada/partida
    void OnPlayerJoin(PlayerRef player);         // player entrou na sala
    void OnPlayerLeave(PlayerRef player);        // (recomendado) player saiu
    void OnTick(float dt);                       // tick do servidor (autoritativo)
    void OnGameEnd(GameResult result);           // encerramento (limpeza/telemetria)
}
```

**Notas:**
- No MVP, `OnTick` roda no **servidor**.
- No client, o mini-game pode ter “companheiro visual” (opcional) com hooks próprios (ex.: `IMinigameClientView`), mas **nunca** decide regras.

---

## 3) APIs expostas ao mini-game (mínimo)

### 3.1 Spawn / Entities
```csharp
EntityRef Spawn(EntityId id, Vector3 pos, Quaternion rot, SpawnOptions? options = null);
void Despawn(EntityRef entity);
IReadOnlyList<PlayerRef> GetPlayers();
```

### 3.2 Pontuação / Resultado
```csharp
void SetScore(PlayerRef player, int value);
void AddScore(PlayerRef player, int delta);
Scoreboard GetScoreboard();
void EndGame(EndGameReason reason, GameResult? overrideResult = null);
```

### 3.3 Eventos e comunicação
```csharp
void Broadcast(string eventName, object payload);          // server -> todos
void SendTo(PlayerRef player, string eventName, object payload);
void Log(string message, LogLevel level = LogLevel.Info, object? fields = null);
```

**Regras:**
- `payload` deve ser serializável e versionável (ex.: structs/DTOs).
- Evite enviar grandes payloads em alta frequência.

---

## 4) Regras de segurança e autoridade
- Mini-game **não** pode:
  - abrir sockets arbitrários
  - acessar filesystem fora de sandbox (futuro)
  - executar reflexão perigosa (futuro)
- Toda ação que muda o estado deve ocorrer no server.

---

## 5) Manifesta do Mini-game (MVP)
O runtime usa um manifesto (data-driven) para listar assets e entrypoints.

### Exemplo (conceitual)
```json
{
  "schema_version": 1,
  "id": "arena_v1",
  "display_name": "Arena",
  "version": "0.1.0",
  "server_entry": "Game.Minigames.Arena.ArenaMinigame, Game.Minigames.Arena",
  "client_entry": "Game.Minigames.Arena.ArenaClientView, Game.Minigames.Arena",
  "addressables": {
    "scenes": ["ArenaScene"],
    "prefabs": ["PlayerAvatar", "PickupCoin"]
  },
  "settings": {
    "match_duration_s": 480,
    "score_to_win": 20
  }
}
```

### Versionamento
- `schema_version`: versão do schema do manifesto (incrementar quando o formato mudar).
- `version`: versão do mini-game (semver recomendado).
 - Loader aceita `id@version` para selecionar uma versao especifica.

### Validação (SDK base)
Use o script `.\scripts\validate-manifests.ps1` para validar:
- presença de campos obrigatórios
- resolução dos entrypoints (`server_entry` / `client_entry`)

---

## 6) Teste de conformidade (recomendado)
Criar uma suíte que:
- carrega o mini-game
- simula 2–4 players
- executa ticks
- valida que não há exceções
- valida que `EndGame` produz `GameResult` coerente.

Isso vira um “gate” de qualidade para mini-games internos e (futuro) UGC.
