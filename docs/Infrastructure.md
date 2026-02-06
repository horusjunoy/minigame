# Infrastructure — Visão de Infra (MVP → Escala)

## 1) MVP (local/dev)
- `matchmaker` (processo local)
- `server headless` (processo local por sala)
- `client` (Unity editor/build)

Opcional com Docker:
- matchmaker + redis/postgres (se necessário)
- logs centralizados (futuro)

---

## 2) Produção simples (primeiro deploy)
- 1 máquina/VM por região (ou por ambiente)
- Orquestração básica para subir/derrubar servidores de sala
- Matchmaker guarda estado de sala + health

---

## 3) Escala (visão)
- Orquestração (Kubernetes ou equivalente)
- Auto-scaling por número de salas
- Observabilidade com métricas e dashboards
- Cache/DB para contas e inventário (quando existir)

---

## 4) Requisitos para sala headless
- Porta UDP/TCP definida (conforme networking)
- Health endpoint/heartbeat para matchmaker
- Logs estruturados por match_id

---

## 5) Checklist de hardening
- Limites de recursos por processo
- Restart policy com backoff
- Quota de salas por host
- Rate limit no matchmaker
