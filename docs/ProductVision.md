# ProductVision — Plataforma de Mini-games (UGC Runtime)

## 1) Problema que resolvemos
Jogadores querem experiências rápidas multiplayer (5–15 min). Criadores (futuro) querem publicar mini-games dentro do ecossistema sem reinventar rede, sala, scoring e ciclo de vida.

## 2) O produto (definição simples)
- Um **hub** (lobby) + sistema de **salas/instâncias**.
- Cada sala executa **um mini-game** específico.
- Mini-game = pacote isolado com manifesto + assets + lógica.
- O **core** fornece rede, players, spawn, scoring e ciclo de vida.

## 3) Público-alvo
- Jogadores: entram e jogam experiências rápidas.
- Criadores (futuro): usam SDK/API para criar mini-games e publicar.

## 4) Metas do MVP
- Plataforma inicial: PC (Windows) para acelerar.
- Multiplayer: instâncias/salas (match). Nada persistente.
- 8–16 jogadores por sala.
- Servidor headless por sala roda estável por ≥ 60 minutos.
- Loop completo: entrar → criar/entrar sala → jogar → terminar → voltar ao lobby.
- Mini-game plugável: adicionar novo mini-game sem mexer no core (ou mudanças mínimas documentadas).
- Logs/telemetria básica (salas sobem/caem, latência média, erros).

## 5) Não-objetivos do MVP (evitar escopo infinito)
- MMO persistente / mundo aberto persistente.
- Física complexa com simulação pesada no servidor.
- Marketplace aberto para UGC no início.
- Editor completo para criadores no começo.

## 6) Visão pós-MVP (produto final)
- Mobile (Android/iOS) como plataforma principal.
- UGC: terceiros criam e publicam mini-games.
- Runtime com suporte a **conteúdo hot-load** (assets) e **lógica sandbox** (script interpretado) sem depender de JIT.
- Catálogo de mini-games, moderação, métricas por mini-game, e economia/skins.

## 7) Roadmap de marcos (alto nível)
- M0: Fundação (repo, build, CI, estrutura)
- M1: Multiplayer básico (server + client conectando e sincronizando)
- M2: Sala/Match (criar sala, entrar, sair, iniciar partida)
- M3: Runtime v1 (mini-game carregável + lifecycle)
- M4: Mini-game exemplo (arena/race) + loop completo
- M5: Hardening (logs, perf, testes, deploy básico)
