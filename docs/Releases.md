# Releases — Baselines e Evidencias

## 2026-02-13 — baseline-green-2026-02-13
- Tag: `baseline-green-2026-02-13`
- Commit: `f435e5f5847ff5bbaf0637478bfa9b027efaa679`
- Escopo: estabilizacao do build Android em batchmode com guarda de modulo Android.

### Comandos executados
- `.\scripts\build-android.ps1`
- `.\scripts\smoke-mobile.ps1`

### Resultado
- `build-android`: `Passed`
- `smoke-mobile`: `Passed`

### Evidencias
- `logs/build_android_results_20260213_094751.log`
- `logs/smoke_mobile_results_20260213_095714.log`
- `logs/unity-build-android.log` com `BuildAndroid: result=Succeeded`
- `logs/unity-smoke-mobile.log` com `mobile_smoke_ok`
- `artifacts/builds/android/MinigameClient.apk` (gerado em `2026-02-13 09:56:40`)
