# Matchmaker (MVP)

Serviço HTTP simples para criar/listar/join/end matches com token HMAC e heartbeat.

## Executar (local)
```
node services/matchmaker/index.js
```

## Variáveis de ambiente
- `MATCHMAKER_PORT` (default: 8080)
- `MATCHMAKER_SECRET` (default: dev_secret_change_me)
- `MATCHMAKER_DEFAULT_ENDPOINT` (default: 127.0.0.1:7770)
- `MATCHMAKER_TOKEN_TTL_S` (default: 300)
- `MATCHMAKER_HEARTBEAT_TTL_S` (default: 60)
- `MATCHMAKER_SERVER_POOL` (ex.: 127.0.0.1:7770=2;127.0.0.1:7771=2)
- `MATCHMAKER_MAX_MATCHES` (default: 200)
- `MATCHMAKER_ERROR_ALERT_THRESHOLD` (default: 5)
- `MATCHMAKER_ALERT_WINDOW_MS` (default: 60000)
- `BUILD_VERSION` (default: build_version.txt)

## Endpoints
- `POST /matches`
- `GET /matches`
- `POST /matches/{match_id}/join`
- `POST /matches/{match_id}/end`
- `POST /matches/{match_id}/heartbeat`
- `POST /tokens/verify`
- `GET /metrics`
- `GET /dashboard`
