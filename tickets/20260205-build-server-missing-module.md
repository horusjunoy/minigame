# Build server falha por modulo Dedicated Server ausente
Assinatura: codex

## Contexto
- minigame_id: stub_v1
- fluxo: BuildAndRelease (server headless)
- ambiente: Windows 11, Unity 2022.3.62f3 (LTS), build_version 0.1.0-dev

## Comandos executados
```powershell
.\scripts\build-server.ps1
```

## Erro/stacktrace completo
```text
IOException: Failed to Copy File / Directory from 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Data/PlaybackEngines/WindowsStandaloneSupport/Variations/win64_server_nondevelopment_mono/WindowsPlayer.exe' to 'Temp/StagingArea/WindowsPlayer.exe'.
```

## Caminhos de log + tail 200
- `.\logs\build_server_20260205_162126.log`
- `.\logs\unity-build-server.log`

Trecho final (unity-build-server):
```text
IOException: Failed to Copy File / Directory from 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Data/PlaybackEngines/WindowsStandaloneSupport/Variations/win64_server_nondevelopment_mono/WindowsPlayer.exe' to 'Temp/StagingArea/WindowsPlayer.exe'.
Build failed: Failed
```

## Esperado
Build do server headless gerar artefatos em `.\artifacts\builds\server\`.

## Impacto
Build do server bloqueado, DoD da Sprint 2/3 nao atende.

## Hipotese inicial (testavel)
Modulo **Windows Dedicated Server Build Support** nao esta instalado para Unity 2022.3.62f3.

## Proximo teste isolado sugerido
1) Instalar no Unity Hub o modulo **Windows Dedicated Server Build Support** para 2022.3.62f3.
2) Rodar:
```powershell
.\scripts\build-server.ps1
```

## DoD da correcao
- build-server sai com exit code 0
- artefatos do server gerados
- logs/telemetria nao regrediram
