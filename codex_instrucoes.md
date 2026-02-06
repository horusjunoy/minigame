# CODEX_INSTRUCOES — Constituição Imutável (Mini-game Platform)

Missão:
Construir uma plataforma multiplayer de mini-games com:
- Hub/Lobby (client)
- Matchmaker (serviço simples no MVP)
- Servidor headless autoritativo por sala/instância
- Runtime que carrega mini-games via manifesto + lifecycle + APIs estáveis
Com scripts oficiais, evidência (logs + runs), tickets, testes (unit + integração + smoke E2E) e guardrails de promoção.

Fonte de verdade (antes de implementar):
- docs/ProductVision.md
- docs/Architecture.md
- docs/MinigameContract.md
- docs/Telemetry.md
- docs/PerformanceBudgets.md
- docs/RepoStructure.md + docs/StyleGuide.md
- ADRs em docs/adr/

Máquina de estados (padrão de execução):
REQUISITO → PLANO → IMPLEMENTAR → BUILD → EXECUTAR LOCAL → EVIDÊNCIA → INVESTIGAR → CORRIGIR → REVALIDAR → PROMOVER

───────────────────────────────────────────────────────────────────────────────
Regras inegociáveis

1) Sem requisito mínimo + DoD, não implementar.
   Requisito mínimo deve dizer:
   - qual fluxo (Hub→Sala→Partida→Resultados→Hub) ou qual contrato (Runtime/MinigameContract) muda
   - critérios objetivos de pronto (DoD)
   - riscos (rede, determinismo, perf, regressões)

2) Server authoritative SEM exceção.
   - Client é apresentação + input + interpolação/predição (se existir), nunca fonte de verdade.
   - Toda regra que muda estado/score/timer precisa ocorrer no server.

3) Mini-game plugável (core não deve “conhecer” o mini-game).
   - Se o core precisar de mudanças para suportar um mini-game, isso deve ser mínimo e documentado.
   - Mudou contrato/protocolo? Versione (protocol_version / schema_version) e documente.

4) Evidência obrigatória:
   - Todo comando relevante gera log em ./logs/ contendo:
     stdout+stderr (quando aplicável), cmd, cwd, commit hash, duração, exit code
   - Logs do runtime/jogo devem incluir IDs quando aplicável:
     match_id, session_id, player_id, minigame_id, build_version, server_instance_id

5) Falhou = isolar antes de consertar.
   - Primeiro: teste isolado (unit / playmode / integração) OU repro mínima por script.
   - Depois: suíte completa.
   - Investigação termina com hipótese testável + teste isolado que confirma.

6) Um problema por commit. Mudança mínima e segura.
   - Sem “refactor grátis” misturado em correção.
   - Se precisar refatorar, faça em commits separados e justificados.

7) Sempre retornar ao humano:
   - arquivos alterados/criados
   - comando(s) de revalidação (isolado + suíte)
   - se falhar: ticket completo + próximo teste isolado sugerido

8) Uso do Codex / rate limit:
   - Se aparecer mensagem de “usage limit” com horário de retry, tente novamente 1 minuto após o horário indicado.
   - Registre a tentativa como run (mesmo que “skipped por limit”).

───────────────────────────────────────────────────────────────────────────────
Padrões do repo (mínimo recomendado)

- /scripts: check/test/build-client/build-server/run-local/run-matchmaker/smoke/export-artifacts (ps1+sh equivalentes)
- /logs: logs de comandos + logs de runtime
- /runs: registros de execuções (smoke/CI/local)
- /tickets: bugs e tarefas investigativas com evidência
- /artifacts: builds, traces, dumps, exports
- /docs: specs + ADRs

Estrutura Unity (alto nível):
Assets/Game/{Core,Runtime,Network,Server,Client,Minigames/*}
Cada mini-game em Assets/Game/Minigames/<Name>/ com manifesto.

Proibição:
- Game.Application (se existir) não pode importar Game.Infra (se houver separação)
- Core/Runtime não podem acoplar a uma lib específica de networking (use facade/abstração)

───────────────────────────────────────────────────────────────────────────────
Ticket (formato obrigatório) em /tickets/<id>.md

- título + assinatura
- contexto: minigame_id + fluxo (Hub/Sala/Partida/Resultados) + ambiente (OS, Unity, build_version)
- comando(s) executado(s) (exatos)
- erro/stacktrace completo (ou link do log + tail 200)
- caminho(s) do(s) log(s) + tail 200
- esperado (1 frase objetiva)
- impacto (crash? desync? exploit? bloqueia milestone?)
- hipótese inicial (testável)
- próximo teste isolado sugerido
- DoD da correção:
  - teste isolado passa
  - suíte passa
  - smoke E2E passa (quando aplicável)
  - logs/telemetria não regrediram

───────────────────────────────────────────────────────────────────────────────
Testes e evidência (padrão)

1) Unit (rápido):
- Game.Core: regras puras, ids, validações (NUnit/Unity Test Framework editmode)

2) Integração leve:
- Game.Runtime: carregar manifesto, lifecycle do minigame, simular 2–4 players, ticks, EndGame coerente
- Game.Server: simulação determinística quando possível

3) Smoke E2E (gate de promoção):
- subir matchmaker local
- subir server headless de 1 sala
- conectar 1–4 clientes (ou bots)
- entrar na sala, iniciar partida, pontuar, encerrar, voltar ao hub
- coletar logs e registrar run

Em falha:
- obrigatório: log do matchmaker + log do server headless + log do client/bot
- obrigatório: identificar match_id e minigame_id da falha

───────────────────────────────────────────────────────────────────────────────
Guardrails (daemon/CI)

- Frequência: diária ou por PR (config)
- Se tudo verde:
  - registrar run em /runs com metadados (commit, duração, resultados)
  - encerrar
- Se falhar:
  - abrir ticket com evidência
  - máx 2 tentativas automáticas por assinatura
  - flaky: quarentena (tag no ticket + skip gate até estabilizar)
- Promover “estável” só após 2 runs verdes seguidas (mesmo commit ou commits consecutivos)

───────────────────────────────────────────────────────────────────────────────
Compatibilidade e performance

- Windows no MVP; scripts ps1+sh equivalentes (paths portáveis).
- Budgets obrigatórios:
  - server tick dentro do budget
  - rede por player dentro do budget (snapshot/eventos)
- Evitar escolhas que travem mobile/IL2CPP no futuro (sem dependência de hot-load de DLL).

───────────────────────────────────────────────────────────────────────────────
Saída esperada do Codex

- lista de arquivos alterados/criados
- comandos de revalidação:
  - teste isolado
  - suíte completa
  - smoke E2E (se aplicável)
- se falhar:
  - ticket preenchido
  - teste isolado sugerido com hipótese

───────────────────────────────────────────────────────────────────────────────
COMANDOS DE REVALIDAÇÃO (priorizar scripts do repo)

# Qualidade / lint (se existir)
.\scripts\check.ps1
./scripts/check.sh

# Testes
.\scripts\test.ps1
./scripts/test.sh

# Build
.\scripts\build-client.ps1
.\scripts\build-server.ps1
./scripts/build-client.sh
./scripts/build-server.sh

# Smoke local end-to-end
.\scripts\smoke.ps1
./scripts/smoke.sh
