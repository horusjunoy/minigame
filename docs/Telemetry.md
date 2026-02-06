# Telemetry — Logs, Métricas e Tracing (mínimo viável)

## 1) Objetivo
Conseguir responder:
- “Quantas salas estão no ar?”
- “Qual mini-game cai mais?”
- “Qual latência média?”
- “Por que essa partida travou?”

---

## 2) IDs obrigatórios
Todo log/evento deve (quando aplicável) incluir:
- `match_id`
- `minigame_id`
- `player_id`
- `session_id` (client)
- `build_version`
- `server_instance_id`

---

## 3) Eventos mínimos (server)
- `match_started`
- `match_ended`
- `player_joined`
- `player_left`
- `minigame_loaded`
- `minigame_error`
- `tick_over_budget` (quando tick > X ms)
- `network_bytes_per_s` (debug)

---

## 4) Métricas (mínimo)
- Contador: matches criados/encerrados
- Gauge: players online por match
- Histograma: latência (RTT) e jitter
- Histograma: tempo de tick (ms)
- Erros por categoria (exceptions)

---

## 5) Logging estruturado (formato sugerido)
JSON por linha:
```json
{
  "ts":"2026-02-05T12:34:56.789Z",
  "lvl":"INFO",
  "event":"player_joined",
  "match_id":"m_123",
  "player_id":"p_9",
  "minigame_id":"arena_v1",
  "fields": { "endpoint":"127.0.0.1:7777" }
}
```

---

## 6) Política
- Sem PII sensível em logs.
- Erros críticos sempre logados (sem amostragem).
- Logs Info podem ter amostragem no futuro.
