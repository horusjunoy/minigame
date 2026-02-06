# Método do Quadrado (Codex + VS Code + Scripts + Logs) — Base Genérica para Assistentes

## Objetivo
Permitir que qualquer pessoa (mesmo sem saber programar) desenvolva e mantenha software robusto usando um fluxo repetível, com **evidência (logs)**, **testes** e **correções rápidas guiadas por IA**.

O método “fecha” quando:
- o sistema roda local
- os testes passam
- os logs permitem diagnosticar qualquer falha

---

## Visão geral: os 4 lados do Quadrado

1. **Requisito** (clareza do que construir)  
2. **Implementação** (Codex + VS Code como motor)  
3. **Execução com evidência** (scripts + logs)  
4. **Investigação e validação** (Investigar/Agente + hipótese testável)

---

## 1) Requisito (clareza do que construir)

### Função
Transformar intenção em **especificação mínima testável** para evitar “invenção” e retrabalho.

### Saída obrigatória do assistente (sempre)
- **Objetivo em 1 frase**
- **Entradas / Saídas** (arquivos, endpoints, formatos)
- **Regras de negócio** (validações, limites, fallback)
- **Restrições** (segurança, performance, compatibilidade)
- **Definition of Done** (critério de pronto)
- **Riscos** (o que pode quebrar e por quê)

### Checklist de requisito (o assistente coleta ou assume)
- Onde roda? (Windows/Linux/Mac)
- Como executa? (CLI / API / UI)
- Onde ficam dados? (pasta local / rede / S3 / DB)
- Quais formatos? (PDF, DOCX, XLSX, JSON, etc.)
- O que é sucesso? (ex.: “retornar markdown”, “gerar manifest”, “passar nos testes”)

### Regra de ouro
Sem requisito mínimo, o Codex vai “inventar” e você perde tempo.

---

## 2) Implementação (Codex + VS Code como motor)

### Função
Fazer mudanças no código com velocidade e precisão, mantendo o projeto saudável (testes, logs, commits pequenos).

### Modo padrão
- **VS Code** aberto no projeto
- **Codex Chat** (no VS Code) para implementar, rodar comandos e ajustar arquivos
- Usuário não “programa”: **executa o roteiro**

### Como o assistente deve orientar o uso do Codex
1) Abrir o projeto no VS Code  
2) Abrir o Codex Chat  
3) Colar para o Codex sempre:
   - **Contexto / objetivo**
   - **Comando executado**
   - **Log/stacktrace**
   - **Comportamento esperado**

### Regras do Codex (o assistente impõe sempre)
- Mudanças **pequenas** e **isoladas** (um problema por commit).
- Rodar **teste isolado** que falhou.
- Depois rodar **suíte completa**.
- Só ajustar teste quando o **contrato** mudou de verdade.
- Commit **somente** quando:
  - teste isolado passa
  - suíte completa passa

### Entendendo “Keep changes”
- **Keep changes** = manter alterações no seu código local (working tree).
- **Não é commit**.
- Commit só existe depois de:
  - `git add ...` e `git commit ...` (ou o Codex fazendo isso por você)

---

## 3) Execução com evidência (scripts + logs)

### Função
Parar de “adivinhar” e sempre ter prova do que aconteceu (logs e comandos reproduzíveis).

### Estrutura recomendada (padrão)
```
<repo>/
  scripts/
    env.ps1
    check.ps1
    run-backend.ps1
    test.ps1
    start.ps1
    run-codex.ps1   (opcional, mas recomendado)
  logs/
    ...
```

### Regra
Todo comando relevante gera **log** em `.\logs\...`.

### Ordem padrão do dia (rotina)
1) **Subir o backend**
```powershell
.\scripts\run-backend.ps1
```
- Log: `.\logs\backend_<timestamp>.log`

2) **Rodar a suíte de testes**
```powershell
.\scripts\test.ps1
```
- Log: `.\logs\pytest_<timestamp>.log`

3) **Se falhar: isolar o erro com log dedicado**
```powershell
uv run pytest -q tests\caminho\test_x.py::test_y -x -vv --tb=long -s 2>&1 |
  Tee-Object .\logs\fail_test_y.log
```

4) **Enviar evidência para o Codex** (sempre neste pacote)
- comando
- log
- expectativa

---

## Passar evidência para o Codex (pacote mínimo)

### Sempre enviar (3 itens)
- **Comando** que você executou
- **Log** (arquivo + trecho relevante)
- **Expectativa** (o que deveria acontecer)

### Padrão de evidência mínima para qualquer bug (copiar/colar)
1) **Comando executado**
```powershell
<cole aqui o comando exato>
```

2) **Erro completo (stacktrace)**
```text
<cole aqui o stacktrace completo>
```

3) **Arquivo de log**
- Caminho do log: `.\logs\<nome_do_log>.log`
- Trecho final do log (últimas ~200 linhas):
```powershell
Get-Content .\logs\<nome_do_log>.log -Tail 200
```

4) **O que era esperado**
- Em 1 frase: `<descreva o comportamento correto>`
- Se tiver critério objetivo: `<ex.: "status=finished", "markdown vazio", "rules_applied contém ocr">`

---

## 4) Investigação e validação (Investigar/Agente + hipótese testável)

### Função
Confirmar causa raiz, evitar correção errada e garantir robustez.

### Quando o assistente deve entrar em modo “investigar”
- o bug depende de ambiente (Windows/permissão/path/encoding)
- comportamento inconsistente (passa às vezes / falha às vezes)
- parsing/OCR/encoding/filesystem/rede/permissões
- config e defaults (settings/env/feature flags)

### Perguntas padrão de investigação
- Onde nasce essa configuração? (settings/env/default)
- Quem decide o comportamento final? (função/regra)
- O teste está cobrindo o comportamento correto?
- Existe fallback escondido mascarando erro?
- É determinístico (sempre) ou flaky (às vezes)?

### Regra
Investigação termina com:
- uma **hipótese testável**
- um **teste isolado** para confirmar

---

## Fluxo completo do Método do Quadrado

### Fase A — Preparação (uma vez por projeto)
1) Garantir scripts de execução e logs:
- backend com log
- testes com log
- pasta `.\logs\`

2) Garantir que o projeto roda local:
```powershell
.\scripts\run-backend.ps1
```
- validar endpoint/health (ex.: `http://127.0.0.1:8000`)

3) Rodar suíte de testes:
```powershell
.\scripts\test.ps1
```

4) Se existe CI, alinhar comando local com CI (mesma forma de rodar testes).

### Fase B — Construção de feature (sempre)
1) Assistente define **requisito mínimo** + **Definition of Done**  
2) Assistente gera plano em passos curtos  
3) Codex implementa no VS Code com commits pequenos  
4) Rodar testes (isolado + suíte)  
5) Validar via logs e endpoints/funcionalidade  
6) Se ok: commit + push

### Fase C — Correção de falhas (playbook único)
1) Rodar suíte:
```powershell
.\scripts\test.ps1
```

2) Isolar falha com log dedicado:
```powershell
uv run pytest -q tests\caminho\test_x.py::test_y -x -vv --tb=long -s 2>&1 |
  Tee-Object .\logs\fail_test_y.log
```

3) Enviar para o Codex (VS Code) este pacote:
- comando
- log (arquivo + trecho final)
- expectativa

4) Codex aplica correção mínima  
5) Rodar de novo:
- teste isolado
- suíte completa

6) Commit + push

---

## Regras de robustez (o assistente deve impor sempre)
- **Logs antes de luxo:** sem log bom, vira tentativa e erro.
- **Um problema por commit.**
- Evitar “fallback burro” que mascara bug e quebra contrato.
- Se o comportamento mudou, o teste deve refletir o **novo contrato** (sem esconder falha).
- Evitar mexer em muitos arquivos para consertar 1 teste (sinal de gambiarra).
- Preferir configs explícitas (settings/env) com defaults **testados**.
- Todo bug tem **reprodução**: comando + log.

---

## Template de mensagem para o Codex (copiar/colar)

**Contexto**
- Objetivo: `<o que deve acontecer>`
- Ambiente: `<Windows / Python / uv / etc.>`

**Comando executado**
```powershell
<cole o comando>
```

**Evidência**
- Log: `.\logs\<nome>.log`
```text
<cole aqui o trecho relevante do log/stacktrace>
```

**Esperado**
- `<o que deveria acontecer>`

**Pedido**
- Corrija com o **menor impacto possível**
- Rode o **teste isolado** que falhou
- Rode a **suíte completa** via `.\scripts\test.ps1`
- Faça **commit só se tudo passar**
- Explique em **5 linhas** a causa raiz e por que a correção resolve

---

## Template de Definition of Done (para qualquer software)
O software está pronto quando:
- roda local via script (backend/UI/CLI)
- gera logs em `.\logs\`
- teste isolado passa
- suíte completa passa
- configuração default é testada (settings)
- erros têm mensagens úteis e rastreáveis
- existe reprodução clara (comando + log)
