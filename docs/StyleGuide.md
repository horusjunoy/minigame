# StyleGuide — Padrões de Código e Arquitetura (Unity + C#)

## 1) Princípios
- Clareza > esperteza.
- Preferir composição.
- Preferir tipos imutáveis para dados (struct/record) quando possível.
- Evitar alocações no hot path (Update/Tick).

---

## 2) C# e Unity
- Use `PascalCase` para tipos/métodos, `camelCase` para variáveis.
- Evite `FindObjectOfType` e singletons globais não controlados.
- Configuração: preferir `ScriptableObject` para dados e tuning.
- DI (se adotado): preferir injeção no bootstrap, não em runtime aleatório.

---

## 3) Async
- Use async apenas onde faz sentido (I/O, Addressables).
- No server tick, evite async dentro do loop; agende e trate fora do tick.

---

## 4) Eventos e mensagens
- Commands: intenção do player (ex.: `TryUseAbility`)
- Events: fato ocorrido (ex.: `AbilityUsed`)
- Snapshots: estado replicado compacto

Nomeie mensagens com verbo no passado para eventos (`PlayerJoined`), e no imperativo para commands (`Move`, `Shoot`).

---

## 5) Logs
- Sempre logar com campos estruturados:
  - `match_id`, `player_id`, `minigame_id`, `phase`, `elapsed_ms`
- Nunca logar PII sensível.
- Preferir níveis: Debug/Info/Warn/Error.

---

## 6) Testes
- `Game.Core`: unit tests puros.
- `Game.Runtime`: testes de conformidade e integração leve.
- `Game.Server`: simulações determinísticas quando possível.

---

## 7) “Moderninho, mas seguro” (recomendações)
- Use `readonly struct` para ids (`PlayerId`, `MatchId`).
- Evite reflection no hot path.
- Use pooling para listas/buffers em loops frequentes.
- Padronize serialização (DTOs versionáveis) e evite “serializar objetos Unity” direto.
