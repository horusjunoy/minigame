# PlayMode batchmode hang in NetworkSmokePlayModeTests — codex

## contexto
- minigame_id: stub_v1
- fluxo: PlayMode test (network handshake) via batchmode
- ambiente: Windows 11, Unity 2022.3.62f3, build_version 0.1.0-dev

## comandos executados
- .\scripts\test-playmode.ps1 2>&1 | Tee-Object .\logs\w01_test_playmode.log
- taskkill /F /IM Unity.exe

## erro/stacktrace
- Unity batchmode nao finaliza; processo fica rodando e precisa ser encerrado manualmente.
- sem stacktrace explicita (apenas log de execucao longo)

## logs
- logs/w01_test_playmode.log
- logs/test_playmode_20260206_215506.log
- logs/unity-tests-playmode.log

## esperado
PlayMode test concluir automaticamente e gerar artifacts\playmode-test-results.xml com resultado.

## impacto
Bloqueia automacao de validacao de handshake em CI/local batchmode.

## hipotese inicial
O TestRunnerApi em batchmode nao esta encerrando o PlayMode (EditorApplication.playModeState não retorna / teste deixa objetos vivos), fazendo o Unity nao sair mesmo com -quit.

## proximo teste isolado sugerido
Rodar o test no Test Runner do editor para verificar se o teste completa e observar onde o PlayMode fica preso.

## DoD da correção
- PlayMode test conclui em batchmode sem intervenção manual.
- artifacts\playmode-test-results.xml é gerado.
- suite de testes (scripts/test.ps1) continua verde.
- logs/telemetria não regrediram.
