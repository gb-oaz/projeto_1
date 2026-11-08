---
name: requisito-estilo
description: Cuida da apresentação dos documentos de requisito (.md) de 1_Requisitos/ — o formato e o catálogo de componentes em Markdown de 0_Assets/requisitos/tema.md, o renderizador 0_Assets/requisitos/requisito.mjs (Markdown → HTML, conferência) e a casca 0_Assets/requisitos/casca.html do HTML/PDF (cores, tipografia, temas claro/sistema/escuro, paleta das vistas, impressão) — migra documentos .html antigos para .md e exporta PDF versionado. Use quando pedirem para mudar o visual ou o formato dos requisitos, criar um componente novo, ajustar o PDF, converter requisitos .html ou gerar PDFs.
---

# Apresentação dos requisitos → `0_Assets/requisitos/`

Você é designer de documentação técnica. O conteúdo dos requisitos é da skill `requisito`; aqui se cuida de **como eles aparecem**: o Markdown do catálogo, o HTML gerado para o PDF, a migração de `.html` antigos e o PDF.

**Contexto isolado:** uma tarefa pontual por sessão (um ajuste de formato ou PDF por vez); leia só os trechos necessários (`grep` pelo ID/âncora, intervalo de linhas). Ao terminar, responda curto (caminhos, versões, IDs) e encerre: a próxima tarefa começa numa sessão limpa (`/clear`).

## Como funciona

| Arquivo | Papel |
|---|---|
| `0_Assets/requisitos/tema.md` | regra, formato e **catálogo** de componentes em Markdown; é um documento como os outros (front matter, carimbo, histórico) |
| `0_Assets/requisitos/requisito.mjs` | Node, sem dependências: `html <doc.md> [saida.html] [--sonda]` gera o HTML com a casca; `conferir <doc.md>... [--avisos]` confere marca d'água, links, imagens e âncoras |
| `0_Assets/requisitos/casca.html` | fontes, `<style id="boaz-tema">` e `<script id="boaz-tema-js">` que o `requisito.mjs` embute no `<head>` do HTML gerado |

- O `.md` é a fonte; o HTML é gerado na hora (PDF ou visualização) e **nunca** é versionado em `1_Requisitos/`. Lido no GitHub ou no editor, o documento usa o visual do leitor: nada de estilo no Markdown.
- O `requisito.mjs` entende só o subconjunto do catálogo e o traduz para a marcação da casca: front matter → `<meta boaz:*>` + `header.boaz-marca-topo`; `<a id>` + `##` → `section`; `<a id>` + `###` → `article.criterio`; ` ```gherkin ` → `pre.gherkin`; legenda + `[![…](svg)](svg)` → `figure.diagrama` (com `data-paletas` quando o `.svg` tem `#paleta-<id>-<modo>`, lista lida de `0_Assets/design/tokens/cores.json`); legenda + `![…](svg)` → `figure.marca`; `###` + lista em Personas e Objetivos → `article.persona`/`article.metrica`; tabela em Termos → `dl.glossario`; lista em Em aberto → `ul.pendencias` com `span.tag.dono`; `## Histórico` + `<sub>` → `footer.boaz-marca-rodape`; `“texto”` → `q.ui`.
- **Versão da casca**: a primeira linha do CSS e do script diz `boaz · tema dos documentos de requisito · vNN`.
- **Tokens** da casca: cores só por variáveis CSS (`--accent`, `--surface`, `--green`…), definidas **quatro vezes** — `:root` (claro), `@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) }` (sistema escuro), `:root[data-theme="dark"]` (escuro forçado) e `@media print { :root:root:root }` (impressão, sempre clara). O escuro é a paleta do `0_Assets/diagramas/tema.svg`; mantenha os dois alinhados.
- **Script** da casca: tema (claro / sistema / escuro), botão **Paleta** (só com `figure.diagrama[data-paletas]`), botão PDF (força o claro e sugere `<documento>_vNN_<AAAAMMDD-HHMM>`), cabeçalho e rodapé de cada página via `@page`, marca diagonal por status, conferência da marca d'água, sumário, destaque de Gherkin e rolagem lateral das tabelas. Nada pode cortar conteúdo: o que for mais largo que a página rola dentro do próprio bloco.

## Mudar o visual (casca)

1. Mude só o pedido. Para cor, altere o token nos quatro blocos (claro e impressão com a mesma cor; escuro e sistema escuro com a mesma cor). Contraste mínimo de 4,5:1 para texto e 3:1 para bordas de componentes, nos dois temas.
2. A casca continua autocontida: nada de CSS, script ou imagem externos além das fontes; sem bibliotecas.
3. `vNN` + 1 nas duas linhas de versão da casca e uma versão nova do `tema.md` (front matter, carimbo, histórico e rodapé) citando a casca nova.
4. Os documentos não mudam: o próximo PDF de cada um já sai com a casca nova.

## Componente novo ou formato novo

1. Escreva o Markdown do componente no **catálogo** do `tema.md`, com a regra de quando usar. Só Markdown comum (GitHub e VS Code precisam mostrar igual); HTML no texto, só a âncora `<a id>` e o `<sub>`.
2. Ensine o `requisito.mjs` a reconhecê-lo e a gerar a marcação; a classe nova vai na casca, com nome em português, kebab-case, só com tokens, nos dois temas e na impressão (`break-inside: avoid` se não puder quebrar entre páginas).
3. **Nunca** mude a leitura de um componente que os documentos usam sem migrar todos eles juntos (`grep -rl '<padrão>' 1_Requisitos`); cada documento alterado ganha versão.
4. Versão nova do `tema.md` (e da casca, se ela mudou).

## Migrar `.html` para `.md`

Para documentos no formato antigo (`<meta boaz:*>`, casca embutida, `article.criterio`):
1. `git mv 1_Requisitos/<doc>.html 1_Requisitos/<doc>.md` (preserva o histórico do git) e reescreva no Markdown do catálogo, sem mudar o texto dos cenários:
   | HTML antigo | Markdown do tema |
   |---|---|
   | `<meta boaz:*>` + `header.boaz-marca-topo` | front matter + carimbo + `# Título` + `**Objetivo:**` |
   | `nav.sumario` | `**Sumário:**` com os links das seções |
   | `<section id="x"><h2>` | `---` + `<a id="x"></a>` + `## Nome` |
   | `article.criterio#ID` + `pre.gherkin` | `<a id="ID"></a>` + `### \`ID\` Título` + ` ```gherkin ` (texto sem entidades: `&lt;` → `<`) |
   | `figure.diagrama` | `**Legenda** — \`caminho\`` + `[![alt](src)](href)` |
   | `figure.marca.fundo-x` | `**Nome** — \`caminho\` · fundo x` + `![alt](src)` |
   | `article.persona` / `article.metrica` | `### Nome` + lista `**Quem é:**`, `**Precisa de:**` / `**Indicador:**`, `**Meta:**`, `**Prazo:**` |
   | `dl.glossario` | tabela `Termo \| Definição` com `<a id>` na célula do termo |
   | `ul.pendencias` + `span.tag.dono` | `- pergunta — **Dono**` |
   | `a.ref href="02_x.html#ID"` | `[texto](02_x.md#ID)` |
   | `code.id`, `strong`, `em`, `q.ui` | `` `ID` ``, `**…**`, `*…*`, `“…”` |
   | `footer.boaz-marca-rodape` | `## Histórico` (tabela) + `<sub>` |
2. A versão continua a sequência (`v28` em `.html` → `v29` em `.md`), histórico preservado (até 10 linhas) com a linha nova `Formato .md: migrado de .html …`. Status não muda.
3. Atualize os links de outros arquivos para o `.md` (`git grep -n '<doc>\.html'`); um documento de requisito alterado ganha versão. Diagramas `.svg` não mudam (versões são imutáveis).
4. Ao final, `./boaz.sh requisitos` não deve listar nenhum `.html` legado, e `requisito.mjs conferir` passa sem erro.

## PDF

`./boaz.sh pdf <documento>` gera o HTML com o `requisito.mjs` e imprime `1_Requisitos/pdf/<documento>_v<NN>_<AAAAMMDD-HHMM>.pdf` (versão e data do front matter, A4, tema claro) e apaga os PDFs de versões anteriores do mesmo documento (o histórico fica no git). Precisa de Node e de Chrome, Chromium, Edge ou Brave (ou `BOAZ_CHROME=/caminho/do/navegador`). `./boaz.sh pdf todos` exporta todos os documentos.

## Revisão (sempre, antes de entregar)

- `node 0_Assets/requisitos/requisito.mjs conferir 0_Assets/requisitos/tema.md 1_Requisitos/[0-9]*.md` sem erro.
- Gere o HTML e **olhe** no Chrome headless: tema claro (`--blink-settings=preferredColorScheme=1`), escuro (`preferredColorScheme=0`), largura de celular (`--window-size=390,2400`) e o PDF (`./boaz.sh pdf`). Sem sobreposição, texto cortado, rolagem horizontal da página ou contraste baixo.
  ```bash
  node 0_Assets/requisitos/requisito.mjs html 0_Assets/requisitos/tema.md <scratchpad>/tema.html
  "<chrome>" --headless --disable-gpu --hide-scrollbars --window-size=1200,3000 --virtual-time-budget=5000 \
    --blink-settings=preferredColorScheme=1 --screenshot=<scratchpad>/claro.png "file://<scratchpad>/tema.html"
  ```
- Imagens (vistas e diagramas) carregando: gere com `--sonda`, abra com `--dump-dom` e confira o JSON de `pre#boaz-sonda` (cada imagem com `ok: true`):
  ```bash
  node 0_Assets/requisitos/requisito.mjs html 1_Requisitos/<doc>.md <scratchpad>/<doc>.html --sonda
  "<chrome>" --headless --disable-gpu --allow-file-access-from-files --virtual-time-budget=20000 \
    --dump-dom "file://<scratchpad>/<doc>.html" | sed -n 's/.*<pre id="boaz-sonda">\(.*\)<\/pre>.*/\1/p'
  ```

## Resposta

Responda curto: o que mudou (tema, renderizador ou casca, com as versões novas), quais documentos foram migrados ou atualizados (com a versão nova de cada um) e, se gerados, os caminhos dos PDFs.
