# Build client trava em batchmode
Assinatura: codex

## Contexto
- minigame_id: stub_v1
- fluxo: BuildAndRelease (build client)
- ambiente: Windows 11, Unity 2022.3.62f3 (LTS), build_version 0.1.0-dev

## Comandos executados
```powershell
.\scripts\build-client.ps1
```

## Erro/stacktrace completo
Sem stacktrace. O Unity fica em batchmode compilando e nao encerra; foi necessario finalizar o processo manualmente.

## Caminhos de log + tail 200
- `.\logs\build_client_20260205_114233.log`
- `.\logs\unity-build-client.log`

Trecho final (unity-build-client):
```text
DisplayProgressbar: Compiling Scripts
... (ILPP/bee_backend)
```

## Esperado
Build client finalizar e encerrar o Unity com exit code 0, gerando artifacts em `artifacts/builds/client/`.

## Impacto
Pipeline de build bloqueado.

## Hipotese inicial (testavel)
Unity nao encerra em batchmode apos build; necessario chamar `EditorApplication.Exit` no metodo de build (ajuste ja feito em `Assets/Game/Editor/BuildScripts.cs`).

## Proximo teste isolado sugerido
```powershell
.\scripts\build-client.ps1
```

## DoD da correcao
- build-client termina com exit code 0
- artifacts presentes
- logs/telemetria nao regrediram
