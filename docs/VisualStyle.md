# VisualStyle — Direção Visual (MVP → Mobile)

## 1) Princípios
- **Low-poly estilizado**: leve, legível e “bonitinho”.
- Leitura de gameplay > detalhes.
- Silhueta clara do player e pickups.
- Cores com contraste (acessibilidade).

---

## 2) URP e iluminação
- Preferir luz baked/mixed.
- Sombras moderadas (mobile sofre).
- Pós-processo leve: Bloom sutil + Color Adjustments.

---

## 3) UI
- Tipografia grande e legível.
- Poucos elementos em HUD.
- Animações simples (fade/scale) para feedback.

---

## 4) VFX
- Partículas simples e baratas.
- Usar pooling.
- Evitar transparências em excesso (overdraw).

---

## 5) Assets (estratégia)
- MVP: usar packs low-poly (placeholders) e padronizar materiais.
- Produto: substituir por assets próprios gradualmente sem quebrar prefabs/ids.
