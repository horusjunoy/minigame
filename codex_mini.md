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

Itens

Handshake com protocol_version e build_version (bloquear mismatch com erro claro no client).

Consolidar a camada de abstração de rede (INetworkFacade/INetworkTransport) para reduzir acoplamento (e permitir decidir Mirror vs FishNet sem reescrever o core).

Instrumentar bytes/s por player e tick_ms por match (com sampling em debug).

Ajustes rápidos de replicação para bater budget (quantização simples, reduzir frequência de snapshots por entidade, etc.).

DoD

Cliente incompatível recebe mensagem clara e volta ao hub (sem travar).

16 jogadores: tráfego e tick dentro do budget definido.

Logs e métricas incluem ids de correlação (match/player/minigame). 

BuildAndRelease

 

ADR-0002-networking

 

PerformanceBudgets

 

Telemetry

Sprint 8 — Pipeline de Conteúdo por Mini‑game (Addressables)

Objetivo: permitir evoluir mini‑games e assets sem dor e sem leak de memória ao voltar pro lobby.

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

ADR-0004-addressables

 

BuildAndRelease

 

PerformanceBudgets

 

MinigameContract

Sprint 9 — Gate de Qualidade para Mini‑games (Conformidade)

Objetivo: evitar que cada mini‑game novo quebre o runtime/rede.

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

MinigameContract

 

Telemetry

 

ADR-0006-observability

Sprint 10 — Kit do Criador Interno + 1 Mini‑game Novo

Objetivo: provar que “mini‑game plugável” é real e acelerar criação.

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

MinigameContract

 

Architecture

 

AssistantContext

Sprint 11 — Infra/Orquestração v2 (Escala simples)

Objetivo: sair do “1 sala = 1 host” e ir para “N salas por host”, com proteção de recursos.

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

Infrastructure

 

MatchmakingSpec

 

ADR-0007-matchmaking-backend

Sprint 12 — LiveOps de Beta (controle + operação)

Objetivo: operar beta sem “deploy a cada ajuste”.

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