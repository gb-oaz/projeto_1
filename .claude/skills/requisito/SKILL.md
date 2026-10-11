---
name: requisito
description: Cria, altera, revisa ou exporta em PDF documentos de requisito do produto (.html) em 1_Requisitos/ — visão do produto, funcionalidades, requisitos transversais e glossário — no padrão de 0_Assets/requisitos/tema.html, com marca d'água de topo (projeto, documento, versão, data/hora, status) e histórico no rodapé. Use para escrever requisitos funcionais, não funcionais, regras de negócio, critérios de aceite, personas ou glossário, para alterar, revisar ou mudar o status de um documento dessa pasta, ou para gerar o PDF de um requisito.
---

# Documento de requisito → `1_Requisitos/`

Você é analista de produto. A saída é **sempre um `.html` autocontido em `1_Requisitos/`**, curto e claro, no padrão de `0_Assets/requisitos/tema.html`.

## Escopo

Pertence aqui **o produto**: o que ele faz, para quem, com quais regras e limites mensuráveis.

Não pertence: backend, frontend, banco, API, framework, provedor ou qualquer decisão de solução. Se o pedido trouxer tecnologia, reescreva como comportamento e avise. Diagramas são das skills `diagrama-dev` (`2_Diagramas/`) e `diagrama-arquitetura` (`3_Arquitetura/`); sugira-as quando um fluxo ficar mais claro desenhado. Mudar o **visual** (cores, fontes, componentes), migrar documentos `.md` antigos ou trocar a casca de documentos é da skill `requisito-estilo`.

## O padrão

`0_Assets/requisitos/tema.html` é a fonte da verdade: marca d'água, casca, visual, formato, escrita, catálogo de componentes, tipos de documento e versionamento. **Leia-o inteiro antes de escrever** (é a regra e, no código-fonte, o HTML pronto de cada componente) e siga-o mesmo quando divergir deste resumo — o projeto pode tê-lo customizado. Se não existir, rode `./boaz.sh init`.

Resumo do que não pode faltar:
- `<head>`: `charset`, `viewport`, `<title>Título · Projeto · vNN</title>`, o comentário `<!-- boaz:marca -->` e os 7 `<meta name="boaz:…">` (projeto = `PROJECT_NAME` de `.boaz`; data = `date '+%Y-%m-%d %H:%M'`), um por linha, na ordem do tema; depois a **casca** copiada sem alterar: `awk '/<!-- boaz:casca:inicio/,/<!-- boaz:casca:fim/' 0_Assets/requisitos/tema.html`.
- `<body>`: `<header class="boaz-marca-topo">` (sobretítulo `Projeto · Tipo`, `<h1>`, objetivo de uma frase, carimbo com documento, versão, data `AAAA-MM-DD · HH:MM` e status) → `<main>` com um `<section id="…"><h2>` por seção do tipo, na ordem do tema (opcional vazia é removida) → `<footer class="boaz-marca-rodape">` (histórico `Versão | Data | Mudança`, mais nova no topo, até 10 linhas, e a linha `Projeto · vNN · data` + caminho).
- Só componentes do catálogo, estilizados **só por classe**: nada de `style=""` (exceto o recorte de diagrama), `<style>`/`<script>` próprios, cores, fontes ou CDN fora da casca.
- Requisitos em tabela, linha com `id` igual ao ID (`<tr id="RF-01">`), ID em `<code class="id">`, uma obrigação por frase, "O sistema deve …", prioridade em `<span class="prio prio-deve">`, critério de aceite em `<article class="criterio">` + `<pre class="gherkin">` para cada `Deve`.
- Texto escapado: `&` → `&amp;`, `<` → `&lt;`, `>` → `&gt;` (também dentro do Gherkin).
- Curto: ≤ 12 requisitos funcionais, ≤ 4 páginas A4 impressas, uma funcionalidade por documento — passou, divida.
- HTML indentado com 2 espaços, um bloco por linha: o diff do git precisa ser legível.

## Diagramas e outros documentos

- Diagrama: `<figure class="diagrama">` apontando para o `.svg` **com a versão explícita**, por caminho relativo (`../2_Diagramas/<legenda>_vNN_<AAAAMMDD-HHMM>.svg`), com `alt` descritivo e o caminho na `<figcaption>`. Use a versão mais recente (`./boaz.sh diagramas`) e confira que o arquivo existe.
- Outro documento: `<a class="ref" href="02_cadastro-usuario.html#RF-01">Cadastro de usuário <code class="id">RF-01</code></a>`; termo do glossário: `99_glossario.html#<termo>`.

## Fluxo

**Antes de escrever**
1. Leia o tema, rode `./boaz.sh requisitos` e leia `00_visao-produto.html` e `99_glossario.html` se existirem — reaproveite nomes de personas e termos.
2. Próximos IDs (únicos no projeto inteiro, nunca reaproveitados):
   `for p in RF RNF RN; do grep -rhoE --include='*.html' --include='*.md' "\b$p-[0-9]+" 1_Requisitos | sort -t- -k2,2n | tail -1; done`

**Documento novo**
1. Escolha o tipo pelo tema (visão, funcionalidade, transversal, glossário) e o arquivo `<NN>_<nome>.html`: `NN` = próximo livre da faixa do tipo (`ls 1_Requisitos`), `nome` em kebab-case sem acentos.
2. Se uma ambiguidade mudar o escopo, faça no máximo 1–2 perguntas. O resto **não invente**: regra, limite ou número que o pedido não deu vira item de **Em aberto**.
3. Escreva com `v01`, status `Rascunho` e histórico `v01 · Criação`.

**Alteração** ("muda", "adiciona", "remove", "aprova" num documento existente)
1. Abra o documento e aplique só o que foi pedido; não reescreva o resto, não troque a casca e não renomeie o arquivo. Documento `.md` antigo? Peça a migração pela skill `requisito-estilo` antes (ou faça-a seguindo aquela skill).
2. Nova versão: `vNN` + 1 e data/hora atuais nos 5 lugares — `<title>`, `<meta boaz:versao/data>`, carimbo, linha nova no topo do histórico (o que mudou, citando os IDs) e linha do rodapé. Status muda no `<meta boaz:status>` e no carimbo (classe `status-…` junto). Mudança de status também é versão.
3. Requisito removido sai da tabela (e dos critérios); o ID não é reutilizado. Pendência respondida sai de Em aberto e vira requisito/regra.
4. Se o documento estava `Aprovado`, volte-o para `Em revisão`, a menos que o pedido diga outra coisa.
5. Termo novo do domínio? Proponha incluí-lo em `99_glossario.html` (que também ganha versão).

**PDF** ("exporta", "gera o PDF", "manda o pdf do …")
1. Rode `./boaz.sh pdf <documento>` (ex.: `./boaz.sh pdf 02_cadastro-usuario`): gera `1_Requisitos/pdf/<documento>_v<NN>_<AAAAMMDD-HHMM>.pdf` com a versão e a data do documento, tema claro, A4, marca de topo, histórico e cabeçalho/rodapé paginados.
2. Gere o PDF **depois** de salvar a versão pedida; não crie versão só para exportar.
3. Sem navegador Chromium instalado, o comando avisa: oriente a abrir o `.html` e usar o botão **PDF** (nome sugerido já versionado).

**Revisão** (sempre, antes de entregar)
- Marca d'água: `grep -nE 'boaz:(versao|data|status)"|<title>|boaz-versao|boaz-data|<td>[0-9]{4}-|boaz-rodape-linha' -A1 <arquivo>` — mesma versão e data/hora em todos. No navegador, o botão PDF fica vermelho se divergirem.
- Casca intacta: `diff <(awk '/<!-- boaz:casca:inicio/,/<!-- boaz:casca:fim/' 0_Assets/requisitos/tema.html) <(awk '/<!-- boaz:casca:inicio/,/<!-- boaz:casca:fim/' <arquivo>)` — vazio num documento novo; num existente, só pode diferir se ele usa um tema anterior (não atualize sem pedirem).
- HTML válido: `xmllint --html --noout <arquivo>` se houver (avisos de tags HTML5 podem ser ignorados; erros de tag não fechada, não).
- Seções na ordem do tema, nenhuma vazia, um `<h1>`, `<h3>` só em critérios e cartões.
- IDs duplicados: `grep -rhoE --include='*.html' '<tr id="(RF|RNF|RN)-[0-9]+"' 1_Requisitos | sort | uniq -d` (deve sair vazio).
- Palavras vagas: `sed -n '/<body>/,$p' <arquivo> | grep -niE 'rápid|fácil|intuitiv|amigável|simples|adequad|eficiente|robust|flexível|etc\.|e/ou|entre outros|se possível|geralmente|normalmente|TBD|a definir'` — troque cada uma por critério verificável.
- Tecnologia: `sed -n '/<body>/,$p' <arquivo> | grep -niE 'backend|frontend|banco de dados|\bAPI\b|endpoint|framework|servidor|\bSQL\b|cloud'` — reescreva como comportamento.
- Diagramas e links referenciados existem (`ls`).
- Se houver Chrome/Chromium, tire um `--screenshot` headless do documento (comando na skill `requisito-estilo`) e **olhe** antes de responder.

## Resposta

Responda curto: caminho do arquivo, versão e status, o que mudou (IDs), as pendências de **Em aberto** que precisam de alguém para responder e, se pedido, o caminho do PDF.
