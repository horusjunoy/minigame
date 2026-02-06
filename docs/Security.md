# Security — Regras mínimas para Multiplayer e (futuro) UGC

## 1) Princípios
- Server authoritative sempre.
- Validar tudo que vem do client.
- Não confiar em timestamps do client.
- Limitar taxa de eventos/commands (rate limit).

---

## 2) Ameaças comuns
- Speedhack / teleporte
- Spam de comandos
- Payload malicioso em broadcast/chat
- Exploits de economia/score

---

## 3) Controles no MVP
- Validação de movimento: velocidade máxima + aceleração + bounds.
- Cooldowns server-side.
- Clamp em score e eventos.
- JoinToken assinado com TTL.
- Logs de eventos anômalos (ex.: muitos commands/s).

---

## 4) Preparação para UGC
- Mini-game sandbox: lista de APIs permitidas (allowlist).
- Limitar CPU por tick por mini-game (orçamento).
- Limitar memória e alocações.
- Proibir IO arbitrário e rede no script.

---

## 5) Política de atualizações
- Versionar protocolos e manifestos.
- Compatibilidade com versões antigas de mini-games (quando aplicável).
