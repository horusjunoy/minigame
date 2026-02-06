# RepoStructure — Organização do Repositório (Unity)

## 1) Objetivo
Manter um monorepo simples no MVP e modularizado para evoluir para multiplos serviços.

---

## 2) Estrutura sugerida (Unity)
```
Assets/
  Game/
    Core/                 # domínio puro (sem UnityEngine quando possível)
    Runtime/              # lifecycle + APIs de mini-game
    Network/              # abstração de rede, serialização, snapshots
    Server/               # sistemas autoritativos
    Client/               # UI, câmera, input, FX
    Minigames/
      Arena/              # mini-game exemplo (assets + scripts)
      Shared/             # utilitários comuns entre mini-games (com cuidado!)
  Art/
  Audio/
  Prefabs/
  Scenes/
  Settings/
Packages/
ProjectSettings/
docs/
```
**Regras:**
- Cada mini-game em `Assets/Game/Minigames/<Name>/` com seu manifesto.
- `Minigames/Shared` só para coisas realmente genéricas (evitar acoplamento).

---

## 3) Assembly Definitions (asmdef)
- `Game.Core.asmdef`
- `Game.Runtime.asmdef`
- `Game.Network.asmdef`
- `Game.Server.asmdef`
- `Game.Client.asmdef`
- `Game.Minigames.Arena.asmdef`
- `Game.Minigames.Shared.asmdef` (se necessário)

**Objetivo:** isolar dependências e reduzir tempo de compilação.

---

## 4) Pastas para serviços externos (futuro)
Quando separar repos ou subprojetos:
```
/services/matchmaker
/services/metrics
/tools
```
Mas no MVP, manter simples para velocidade.
