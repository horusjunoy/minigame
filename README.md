# Minigame Platform (E0)

## Objetivo
Repositório base da plataforma multiplayer de mini-games com estrutura modular, runtime plugável e scripts de build/teste.

## Requisitos
- Windows
- Unity LTS (2022.3.x recomendado) com URP instalado
- PowerShell 7+ (ou Windows PowerShell)

## Setup rápido
1) Clone o repo e abra no Unity Hub.
2) Aponte o Unity Editor via variável:
   - `setx UNITY_PATH "C:\Program Files\Unity\Hub\Editor\2022.3.x\Editor\Unity.exe"`

## Scripts principais
- `.\scripts\check.ps1`
- `.\scripts\test.ps1`
- `.\scripts\build-client.ps1`
- `.\scripts\build-server.ps1`

Todos geram logs em `.\logs\`.

## Estrutura
Veja `docs/RepoStructure.md` e `docs/Architecture.md`.
