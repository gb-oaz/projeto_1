---
name: requisito-estilo
description: Cuida da estilização dos documentos de requisito (.html) de 1_Requisitos/ — personaliza o tema 0_Assets/requisitos/tema.html (cores, tipografia, componentes, marca d'água, temas claro/sistema/escuro, impressão), aplica o tema atualizado a documentos existentes, migra documentos .md antigos para .html e exporta PDF versionado. Use quando pedirem para mudar o visual dos requisitos, criar um componente novo, atualizar a casca de documentos, converter requisitos .md ou gerar PDFs.
---

# Estilização dos requisitos → `0_Assets/requisitos/tema.html`

Você é designer de documentação técnica. O conteúdo dos requisitos é da skill `requisito`; aqui se cuida de **como eles aparecem**: o tema, a casca embutida em cada documento, a migração de `.md` e o PDF.

## Como o tema funciona

`0_Assets/requisitos/tema.html` é ao mesmo tempo **regra**, **catálogo renderizado** e **fonte da casca**. Leia-o inteiro antes de mexer.

- **Casca**: o bloco entre `<!-- boaz:casca:inicio -->` e `<!-- boaz:casca:fim -->` no `<head>` — fontes (Google Fonts: Inter e JetBrains Mono), `<style id="boaz-tema">` e `<script id="boaz-tema-js">`. Todo documento embute uma cópia idêntica: é autocontido, e o PDF de uma versão nunca muda por causa de outra.
- **Versão da casca**: a primeira linha do CSS e do script diz `boaz · tema dos documentos de requisito · vNN` — é por ela que `./boaz.sh requisitos` mostra quais documentos usam um tema anterior.
- **Tokens**: cores só por variáveis CSS (`--accent`, `--surface`, `--green`…), definidas **quatro vezes** — `:root` (claro), `@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) }` (sistema escuro), `:root[data-theme="dark"]` (escuro forçado) e `@media print { :root:root:root }` (impressão, sempre clara). O escuro é a paleta do `0_Assets/diagramas/tema.svg`; mantenha os dois alinhados.
- **Script**: tema (claro / sistema / escuro, salvo no navegador), botão **Paleta** (só quando há `figure.diagrama[data-paletas]`: troca tema de cor e modo das vistas pelo fragmento `#paleta-<id>-<claro|escuro>` do `.svg`; abre sempre no primeiro tema, claro), botão PDF (força o claro e sugere `<documento>_vNN_<AAAAMMDD-HHMM>`), cabeçalho e rodapé de cada página via `@page` margin boxes, marca diagonal por status, conferência da marca d'água, sumário, destaque de Gherkin e rolagem lateral das tabelas (envolve cada `<table>` em `.boaz-tabela`; não escreva esse wrapper à mão). Ele lê os `<meta name="boaz:…">`. Nada no tema pode cortar conteúdo: o que for mais largo que a página rola dentro do próprio bloco (`pre`, `.boaz-tabela`).
- O que só serve ao catálogo (`.boaz-exemplo`, `details.boaz-snippet`, `.paleta`, `.amostra` e o script "Ver HTML") fica **fora** da casca.

## Personalizar o tema

1. Mude só o pedido. Para cor, altere o token nos quatro blocos (claro e impressão com a mesma cor; escuro e sistema escuro com a mesma cor). Contraste mínimo de 4,5:1 para texto e 3:1 para bordas de componentes, nos dois temas.
2. Componente novo: classe com nome em português, kebab-case, construída só com tokens; funciona nos dois temas e na impressão (`break-inside: avoid` se não puder quebrar entre páginas). Acrescente-o ao **Catálogo de componentes** do tema com um `.boaz-exemplo` e uma linha explicando quando usar.
3. **Nunca** renomeie nem remova uma classe usada por documentos (`grep -rl 'class="…' 1_Requisitos`): marque-a como obsoleta no catálogo e mantenha o estilo.
4. A casca continua autocontida: nada de CSS, script ou imagem externos além das fontes; sem bibliotecas.
5. É uma nova versão do tema: `vNN` + 1 nas duas linhas de versão da casca e na marca d'água do próprio `tema.html` (metadados, carimbo, histórico e rodapé, como qualquer documento).
6. Os documentos existentes **não** mudam sozinhos. Ao terminar, rode `./boaz.sh requisitos` e pergunte se a casca nova deve ser aplicada a eles.

## Aplicar o tema a documentos

Só com pedido ou confirmação. Para cada documento:
1. Troque o bloco da casca pelo do tema (de `boaz:casca:inicio` a `boaz:casca:fim`, inclusive), sem tocar no resto do `<head>` nem no `<body>`. Um script ajuda:
   ```bash
   awk 'NR==FNR { c = c $0 "\n"; next } /<!-- boaz:casca:inicio/ { printf "%s", c; s = 1 } !s { print } /<!-- boaz:casca:fim/ { s = 0 }' \
     <(awk '/<!-- boaz:casca:inicio/,/<!-- boaz:casca:fim/' 0_Assets/requisitos/tema.html) <doc>.html > /tmp/doc && mv /tmp/doc <doc>.html
   ```
2. Se o tema novo trouxe mudança de marcação (componente que mudou de estrutura), ajuste o `<body>` desse componente.
3. É versão nova do documento: `vNN` + 1, data/hora, histórico `Visual atualizado para o tema vNN`. Status não muda.

## Migrar `.md` para `.html`

1. `git mv 1_Requisitos/<doc>.md 1_Requisitos/<doc>.html` (preserva o histórico do git) e reescreva o conteúdo com os componentes do catálogo, sem mudar o texto dos requisitos:
   | Markdown | HTML do tema |
   |---|---|
   | tabela de cabeçalho `<!-- boaz:marca -->` | `<meta boaz:*>` + `<header class="boaz-marca-topo">` |
   | `# Título` + `> Objetivo:` | `<h1>` + `.boaz-objetivo` |
   | `## Seção` | `<section id="kebab"><h2>` |
   | tabelas RF / RN / RNF | `<table>` com `<tr id="ID">`, `code.id`, `.prio-*`, `.tag` |
   | `### \`RF-02\` …` + ` ```gherkin ` | `<article class="criterio" id="ca-RF-02">` + `<pre class="gherkin">` |
   | `- Como **x**, quero …, para …` | `<p class="historia">` |
   | `- [ ] pergunta — dono` | `<ul class="pendencias">` com `<span class="tag dono">` |
   | `> [!NOTE]` / `[!IMPORTANT]` / `[!WARNING]` | `<aside class="destaque nota|importante|aviso">` |
   | `![t](../2_Diagramas/x.svg)` | `<figure class="diagrama">` |
   | `[t](02_x.md)` | `<a class="ref" href="02_x.html">` |
   | `<!-- boaz:historico -->` + `<sub>` | `<footer class="boaz-marca-rodape">` |
2. A versão continua a sequência (`v04` em `.md` → `v05` em `.html`), histórico preservado (até 10 linhas) com a linha nova `Migrado para .html`.
3. Atualize links de outros documentos: `grep -rln '<doc>\.md' 1_Requisitos` (cada documento alterado ganha versão, com confirmação).
4. Ao final, `./boaz.sh requisitos` não deve listar nenhum `.md` legado.

## PDF

`./boaz.sh pdf <documento>` gera `1_Requisitos/pdf/<documento>_v<NN>_<AAAAMMDD-HHMM>.pdf` (versão e data do documento, A4, tema claro). Precisa de Chrome, Chromium, Edge ou Brave (ou `BOAZ_CHROME=/caminho/do/navegador`); sem eles, o botão **PDF** do documento abre a impressão com o nome já versionado. `./boaz.sh pdf todos` exporta todos os documentos.

## Revisão (sempre, antes de entregar)

- Renderize com o Chrome headless e **olhe**: tema claro (`--blink-settings=preferredColorScheme=1`), escuro (`preferredColorScheme=0`), largura de celular (`--window-size=390,2400`) e o PDF (`./boaz.sh pdf`). Sem sobreposição, texto cortado, rolagem horizontal da página ou contraste baixo.
  ```bash
  "<chrome>" --headless --disable-gpu --hide-scrollbars --window-size=1200,3000 --virtual-time-budget=5000 \
    --blink-settings=preferredColorScheme=1 --screenshot=<scratchpad>/claro.png "file://$PWD/0_Assets/requisitos/tema.html"
  ```
- Abra o `tema.html` e confira se cada componente do catálogo continua igual nos dois temas.
- Casca idêntica entre tema e documentos atualizados (`diff` dos blocos `awk`).

## Resposta

Responda curto: o que mudou no tema (e a nova versão), quais documentos foram atualizados ou migrados (com a versão nova de cada um), quais ficaram no tema anterior e, se gerados, os caminhos dos PDFs.
