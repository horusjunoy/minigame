# Testes EditMode nao geram test-results.xml
Assinatura: codex

## Contexto
- minigame_id: stub_v1
- fluxo: Runtime/MinigameContract (testes de conformidade)
- ambiente: Windows 11, Unity 2022.3.62f3 (LTS), build_version 0.1.0-dev

## Comandos executados
```powershell
.\scripts\test.ps1
```

## Erro/stacktrace completo
Nao ha stacktrace. O Unity encerra com exit code 0, mas o arquivo `artifacts\test-results.xml` nao e gerado.

## Caminhos de log + tail 200
- `.\logs\test_20260205_113718.log`
- `.\logs\unity-tests.log`

Trecho final (unity-tests):
```text
Batchmode quit successfully invoked - shutting down!
...
Exiting batchmode successfully now!
Exiting without the bug reporter. Application will terminate with return code 0
```

## Esperado
Arquivo `artifacts\test-results.xml` gerado com os resultados dos testes EditMode.

## Impacto
Sem evidencia de testes, o pipeline nao atende o DoD.

## Hipotese inicial (testavel)
Os testes nao estao sendo descobertos pelo Unity Test Runner (assembly nao marcada corretamente para testes EditMode ou localizacao/asmdef incorretos).

## Proximo teste isolado sugerido
1) Garantir asmdef com `testAssemblies=true` e `includePlatforms=["Editor"]` (ja aplicado).
2) Rodar novamente:
```powershell
.\scripts\test.ps1
```
3) Verificar se `artifacts\test-results.xml` foi criado.

## DoD da correcao
- teste isolado passa
- suite passa
- smoke E2E passa (quando aplicavel)
- logs/telemetria nao regrediram

## Status
Resolvido em 2026-02-05. Implementado runner de testes via `Game.Editor.BatchTestRunner` e ajuste no `scripts/test.ps1`.

## Evidencia da correcao
- Comando: `.\scripts\test.ps1`
- Logs:
  - `.\logs\test_20260205_161944.log`
  - `.\logs\test_results_20260205_161944.log`
  - `.\logs\unity-tests.log`
- Resultado: `artifacts\test-results.xml` gerado e testes listados no terminal.
