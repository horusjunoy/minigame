# Unity nao encontrado para scripts de teste/build
Assinatura: codex

## Contexto
- minigame_id: stub_v1
- fluxo: Hub/Sala/Partida (setup/runtime)
- ambiente: Windows, Unity (nao encontrado), build_version 0.1.0-dev

## Comandos executados
```powershell
.\scripts\test.ps1
.\scripts\build-client.ps1
.\scripts\build-server.ps1
.\scripts\smoke.ps1
```

## Erro/stacktrace completo
```text
Write-Error 'Unity Editor nao encontrado. Defina UNITY_PATH.'; exit 1 : Unity Editor nao encontrado. Defina UNITY_PATH.
```

## Caminhos de log + tail 200
- `.\logs\test_20260205_070959.log`
```text
cmd=Write-Error 'Unity Editor nao encontrado. Defina UNITY_PATH.'; exit 1
cwd=C:\Users\horus.junoy\minigame
commit=unknown
start=2026-02-05T07:09:59.4359156-03:00
Write-Error 'Unity Editor nao encontrado. Defina UNITY_PATH.'; exit 1 : Unity Editor nao encontrado. Defina UNITY_PATH.
    + CategoryInfo          : NotSpecified: (:) [Write-Error], WriteErrorException
    + FullyQualifiedErrorId : Microsoft.PowerShell.Commands.WriteErrorException
 
end=2026-02-05T07:10:03.3876051-03:00
duration_ms=3952
exit_code=1
```
- `.\logs\build_client_20260205_071017.log`
```text
cmd=Write-Error 'Unity Editor nao encontrado. Defina UNITY_PATH.'; exit 1
cwd=C:\Users\horus.junoy\minigame
commit=unknown
start=2026-02-05T07:10:17.5483700-03:00
Write-Error 'Unity Editor nao encontrado. Defina UNITY_PATH.'; exit 1 : Unity Editor nao encontrado. Defina UNITY_PATH.
    + CategoryInfo          : NotSpecified: (:) [Write-Error], WriteErrorException
    + FullyQualifiedErrorId : Microsoft.PowerShell.Commands.WriteErrorException
 
end=2026-02-05T07:10:21.2048419-03:00
duration_ms=3656
exit_code=1
```
- `.\logs\build_server_20260205_071034.log`
```text
cmd=Write-Error 'Unity Editor nao encontrado. Defina UNITY_PATH.'; exit 1
cwd=C:\Users\horus.junoy\minigame
commit=unknown
start=2026-02-05T07:10:34.6678776-03:00
Write-Error 'Unity Editor nao encontrado. Defina UNITY_PATH.'; exit 1 : Unity Editor nao encontrado. Defina UNITY_PATH.
    + CategoryInfo          : NotSpecified: (:) [Write-Error], WriteErrorException
    + FullyQualifiedErrorId : Microsoft.PowerShell.Commands.WriteErrorException
 
end=2026-02-05T07:10:38.3647587-03:00
duration_ms=3697
exit_code=1
```
- `.\logs\smoke_20260205_071049.log`
```text
cmd=Write-Error 'Unity Editor nao encontrado. Defina UNITY_PATH.'; exit 1
cwd=C:\Users\horus.junoy\minigame
commit=unknown
start=2026-02-05T07:10:49.0818977-03:00
Write-Error 'Unity Editor nao encontrado. Defina UNITY_PATH.'; exit 1 : Unity Editor nao encontrado. Defina UNITY_PATH.
    + CategoryInfo          : NotSpecified: (:) [Write-Error], WriteErrorException
    + FullyQualifiedErrorId : Microsoft.PowerShell.Commands.WriteErrorException
 
end=2026-02-05T07:10:51.9369364-03:00
duration_ms=2855
exit_code=1
```

## Esperado
Scripts devem localizar o Unity e executar testes/build/smoke com logs e resultados.

## Impacto
Bloqueia testes e builds locais/CI.

## Hipotese inicial (testavel)
Unity Editor nao esta instalado ou `UNITY_PATH` nao foi configurado no ambiente.

## Proximo teste isolado sugerido
```powershell
setx UNITY_PATH "C:\Program Files\Unity\Hub\Editor\2022.3.x\Editor\Unity.exe"
.\scripts\test.ps1
```

## DoD da correcao
- teste isolado passa
- suite passa
- smoke E2E passa (quando aplicavel)
- logs/telemetria nao regrediram
