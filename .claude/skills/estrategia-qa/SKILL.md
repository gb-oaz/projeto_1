---
name: estrategia-qa
description: Competência de QA do boaz — estratégia de testes guiada por risco e pela pirâmide do projeto, derivação de casos a partir dos cenários Gherkin (positivos, negativos, alternativos, limites), testes de aceitação caixa-preta pela API (Reqnroll + xUnit), funcionais web (Cypress) e mobile (Appium com C#/xUnit), carga e desempenho contra os RNF, regressão, testes exploratórios, análise de cobertura por ID de requisito e contrato das suítes em contêiner. Use ao planejar a cobertura de um módulo, escrever ou revisar testes em 5_DEV/qa/, atualizar .feature quando um requisito muda, investigar regressão ou avaliar se um módulo está pronto.
---

# Estratégia de QA → `5_DEV/qa/`

O `deboaz-qa` **define a estratégia e valida a qualidade**. Cada teste nasce de um cenário Gherkin do `.md` (skill `gherkin-bdd`) e cita o ID. O qa não decide regra nem muda contrato (ambos do `deboaz-arc`) e não corrige código de produto (back/front). Fluxo da tarefa: etapa 2 da skill `desenvolvimento` (vermelho antes do código).

Prevalência: `14_estrutura-projetos` (RN-151 a RN-156, RNF-190 a RNF-196), `03_ci-qualidade` (RN-142, RNF-18, RNF-194 a RNF-197), `5_DEV/qa/README.md` → esta skill.

## 1 · Princípios

- **Caixa-preta** (RN-151): o qa nunca importa código de `5_DEV/backend` ou `5_DEV/frontend`; testa o que está rodando, pelo contrato e pela tela.
- **Requisito é a referência**: nenhum teste sem cenário; nenhum limite próprio (RN-155) — o número vem do `RNF`.
- **Comportamento visível** (RN-142): detalhe interno é do unitário de cada frente.
- **Vermelho pelo motivo certo**: antes do código, o teste falha porque falta a funcionalidade, não por erro de passo, fixture ou ambiente.
- **Independência**: cada cenário prepara e limpa o próprio estado; ordem de execução não importa.

## 2 · Estratégia de um módulo

1. **Inventário**: liste os IDs do documento (comando em `gherkin-bdd/referencias/rastreabilidade.md`), separe tela × API × `RNF` numérico.
2. **Risco** (probabilidade × impacto): autenticação, autorização, isolamento por escola, dinheiro/contratação, perda de dado e integração nova primeiro.
3. **Nível certo** (pirâmide): Cenários de API → `backend/integracao`; Cenários de tela → `frontend/web|mobile/funcional` (só fluxos de valor e o que a API não prova); `RNF` com número → `backend/carga`; regra interna → pedir unitário ao dono da frente.
4. **Derivação de casos**: para cada regra, positivos, negativos, alternativos e limites — técnicas em `referencias/tecnicas-de-teste.md`. Caso necessário que **não está** no Gherkin → **não teste por conta própria**: proponha o cenário ao `deboaz-arc`.
5. **Matriz de cobertura** por ID (formato em `gherkin-bdd/referencias/rastreabilidade.md`) na entrega.

## 3 · Suítes e ferramentas

| Suíte | Ferramenta | Base | Referência |
|---|---|---|---|
| `backend/integracao/` | Reqnroll + xUnit, `.feature` em pt, banco e cache reais | Cenários de API | `referencias/api-reqnroll.md` |
| `frontend/web/funcional/` | Cypress + pré-processador Cucumber, `.feature` em pt (RNF-18, RNF-192) | Cenários de tela | `referencias/web-cypress.md` |
| `frontend/mobile/funcional/` | Appium + C#/xUnit (RNF-196) | Cenários de tela | `referencias/mobile-appium.md` |
| `backend/carga/` | livre (ex.: k6), sob demanda (RN-156) | `RNF` numéricos | `referencias/carga-desempenho.md` |

Contrato comum (RNF-191): `Dockerfile` na raiz da suíte, alvo por `API_URL`/`APP_URL`/`APP`, JUnit XML em `/resultados`, saída 0 só se tudo passou. Mobile: `executar.sh` com as variáveis do README.

## 4 · Requisito mudou

1. Compare a versão citada no cabeçalho do `.feature` com o front matter do `.md`.
2. Copie de novo os cenários alterados (o `.feature` é cópia fiel), ajuste passos e a versão no cabeçalho.
3. Remova testes de IDs removidos; procure órfãos.
4. Divergência entre requisito, contrato e comportamento → reporte ao dono, com ID e evidência.

## 5 · Regressão e exploratório

- **Regressão**: a suíte inteira roda no CI a cada PR (job `qa`); bug corrigido ganha cenário (via `deboaz-arc`) e teste que o reproduz antes do fix (`fix_<NN>`).
- **Exploratório**: sessões curtas com missão ("explorar a troca de permissões com sessão aberta em dois aparelhos"), anotando achados; achado vira proposta de cenário ao `deboaz-arc`, não teste avulso.
- Teste instável (flaky) é defeito: isole a causa (espera, dado compartilhado, ordem), nunca só aumente o timeout ou repita.

## 6 · Entrega

Lista de testes por ID com estado (vermelho pelo motivo certo / verde / pendente), matriz de cobertura, lacunas enviadas ao `deboaz-arc` e riscos sem cobertura.

## Critérios de qualidade

- [ ] Todo teste tem a tag/nome com o ID; todo ID testável do documento tem teste ou justificativa.
- [ ] Negativos e limites cobertos onde o Gherkin os traz; os que faltam viraram proposta ao `deboaz-arc`.
- [ ] Nenhum import de código de produto; estado preparado pela API sempre que possível.
- [ ] Sem espera fixa (`sleep`); seletores estáveis; dados únicos por execução.
- [ ] Contrato da suíte respeitado (Dockerfile, variáveis, JUnit, código de saída).

## Skills relacionadas

`gherkin-bdd` · `desenvolvimento` (etapa 2) · `engenharia-software` (`referencias/testes-processo.md`) · `devops-cloud` (Docker, CI) · `ux-usabilidade` (`referencias/acessibilidade.md`, verificações de a11y nos funcionais).
