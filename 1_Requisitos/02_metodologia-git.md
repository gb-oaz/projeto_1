---
projeto: "boaz"
documento: "02_metodologia-git"
tipo: "Transversal"
titulo: "Metodologia git"
versao: "v10"
data: "2026-11-05 12:34"
status: "Aprovado"
objetivo: "definir como cada atividade é desenvolvida em branch própria e chega à branch principal, que guarda só código homologado, e como uma versão oficial é marcada."
---

> **boaz · Transversal** · `02_metodologia-git` · **v10** · 2026-11-05 12:34 · **Aprovado**

# Metodologia git

**Objetivo:** definir como cada atividade é desenvolvida em branch própria e chega à branch principal, que guarda só código homologado, e como uma versão oficial é marcada.

---

<a id="cenarios"></a>

## Cenários

<a id="caminho-da-atividade"></a>

### Caminho de uma atividade

```gherkin
Cenário: da tarefa às imagens
  Dado uma tarefa mergeada na master
  Então o CI publica as imagens de desenvolvimento dos componentes alterados
  E uma versão oficial só sai quando o time marca um commit com uma tag de versão
```

<a id="RN-10"></a>

### `RN-10` Branch principal

```gherkin
Cenário: nome da principal
  Então a branch principal é a master
```

<a id="RN-05"></a>

### `RN-05` Branch própria

```gherkin
Cenário: começar uma atividade
  Quando a pessoa começa a tarefa 42
  Então cria uma branch própria a partir da master
```

<a id="RN-06"></a>

### `RN-06` Nome da branch

```gherkin
Esquema do Cenário: padrão do nome
  Quando a atividade é <atividade>
  Então a branch se chama "<branch>"

  Exemplos:
    | atividade                | branch               |
    | o requisito 42, cadastro | tarefa_42-cadastro   |
    | um bug no requisito 42   | fix_42-erro_cadastro |
```

<a id="RN-09"></a>

### `RN-09` Convenção, não bloqueio

```gherkin
Cenário: branch fora do padrão
  Quando alguém abre um pull request da branch "cadastro-novo"
  Então o CI não recusa o pull request por causa do nome
```

<a id="RN-07"></a>

### `RN-07` Só por pull request

```gherkin
Cenário: entrada na master
  Então código entra na master só por pull request de uma branch de tarefa ou de correção
```

<a id="RN-08"></a>

### `RN-08` Imagens só na master

```gherkin
Cenário: push na branch de tarefa
  Quando a pessoa envia commits para tarefa_42-cadastro
  Então nenhuma imagem Docker é gerada nem publicada
```

<a id="RN-11"></a>

### `RN-11` Tag de versão

```gherkin
Cenário: versão oficial
  Então a versão é uma tag anotada "vX.Y.Z", em SemVer, num commit da master
```

<a id="RN-12"></a>

### `RN-12` Qual número sobe

```gherkin
Esquema do Cenário: próxima versão
  Dado a última tag "v1.2.3"
  Quando a versão nova traz <mudança>
  Então a tag é "<tag>"

  Exemplos:
    | mudança                            | tag    |
    | só correções                       | v1.2.4 |
    | funcionalidade nova compatível     | v1.3.0 |
    | mudança que quebra compatibilidade | v2.0.0 |
```

<a id="RN-13"></a>

### `RN-13` Antes do lançamento

```gherkin
Cenário: primeiras versões
  Então as versões começam em "v0.1.0" e sobem o MINOR
  E "v1.0.0" marca o primeiro lançamento
```

<a id="RN-14"></a>

### `RN-14` Tag por decisão

```gherkin
Cenário: merge não cria tag
  Quando um pull request é mergeado na master
  Então nenhuma tag é criada
  E a tag nasce quando o time decide lançar
```

<a id="RN-15"></a>

### `RN-15` Número não reaproveitado

```gherkin
Cenário: tag criada por engano
  Dado a tag "v1.3.0" criada por engano
  Quando o time comenta "deny" na issue de aprovação
  Então apaga a tag com "git tag -d v1.3.0" e "git push origin :refs/tags/v1.3.0"

Cenário: correção de versão publicada
  Dado a "v1.3.0" publicada com um erro
  Então a correção sai na "v1.3.1"
```

<a id="RN-16"></a>

### `RN-16` Enviar a tag sozinha

```gherkin
Cenário: lançar a v1.3.0
  Dado a master atualizada e a última versão "v1.2.3"
  Quando o time roda:
    """
    git tag -a v1.3.0 -m "v1.3.0 - cadastro de usuário"
    git push origin v1.3.0
    """
  Então o CI testa, abre a issue de aprovação e, com "approve", publica 1.3.0, 1.3 e latest
  E "git push --tags" não é usado
```

<a id="RNF-39"></a>

### `RNF-39` Proteção da master

```gherkin
Esquema do Cenário: ação bloqueada
  Quando alguém tenta <ação> na master
  Então o GitHub bloqueia

  Exemplos:
    | ação         |
    | push direto  |
    | push forçado |
    | excluir      |
```

<a id="RNF-40"></a>

### `RNF-40` CI aprovado

```gherkin
Cenário: job agregador vermelho
  Dado o job agregador do CI reprovado no pull request
  Então o merge na master fica bloqueado
```

<a id="RNF-41"></a>

### `RNF-41` Revisão

```gherkin
Cenário: commit novo depois da aprovação
  Dado o pull request com 1 aprovação
  Quando a pessoa envia um commit novo
  Então a aprovação anterior deixa de valer
```

<a id="RNF-42"></a>

### `RNF-42` Squash

```gherkin
Cenário: merge do pull request
  Quando o pull request com 5 commits é mergeado
  Então vira um único commit na master
  E merge commit e rebase ficam desabilitados
```

---

<a id="diagramas"></a>

## Diagramas

**Fluxo de desenvolvimento git** — `2_Diagramas/fluxo-git_v01_20261011-1510.svg`

[![Diagrama de atividades de exemplo: a pessoa desenvolvedora cria a branch tarefa_42-cadastro ou fix_42-erro_cadastro a partir da master, faz commits e push e abre pull request; o CI do PR e 1 aprovação de revisão decidem: sem aprovação, novo commit; aprovado, merge por squash na master protegida e o CI publica as imagens dos componentes alterados; tag vX.Y.Z opcional publica as quatro imagens](../2_Diagramas/fluxo-git_v01_20261011-1510.svg)](../2_Diagramas/fluxo-git_v01_20261011-1510.svg)

---

<a id="historico"></a>

## Histórico

| Versão | Data | Mudança |
|---|---|---|
| **v10** | 2026-11-05 12:34 | Formato `.md`: migrado de `.html` sem mudar cenários, telas nem diagramas; links entre documentos apontam para `.md` |
| v09 | 2026-11-03 22:11 | Aprovado, sem pendências em aberto |
| v08 | 2026-11-01 18:26 | Só Gherkin e diagramas: contexto, RN, exemplo de lançamento e RNF viram cenários (mesmos IDs como âncora); status → Em revisão |
| v07 | 2026-10-11 23:31 | Status → Aprovado |
| v06 | 2026-10-11 23:22 | Versão oficial por tag git `vX.Y.Z`: `RN-11` a `RN-16` incluídos, com exemplo de comandos; status → Em revisão |
| v05 | 2026-10-11 15:15 | Status → Aprovado |
| v04 | 2026-10-11 15:11 | Diagrama de exemplo incluído (`fluxo-git_v01`) |
| v03 | 2026-10-11 15:02 | Pendência respondida: a branch principal continua `master`; `RN-10` incluído; seção Em aberto removida |
| v02 | 2026-10-11 14:59 | Pendências respondidas: `RN-06`, `RN-07` e `RN-08` alterados (branches `tarefa_*` e `fix_*`), `RN-09`, `RNF-41` e `RNF-42` incluídos |
| v01 | 2026-10-11 14:46 | Criação |

---

<sub>boaz · v10 · 2026-11-05 12:34 · `1_Requisitos/02_metodologia-git.md`</sub>
