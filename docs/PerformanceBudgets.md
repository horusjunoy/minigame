# PerformanceBudgets — Metas e Limites (PC agora, Mobile depois)

> Ajuste estes números quando tiver benchmarks reais. O importante é existir um budget desde o dia 1.

## 1) Metas por plataforma
### PC (MVP)
- FPS alvo: 60
- Tick do servidor: 20–30 Hz (dependendo do jogo)
- Latência aceitável: < 150ms (jogável), ideal < 80ms

### Mobile (futuro)
- FPS alvo: 60 (preferível) **ou** 30 (modo econômico) — decidir via ADR
- Memória: manter picos controlados; evitar spikes de GC

---

## 2) Budget de rede (por player)
- Snapshots: 10–20 Hz
- Tamanho alvo por snapshot: 200–800 bytes (depende do número de entidades)
- Eventos: preferir eventos pequenos e ocasionais
- Total alvo por player: ~5–20 KB/s (ajustável)

**Regras:**
- Nunca enviar transforms “crus” de centenas de entidades em alta frequência.
- Prefira compressão de estado: quantização, bitpacking quando necessário.

---

## 3) Budget de CPU (server)
- Tick deve manter folga (ideal < 50% do tempo do frame/tick).
- Evitar alocações e LINQ no loop.
- Evitar física pesada no server (MVP).

---

## 4) Budget de GPU (client)
- Low-poly estilizado
- Pós-processamento leve
- Evitar transparências e overdraw exagerado
- LODs e culling quando necessário

---

## 5) Budget de memória
- Evitar instanciar/destroçar em massa durante partida (usar pooling).
- Addressables: descarregar cenas/prefabs ao sair da sala.

---

## 6) Checklist de performance (gate)
Antes de “feature completa”:
- Sem GC spikes recorrentes durante gameplay
- Sem picos de CPU acima do budget no server
- Sem tráfego anormal (logar bytes/s por conexão em debug)
