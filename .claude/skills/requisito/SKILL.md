---
name: requisito
description: Cria, altera, revisa ou exporta em PDF documentos de requisito do produto (.md) em 1_Requisitos/ — visão do produto, funcionalidades, requisitos transversais e glossário — no padrão de 0_Assets/requisitos/tema.md, com front matter, carimbo de topo (projeto, documento, versão, data/hora, status) e histórico no rodapé. Use para escrever requisitos funcionais, não funcionais, regras de negócio, critérios de aceite, personas ou glossário, para alterar, revisar ou mudar o status de um documento dessa pasta, ou para gerar o PDF de um requisito.
---

# Documento de requisito → `1_Requisitos/`

Você é analista de produto. A saída é **sempre um `.md` em `1_Requisitos/`**, no padrão de `0_Assets/requisitos/tema.md`, com **só cenários em Gherkin, telas e diagramas**: nada de parágrafos de contexto nem tabelas de requisitos, porque o time usa no dia a dia só o Gherkin, as telas e os diagramas. A ordem de trabalho é **requisito em Gherkin → telas (UX/UI, skill `diagrama-dev`, vistas) → diagramas**.

## Escopo

Pertence aqui **o produto**: o que ele faz, para quem, com quais regras e limites mensuráveis.

Não pertence: backend, frontend, banco, API, framework, provedor ou qualquer decisão de solução. Se o pedido trouxer tecnologia, reescreva como comportamento e avise. Diagramas são das skills `diagrama-dev` (`2_Diagramas/`) e `diagrama-arquitetura` (`3_Arquitetura/`); sugira-as quando um fluxo ficar mais claro desenhado. Mudar o **visual** do HTML/PDF (cores, fontes, casca), o renderizador `requisito.mjs` ou um componente do catálogo é da skill `requisito-estilo`.

## O padrão

`0_Assets/requisitos/tema.md` é a fonte da verdade: marca d'água, formato, escrita, catálogo de componentes (o Markdown pronto de cada um), tipos de documento e versionamento. **Leia-o inteiro antes de escrever** e siga-o mesmo quando divergir deste resumo — o projeto pode tê-lo customizado. Se não existir, rode `./boaz.sh init`.

Resumo do que não pode faltar:
- **Front matter** no topo, um campo por linha, nesta ordem e entre aspas duplas: `projeto` (`PROJECT_NAME` de `.boaz`), `documento` (nome do arquivo sem `.md`), `tipo`, `titulo`, `versao`, `data` (`date '+%Y-%m-%d %H:%M'`), `status`, `objetivo` (uma frase).
- **Topo**: a linha do carimbo `> **Projeto · Tipo** · \`NN_nome\` · **vNN** · AAAA-MM-DD HH:MM · **Status**`, o `# Título` (o único `#`), `**Objetivo:** …` e, com 6 seções ou mais, `**Sumário:**` com os links das seções e do Histórico.
- **Seções**: cada uma com `---`, a âncora `<a id="…"></a>` e o `## Nome`, na ordem do tema (opcional vazia é removida). Fecha com `## Histórico` (âncora `historico`; tabela `Versão | Data | Mudança`, mais nova no topo e em negrito, até 10 linhas) e a linha `<sub>Projeto · vNN · AAAA-MM-DD HH:MM · \`1_Requisitos/NN_nome.md\`</sub>`.
- Só o Markdown do catálogo do tema: HTML apenas na âncora `<a id>` e no `<sub>` do rodapé; nada de cor, estilo, `<img>`, `<details>` ou tabela HTML.
- Funcionalidade (módulo), nesta ordem: `cenarios-tela` (Gherkin de tela; sem contrato de API, `cenarios`) → `cenarios-api` (Gherkin de API) → `telas` com `### Web`, `### PWA` e `### Mobile`, cada um com as vistas daquela plataforma (`vista-<aplicacao>-<tela>-web|pwa|mobile`) → `diagramas` → `em-aberto`; transversal: cenários, diagramas e em aberto; visão: listas e cartões (`###` + lista de campos); glossário: tabela de termos.
- Cada regra é `<a id="RF-01"></a>` (a âncora é o próprio ID, para os links `doc.md#RF-01` valerem), uma linha em branco, `### \`RF-01\` Título` e um bloco ` ```gherkin ` com um a três cenários concretos (pessoa, valor, texto exibido entre aspas). `RF` = o que a pessoa faz e o sistema responde; `RN` = a regra num caso que a prova; `RNF` = a medida ou o limite, com o número. Regra em tabela vira `Esquema do Cenário` + `Exemplos`; exemplo de dado vira doc string `"""`, e JSON vai sempre expandido (um campo por linha, objetos e listas abertos com 2 espaços). Grupo sem ID (exemplo de ponta a ponta, `fora-de-escopo`) usa âncora em kebab-case e título sem o ID em código.
- **Cenários de tela × de API** (funcionalidade com contrato de API): `cenarios-tela` ("Cenários de tela") tem o que a pessoa vê, toca e lê, sem endereço; `cenarios-api` ("Cenários de API") tem método e endereço do contrato, pedido em JSON expandido, status e código de erro (`Quando envia POST /auth/hub/entrar … Então a resposta é 401 ENTRADA_INVALIDA`), só com o que o contrato traz. Regra nas duas: o cenário de tela fica com a âncora do ID e o de API ganha `-api` (`RF-139-api`); regra só de uma seção fica com a âncora do ID. Endereço que o cenário precisa e o contrato não tem: atualize o contrato antes (skill `diagrama-dev`). Os de API alimentam `5_DEV/qa/backend/`; os de tela, `5_DEV/qa/frontend/`.
- Dentro do Gherkin o texto é puro (sem escapar `<`, `>` ou `&`). Linha de Gherkin com até 100 caracteres; passou, quebre em mais um `E`.
- Referência a outro documento que um cenário usa: citação logo acima do bloco, `> Conteúdo dos cards: [16_modulo-catalogo \`RF-164\`](16_modulo-catalogo.md#RF-164)`.
- Curto: ≤ 12 IDs `RF`, uma funcionalidade por documento — passou, divida em `NN_nome-a-…` e `NN_nome-b-…`.
- Uma linha em branco entre blocos, um item de lista por linha: o diff do git precisa ser legível.

## Diagramas e outros documentos

- Conjunto padrão de uma funcionalidade, sempre que couber (skill `diagrama-dev`, nesta ordem em Diagramas): **casos de uso** (quem faz o quê), **sequência com as telas** (os passos com os endereços e as recusas, e a tela de cada passo embutida da vista, em escala 0,5, numa faixa por fase), **contrato de API** (cada endereço com uso, quem usa, login, pedido e respostas em JSON expandido), **classes** e **modelo de documentos do banco** (coleções, embutidos, chaves no Redis e o que é só lido de outro requisito). Faltou algum? Diga na resposta e proponha desenhar.
- Diagrama ou vista: a legenda `**Título** — \`2_Diagramas/<legenda>_vNN_<AAAAMMDD-HHMM>.svg\``, uma linha em branco e `[![alt descritivo](../2_Diagramas/<arquivo>.svg)](../2_Diagramas/<arquivo>.svg)` — o `.svg` **com a versão explícita**, por caminho relativo, `alt` sem colchetes. Use a versão mais recente (`./boaz.sh diagramas`) e confira que o arquivo existe.
- Outro documento: `[Cadastro de usuário \`RF-01\`](02_cadastro-usuario.md#RF-01)`; termo do glossário: `99_glossario.md#<termo>`.

## Fluxo

**Antes de escrever**
1. Leia o tema, rode `./boaz.sh requisitos` e leia `00_visao-produto.md` e `99_glossario.md` se existirem — reaproveite nomes de personas e termos.
2. Próximos IDs (únicos no projeto inteiro, nunca reaproveitados):
   `for p in RF RNF RN; do grep -rhoE --include='*.md' "\b$p-[0-9]+" 1_Requisitos | sort -t- -k2,2n | tail -1; done`

**Documento novo**
1. Escolha o tipo pelo tema (visão, funcionalidade, transversal, glossário) e o arquivo `<NN>_<nome>.md`: `NN` = próximo livre da faixa do tipo (`ls 1_Requisitos`), `nome` em kebab-case sem acentos.
2. Se uma ambiguidade mudar o escopo, faça no máximo 1–2 perguntas. O resto **não invente**: regra, limite ou número que o pedido não deu vira item de **Em aberto** (`- pergunta — **Quem responde**`).
3. Escreva com `v01`, status `Rascunho` e histórico `v01 · Criação`.

**Alteração** ("muda", "adiciona", "remove", "aprova" num documento existente)
1. Abra o documento e aplique só o que foi pedido; não reescreva o resto e não renomeie o arquivo.
2. Nova versão: `vNN` + 1 e data/hora atuais nos 4 lugares — front matter (`versao`, `data`), carimbo, linha nova no topo do histórico (o que mudou, citando os IDs; a anterior perde o negrito; acima de 10 linhas, a mais antiga sai) e `<sub>` do rodapé. Status muda no front matter e no carimbo. Mudança de status também é versão.
3. Regra removida tem o cenário apagado; o ID não é reutilizado. Pendência respondida sai de Em aberto e vira cenário.
4. Se o documento estava `Aprovado`, volte-o para `Em revisão`, a menos que o pedido diga outra coisa.
5. Termo novo do domínio? Proponha incluí-lo em `99_glossario.md` (que também ganha versão).

**PDF** ("exporta", "gera o PDF", "manda o pdf do …")
1. Rode `./boaz.sh pdf <documento>` (ex.: `./boaz.sh pdf 02_cadastro-usuario`): o `requisito.mjs` gera o HTML com a casca e o Chrome imprime `1_Requisitos/pdf/<documento>_v<NN>_<AAAAMMDD-HHMM>.pdf` com a versão e a data do front matter, tema claro, A4, faixa de topo, histórico e cabeçalho/rodapé paginados.
2. Gere o PDF **depois** de salvar a versão pedida; não crie versão só para exportar.
3. Sem Node ou sem navegador Chromium, o comando avisa: oriente a instalar, ou a gerar o HTML (`node 0_Assets/requisitos/requisito.mjs html 1_Requisitos/<doc>.md <scratchpad>/<doc>.html`) e usar o botão **PDF** no navegador.

**Revisão** (sempre, antes de entregar)
- `node 0_Assets/requisitos/requisito.mjs conferir <arquivo> --avisos` — sem erro: front matter completo, carimbo, título, histórico e rodapé com a mesma versão e data, links e imagens existentes, âncoras citadas por outros documentos existentes, nenhuma âncora repetida, nenhum link `.html`. Os avisos (linha de Gherkin > 100) se corrigem no que você escreveu.
- Seções na ordem do tema, nenhuma vazia, um `#`, `###` só em cenários, plataformas de Telas e cartões; nenhum parágrafo, tabela ou lista fora da visão, do glossário, de Em aberto e das citações de referência.
- IDs duplicados no projeto: `grep -rhoE --include='*.md' '^<a id="(RF|RNF|RN)-[0-9]+(-api)?"' 1_Requisitos | sort | uniq -d` (deve sair vazio).
- Palavras vagas: `sed '1,/^---$/d' <arquivo> | grep -niE 'rápid|fácil|intuitiv|amigável|simples|adequad|eficiente|robust|flexível|etc\.|e/ou|entre outros|se possível|geralmente|normalmente|TBD|a definir'` — troque cada uma por critério verificável.
- Tecnologia (fora de `cenarios-api`, onde endereço, status e código são o assunto): `sed '1,/^---$/d' <arquivo> | grep -niE 'backend|frontend|banco de dados|\bAPI\b|endpoint|framework|servidor|\bSQL\b|cloud'` — reescreva como comportamento.
- Imagens aparecendo: com Chrome/Chromium, gere o HTML e tire um `--screenshot` headless (comando na skill `requisito-estilo`) e **olhe** antes de responder.

## Resposta

Responda curto: caminho do arquivo, versão e status, o que mudou (IDs), as pendências de **Em aberto** que precisam de alguém para responder e, se pedido, o caminho do PDF.
