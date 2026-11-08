# Funcional da web — Cypress

Suíte: `5_DEV/qa/frontend/web/funcional/` (RNF-18, RNF-192), um app por vez (`APP`), pastas do contexto do app + `commons/`, alvo `APP_URL`. Ainda não existe no repositório: a primeira tarefa cria a suíte respeitando o contrato do README (Dockerfile, JUnit em `/resultados`, saída 0).

## Organização

- **BDD com `.feature`** (decisão de 2026-11-08): pré-processador de Cucumber para Cypress (`@badeball/cypress-cucumber-preprocessor`, versão exata), igual à suíte da API com Reqnroll.
- `<contexto>/<feature>/<feature>.feature` com os nomes do core; `# language: pt`, cabeçalho com o documento e a **versão** do requisito, `Funcionalidade:` com o título.
- Os `.feature` são **cópias fiéis dos Cenários de tela** (mesmo texto, mesmos dados), cada cenário com a tag do ID (`@RF-134`); requisito mudou → `.feature` muda junto.
- Passos (step definitions) em TypeScript, compartilhados por frase; nunca reescreva o cenário para facilitar o passo.
- O README da suíte registra o pré-processador e como rodar.

## Boas práticas

- Seletores por papel, rótulo ou texto visível (os textos do Gherkin) — `cy.contains`, `findByRole` (Testing Library) —, ou `data-*` de teste quando o texto não basta; nunca classe CSS ou estrutura.
- Sem `cy.wait(ms)`: espere pelo estado (texto, rota, `cy.intercept` de um pedido nomeado).
- Preparo pela API (`cy.request` no `API_URL`) e login programático; a UI só no passo que o cenário testa.
- Cada teste independente; dados únicos por execução.
- Verificar os estados que o cenário cita (erro com o texto exato, carregando quando relevante).
- Viewports das classes de janela que a vista define (web e compact para PWA).
- Acessibilidade: checagem automática (ex.: axe) nos fluxos principais quando a suíte adotar.
