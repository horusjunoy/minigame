# MatchmakingSpec — Salas/Instâncias (MVP)

## 1) Objetivo
Oferecer um serviço simples para criar/entrar em salas e descobrir o endpoint do servidor headless.

---

## 2) Entidades
- **Match**: { match_id, minigame_id, status, created_at, max_players, region? }
- **JoinToken**: token assinado com TTL (ex.: 5 minutos), contém match_id + player_id + permissões.

---

## 3) Endpoints (exemplo REST)
### Criar match
`POST /matches`
```json
{
  "minigame_id": "arena_v1",
  "max_players": 16,
  "visibility": "public"
}
```
Resposta:
```json
{
  "match_id": "m_123",
  "endpoint": "127.0.0.1:7777",
  "join_token": "jwt_or_hmac_token",
  "status": "waiting"
}
```

### Listar matches
`GET /matches?minigame_id=arena_v1`
```json
[{ "match_id":"m_123", "players":3, "max_players":16, "status":"waiting" }]
```

### Entrar no match
`POST /matches/{match_id}/join`
Resposta:
```json
{
  "endpoint": "127.0.0.1:7777",
  "join_token": "jwt_or_hmac_token"
}
```

### Encerrar match (server -> matchmaker)
`POST /matches/{match_id}/end`
```json
{ "reason": "completed", "duration_s": 480 }
```

---

## 4) Regras e timeouts
- Match “zumbi” (sem heartbeat): marcar como morto e limpar.
- Heartbeat do server a cada 5–15s.
- JoinToken com TTL curto.

---

## 5) Caminho de evolução
- Trocar serviço simples por solução robusta (ex.: Nakama) mantendo:
  - `match_id`, `join_token`, `endpoint` como contrato.
