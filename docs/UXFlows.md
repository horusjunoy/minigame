# UXFlows — Telas e Fluxos (MVP)

## 1) Telas mínimas
1. **Hub/Lobby**
   - Botão: “Jogar” (quick play)
   - Lista de salas (opcional)
   - Criar sala (opcional)
2. **Sala (pré-jogo)**
   - Lista de jogadores
   - Status: aguardando / iniciando
   - Botão: sair
3. **HUD em partida**
   - Timer
   - Score (top 3 + você)
   - Indicadores de evento (ex.: “+1 ponto”)
4. **Resultados**
   - Rank final, pontuação
   - Botões: “Jogar de novo” / “Voltar ao lobby”

---

## 2) Fluxo principal (happy path)
Hub → Criar/Entrar Sala → Conectar → Loading → Partida → Resultados → Hub

---

## 3) Erros e estados
- “Servidor indisponível”: oferecer voltar ao hub e tentar outra sala.
- “Token expirou”: re-solicitar join ao matchmaker.
- “Desconectou”: reconectar (opcional MVP) ou voltar pro hub.

---

## 4) Regras de UX para mobile (futuro)
- UI responsiva (safe areas)
- Botões grandes (touch)
- Feedback visual forte (vibração opcional, flashes leves)
