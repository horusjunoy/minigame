# Como executar
# (PowerShell)
$env:Path += ";$HOME\.minigame\bin"
codex-retry.ps1 -RepoRoot "C:\Users\horus.junoy\Minigame" -PromptFile "codex_mini.md" -ExtraMinutes 5 -MaxRetries 20 -Yolo

# CODEX_QUAD - Runner de Sprints (cole sprints abaixo)

REGRA: estas Sprints devem ser executadas seguindo estritamente as instrucoes imutaveis em `codex_instrucoes.md`.

PRECEDENCIA:
1) A Sprint colada abaixo (contrato principal).
2) `codex_instrucoes.md` (regras imutaveis do projeto).
3) `/docs` (referencia secundaria).
Conflito: Sprint > codex_instrucoes.md > docs.

MODO DE ACAO:
- Se a Sprint não tiver Requisito mínimo + DoD + comandos de revalidação: primeiro complete a Sprint (patch), não implemente código.
- Não iniciar próxima Sprint sem a atual 100% verde.
- Os envie o comando para o usuário executar no terminal os testes, e os testes precisam aparecer no terminal o nome do teste e se passou ou deu falha, apenas isso. 
- Ao finalizar o teste, aparecer no terminal finalizado. E quando não passar, logar o erro em /logs para você mesmo ajustar.
- Um problema por commit; mudança mínima e segura.
- Ao concluir integralmente uma Sprint, você DEVE retornar a este arquivo e marcar explicitamente a Sprint como **SPRINT FEITO POR COMPLETO**.
- A partir desse marcador (**SPRINT FEITO POR COMPLETO**), você ESTÁ AUTORIZADO a continuar as implementações na próxima Sprint definida abaixo, caso exista.
- Nunca continue implementações acima de uma Sprint não marcada como **SPRINT FEITO POR COMPLETO**.

---

# SPRINTS
Sprint 7 — Rede e Protocolo “de verdade”

Objetivo: parar de “torcer” para compatibilidade e garantir budget de rede/CPU com 8–16p.

Requisito mínimo

- Fluxo/contrato afetado: handshake de rede (client↔server), contrato de versionamento do protocolo e métricas de runtime por match/player.
- Pronto quando:
  - client com `protocol_version` incompatível recebe erro claro e volta ao hub.
  - logs/telemetria registram `match_id`, `player_id`, `minigame_id`, `build_version` nas métricas novas.
  - budget de rede/tick documentado e reportado por match em modo debug.
- Riscos: regressão de compatibilidade (client antigo), aumento de tráfego por métricas, regressão de tick/CPU em matches 16p.

Itens

Handshake com protocol_version e build_version (bloquear mismatch com erro claro no client).

Consolidar a camada de abstração de rede (INetworkFacade/INetworkTransport) para reduzir acoplamento (e permitir decidir Mirror vs FishNet sem reescrever o core).

Instrumentar bytes/s por player e tick_ms por match (com sampling em debug).

Ajustes rápidos de replicação para bater budget (quantização simples, reduzir frequência de snapshots por entidade, etc.).

DoD

Cliente incompatível recebe mensagem clara e volta ao hub (sem travar).

16 jogadores: tráfego e tick dentro do budget definido.

Logs e métricas incluem ids de correlação (match/player/minigame). 

Comandos de revalidação

.\scripts\check.ps1
.\scripts\test.ps1
.\scripts\smoke-network.ps1
.\scripts\smoke-e2e.ps1

SPRINT FEITO POR COMPLETO
Evidências:
- logs/w7_check.log
- logs/w7_test.log
- logs/w7_smoke_retry.log

BuildAndRelease

 

ADR-0002-networking

 

PerformanceBudgets

 

Telemetry

Sprint 8 — Pipeline de Conteúdo por Mini‑game (Addressables)

Objetivo: permitir evoluir mini‑games e assets sem dor e sem leak de memória ao voltar pro lobby.

Requisito mínimo

- Fluxo/contrato afetado: build e load de conteúdo via Addressables por mini‑game (manifesto + runtime).
- Pronto quando:
  - cada minigame possui `content_version` e isso é registrado em logs de match.
  - runtime carrega e descarrega Addressables no OnGameEnd/volta ao hub.
  - validação de memória registra antes/depois (pico + GC) sem crescimento linear em 10 ciclos.
- Riscos: leaks de Addressables, aumento de tempo de load, regressão de memória GC.

Itens

Padronizar content_version por mini‑game e amarrar isso ao manifesto (mesmo que inicialmente “interno”).

Pipeline de build de conteúdo (Addressables) por mini‑game:

gera catálogos por mini‑game

empacota/armazenamento simples (staging/local)

Runtime:

load de cena/prefabs via Addressables

unload/cleanup garantido no OnGameEnd e na volta ao hub

Validação de memória:

“antes/depois” de uma partida (pelo menos logs de pico + GC + contagem de objetos críticos)

DoD

Entrar/jogar/sair 10x seguidas sem crescimento linear de memória.

Conteúdo tem versão rastreável e dá pra saber “qual conteúdo” estava rodando em cada match. 

Comandos de revalidação

.\scripts\check.ps1
.\scripts\test.ps1
.\scripts\build-content.ps1
.\scripts\build-client.ps1
.\scripts\smoke-e2e.ps1

SPRINT FEITO POR COMPLETO
Evidências:
- logs/w8_check.log
- logs/w8_test.log
- logs/w8_build_content_retry.log
- logs/w8_build_client.log
- logs/w8_smoke.log

ADR-0004-addressables

 

BuildAndRelease

 

PerformanceBudgets

 

MinigameContract

Sprint 9 — Gate de Qualidade para Mini‑games (Conformidade)

Objetivo: evitar que cada mini‑game novo quebre o runtime/rede.

Requisito mínimo

- Fluxo/contrato afetado: conformidade do MinigameContract + smoke de replicação.
- Pronto quando:
  - suíte de conformidade roda em CI/local e falha se lifecycle não completar.
  - smoke de replicação valida snapshots e retorno ao hub.
  - logs de falha trazem match_id/minigame_id/build_version.
- Riscos: falsos positivos, tempo de CI elevado, flaky por rede.

Itens

Implementar uma suíte de conformidade do contrato:

carrega manifesto

roda lifecycle (OnLoad/OnGameStart/OnTick/OnGameEnd)

simula 2–4 players (bots simples)

valida ausência de exceptions e resultado consistente

Rodar isso no CI como gate de PR.

Adicionar “smoke test” de replicação (mínimo): entrar → receber snapshots → encerrar → voltar.

DoD

Todo mini‑game interno precisa passar na suíte para ser mergeado.

Falhas geram log com match_id/minigame_id/build_version suficiente para corrigir. 

Comandos de revalidação

.\scripts\check.ps1
.\scripts\test.ps1
.\scripts\smoke-network.ps1
.\scripts\smoke-e2e.ps1

SPRINT FEITO POR COMPLETO
Evidências:
- logs/w9_check.log
- logs/w9_test.log
- logs/w9_smoke_network.log
- logs/w9_smoke.log

MinigameContract

 

Telemetry

 

ADR-0006-observability

Sprint 10 — Kit do Criador Interno + 1 Mini‑game Novo

Objetivo: provar que “mini‑game plugável” é real e acelerar criação.

Requisito mínimo

- Fluxo/contrato afetado: criação de mini‑game (template + helpers) e adição de novo mini‑game seguindo MinigameContract.
- Pronto quando:
  - template gera manifesto/asmdef/settings e passa validação.
  - helpers de HUD/scoreboard e eventos comuns reutilizáveis.
  - novo mini‑game pequeno adicionado sem mudanças invasivas no core e passa gate do Sprint 9.
- Riscos: acoplamento ao core, bagunça de shared utils, falhas no contrato.

Itens

Template de mini‑game:

manifesto pronto

estrutura de pastas/asmdef

settings via ScriptableObject (tuning) e validações

“Minigame Kit” (runtime helpers):

scoreboard/hud hooks padronizados

eventos comuns (round start/end, countdown, etc.)

Criar 1 mini‑game novo (escopo pequeno, 5–10 min):

ex.: King of the Hill / Coin Rush / Tag

sem mudança no core (ou mudança mínima documentada)

DoD

Novo mini‑game adicionado seguindo contrato e passando no gate do Sprint 9.

Reuso controlado (evitar virar “Minigames/Shared” bagunçado). 

Comandos de revalidação

.\scripts\check.ps1
.\scripts\test.ps1
.\scripts\validate-manifests.ps1
.\scripts\smoke-e2e.ps1

SPRINT FEITO POR COMPLETO
Evidências:
- logs/w10_check.log
- logs/w10_test_retry.log
- logs/w10_validate_manifests.log
- logs/w10_smoke.log

MinigameContract

 

Architecture

 

AssistantContext

Sprint 11 — Infra/Orquestração v2 (Escala simples)

Objetivo: sair do “1 sala = 1 host” e ir para “N salas por host”, com proteção de recursos.

Requisito mínimo

- Fluxo/contrato afetado: alocação de salas pelo matchmaker + supervisão de processos do host.
- Pronto quando:
  - quota de salas por host respeitada com restart/backoff por sala.
  - limpeza de zumbis baseada em heartbeat continua ativa.
  - rate limit e logs por rota ativados no matchmaker.
- Riscos: spawn falhar, vazamento de processos, regressão de capacidade.

Itens

Host supervisor (ou camada no matchmaker) para:

spawn/kill de processos headless

quota de salas por host

restart policy com backoff

Matchmaker:

seleção de host

limpeza de zumbis baseada em heartbeat

timeouts/retries alinhados com client

Rate limit no matchmaker (mínimo), e logs por rota.

DoD

Um host roda N salas (definir N por CPU/memória) e se recupera de crash de uma sala sem matar as outras.

Matchmaker não acumula salas zumbis. 

Comandos de revalidação

.\scripts\check.ps1
.\scripts\test.ps1
.\scripts\smoke-e2e.ps1

SPRINT FEITO POR COMPLETO
Evidências:
- logs/w11_check.log
- logs/w11_test.log
- logs/w11_smoke.log

Infrastructure

 

MatchmakingSpec

 

ADR-0007-matchmaking-backend

Sprint 12 — LiveOps de Beta (controle + operação)

Objetivo: operar beta sem “deploy a cada ajuste”.

Requisito mínimo

- Fluxo/contrato afetado: configuração remota (flags) e fallback de matchmaking.
- Pronto quando:
  - configuração remota permite rotação/bloqueio de minigames e parâmetros sem rebuild.
  - fallback automático entra em ação com crash rate alto.
  - load test registra métricas consolidadas.
- Riscos: config inválida, fallback agressivo, rate limit mal calibrado.

Itens

Feature flags / remote config (mínimo):

rotação de mini‑games (pool do quick play)

parâmetros de match (max players, duration, etc.)

Controles de rollout:

bloquear minigame_id específico

fallback automático para outro mini‑game se crash rate subir

Load test de beta:

X matches simultâneos

métricas consolidadas (crash rate, latency, tick, bytes/s)

Runbook atualizado com “top 10 incidentes” e como diagnosticar.

DoD

Dá pra ativar/desativar mini‑games e ajustar parâmetros sem rebuild do client.

Operação consegue tomar decisão com dashboard/logs (sem reproduzir local). 

Comandos de revalidação

.\scripts\check.ps1
.\scripts\test.ps1
.\scripts\scale-smoke.ps1
.\scripts\smoke-e2e.ps1

SPRINT FEITO POR COMPLETO
Evidências:
- logs/w12_check.log
- logs/w12_test.log
- logs/w12_scale_smoke.log
- logs/w12_smoke.log

Telemetry

 

BuildAndRelease

 

Infrastructure

Resultado esperado ao fim do Pacote 2

Conteúdo versionado por mini‑game (base do hot‑load de assets)

Rede com contrato/versionamento real e sob budget

“Gate” que impede regressão ao adicionar mini‑games

Infra capaz de crescer sem mudar arquitetura

Operação de Beta com alavancas (flags/rotação/fallback)

Tudo isso é coerente com a arquitetura de isolamento + lifecycle + versionamento + caminho para UGC. 

Architecture

 

MinigameContract

 

ADR-0005-scripting-ugc
