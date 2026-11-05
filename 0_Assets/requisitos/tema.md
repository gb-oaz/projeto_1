---
projeto: "boaz"
documento: "tema"
tipo: "Tema"
titulo: "Tema dos documentos de requisito"
versao: "v11"
data: "2026-11-05 12:45"
status: "Em revisão"
objetivo: "padronizar marca d'água, formato (cenários em Gherkin, telas e diagramas) e escrita de todo documento `.md` de `1_Requisitos/`, com o Markdown pronto de cada componente."
---

> **boaz · Tema** · `tema` · **v11** · 2026-11-05 12:45 · **Em revisão**

# Tema dos documentos de requisito

**Objetivo:** padronizar marca d'água, formato (cenários em Gherkin, telas e diagramas) e escrita de todo documento `.md` de `1_Requisitos/`, com o Markdown pronto de cada componente.

**Sumário:** [Como usar](#como-usar) · [Marca d'água](#marca-dagua) · [Formato](#formato) · [Escrita](#escrita) · [Componentes](#componentes) · [Tipos de documento](#tipos) · [Versionamento e PDF](#versionamento) · [Visual](#visual) · [Histórico](#historico)

---

<a id="como-usar"></a>

## Como usar

As skills `/requisito` (conteúdo) e `/requisito-estilo` (visual e PDF) seguem este arquivo. Ele é a **regra** e o **catálogo**: cada componente aparece aqui no Markdown que se copia. Edite-o para mudar o padrão do projeto; os documentos existentes mudam só na próxima versão de cada um.

| Arquivo | Papel |
|---|---|
| `0_Assets/requisitos/tema.md` | regra, formato e catálogo (este documento) |
| `0_Assets/requisitos/requisito.mjs` | gera o HTML do documento (para o PDF) e confere a marca d'água, os links e as imagens |
| `0_Assets/requisitos/casca.html` | estilo e script do HTML gerado: temas claro, sistema e escuro, paleta das vistas, PDF, marca diagonal e destaque de Gherkin |

O documento é lido como Markdown (GitHub, VS Code, qualquer editor) e só vira HTML para o PDF. Use **só** o Markdown deste catálogo: o `requisito.mjs` entende esse subconjunto e nada mais. HTML no texto, só a âncora `<a id="…"></a>` e o `<sub>` do rodapé.

---

<a id="marca-dagua"></a>

## Marca d'água

Todo documento é identificado em cinco lugares, que dizem **a mesma versão e a mesma data**: o front matter, o carimbo, a primeira linha do histórico e a linha do rodapé (o título repete o front matter). `node 0_Assets/requisitos/requisito.mjs conferir <doc.md>` aponta qualquer diferença.

### Front matter

Primeiro bloco do arquivo, um campo por linha, nesta ordem, todo valor entre aspas duplas (`\"` para aspas dentro do texto). O `./boaz.sh requisitos` e o `./boaz.sh pdf` o leem.

```markdown
---
projeto: "Loja X"
documento: "02_cadastro-usuario"
tipo: "Funcionalidade"
titulo: "Cadastro de usuário"
versao: "v03"
data: "2026-10-11 14:30"
status: "Em revisão"
objetivo: "definir como uma pessoa cria a conta e prova que o e-mail é dela."
---
```

| Campo | Conteúdo |
|---|---|
| `projeto` | `PROJECT_NAME` de `.boaz` |
| `documento` | nome do arquivo sem `.md` |
| `tipo` | `Visão do produto`, `Funcionalidade`, `Transversal` ou `Glossário` |
| `titulo` | o mesmo do `#` |
| `versao` | `vNN`, começa em `v01` |
| `data` | `AAAA-MM-DD HH:MM`, hora local em que a versão foi gerada |
| `status` | `Rascunho` → `Em revisão` → `Aprovado`; `Obsoleto` quando substituído |
| `objetivo` | uma frase, sem ponto final no meio |

### Topo: carimbo, título, objetivo e sumário

Logo depois do front matter, para quem lê no GitHub ou no editor (o HTML do PDF monta a faixa de topo a partir do front matter):

```markdown
> **Loja X · Funcionalidade** · `02_cadastro-usuario` · **v03** · 2026-10-11 14:30 · **Em revisão**

# Cadastro de usuário

**Objetivo:** definir como uma pessoa cria a conta e prova que o e-mail é dela.

**Sumário:** [Cenários de tela](#cenarios-tela) · [Telas](#telas) · [Diagramas](#diagramas) · [Histórico](#historico)
```

O sumário só entra em documento com 6 seções ou mais. Um `#` por documento, só aqui.

### Rodapé: histórico e identificação

Última seção. Histórico com a versão mais nova no topo, em negrito, e no máximo 10 linhas (o restante fica no git); a linha final repete projeto, versão, data e caminho.

```markdown
---

<a id="historico"></a>

## Histórico

| Versão | Data | Mudança |
|---|---|---|
| **v03** | 2026-10-11 14:30 | `RF-04` incluído: limite de tentativas |
| v02 | 2026-10-10 17:05 | Status → Em revisão |
| v01 | 2026-10-10 09:00 | Criação |

---

<sub>Loja X · v03 · 2026-10-11 14:30 · `1_Requisitos/02_cadastro-usuario.md`</sub>
```

No PDF, o status `Rascunho`, `Em revisão` ou `Obsoleto` sai estampado em diagonal em cada página; da segunda página em diante, o cabeçalho traz `projeto · documento` e `versão · data · status`, e o rodapé, o caminho e “Página 2 de 4”.

---

<a id="formato"></a>

## Formato

**Arquivo:** `1_Requisitos/<NN>_<nome>.md`

- `NN`: dois dígitos, ordem de leitura (`00` visão, `01`–`89` funcionalidades, `90`–`98` transversais, `99` glossário)
- `nome`: kebab-case, sem acentos; não muda entre versões (links e âncoras continuam válidos)

**Esqueleto** (funcionalidade; os outros tipos estão em [Tipos de documento](#tipos)). O documento traz só o que o time usa no dia a dia: **cenários em Gherkin**, as **telas** desenhadas (vistas e blueprints) e os **diagramas**. Nada de parágrafos de contexto nem tabelas de requisitos:

```markdown
---                                      ← front matter
…
---

> **Projeto · Tipo** · `NN_nome` · **vNN** · data · **Status**

# Título

**Objetivo:** …

---

<a id="cenarios-tela"></a>

## Cenários de tela

<a id="RF-01"></a>

### `RF-01` Título do cenário

… (um bloco ```gherkin)

---

<a id="cenarios-api"></a>                ← quando há contrato de API

## Cenários de API

---

<a id="telas"></a>                       ← ### Web, ### PWA e ### Mobile: uma vista por plataforma

## Telas

---

<a id="diagramas"></a>                   ← casos de uso, sequência com telas, contrato, classes, documentos

## Diagramas

---

<a id="em-aberto"></a>

## Em aberto

---

<a id="historico"></a>

## Histórico

---

<sub>…</sub>
```

- Seções sempre nesta ordem, cada uma com `---` antes, a âncora `<a id="…"></a>` com o nome em kebab-case e o `##`; seção opcional sem conteúdo é **removida** (nunca fica vazia nem com "N/A")
- Sem numeração nas seções: o nome já é a referência
- Linha em branco entre blocos; uma frase de lista por linha: o diff do git precisa continuar legível

**Tamanho**

| Limite | Valor | Passou? |
|---|---|---|
| Assunto | 1 funcionalidade por documento | crie outro documento |
| Requisitos funcionais | ≤ 12 IDs `RF` por documento | divida em `NN_nome-a-…` e `NN_nome-b-…` |
| Cenários por ID | ≤ 3, um caso concreto cada | use Esquema do Cenário com Exemplos |
| Linha de Gherkin | ≤ 100 caracteres | quebre em mais um `E` |
| Texto fora de Gherkin | nenhum, exceto na visão e no glossário | vire cenário, tela ou diagrama |

---

<a id="escrita"></a>

## Escrita

Todo requisito é um **cenário em Gherkin**, em português, com uma pessoa e um dado concretos (a Maria, a escola `EBFGA`, o código `EBFGA-LUCAS-0412`). O que antes era tabela, contexto ou parágrafo vira cenário; o que é desenho vira tela ou diagrama.

| ID | O que o cenário mostra | Exemplo |
|---|---|---|
| `RF` | o que a pessoa faz e o que o sistema responde | `Quando a Maria informa a entrada e a senha · Então entra no hub` |
| `RN` | a regra do negócio, num caso que a prova | `Dado a sigla "EBFGA" já usada · Quando outra escola informa "ebfga" · Então é recusada` |
| `RNF` | a medida ou o limite técnico, com o número | `Quando a fatura muda · Então o total aparece em até 1 s` |

- Palavras-chave: `Cenário`, `Esquema do Cenário` + `Exemplos`, `Dado`, `Quando`, `Então`, `E`, `Mas`; textos exibidos entre aspas
- Regra em forma de tabela (faixas, perfis, estados, tamanhos): `Esquema do Cenário` com `Exemplos`, colunas curtas
- Exemplo de dado (token, documento, comando): *doc string* entre `"""` dentro do passo
- JSON sempre expandido: um campo por linha, objetos e listas abertos com 2 espaços, nunca vários campos na mesma linha
- Um cenário, um comportamento; até 3 cenários por ID
- Diga **o quê**: a tecnologia só aparece nos documentos transversais e nos RNF que a decidem
- Datas `AAAA-MM-DD`, horas `HH:MM`; número e unidade com espaço (`2 s`, `R$ 1.500,00`), decimal com vírgula
- Dúvida não vira cenário: vai para Em aberto
- Funcionalidade com contrato de API: os cenários se dividem em **Cenários de tela** (`cenarios-tela`: o que a pessoa vê, toca e lê, sem endereço) e **Cenários de API** (`cenarios-api`: método e endereço do contrato, pedido em JSON, status e código de erro, como `Quando envia POST /auth/hub/entrar … Então a resposta é 401 ENTRADA_INVALIDA`); sem contrato, a seção é uma só, `cenarios`
- Ordem de um módulo (funcionalidade): **Cenários de tela** → **Cenários de API** → **Telas** (`###` Web, PWA e Mobile, cada uma com as vistas daquela plataforma: `vista-<aplicação>-<tela>-web`, `-pwa` e `-mobile`) → **Diagramas**, nesta ordem: casos de uso, sequência com as telas de cada passo, contrato de API, classes e modelo de documentos do banco
- Regra que vale nas duas seções tem um cenário em cada: o de tela fica com a âncora do ID (`RF-139`, os links continuam valendo) e o de API ganha `-api` (`RF-139-api`); regra só de uma seção fica com a âncora do ID. Os de API são a base do qa do backend; os de tela, do qa da web e do mobile

Palavras vagas são proibidas; troque por um valor no cenário:

| Evite | Troque por |
|---|---|
| rápido, eficiente, performático | tempo com limite: `em até 2 s` |
| fácil, intuitivo, amigável, simples | tarefa + medida: `em até 3 toques` |
| seguro, robusto, confiável | ameaça + controle: `a 6ª tentativa é bloqueada` |
| etc., entre outros, e/ou | a lista completa nos Exemplos |
| TBD, a definir | uma pendência em Em aberto |

---

<a id="componentes"></a>

## Componentes

| Elemento | Markdown | Regra |
|---|---|---|
| Título | `# Título` | só no topo; primeira letra maiúscula (e nomes próprios), sem ID nem ponto final |
| Seção | `<a id="…"></a>` + `## Nome` | só os nomes do esqueleto do tipo, na ordem dele |
| Subseção | `### …` | no título de cada cenário, nas plataformas de Telas e nos cartões da visão; nunca `####` |
| Identificador | `` `RF-01` `` | IDs em código; valores literais, campos e arquivos também em código |
| Termo definido | `**termo**` | primeira ocorrência de um termo do glossário; no máximo um por frase |
| Texto exibido | `“Código expirado”` | mensagem ou rótulo, exatamente como aparece, entre aspas curvas (fora do Gherkin) |
| Estrangeirismo | `*checkout*` | não use itálico para mais nada |
| Lista | `- item` | `1.` só quando a ordem importa; itens e células sem ponto final |
| Separador | `---` | só antes de cada seção e do rodapé |

### Cenário

Uma âncora por ID, com o próprio ID: outro documento aponta para `17_modulo-usuario.md#RN-179`. Grupo sem ID (como `fora-de-escopo` ou um exemplo de ponta a ponta) usa uma âncora em kebab-case e o título sem o ID em código. O Gherkin vai num bloco ` ```gherkin ` (o GitHub e o editor colorem; o PDF também).

````markdown
<a id="RN-02"></a>

### `RN-02` Validade do código

```gherkin
Esquema do Cenário: código informado depois de um tempo
  Dado o código enviado a "ana@exemplo.com" há <tempo>
  Quando a Ana informa o código
  Então <resultado>

  Exemplos:
    | tempo  | resultado                 |
    | 14 min | a conta fica confirmada   |
    | 16 min | aparece "Código expirado" |
```
````

Referência a outro documento que o cenário usa vai numa citação logo acima do bloco (dentro do Gherkin não há link):

```markdown
> Conteúdo dos cards: [16_modulo-catalogo `RF-164`](16_modulo-catalogo.md#RF-164)
```

### Diagramas e telas

Referencie o `.svg` de `2_Diagramas/` ou `3_Arquitetura/` **com a versão explícita**, por caminho relativo: a legenda em negrito com o caminho, uma linha em branco e a imagem que, clicada, abre o diagrama em tamanho real. As vistas (`vista-*`) e as blueprints (`blueprint-*`) usam o mesmo componente, na seção Telas.

```markdown
**Fluxo de cadastro** — `2_Diagramas/fluxo-cadastro_v02_20261011-0915.svg`

[![Fluxo de cadastro: da criação da conta à confirmação do e-mail](../2_Diagramas/fluxo-cadastro_v02_20261011-0915.svg)](../2_Diagramas/fluxo-cadastro_v02_20261011-0915.svg)
```

- O texto alternativo descreve o diagrama inteiro, sem colchetes
- Vista com paletas: quando o `.svg` traz os fragmentos `#paleta-<id>-<claro|escuro>`, o HTML do PDF ganha sozinho o botão **Paleta** (temas de `0_Assets/design/tokens/cores.json`); no Markdown a vista aparece no primeiro tema, claro

### Visão do produto: objetivos, personas, escopo

Um `###` por objetivo ou persona, com uma lista de campos em negrito (o HTML monta os cartões; a inicial do avatar é a primeira letra do nome):

```markdown
### Reduzir abandono

- **Indicador:** carrinhos abandonados / iniciados
- **Meta:** ≤ 40 %
- **Prazo:** em 6 meses

### Cliente

- **Quem é:** compra pela loja, pelo celular
- **Precisa de:** finalizar a compra em poucos passos
```

Escopo e fora de escopo são listas simples, cada uma na sua seção.

### Em aberto

Pergunta e, depois do travessão, quem responde. Respondida, vira cenário e sai da lista (o histórico registra).

```markdown
- Clientes de outros países precisam de cadastro? — **Produto**
- Quantos reenvios de código por hora? — **Segurança**
```

### Amostras da marca

Só na seção Identidade da visão do produto: logos, símbolo, ícone e favicon de `0_Assets/design/identidade/`, por caminho relativo. A legenda dá o nome, o caminho e o fundo para o qual o arquivo foi feito (`fundo claro`, `fundo medio` ou `fundo escuro`; sem fundo, acompanha o tema).

```markdown
**Símbolo** — `logos/simbolo.svg` · fundo claro

![Símbolo de duas cores](../0_Assets/design/identidade/logos/simbolo.svg)
```

### Referência a outro documento

```markdown
Usa a conta de [Cadastro de usuário `RF-01`](02_cadastro-usuario.md#RF-01).
```

### Glossário

Termos em ordem alfabética numa tabela, definição sem ponto final; a âncora em kebab-case na célula do termo recebe os links (`99_glossario.md#pedido`).

```markdown
| Termo | Definição |
|---|---|
| <a id="conta-confirmada"></a>**Conta confirmada** | conta cujo e-mail foi validado pelo código |
| <a id="pedido"></a>**Pedido** | conjunto de itens pagos numa mesma compra |
```

---

<a id="tipos"></a>

## Tipos de documento

| Tipo | Arquivo | Seções, na ordem (**obrigatórias** em negrito) |
|---|---|---|
| Visão do produto | `00_visao-produto.md` | **Problema** (lista), **Objetivos e métricas**, **Personas**, **Escopo**, Identidade, Fora de escopo |
| Funcionalidade | `01`–`89` | **Cenários de tela**, Cenários de API (quando há contrato), Telas (Web, PWA, Mobile), Diagramas (casos de uso, sequência com telas, contrato, classes, documentos), Em aberto |
| Transversal | `90`–`98` e as regras do projeto | **Cenários**, Diagramas, Em aberto |
| Glossário | `99_glossario.md` | **Termos** |

Todos começam com o front matter e o topo e terminam com o histórico e o rodapé. O que antes era Contexto, Fora de escopo ou um exemplo vira o primeiro ou o último cenário da seção Cenários (âncora em kebab-case). A ordem de trabalho é **requisito em Gherkin → telas (UX/UI) → diagramas**.

---

<a id="versionamento"></a>

## Versionamento e PDF

- Um arquivo por documento, editado no lugar; o git guarda o conteúdo antigo (diferente dos diagramas, que geram um arquivo por versão)
- Toda alteração, inclusive de status, é uma versão nova: `vNN` + 1 e data/hora atuais no front matter, no carimbo, numa linha nova no topo do histórico e no rodapé
- IDs são únicos no projeto inteiro, não por documento: o próximo é o maior existente + 1
- IDs nunca são reaproveitados nem renumerados; o cenário removido sai do documento e o histórico registra (`RF-07` removido)
- Ao trocar a versão de um diagrama referenciado, o documento também ganha versão
- **PDF versionado**: `./boaz.sh pdf 02_cadastro-usuario` gera `1_Requisitos/pdf/02_cadastro-usuario_v03_20261011-1430.pdf` (versão e data do front matter: a mesma versão gera sempre o mesmo nome), pelo HTML do `requisito.mjs` com a casca, em tema claro e A4. Precisa de Node e de Chrome, Chromium, Edge ou Brave
- Para ver o documento com a casca no navegador (temas, paleta das vistas, botão PDF): `node 0_Assets/requisitos/requisito.mjs html 1_Requisitos/<doc>.md /tmp/<doc>.html` e abra o arquivo

---

<a id="visual"></a>

## Visual

O visual do HTML gerado (PDF e navegador) é da `casca.html`, a mesma paleta dos diagramas: acento para links, IDs e passos do Gherkin; verde para Aprovado e escopo; amarelo para Rascunho e Em aberto; vermelho para Obsoleto. Inter no texto, JetBrains Mono em código, IDs, datas e versões. A casca é versionada na primeira linha do CSS e do script (`boaz · tema dos documentos de requisito · vNN`) e é mudada só pela skill `/requisito-estilo`. No Markdown, o visual é o do leitor (GitHub, editor): nada de cor, estilo ou HTML além da âncora e do `<sub>`.

---

<a id="historico"></a>

## Histórico

| Versão | Data | Mudança |
|---|---|---|
| **v11** | 2026-11-05 12:45 | Documentos em `.md`: front matter no lugar dos `<meta>`, carimbo em citação, cenário com âncora `<a id>` + `###` + bloco `gherkin`, figura com legenda e imagem clicável, glossário em tabela; o HTML (PDF) é gerado pelo `requisito.mjs` com a `casca.html`, que sai do `tema.html` |
| v10 | 2026-11-03 11:37 | Ordem de um módulo: cenários de tela, cenários de API, telas por plataforma (Web, PWA e Mobile, uma vista para cada) e diagramas na ordem casos de uso, sequência com telas, contrato, classes e documentos |
| v09 | 2026-11-02 00:11 | Escrita e esqueleto: cenários de tela e de API em seções separadas (`cenarios-tela`, `cenarios-api`, sufixo `-api`) |
| v08 | 2026-11-01 22:01 | Escrita: JSON sempre expandido nas doc strings |
| v07 | 2026-11-01 18:31 | Requisito só com Gherkin, telas e diagramas; cada regra (RF, RN, RNF) é um cenário com o ID como âncora; casca v07 |
| v06 | 2026-10-24 03:59 | Vista com paletas: botão **Paleta** troca o tema de cor e o modo das vistas pelo fragmento `#paleta-<id>-<modo>` do `.svg`; casca v06 |
| v05 | 2026-10-17 18:14 | Visão do produto ganha a seção opcional Identidade, com as amostras da marca |
| v04 | 2026-10-11 04:38 | Impressão: diagrama com no máximo 75 mm de altura |
| v03 | 2026-10-11 03:21 | Impressão: histórico pode quebrar entre páginas; legenda e cabeçalho de tabela não se separam das linhas |
| v02 | 2026-10-11 16:00 | Rolagem lateral: tabelas largas rolam dentro do documento, código longo quebra a linha |

---

<sub>boaz · v11 · 2026-11-05 12:45 · `0_Assets/requisitos/tema.md`</sub>
