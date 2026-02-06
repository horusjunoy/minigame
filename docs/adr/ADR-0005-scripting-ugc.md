# ADR-0005 — Scripting/Sandbox para UGC (futuro)

## Status
Proposto

## Contexto
Mobile (especialmente iOS) não é amigável a carregar código nativo/dll em runtime.
Para UGC, precisamos de lógica carregável sem quebrar políticas da plataforma.

## Decisão
- MVP: mini-games em C# (interno, compilado no app/server).
- Preparar `IScriptEngine` para futuro:
  - Lua/JS interpretado (sem JIT)
  - APIs em allowlist
  - limites de CPU/memória por tick
- Manifesto define permissões e entrypoints.

## Consequências
- Habilita UGC com menor risco de “re-escrever tudo”.
- Exige design cuidadoso de segurança e budgets.
