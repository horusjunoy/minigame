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
- (Opcional) build Addressables

---

## 5) Release (MVP)
- Tags no git para builds estáveis.
- Changelog básico.
- Checklist de “go/no-go”:
  - servidor roda 60 min com 8–16 players
  - sem crash loops
  - logs de erro analisáveis

---

## 6) Mobile (futuro)
- Automatizar builds Android (CI) e iOS (mac runner).
- Controle de quality settings por plataforma.
