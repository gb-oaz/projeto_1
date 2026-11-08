---
projeto: "boaz"
documento: "04_publicacao-imagens"
tipo: "Transversal"
titulo: "Publicação das imagens"
versao: "v15"
data: "2026-11-05 12:34"
status: "Aprovado"
objetivo: "definir quais imagens Docker o CI gera, quando e com quais tags elas são enviadas ao Docker Hub, quem aprova o envio e como o CI se autentica."
---

> **boaz · Transversal** · `04_publicacao-imagens` · **v15** · 2026-11-05 12:34 · **Aprovado**

# Publicação das imagens

**Objetivo:** definir quais imagens Docker o CI gera, quando e com quais tags elas são enviadas ao Docker Hub, quem aprova o envio e como o CI se autentica.

---

<a id="cenarios"></a>

## Cenários

<a id="da-master-ao-docker-hub"></a>

### Da master ao Docker Hub

```gherkin
Cenário: publicação com aprovação
  Dado todos os jobs verdes na master
  Quando o CI abre a issue de aprovação e uma pessoa autorizada aprova
  Então as imagens dos componentes alterados vão versionadas ao Docker Hub
```

<a id="RNF-27"></a>

### `RNF-27` Quatro imagens

```gherkin
Esquema do Cenário: imagem de cada componente
  Então o componente <componente> gera a imagem <imagem>

  Exemplos:
    | componente                       | imagem                |
    | a API backend                    | backend-mono          |
    | o hub web, estático do Vite      | frontend-hub          |
    | a landing page, estática do Vite | frontend-landing-page |
    | o quest web, estático do Vite    | frontend-quest        |
```

<a id="RNF-28"></a>

### `RNF-28` Só com tudo verde

```gherkin
Cenário: um job reprovado
  Dado o job da web reprovado na master
  Então nenhuma imagem é enviada ao Docker Hub
```

<a id="RNF-29"></a>

### `RNF-29` Tags do merge

```gherkin
Cenário: merge do commit a1b2c3d
  Quando o commit a1b2c3d entra na master
  Então as imagens recebem as tags "sha-a1b2c3d" e "master"
```

<a id="RNF-30"></a>

### `RNF-30` Tags da versão

```gherkin
Cenário: tag v1.3.0
  Quando o time cria a tag "v1.3.0" na master
  Então as imagens recebem as tags "1.3.0", "1.3" e "latest"
```

<a id="RNF-38"></a>

### `RNF-38` Quais imagens

```gherkin
Esquema do Cenário: imagens geradas
  Quando acontece <evento>
  Então são geradas <imagens>

  Exemplos:
    | evento                         | imagens           |
    | um merge que muda só o backend | só backend-mono   |
    | a tag v1.3.0                   | as quatro imagens |
```

<a id="RNF-31"></a>

### `RNF-31` Branches sem imagem

```gherkin
Cenário: branch de tarefa
  Quando a pessoa envia commits para tarefa_42-cadastro ou fix_42-erro
  Então o CI compila e testa sem gerar nem publicar imagens
```

<a id="RNF-32"></a>

### `RNF-32` Repositórios públicos

```gherkin
Esquema do Cenário: destino
  Então a imagem <imagem> vai para "gustavoboaz/<imagem>" no Docker Hub

  Exemplos:
    | imagem                |
    | backend-mono          |
    | frontend-hub          |
    | frontend-landing-page |
    | frontend-quest        |
```

<a id="RNF-43"></a>

### `RNF-43` Issue de aprovação

```gherkin
Cenário: pedido de aprovação
  Dado todos os jobs disparados aprovados
  Então o CI abre uma issue com a lista das imagens e o link da execução
  E espera a aprovação antes de gerar e enviar
```

<a id="RNF-44"></a>

### `RNF-44` Quem aprova

```gherkin
Cenário: aprovação
  Dado a variável APROVADORES com "GustavoBoaz"
  Quando ele comenta "approve" na issue
  Então a publicação segue, com 1 aprovação, mesmo sendo o autor do push

Cenário: motivo da variável
  Então os aprovadores vêm da variável porque o dono do repositório é a organização gb-oaz
```

<a id="RNF-45"></a>

### `RNF-45` Cancelamento

```gherkin
Esquema do Cenário: publicação cancelada
  Quando <evento>
  Então a publicação é cancelada sem enviar imagem

  Exemplos:
    | evento                            |
    | um aprovador comenta "deny"       |
    | a issue fica 2 horas sem resposta |
```

<a id="RNF-46"></a>

### `RNF-46` Tag fora da master

```gherkin
Cenário: tag num commit de outra branch
  Quando alguém cria "v1.3.0" num commit fora da master
  Então a execução falha antes de pedir aprovação, sem publicar
```

<a id="RNF-33"></a>

### `RNF-33` Credencial do Docker Hub

```gherkin
Cenário: token restrito
  Então o CI usa um token pessoal de leitura e escrita da conta gustavoboaz
  E o token fica em DOCKERHUB_USERNAME e DOCKERHUB_TOKEN no environment dockerhub-publish
  E o environment só vale na master e nas tags v*
```

---

<a id="diagramas"></a>

## Diagramas

**Pipeline de CI e qualidade** — `3_Arquitetura/pipeline-ci_v08_20261015-0019.svg`

[![Pipeline de CI no monorepo: push e pull request em qualquer branch passam pela detecção de mudanças, que dispara só os jobs Linux dos componentes alterados, com testes unitários e de integração; PR e merge na principal disparam iOS em macOS; com os jobs disparados verdes, merge e tag na branch principal pedem aprovação manual por issue no repositório e, aprovados, geram e publicam as imagens versionadas nos repositórios públicos gustavoboaz/* do Docker Hub; branches de tarefa e de correção não geram imagem](../3_Arquitetura/pipeline-ci_v08_20261015-0019.svg)](../3_Arquitetura/pipeline-ci_v08_20261015-0019.svg)

---

<a id="historico"></a>

## Histórico

| Versão | Data | Mudança |
|---|---|---|
| **v15** | 2026-11-05 12:34 | Formato `.md`: migrado de `.html` sem mudar cenários, telas nem diagramas; links entre documentos apontam para `.md` |
| v14 | 2026-11-03 22:11 | Aprovado, sem pendências em aberto |
| v13 | 2026-11-01 18:27 | Só Gherkin e diagramas: contexto e RNF viram cenários (mesmos IDs como âncora); status → Em revisão |
| v12 | 2026-10-25 01:12 | Status → Aprovado |
| v11 | 2026-10-25 01:10 | `RNF-44` alterado: aprovadores são os usuários da variável `APROVADORES` do repositório, porque o dono é a organização `gb-oaz`; status → Em revisão |
| v10 | 2026-10-15 00:19 | `RNF-33` alterado: environment `dockerhub-publish`, o nome existente no GitHub e usado pelo CI; diagrama atualizado para `pipeline-ci_v08` |
| v09 | 2026-10-11 22:24 | Status → Aprovado |
| v08 | 2026-10-11 22:21 | Aprovação manual antes da publicação: `RNF-43` a `RNF-46` incluídos; `RNF-33` alterado (environment `dockerhub` também nas tags `v*`); diagrama atualizado para `pipeline-ci_v07`; status → Em revisão |
| v07 | 2026-10-11 15:15 | Status → Aprovado |
| v06 | 2026-10-11 15:02 | `RNF-29` alterado: tag de imagem `master` no lugar de `main`; diagrama atualizado para `pipeline-ci_v06` |

---

<sub>boaz · v15 · 2026-11-05 12:34 · `1_Requisitos/04_publicacao-imagens.md`</sub>
