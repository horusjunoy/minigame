# CreatorQuickstart — Mini-game SDK (Base)

## 1) Estrutura minima
- Crie uma pasta em `Assets/Game/Minigames/<Nome>/`.
- Inclua um manifesto `<Nome>.manifest.json`.
- Implemente um `IMinigame` server-side.

## 2) Manifesto (exemplo)
```json
{
  "schema_version": 1,
  "id": "minigame_exemplo_v1",
  "display_name": "Exemplo",
  "version": "0.1.0",
  "server_entry": "Game.Minigames.Exemplo.ExemploMinigame, Game.Minigames.Exemplo",
  "client_entry": "",
  "addressables": {
    "scenes": [],
    "prefabs": []
  },
  "settings": {
    "match_duration_s": 300,
    "score_to_win": 10
  }
}
```

## 3) Validacao
Rode:
```
.\scripts\validate-manifests.ps1
```

## 4) Contrato
O contrato de lifecycle e APIs esta em `docs/MinigameContract.md`.
