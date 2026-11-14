---
name: gherkin-bdd
description: Base comum de todos os agentes do boaz para ler, interpretar e rastrear requisitos escritos em Gherkin dentro dos documentos Markdown de 1_Requisitos/ — localizar o documento e a seção certa, achar o cenário pelo ID (RF/RN/RNF), entender Cenário, Esquema do Cenário, Exemplos e Dado/Quando/Então/E/Mas, separar regra de negócio de critério de aceitação, achar ambiguidades e lacunas, ligar cenário a teste e a código e dizer o que ficou atendido ou pendente. Use antes de qualquer trabalho que dependa de um requisito existente (desenho, vista, diagrama, teste, código, revisão) e sempre que precisar citar, cobrir ou questionar um cenário.
---

# Gherkin e BDD → requisitos em Markdown

Os requisitos do boaz são **documentos `.md` em `1_Requisitos/`**; o comportamento esperado está em **blocos ` ```gherkin `** dentro deles. Não existe um `.feature` por requisito: os `.feature` de `5_DEV/qa/` são **cópias** dos cenários de API, com a tag do ID. A fonte da verdade é sempre o `.md`.

Esta skill é de **leitura e rastreabilidade**. Escrever ou mudar requisito é só do `deboaz-arc` (skill `requisito`); os outros agentes **propõem** o cenário na resposta, nunca editam `1_Requisitos/`.

## 1 · Localizar (sem varrer pastas)

1. Lista dos documentos: `./boaz.sh requisitos` (ou `ls 1_Requisitos`). Faixas: `00` visão · `01–06` transversais (stack, git, CI, imagens, CD, design system) · `07–13` UX/UI das aplicações · `14` estrutura dos projetos · `15+` módulos · `90` identidade · `99` glossário.
2. Pelo ID: `grep -rn '<a id="RF-134' 1_Requisitos` → documento e linha; leia **só o intervalo** do cenário (`sed -n '<linha>,+25p'`).
3. Por assunto: `grep -rn '^### ' 1_Requisitos/<doc>.md` dá o índice de cenários; `grep -n '<a id="cenarios-' <doc>.md` dá onde começam as seções.
4. Termo do domínio: `99_glossario.md#<termo>`. Use exatamente esse nome (linguagem ubíqua) em código, teste, vista e diagrama.

## 2 · Anatomia de um documento

| Elemento no `.md` | Papel BDD |
|---|---|
| Front matter + carimbo (`versao`, `status`) | versão do requisito que você está cobrindo — cite-a |
| `# Título` / `**Objetivo:**` | a funcionalidade (equivale ao `Feature`) |
| `## Cenários de tela` (`cenarios-tela`) | o que a pessoa vê, toca e lê — base de vistas, front e `qa/frontend` |
| `## Cenários de API` (`cenarios-api`) | método, endereço, JSON, status e código de erro — base de contrato, back e `qa/backend` |
| `<a id="RF-134"></a>` + `### \`RF-134\` Título` | a regra (equivale ao `Rule`); o ID é a chave de rastreio |
| âncora `RF-134-api` | a mesma regra vista pela API |
| âncora kebab-case sem ID (ex.: `formas-de-entrar`) | grupo de exemplo de ponta a ponta |
| `> [doc \`RF-164\`](16_modulo-catalogo.md#RF-164)` acima do bloco | dependência de outro requisito: leia também |
| `## Telas`, `## Diagramas` | vistas e UML que ilustram os cenários |
| `## Em aberto` | pergunta sem resposta — **não implemente** o que depende dela |

Tipos de ID: `RF` o que a pessoa faz e o sistema responde · `RN` a regra num caso que a prova · `RNF` a medida ou o limite, com número.

## 3 · Palavras-chave (o projeto escreve em português)

| No `.md` / `.feature` (`# language: pt`) | Equivalente | Uso |
|---|---|---|
| `Funcionalidade:` (só nos `.feature`) | Feature | título do documento |
| `Regra:` (não usado; o `###` com ID cumpre) | Rule | agrupa cenários de uma regra |
| `Contexto:` (raro; só em `.feature`) | Background | passos comuns a todos os cenários |
| `Cenário:` | Scenario | um exemplo concreto |
| `Esquema do Cenário:` + `Exemplos:` | Scenario Outline + Examples | regra em tabela; `<coluna>` vira valor de cada linha |
| `Dado` | Given | estado anterior (quem, com quais dados) |
| `Quando` | When | **uma** ação ou evento |
| `Então` | Then | resultado observável |
| `E` / `Mas` | And / But | continua o passo anterior (do mesmo tipo) |
| `"""` | doc string | JSON ou texto longo, sempre expandido |
| `\| a \| b \|` | data table | dados de um passo ou linhas de `Exemplos` |

Step definitions são o código que liga cada frase a uma ação de teste; ficam só em `5_DEV/qa/` (skill `estrategia-qa`). No `.md`, o texto é para pessoas: sem seletor, classe, tabela de banco ou nome de framework.

## 4 · Interpretar um cenário

Para cada cenário que a sua tarefa toca, extraia:

- **Ator e estado** (`Dado`): quem, com quais dados concretos — esses dados viram fixture de teste e exemplo de vista.
- **Gatilho** (`Quando`): a ação; em API, método + endereço + corpo.
- **Critério de aceitação** (`Então`/`E`): cada linha é uma verificação objetiva — texto exato entre aspas, status, código de erro, campo, tempo.
- **Regra de negócio**: o *porquê* que o cenário prova (o `RN` e o título do `###`). Uma regra pode precisar de vários cenários.
- **Classe do cenário**: positivo (caminho feliz), negativo (recusa, erro, permissão negada), alternativo (outro caminho válido), limite (valor na fronteira do número do `RNF`).

## 5 · Ambiguidades e lacunas (checklist)

Antes de agir, confira e **anote** o que faltar:

- Termo que não está no glossário ou é usado com dois sentidos.
- `Então` não verificável ("rápido", "corretamente", "adequado") ou sem o texto exato exibido.
- Regra só com caminho feliz: falta o negativo (dado inválido, sem permissão, recurso inexistente, duplicado, sessão vencida).
- `Esquema do Cenário` sem a linha de fronteira (valor no limite, logo acima, logo abaixo).
- Cenário de tela sem o par de API (ou vice-versa) quando a regra existe nas duas pontas.
- Estados de tela não descritos: vazio, carregando, erro, sucesso, sem conexão.
- Dois cenários que se contradizem, ou cenário que contradiz vista, contrato ou código.
- Dependência citada (`> [...]`) que não existe mais ou mudou de versão.
- Item de `Em aberto` que bloqueia a sua tarefa.

Achou? **Não decida sozinho.** Pare a parte afetada, siga com o resto e devolva na resposta, endereçado ao `deboaz-arc`, com o ID, a dúvida e — quando ajudar — o cenário proposto em Gherkin (formato em `referencias/escrita-de-cenarios.md`).

## 6 · Regras invioláveis

- Leia o requisito **antes** de atuar; cite documento, versão e IDs no que entregar.
- Não invente regra, texto exibido, limite ou código de erro: se não está no `.md` (ou no contrato do `deboaz-arc`), é lacuna.
- Não altere requisito em silêncio; só o `deboaz-arc` escreve em `1_Requisitos/`, preservando a estrutura (front matter, âncoras, seções, histórico).
- Divergência entre implementação e requisito é reportada, não "corrigida" no requisito para caber no código.
- Mudou um cenário de API? Os `.feature` e passos do qa precisam mudar junto (`deboaz-qa`).

## 7 · Rastreabilidade e relatório

O ID é a chave que liga tudo: requisito (`<a id>`) → vista/diagrama (citação na legenda) → `.feature`/teste (tag `@RF-134` ou nome do teste) → código (teste unitário que cita o ID). Comandos de rastreio e a matriz em `referencias/rastreabilidade.md`.

Toda resposta que tocou requisito termina com:

```
Requisito: 1_Requisitos/<doc>.md (vNN)
Atendidos: RF-134, RF-137-api
Pendentes: RF-135 (motivo)
Lacunas/ambiguidades → deboaz-arc: RN-xx — <dúvida>
```

## Skills relacionadas

`requisito` (escrever, só o `deboaz-arc`) · `engenharia-requisitos` (qualidade e impacto) · `estrategia-qa` (do cenário ao teste) · `desenvolvimento` (fluxo TDD) · `engenharia-software` (BDD no ciclo).
