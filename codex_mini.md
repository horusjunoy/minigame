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
- Se a Sprint nao tiver Requisito minimo + DoD + comandos de revalidacao: primeiro complete a Sprint (patch), nao implemente codigo.
- Nao iniciar proxima Sprint sem a atual 100% verde.
- Você irá executar no terminal os testes, e os testes precisam aparecer no terminal o nome do teste e se passou ou deu falha, apenas isso. 
- Ao finalizar o teste, aparecer no terminal finalizado. E quando não passar, logar o erro em /logs para você mesmo ajustar.
- Um problema por commit; mudanca minima e segura.

---

# SPRINTS
Épico inaugural (E0): Fundação do Projeto (“Repo Acelerador”)
git clone https://github.com/seu-org/minigame-platform-template.git
Objetivo do épico (1 frase)

Ter um repositório que compila, builda e roda local (client + server headless), com estrutura modular, scripts, CI e logs/telemetria mínimos, para destravar o M1+ sem dor. 

AssistantContext

 

BuildAndRelease

 

Telemetry

Entregáveis do épico (o que “existe” ao final)

Estrutura do repo + módulos + asmdefs (Core/Runtime/Network/Server/Client/Minigames) como base padrão. 

RepoStructure

 

Architecture

Pipeline de build: client build + server headless build (e espaço para content build). 

BuildAndRelease

Scripts de execução e evidência (logs) no estilo “método do quadrado” (rodar e gerar logs sempre). 

metodo_do_quadrado

Observabilidade mínima: logs estruturados com IDs obrigatórios e eventos mínimos (server/client). 

Telemetry

 

ADR-0006-observability

Docs e contratos vivos: docs/ com contexto + arquitetura + contrato do minigame (pra ninguém reinventar a roda). 

AssistantContext

 

Architecture

 

MinigameContract

Fora do escopo (pra não explodir)

Escolha definitiva de networking (Mirror vs FishNet) — aqui a gente só cria a abstração pra não acoplar. 

ADR-0002-networking

Matchmaker “de verdade” (pode ter stub/local só pra smoke test depois). 

MatchmakingSpec

Gameplay / minigame exemplo completo (isso é épico posterior). 

ProductVision

Sprints sugeridas dentro do Épico E0

A ideia é cada sprint terminar com algo rodando + comprovável por comando + log, no padrão DoD do projeto. 

AssistantContext

 

metodo_do_quadrado

Sprint 1 — “Bootstrap do repo + esqueleto Unity modular”

Objetivo: abrir o projeto e já ter a base “certa” (estrutura + assemblies + contratos), sem feature ainda.

STATUS: CONCLUIDA (2026-02-06)
Evidencia:
- check: .\\scripts\\check.ps1
- test: .\\scripts\\test.ps1
- build-client: .\\scripts\\build-client.ps1
- build-server: .\\scripts\\build-server.ps1

Requisito minimo:
- Fluxo/contrato: Runtime/MinigameContract + estrutura de projeto (sem gameplay).
- Pronto quando: projeto Unity abre e compila; existe minigame stub com lifecycle logado; README inicial explica como abrir/rodar.
- Riscos: setup Unity/URP inconsistente; asmdefs com dependencias ciclicas; contrato do minigame divergente do docs.

Backlog (bem direto):

Criar projeto Unity (URP) e baseline de settings (sem travar em patch). 

ADR-0001-unity-version

Aplicar RepoStructure: pastas Assets/Game/{Core,Runtime,Network,Server,Client,Minigames}. 

RepoStructure

Criar asmdefs por módulo (Core/Runtime/Network/Server/Client + Minigame demo). 

RepoStructure

Colocar skeleton do MinigameContract (interfaces + DTOs básicos) e um “minigame vazio” que só loga lifecycle (OnLoad/OnGameStart/OnTick/OnGameEnd). 

MinigameContract

Padrões de código e lint/format básicos (StyleGuide + .editorconfig). 

StyleGuide

DoD da Sprint 1 (checklist):

Projeto abre e compila sem warnings críticos.

Existe um minigame “stub” que roda local (mesmo sem rede) e imprime lifecycle em log.

Documentação inicial do repo (README mínimo: como abrir/rodar). 

AssistantContext

Comandos de revalidacao:
1) Abrir/compilar (headless):
   - Unity -batchmode -quit -projectPath . -logFile logs/unity-compile.log
2) Executar o minigame stub no editor (manual):
   - Abrir a cena base e iniciar Play; verificar logs do lifecycle (OnLoad/OnGameStart/OnTick/OnGameEnd).

Sprint 2 — “Builds + scripts + CI (o acelerador de verdade)”

Objetivo: qualquer pessoa do time roda o projeto do zero com 1–2 comandos, e o CI garante que não quebramos.

STATUS: CONCLUIDA (2026-02-06)
Evidencia:
- check: .\\scripts\\check.ps1
- test: .\\scripts\\test.ps1
- build-client: .\\scripts\\build-client.ps1
- build-server: .\\scripts\\build-server.ps1

Requisito minimo:
- Fluxo/contrato: BuildAndRelease + scripts oficiais + logs (metodo_do_quadrado).
- Pronto quando: scripts `check/test/build-client/build-server` executam e geram logs; CI executa o pipeline.
- Riscos: builds inconsistentes entre maquinas; logs incompletos; CI faltando artefatos.

Backlog:

Implementar scripts (PowerShell) no padrão do projeto:

scripts/check.ps1 (format/lint/validações)

scripts/test.ps1 (unit tests Core)

scripts/build-client.ps1

scripts/build-server.ps1

tudo gerando log em ./logs/ 

metodo_do_quadrado

Implementar build do server headless + build do client conforme BuildAndRelease. 

BuildAndRelease

Subir CI mínimo: rodar check + testes + build (artefatos). 

BuildAndRelease

Estruturar versionamento mínimo (build_version / protocol_version placeholders). 

BuildAndRelease

DoD da Sprint 2:

Rodar local:

.\scripts\check.ps1

.\scripts\test.ps1

.\scripts\build-client.ps1

.\scripts\build-server.ps1
e sair com logs salvos. 

metodo_do_quadrado

CI executa esses passos em PR/push e falha se algo quebrar. 

BuildAndRelease

Comandos de revalidacao:
1) .\scripts\check.ps1
2) .\scripts\test.ps1
3) .\scripts\build-client.ps1
4) .\scripts\build-server.ps1

Sprint 3 — “Observabilidade mínima + smoke tests do runtime”

Objetivo: ter diagnóstico desde o começo (pra multiplayer isso é ouro) + testes mínimos de conformidade do runtime/minigame.

STATUS: CONCLUIDA (2026-02-06)
Evidencia:
- test: .\\scripts\\test.ps1
- smoke: .\\scripts\\smoke.ps1

Requisito minimo:
- Fluxo/contrato: Telemetry + MinigameContract + Runtime.
- Pronto quando: logs estruturados com IDs obrigatorios; eventos minimos do server; teste de conformidade do minigame passa.
- Riscos: logs sem IDs; eventos incompletos; teste flakey ou acoplado demais.

Backlog:

Logs estruturados (JSON) com IDs obrigatórios (match_id, player_id, minigame_id, etc.). 

Telemetry

Emitir eventos mínimos no server: minigame_loaded, minigame_error, match_started, match_ended (mesmo que “match” ainda seja local). 

Telemetry

Marcar budget/limite básico (ex.: tick budget e logar tick_over_budget). 

PerformanceBudgets

Criar teste de conformidade do minigame (carrega, simula ticks, garante que não explode). 

MinigameContract

(Opcional leve) Um “smoke scene” no editor que roda o runtime + minigame stub e encerra.

DoD da Sprint 3:

Se der erro, dá pra responder “por quê” olhando logs (com match_id etc.). 

Telemetry

Existe um teste automatizado garantindo que o contrato do minigame não quebrou. 

MinigameContract

Comandos de revalidacao:
1) .\scripts\test.ps1
2) .\scripts\smoke.ps1 (ou teste de conformidade do runtime)

Por que esse épico primeiro (e por que ele acelera mesmo)

Ele materializa as regras “não negociáveis”: modularização, server-authoritative (sem duplicar regra no client), e base pronta pra runtime plugável. 

Architecture

 

ADR-0003-authority-model

Ele instala desde cedo o “modo multiplayer profissional”: logs + IDs + CI (ADR-0006). 

ADR-0006-observability

Ele reduz o risco do “vamos codar e ver”: com scripts + logs, toda falha vira evidência reproduzível. 

metodo_do_quadrado

Gancho pro próximo épico (só pra já deixar o trilho)

Depois do E0, o natural é:

E1 (M1): Multiplayer básico (client/server conectando com INetworkFacade sem acoplar) 

ADR-0002-networking

E2 (M2): Sala/Match + matchmaker (create/join/end + tokens) 

MatchmakingSpec

E3 (M3): Runtime v1 (carregar minigame de manifesto, lifecycle completo) 

MinigameContract

Se você topar essa linha, eu chamaria o épico inaugural literalmente de “E0 — Repo Acelerador (Fundação + Build + CI + Observabilidade)” e já colocaria essas 3 sprints como “pacote fechado” pra garantir que o projeto começa com o pé no chão.
