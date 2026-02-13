# BuildAndRelease — Builds, Versionamento e Releases

## 1) Objetivo
Ter um pipeline previsível para:
- Client (PC → futuramente Android/iOS)
- Server headless por sala
- Conteúdo (Addressables) versionado por mini-game

---

## 2) Artefatos
- **Client Build**: executável + versão
- **Server Build**: headless Linux/Windows (decidir)
- **Content Build**: catálogos Addressables por mini-game

---

## 3) Versionamento (sugestão)
- `build_version`: semver do produto (ex.: 0.1.0)
- `content_version`: semver por mini-game (ex.: arena 0.1.3)
- `protocol_version`: inteiro (incrementa quando quebra compatibilidade)

---

## 4) CI (mínimo)
- Lint/format
- Unit tests (Core)
- Build client
- Build server headless
- Build Android (dev): `.\scripts\build-android.ps1`
- Build content (Addressables catalog): `.\scripts\build-content.ps1`
- Smoke mobile de fluxo curto (hub -> partida -> resultados -> hub): `.\scripts\smoke-mobile.ps1`

---

## 5) Release (MVP)
- Tags no git para builds estáveis.
- Changelog básico.
- Checklist de “go/no-go”:
  - servidor roda 60 min com 8–16 players
  - sem crash loops
  - logs de erro analisáveis

---

## 6) Mobile
- Build Android automatizado via script de CI (`.\scripts\build-android.ps1`).
- Quando o modulo Android nao estiver instalado, o build falha com erro claro no log.
- Smoke mobile em batch para validar loop basico sem device farm (`.\scripts\smoke-mobile.ps1`).
- iOS ainda depende de runner macOS dedicado.

### 6.1) Mobile Gate em CI (self-hosted)
- Workflow dedicado: `.github/workflows/unity-mobile-gate.yml`.
- Ativacao: criar variavel de repositório `UNITY_CI_ENABLED=true`.
- Runner requerido: self-hosted Windows com labels `self-hosted`, `windows`, `unity`.
- Secret requerido: `UNITY_PATH` apontando para o `Unity.exe` instalado no runner.
- Gate executado quando ativo:
  - `.\scripts\build-android.ps1`
  - `.\scripts\smoke-mobile.ps1`
