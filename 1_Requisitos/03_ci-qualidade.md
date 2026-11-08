---
projeto: "boaz"
documento: "03_ci-qualidade"
tipo: "Transversal"
titulo: "CI e qualidade"
versao: "v26"
data: "2026-11-05 12:34"
status: "Aprovado"
objetivo: "definir onde e quando o CI roda e quais testes e verificações de qualidade reprovam um build."
---

> **boaz · Transversal** · `03_ci-qualidade` · **v26** · 2026-11-05 12:34 · **Aprovado**

# CI e qualidade

**Objetivo:** definir onde e quando o CI roda e quais testes e verificações de qualidade reprovam um build.

---

<a id="cenarios"></a>

## Cenários

<a id="ci-local-e-github"></a>

### CI local e GitHub Actions

```gherkin
Cenário: prévia antes do push
  Dado a pessoa com uma mudança no backend
  Quando roda o CI local em 4_CI_CD/local/
  Então ele repete na máquina as verificações do GitHub Actions e o qa contra os contêineres
  E o GitHub Actions continua sendo o veredito do merge e o único que publica imagens
```

<a id="RN-140"></a>

### `RN-140` CI local não publica

```gherkin
Cenário: imagens locais
  Quando o CI local gera as imagens
  Então elas ficam só na máquina
```

<a id="RN-141"></a>

### `RN-141` CI local é prévia

```gherkin
Cenário: CI local verde
  Dado o CI local aprovado
  Então o merge ainda depende do job agregador do GitHub Actions
```

<a id="RN-142"></a>

### `RN-142` O que o qa cobre

```gherkin
Esquema do Cenário: onde o teste fica
  Quando o teste verifica <comportamento>
  Então fica em <lugar>

  Exemplos:
    | comportamento                              | lugar                   |
    | a regra de bloqueio da entrada pela API    | 5_DEV/qa/               |
    | o cálculo interno de uma classe do domínio | os unitários do backend |
```

<a id="RNF-11"></a>

### `RNF-11` O que o CI compila

```gherkin
Cenário: três frentes
  Então o CI compila e testa backend, web, Android e iOS com as versões da stack
```

<a id="RNF-13"></a>

### `RNF-13` Executores

```gherkin
Esquema do Cenário: executor de cada job
  Quando o job é <job>
  Então roda num executor <executor>

  Exemplos:
    | job     | executor        |
    | backend | Linux           |
    | web     | Linux           |
    | Android | Linux           |
    | iOS     | macOS hospedado |
```

<a id="RNF-23"></a>

### `RNF-23` A cada push

```gherkin
Cenário: push numa branch qualquer
  Quando a pessoa envia um commit que muda o backend
  Então o backend compila e roda os testes unitários e de integração
```

<a id="RNF-34"></a>

### `RNF-34` Só a frente que mudou

```gherkin
Esquema do Cenário: jobs disparados
  Quando o push muda <pasta>
  Então rodam os jobs de <frente>

  Exemplos:
    | pasta                     | frente  |
    | 5_DEV/backend/            | backend |
    | 5_DEV/qa/backend/         | backend |
    | 5_DEV/frontend/web/       | web     |
    | 5_DEV/qa/frontend/web/    | web     |
    | 5_DEV/frontend/mobile/    | mobile  |
    | 5_DEV/qa/frontend/mobile/ | mobile  |
```

<a id="RNF-35"></a>

### `RNF-35` Arquivo comum

```gherkin
Esquema do Cenário: tudo roda
  Quando o push muda <arquivo>
  Então rodam os jobs de todos os componentes

  Exemplos:
    | arquivo        |
    | .github/       |
    | global.json    |
    | .xcode-version |
    | .nvmrc         |
    | docker-compose |
```

<a id="RNF-188"></a>

### `RNF-188` Job da web

```gherkin
Cenário: um projeto, três builds
  Então o job da web roda lint, formatação, tipos e testes uma vez para o projeto
  E depois o build de cada app, com APP=landing_page, APP=hub e APP=quest
```

<a id="RNF-189"></a>

### `RNF-189` Jobs do mobile

```gherkin
Cenário: Android e iOS
  Então o job Android compila e testa a solution no Linux, sem o alvo iOS
  E o job iOS confere a formatação e compila o net10.0-ios de cada app
```

<a id="RNF-194"></a>

### `RNF-194` Job de qa

```gherkin
Cenário: qa depois do backend e da web
  Dado os jobs do backend e da web aprovados
  Então o job de qa sobe backend e web em contêineres e roda integração e funcionais
  E cada suíte deixa o JUnit XML como artefato
```

<a id="RNF-195"></a>

### `RNF-195` Carga em homologação

```gherkin
Cenário: disparo manual
  Quando alguém dispara o workflow de carga na master com o endereço de homologação
  Então a suíte 5_DEV/qa/backend/carga/ roda contra homologação
```

<a id="RNF-197"></a>

### `RNF-197` Funcional do mobile no CI

```gherkin
Cenário: pull request para a master
  Então o funcional do mobile roda no Android, num emulador Linux, com o backend em contêiner
  E o funcional do iOS roda só no CI local
```

<a id="RNF-36"></a>

### `RNF-36` Job agregador

```gherkin
Esquema do Cenário: veredito do merge
  Dado <situação>
  Então o job agregador <resultado>

  Exemplos:
    | situação                                | resultado |
    | todos os jobs disparados aprovados      | aprova    |
    | o job da web reprovado                  | falha     |
    | o mobile sem mudança e sem jobs rodados | aprova    |
```

<a id="RNF-24"></a>

### `RNF-24` Build iOS

```gherkin
Esquema do Cenário: quando o iOS compila
  Quando acontece <evento>
  Então o build iOS <resultado>

  Exemplos:
    | evento                     | resultado |
    | push em tarefa_42-cadastro | não roda  |
    | pull request para a master | roda      |
    | merge na master            | roda      |

Cenário: motivo
  Então o iOS fica restrito porque o macOS custa mais por minuto que o Linux
```

<a id="RNF-21"></a>

### `RNF-21` Dependabot

```gherkin
Cenário: atualizações
  Então o Dependabot abre pull requests de dependências, imagens Docker e actions
```

<a id="RNF-14"></a>

### `RNF-14` xUnit

```gherkin
Cenário: testes do backend e do mobile
  Então os testes usam xUnit, com cobertura pelo coverlet
```

<a id="RNF-37"></a>

### `RNF-37` Integração com banco real

```gherkin
Cenário: testes de integração
  Então rodam contra MongoDB 8.0 e Redis 8 em contêineres do próprio job
```

<a id="RNF-25"></a>

### `RNF-25` Cobertura mínima

```gherkin
Esquema do Cenário: cobertura reprova
  Quando a cobertura de linhas do <componente> é <cobertura>
  Então o build <resultado>

  Exemplos:
    | componente | cobertura | resultado |
    | backend    | 79 %      | reprova   |
    | backend    | 80 %      | passa     |
    | web        | 79 %      | reprova   |
```

<a id="RNF-26"></a>

### `RNF-26` Cobertura do mobile

```gherkin
Cenário: só medida
  Quando a cobertura do mobile é 40 %
  Então o CI publica o número e o build passa
```

<a id="RNF-15"></a>

### `RNF-15` dotnet format

```gherkin
Cenário: formatação errada
  Quando "dotnet format --verify-no-changes" encontra diferença
  Então o build reprova
```

<a id="RNF-22"></a>

### `RNF-22` Avisos são erros

```gherkin
Cenário: campo não usado
  Quando o analisador acusa um aviso
  Então o build do CI reprova
```

<a id="RNF-16"></a>

### `RNF-16` Vitest

```gherkin
Cenário: testes da web
  Então os unitários e de integração da web usam Vitest com Vue Test Utils
```

<a id="RNF-17"></a>

### `RNF-17` Qualidade da web

```gherkin
Esquema do Cenário: verificação reprova
  Quando <ferramenta> aponta erro
  Então o build reprova

  Exemplos:
    | ferramenta |
    | ESLint     |
    | Prettier   |
    | vue-tsc    |
```

<a id="RNF-18"></a>

### `RNF-18` Cypress

```gherkin
Cenário: ponta a ponta da web
  Então os testes de ponta a ponta usam Cypress em 5_DEV/qa/frontend/web/funcional/
```

<a id="RNF-160"></a>

### `RNF-160` Um comando

```gherkin
Cenário: pré-requisitos
  Então o CI local roda com um comando e só exige Docker com Compose v2, git e bash
  E o mobile também pede SDK .NET, workload do MAUI, JDK 17 e SDK do Android ou o Xcode
  E o funcional do mobile pede o Node.js de .nvmrc e o emulador ou simulador ligado
```

<a id="RNF-161"></a>

### `RNF-161` Mesmas verificações

```gherkin
Cenário: réplica do GitHub Actions
  Então o CI local repete RNF-14 a RNF-18, RNF-22, RNF-25, RNF-26 e RNF-37
  E com as mesmas versões de SDK, Node.js, MongoDB e Redis
```

<a id="RNF-162"></a>

### `RNF-162` Contêineres e saúde

```gherkin
Cenário: qa contra os contêineres
  Dado as verificações aprovadas
  Então o CI local sobe backend e web pelos mesmos Dockerfiles das imagens
  E confere 200 em /health antes de rodar cada suíte do qa pela própria imagem
```

<a id="RNF-163"></a>

### `RNF-163` Só o que mudou

```gherkin
Cenário: mudança não commitada
  Dado uma mudança no backend ainda não commitada
  Quando a pessoa roda o CI local
  Então roda só o backend, comparado com a master
  E uma opção roda todos os componentes
```

<a id="RNF-164"></a>

### `RNF-164` Cópia de trabalho intacta

```gherkin
Cenário: depois da execução
  Então o repositório entrou só para leitura e o mobile compilou numa cópia temporária
  E banco e cache começaram vazios
```

<a id="RNF-165"></a>

### `RNF-165` Veredito único

```gherkin
Cenário: web reprovada
  Dado o backend aprovado e a web reprovada
  Então o CI local mostra cada componente e etapa e o veredito "falhou"
```

<a id="RNF-166"></a>

### `RNF-166` Mobile na máquina

```gherkin
Cenário: sem Xcode
  Dado uma máquina Linux sem Xcode
  Quando o mobile muda
  Então o iOS é pulado com o aviso do que falta
  E a verificação fica para o GitHub Actions
```

<a id="RNF-172"></a>

### `RNF-172` Abordagens do qa local

```gherkin
Esquema do Cenário: abordagem escolhida
  Quando a pessoa roda o qa local com <abordagem>
  Então roda <suítes>

  Exemplos:
    | abordagem | suítes                                          |
    | backend   | integração contra o backend                     |
    | frontend  | funcional de cada app web, com o backend no ar  |
    | padrão    | backend e frontend sobre os mesmos contêineres  |
    | carga     | carga contra o backend, só quando pedida        |
    | mobile    | funcional com Appium no emulador e no simulador |
```

---

<a id="diagramas"></a>

## Diagramas

**Pipeline de CI e qualidade** — `3_Arquitetura/pipeline-ci_v10_20261024-2247.svg`

[![Pipeline de CI no monorepo: push e pull request em qualquer branch passam pela detecção de mudanças, que dispara só os jobs Linux dos componentes alterados, com testes unitários e de integração; PR e merge na principal disparam iOS em macOS; depois de backend e web, o job QA roda as suítes de integração e funcionais contra as aplicações em contêiner; em PR e merge na principal, o job QA · mobile roda o Appium no Android em emulador; a carga roda num workflow manual contra homologação; com os jobs disparados verdes, merge e tag na branch principal pedem aprovação manual por issue no repositório e, aprovados, publicam as imagens versionadas nos repositórios públicos gustavoboaz/* do Docker Hub; branches de tarefa e de correção não geram imagem](../3_Arquitetura/pipeline-ci_v10_20261024-2247.svg)](../3_Arquitetura/pipeline-ci_v10_20261024-2247.svg)

**CI local** — `3_Arquitetura/ci-local_v06_20261024-2247.svg`

[![CI local em 4_CI_CD/local com Docker Compose: ci.sh descobre os componentes alterados; a etapa 1 repete os jobs backend e web do GitHub Actions em contêineres, com MongoDB e Redis, e os de Android e iOS direto na máquina; com tudo verde, a etapa 2 sobe backend e frontends das imagens e confere /health; a etapa 3 roda pela própria imagem cada suíte de 5_DEV/qa/ (integração, funcional da web por app, as duas em conjunto ou a carga sob demanda) contra os contêineres e, com --qa mobile, o funcional do mobile com Appium na máquina; o resultado aprovado segue por git push para o GitHub Actions, o único que publica no Docker Hub](../3_Arquitetura/ci-local_v06_20261024-2247.svg)](../3_Arquitetura/ci-local_v06_20261024-2247.svg)

---

<a id="historico"></a>

## Histórico

| Versão | Data | Mudança |
|---|---|---|
| **v26** | 2026-11-05 12:34 | Formato `.md`: migrado de `.html` sem mudar cenários, telas nem diagramas; links entre documentos apontam para `.md` |
| v25 | 2026-11-03 22:11 | Aprovado, sem pendências em aberto |
| v24 | 2026-11-01 18:27 | Só Gherkin e diagramas: contexto, RN e RNF viram cenários (mesmos IDs como âncora); status → Em revisão |
| v23 | 2026-10-25 03:57 | `RNF-35` alterado: sem arquivo comum só aos apps mobile, porque as propriedades comuns do mobile passaram para `5_DEV/frontend/mobile/`, já coberta pelos jobs Android e iOS; status: Aprovado |
| v22 | 2026-10-25 01:12 | Status → Aprovado |
| v21 | 2026-10-24 23:21 | `RNF-160` alterado: o funcional do mobile pede o Node.js de `.nvmrc` e o emulador ou o simulador; o Appium e os drivers vêm do `package.json` da suíte |
| v20 | 2026-10-24 22:47 | Funcional do mobile com Appium: `RNF-160` (pré-requisitos) e `RNF-172` (abordagem mobile) alterados; `RNF-197` (job de qa do mobile, Android em emulador) incluído; diagramas `pipeline-ci_v10` e `ci-local_v06` |
| v19 | 2026-10-24 22:25 | QA por suítes (`14_estrutura-projetos`): `RNF-18` (ponta a ponta no qa), `RNF-162`, `RNF-172` (abordagem carga) e `RNF-188` (job da web sem ponta a ponta) alterados; `RNF-194` (job de qa) e `RNF-195` (carga em homologação) incluídos; diagramas `pipeline-ci_v09` e `ci-local_v05` |
| v18 | 2026-10-24 21:15 | `RNF-34`, `RNF-35` e `RNF-166` alterados para a estrutura de `14_estrutura-projetos` (backend, web e mobile num projeto cada); `RNF-188` (job da web, com build e e2e por app) e `RNF-189` (jobs Android e iOS) incluídos; diagrama atualizado para `ci-local_v04`; status → Em revisão |
| v17 | 2026-10-24 07:58 | `RNF-35` inclui `.xcode-version` entre os arquivos comuns e `5_DEV/frontend/Directory.Build.props` como comum aos apps mobile; status → Aprovado |

---

<sub>boaz · v26 · 2026-11-05 12:34 · `1_Requisitos/03_ci-qualidade.md`</sub>
