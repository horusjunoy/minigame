# Runbook VM (Infra MVP)

## Objetivo
Padronizar execução do Matchmaker e Server headless em VM, com health endpoints e política de restart.

## Pré-requisitos
- Windows 11/Server (MVP)
- Unity 2022.3.x instalado
- Node.js 18+
- Portas liberadas: 8080 (matchmaker), 7770 (server), 18080 (health server)

## Variáveis de ambiente
- `UNITY_PATH`: caminho completo para o `Unity.exe`
- `MATCHMAKER_PORT`: porta do serviço
- `MATCHMAKER_SECRET`: segredo HMAC
- `MATCHMAKER_DEFAULT_ENDPOINT`: endpoint do server (ex: `127.0.0.1:7770`)
- `SERVER_HEALTH_ENABLE`: `1` para habilitar health endpoint do server
- `SERVER_HEALTH_PORT`: porta do health endpoint (default 18080)

## Subir local (manual)
1. Matchmaker
```
node services/matchmaker/index.js
```
2. Server headless (Unity)
```
$env:SERVER_HEALTH_ENABLE="1"
$env:SERVER_HEALTH_PORT="18080"
& $env:UNITY_PATH -batchmode -quit -projectPath C:\Users\horus.junoy\minigame -executeMethod Game.Editor.RuntimeSmokeRunner.Run
```
Observacao: o health do server eh inicializado pelo `RuntimeSmokeRunner` quando `SERVER_HEALTH_ENABLE=1`.

## Health endpoints
- Matchmaker: `GET http://127.0.0.1:8080/health`
- Server: TCP/HTTP `http://127.0.0.1:18080/` (retorna JSON simples)

## Restart policy (Windows)
### Opção A: Task Scheduler
- Criar tarefa para iniciar o matchmaker e o server no boot.
- Em **Settings**, habilitar `Restart the task if it fails` (intervalo 1 min).

### Opção B: NSSM (recomendado)
1. Instalar `nssm`.
2. Registrar serviço do matchmaker:
```
nssm install Matchmaker "C:\Program Files\nodejs\node.exe" "C:\Users\horus.junoy\minigame\services\matchmaker\index.js"
```
3. Registrar serviço do server headless (ajustar UNITY_PATH):
```
nssm install MinigameServer "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe" "-batchmode -quit -projectPath C:\Users\horus.junoy\minigame -executeMethod Game.Editor.RuntimeSmokeRunner.Run"
```
4. Configurar `Recovery` para `Restart the Service`.

## Observabilidade
- Logs em `logs/` (matchmaker, soak, testes).
- Server health endpoint responde JSON com `status`, `uptime_s`, `build_version`.
