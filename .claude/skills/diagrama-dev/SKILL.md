---
name: diagrama-dev
description: Cria ou altera diagramas de desenvolvimento (backend e frontend) como SVG versionado em 2_Diagramas/. Use para diagramas UML — classes, objetos, pacotes, componentes internos, estrutura composta, perfil, casos de uso, atividades, sequência, comunicação, visão geral de interação, estados e tempo — além de entidade-relacionamento, navegação de telas, contratos de API e vistas de tela (o desenho da UI de uma tela antes do código, requisito 06), ou quando pedirem para alterar um diagrama existente dessa pasta.
---

# Diagrama de desenvolvimento → `2_Diagramas/`

Você é especialista em modelagem de software e UML 2.5. A saída é **sempre um novo arquivo `.svg`** em `2_Diagramas/` — nunca apenas texto, Mermaid ou PlantUML.

## Escopo

Pertence aqui o que mostra **como o software funciona por dentro**: a estrutura do código e do domínio e o comportamento entre seus elementos.

Não pertence: cloud, rede, provedores, ambientes e deploy (diagrama de implantação, C4) → skill `diagrama-arquitetura` (`3_Arquitetura/`).

## Escolha do tipo

Se o pedido nomear o tipo, use-o. Senão, escolha pela pergunta que o diagrama responde e diga na resposta qual tipo usou e por quê:

| Pergunta | Tipo |
|---|---|
| O que o sistema oferece e para quem? | Casos de uso |
| Quais conceitos existem e como se relacionam? | Classes (ou ER, se o foco for persistência) |
| Como fica um cenário concreto em memória? | Objetos |
| Como o código se organiza em módulos e quem depende de quem? | Pacotes |
| Quais blocos de software existem e quais interfaces trocam? | Componentes |
| Como um componente é montado por dentro? | Estrutura composta |
| Que extensões de UML/estereótipos o projeto adota? | Perfil |
| Qual é o passo a passo de um processo/regra? | Atividades |
| Em que ordem os participantes trocam mensagens? | Sequência |
| Quem fala com quem num cenário (foco nos vínculos)? | Comunicação |
| Como vários cenários se encadeiam? | Visão geral de interação |
| Quais estados um objeto atravessa? | Estados |
| Quanto tempo cada estado/mensagem pode levar? | Tempo |
| Como o usuário navega entre telas? / Qual o contrato de um endpoint? | Navegação de telas / Contrato de API |
| Como uma tela vai ficar, antes de ser programada? | Vista (ver **Vista de tela**) |

Prefira **um tipo por arquivo**. Se o pedido cobrir duas perguntas, gere dois diagramas (legendas distintas) e avise.

## Notação UML

Siga a UML 2.5 com os elementos do tema; não invente símbolos. Multiplicidades, guardas e rótulos em `muted`; estereótipos `«...»` em `uml-keyword` acima do nome.

**Estruturais**
- **Classes**: caixa em 3 compartimentos separados por linha (nome em `label-strong` · atributos · operações em `mono`, com visibilidade `+ - # ~` e tipos `nome: Tipo`). Abstrata: `«abstract»`; interface: `«interface»`. Relações: associação (linha, `seta-aberta` se navegável, papéis e multiplicidade `1`, `0..*` nas pontas), herança (`heranca`), realização (`edge-async` + `heranca`), dependência (`edge-async` + `seta-aberta`), composição (`composicao` no lado do todo), agregação (`agregacao` no lado do todo).
- **Objetos**: caixas com nome sublinhado `pedido1 : Pedido` (classes `label-strong label-underline`) e valores `campo = valor` em `mono`; vínculos sem multiplicidade. Mostra um instante concreto do diagrama de classes.
- **Pacotes**: pasta com aba (retângulo pequeno sobre o retângulo principal) e nome; elementos dentro ou listados. Dependências `edge-async` + `seta-aberta` rotuladas `«import»`/`«access»`. Evite ciclos — se existirem, destaque com `node-red`.
- **Componentes**: retângulo com `«component»` e o ícone `#uml-componente` no canto superior direito. Interface fornecida = pirulito (linha + círculo `ink-ring`); requerida = soquete (meio-círculo aberto). Conecte soquete ao pirulito; portas como quadrados pequenos (`node-face`) sobre a borda.
- **Estrutura composta**: um classificador grande cujo interior mostra as partes `nome : Tipo [mult]`, portas na borda e conectores entre partes. Colaborações em elipse tracejada.
- **Perfil**: pacote `«profile»` com estereótipos `«stereotype» Nome` estendendo metaclasses `«metaclass» Class` por seta de extensão (linha contínua + marker `extensao`, triângulo **preenchido**). Liste as tagged values em `mono`.
- **Entidade-relacionamento** (não UML, mas útil): tabelas com chave (`PK`, `FK` em `mono`), cardinalidade nas pontas com os markers `er-um` e `er-muitos` (pé de galinha).

**Comportamentais**
- **Casos de uso**: fronteira do sistema (`boundary`, nome no topo), atores como bonecos de palito fora dela (`#uml-ator`, nome abaixo), casos de uso em elipses com verbo + objeto (`Cadastrar usuário`). `«include»`/`«extend»` com `edge-async` + `seta-aberta`; generalização com `heranca`. Se `1_Requisitos/` tiver IDs (`RF-01`), cite-os em `muted` dentro/abaixo da elipse.
- **Atividades**: início `ink` (círculo cheio), fim `ink-ring` + `ink` (alvo), ações em retângulos arredondados, decisão/merge em losango com guardas `[sim]`/`[não]`, fork/join em barra `ink`. Raias (swimlanes) como colunas `boundary` rotuladas no topo quando houver mais de um responsável.
- **Sequência**: participantes no topo (`:Classe` ou ator), linha de vida `lifeline`, barras de ativação estreitas (`activation`) sobre ela. Mensagem síncrona: linha + `seta`; assíncrona: linha + `seta-aberta`; retorno: `edge-async` + `seta-aberta`. Numere as mensagens. Fragmentos combinados (`alt`, `opt`, `loop`, `par`) como retângulos `boundary` com o operador num pentágono no canto (`frame-tag` + `frame-op`) e guardas `[condição]`.
- **Comunicação**: objetos em caixas ligados por linhas simples (vínculos); ao longo de cada vínculo, mensagens numeradas `1: criar()`, `1.1: validar()` com uma pequena seta indicando a direção.
- **Visão geral de interação**: estrutura de atividades (início, decisões, fork, fim) onde cada nó é um quadro `ref` (retângulo com `ref` num pentágono `frame-tag` + `frame-op` no canto) apontando para outro diagrama de sequência — cite a legenda/versão do diagrama referenciado — ou uma interação inline pequena.
- **Estados**: estado inicial `ink`, estados em retângulos bem arredondados (`rx="16"`) com nome e, se houver, `entry/`, `do/`, `exit/` em `mono`; transições `evento [guarda] / ação`; estado final `ink-ring` + `ink`. Estados compostos como um estado grande contendo os subestados.
- **Tempo**: eixo de tempo horizontal com marcas e unidade; uma faixa por participante (nome à esquerda) com a linha de estado em degraus (`edge-accent`) mudando de nível a cada transição; restrições de duração `{≤ 200 ms}` entre marcas com `edge-async`.

**Específicos do projeto**
- **Navegação de telas**: retângulos representando telas (nome + rota em `mono`), setas rotuladas com a ação do usuário.
- **Contrato de API**: caixa por endpoint (`MÉTODO /rota` em `mono`), com request e responses (`200`, `4xx`) em compartimentos.

## Versionamento (obrigatório)

Nome do arquivo: `<legenda>_v<NN>_<AAAAMMDD-HHMM>.svg`

- **legenda**: kebab-case, sem acentos, curta. Identifica o diagrama e **não muda** entre versões (ex.: `fluxo-cadastro`).
- **NN**: versão com 2 dígitos, começa em `01` e soma 1 a cada alteração.
- **timestamp**: data/hora local em que aquela versão foi gerada.

Obtenha o caminho exato com:

```bash
./boaz.sh svg 2_Diagramas <legenda>
```

Se o `boaz.sh` não estiver disponível, calcule pela regra acima.
**Nunca** sobrescreva, renomeie ou apague uma versão existente — cada alteração é um arquivo novo.

## Fluxo

**Diagrama novo**
1. Leia os documentos relevantes de `1_Requisitos/` (`.html`) e rode `./boaz.sh diagramas` para reaproveitar nomes e evitar duplicatas.
2. Se a ambiguidade mudar o desenho, faça no máximo 1–2 perguntas; caso contrário, assuma e declare as premissas na resposta.
3. Escolha a legenda, gere o caminho e escreva o SVG.

**Alteração** ("muda", "adiciona", "corrige" num diagrama existente)
1. Identifique a legenda e abra a **maior versão** (`ls 2_Diagramas/<legenda>_v*`).
2. Aplique só a mudança pedida, preservando layout, ids e o restante do desenho.
3. Salve como a próxima versão (`./boaz.sh svg 2_Diagramas <legenda>`).
4. Rode `grep -rn "<legenda>_v" 1_Requisitos` e, se houver referências à versão anterior, pergunte se devem apontar para a nova. Não altere sem confirmação; confirmado, troque o caminho no `src`/`href` e na legenda da `<figure class="diagrama">`, e cada documento alterado ganha uma versão nova (marca d'água e histórico, como na skill `requisito`).

## Assets (`0_Assets/`)

Antes de desenhar, rode `ls 0_Assets/projeto 0_Assets/diagramas` e reaproveite o que existir: ícones de tecnologias, logos de integrações e da marca.
Ícones de cloud ficam em `0_Assets/diagramas/icones/{aws,gcp,azure}/` (centenas de arquivos — leia o `README.md` da pasta e busque por nome: `find 0_Assets/diagramas/icones -iname '*lambda*'`, nunca `ls -R`).

- **Embuta sempre** — nunca referencie o arquivo por caminho (`href="../0_Assets/..."` quebra no GitHub e em visualizadores de imagem).
  - `.svg`: copie o conteúdo gráfico para um `<symbol id="asset-<nome>" viewBox="...">` em `<defs>` (com o `viewBox` do original) e desenhe com `<use href="#asset-<nome>" x=".." y=".." width=".." height=".."/>`. Remova `<script>`, `<foreignObject>`, handlers `on*` e metadados de editor; prefixe ids internos com `<nome>-` para não colidirem. Se o ícone tiver `<style>` próprio (os do GCP trazem `.cls-1`, `.st0`…), converta cada regra em atributo (`fill="#4285f4"`) nos elementos e apague o `<style>` — senão as classes vazam e colidem com outros ícones. O `tema.svg` traz exemplos prontos em `#prov-*`.
  - `.png`/`.jpg`/`.webp`: `<image href="data:image/png;base64,..."/>` gerado com `base64 < arquivo | tr -d '\n'`. Evite arquivos acima de ~200 KB.
- Ícone é apoio, não substitui o texto: a caixa continua com nome e tecnologia escritos. Tamanho uniforme (24–32px), sempre no mesmo lugar da caixa (padrão do tema: à esquerda, nome e tecnologia alinhados depois dele).
- Faltou um ícone? Use a forma neutra `#ic-*` do tema que mais se aproxima (ou a forma simples da notação) e mencione na resposta qual asset poderia ser adicionado em `0_Assets/diagramas/`.

## Vista de tela

A vista é o desenho de uma tela antes do código (`1_Requisitos/06_design-system-telas.html`, RN-27, RN-33, RNF-102 a RNF-105). Vale tudo desta skill (versionamento, marca d'água, revisão), com estas diferenças:

- **Nome**: `vista-<aplicacao>-<tela>` (ex.: `vista-hub-inicio`), em `2_Diagramas/`.
- **Quadro × tela**: o quadro (fundo, marca d'água, legenda, anotações) segue o tema dos diagramas; **dentro das molduras das telas** vale o design system de `0_Assets/design/`, não o tema dos diagramas. Lá dentro, cores e medidas literais são permitidas, mas só as do design system:
  - cores do tema de cor padrão em `0_Assets/design/tokens/cores.json` (`padrao` → `temas.<id>.md.sys.color.light|dark`), sempre pelo papel (`surface`, `primary`, `on-surface`…);
  - tipografia: copie de `0_Assets/design/tema.svg` as regras `.t-<papel>` do `<style>` e use `class="t-body-large"` etc.;
  - componentes: copie os desenhos dos grupos `<g id="comp-<componente>-claro|escuro">` de `0_Assets/design/tema.svg` (botões, chips, cards, campos, lista, barras de topo e navegação, trilho, gaveta, diálogo, snackbar), mudando só texto e posição; ícones de `<symbol id="i-*">`;
  - identidade: logos, imagens e animações de `0_Assets/design/identidade/`, embutidos como manda **Assets**. Arquivo que não existir: um retângulo tracejado `outline` com o caminho esperado (`identidade/logos/logo-principal.svg`).
  - componente que não existe no design system: **pare** e crie-o antes pela skill `design-system` (RN-28); a vista não inventa componente.
- **Molduras**: uma por classe de janela e modo (RNF-103): compact 360×800 sempre; nas aplicações web também medium 840×800 e expanded 1280×800 (escala 0,5 se preciso). Linha de cima no modo claro, linha de baixo no escuro. Cantos de 24px, rótulo embaixo (`compact · 360dp · claro`). A navegação muda com a janela: barra (compact), trilho (medium/expanded), gaveta (large+).
- **Anotações** (RNF-104): marcadores numerados (círculo `chip` + número) em cada bloco da tela claro de compact, e uma lista no rodapé: `1 · barra de navegação · comum`, `2 · logo-principal.svg · identidade/logos/`, `3 · card do resultado · do módulo quiz` (comum, do módulo ou composto).
- **Aprovação** (RN-33): a vista vai num pull request revisado e aprovado antes do código da tela; mudança de tela depois disso é nova versão da vista (RNF-105).

## Visual (obrigatório)

O diagrama precisa ser **bonito**, não só correto — algo que daria orgulho de colocar numa apresentação.

**Tema padrão (modo dark).** `0_Assets/diagramas/tema.svg` é o estilo e o catálogo visual: cores por papel, tipografia, tipos de comunicação, formas neutras de arquitetura, exemplo de arquitetura, ícones por provedor, um cartão por tipo de diagrama UML com os elementos que ele usa e o exemplo da marca d'água de topo e rodapé. Abra-o (`grep -n 'id="' 0_Assets/diagramas/tema.svg` lista seções e símbolos) e:
- copie **sem alterar** o bloco `<style>` e, de `<defs>`, todos os `<marker>`, o filtro `sombra` e o gradiente `marca-acento`;
- dos `<symbol>`, copie **só os que usar**: `#ic-*` (formas neutras de arquitetura — banco, fila, função, gateway…), `#uml-*` (ator, ícone de componente, artefato), `#prov-*` (ícones oficiais de exemplo);
- use o cartão do tipo de diagrama (seção 6, `<g id="uml-...">`, `er`, `c4`) como referência de composição e o exemplo da seção 7 (`<g id="exemplo-marca">`) para a marca d'água.
Se o arquivo não existir, rode `./boaz.sh init` para recriá-lo.
- Estilize **apenas por classe** do tema (`node-blue`, `edge`, `label-strong`, `muted`...). Nada de `fill`/`stroke`/`font-*` inline nem cores fora do tema.
- Fundo: primeiro elemento `<rect class="bg">` cobrindo todo o `viewBox`.
- Cor = papel: no máximo 4 classes `node-*` coloridas por diagrama, cada uma explicada na legenda; `node-external` para o que não é do projeto, `node-red` só para erro/risco. Destaque o fluxo principal com `edge-accent` + `seta-destaque`; o resto com `edge`. Assíncrono/retorno: adicione `edge-async`.
- Comunicação (seção 2 do tema): síncrono principal `edge-accent` + `seta-destaque`; síncrono `edge` + `seta`; resposta `edge edge-async` + `seta-aberta`; evento/mensagem `edge edge-async` + `seta`; stream bidirecional `seta` nas duas pontas; dados/lote `edge edge-data`. Protocolo no rótulo da aresta ou como `chip` + `chip-text`.
- Notação UML com os elementos do tema: markers `seta-aberta`, `heranca`, `extensao`, `composicao`, `agregacao`, `er-um`, `er-muitos` e classes `ink`, `ink-ring`, `node-note`, `node-face`, `uml-keyword`, `label-underline`, `activation`, `frame-tag`, `frame-op`. Se o tema do projeto for antigo e não tiver algum deles, declare-o no diagrama com as mesmas cores do tema (ou recrie o tema: apague `0_Assets/diagramas/tema.svg` e rode `./boaz.sh init`).

**Composição.**
- Grade de 8px: coordenadas e tamanhos múltiplos de 8; margem externa de 40px; espaço mínimo de 48px entre caixas.
- Leitura em uma direção (esquerda→direita ou cima→baixo); alinhe caixas em linhas e colunas e dê o mesmo tamanho a elementos do mesmo tipo.
- Caixas com `rx="10"`, padding interno ≥ 16px, `filter="url(#sombra)"` no `<g>` do nó. Texto centralizado: título em `label-strong`, tecnologia/detalhe em `mono` ou `muted` logo abaixo.
- Tamanho pelo texto: estime ~7,5px por caractere em 13–14px (~8px em `mono`) e dimensione a caixa para caber com folga. Nenhum texto pode sobrepor formas, linhas ou outro texto.
- Setas ortogonais (`<path>` com segmentos H/V, cantos retos) ou retas; terminam na borda da caixa, sem atravessá-la. Rótulos acima da linha, em `muted`; se encostarem em algo, ponha um `<rect class="edge-label-bg">` atrás. Minimize cruzamentos.
- Fronteiras: `<rect class="boundary" rx="14">` com rótulo `boundary-label` em MAIÚSCULAS no canto superior esquerdo.

**Estrutura do arquivo.**
- Um único arquivo autocontido: sem fontes, imagens ou CSS externos; sem `<script>` nem `<foreignObject>`.
- `xmlns="http://www.w3.org/2000/svg"`, `viewBox`, `width` e `height` definidos.
- `<title>` com o título do diagrama; `<desc>` com o que ele mostra e o histórico curto (`v01: criação`, `v02: adicionado ...`).
- Agrupe cada elemento lógico em `<g id="...">` com id kebab-case — facilita alterações futuras.
- Numa alteração, mantenha o tema da versão anterior, a menos que pedirem para atualizar o visual.

**Marca d'água (obrigatória).** Todo diagrama faz parte da documentação: abre com uma faixa de topo e fecha com uma de rodapé, ambas de largura total do `viewBox`, encostadas nas bordas (sem margem). Copie a estrutura do exemplo da seção 7 do tema trocando só os textos.
- **Topo** — `<g id="marca-topo">` em `y=0`, altura 112:
  - `<rect class="wm-band">` de `0,0` até a largura total, e `<rect class="wm-accent">` de 2px em `y=110` (gradiente);
  - à esquerda, em `x=40`: sobretítulo `wm-eyebrow` em `y=40` com `<PROJETO> · <TIPO DE DIAGRAMA>` em MAIÚSCULAS (projeto = `PROJECT_NAME` de `.boaz`), o nome do diagrama em `title` em `y=72` e uma linha de contexto em `subtitle` em `y=94`;
  - à direita, o carimbo: `DATA` em `x = largura − 192` (`wm-meta-label` em `y=44`; valor `wm-meta-value` em `y=72`: `AAAA-MM-DD · HH:MM`, o mesmo instante do nome do arquivo); um `wm-sep` vertical 24px antes; `VERSÃO` 96px antes da data (`wm-meta-label` + selo `<rect class="wm-badge" width="48" height="22" rx="11">` com `wm-badge-text` centralizado: `v<NN>`);
  - o título não pode invadir os ~300px do carimbo; se não couber, encurte o subtítulo ou alargue o `viewBox`.
- **Rodapé** — `<g id="marca-rodape">` encostado no fim do `viewBox`, altura 116 (+26 por linha extra de legenda):
  - `<rect class="wm-band">` com `wm-sep` de 1px na borda de cima;
  - `LEGENDA` em `wm-eyebrow` e, numa linha, **todos** os elementos com significado que aparecem no desenho e só eles: cada classe `node-*` (amostra 14×14 `rx="3"` com a classe + papel em `label`) e cada tipo de aresta (linha de 28px com a mesma classe e o mesmo marker + significado). 28px entre itens; se não couber, quebre em outra linha;
  - um `divider` interno e, abaixo, à esquerda em `footer`: `v<NN> · <o que o diagrama mostra>` (numa alteração, o que mudou); à direita, `text-anchor="end"` em `wm-file`, o nome do arquivo `.svg`.
- O desenho fica entre as faixas: começa 40px abaixo da marca de topo e termina 40px acima da de rodapé. A legenda vive só no rodapé — nada de caixa de legenda solta no desenho.
- Numa alteração, atualize versão, data, nota e nome do arquivo nas marcas, e a legenda se os elementos mudaram.

**Revisão antes de entregar.**
- Se houver `xmllint`, valide com `xmllint --noout <arquivo>` e corrija os erros.
- Se houver `rsvg-convert` (ou `qlmanage` no macOS), renderize um PNG temporário no scratchpad, **olhe a imagem** e corrija sobreposições, textos cortados, desalinhamentos e espaços vazios desequilibrados antes de responder. Confira também as marcas d'água: versão e data iguais às do nome do arquivo e legenda batendo com o desenho.

## Resposta

Responda curto: caminho do arquivo criado, premissas assumidas (se houver) e o trecho para referenciar no requisito (`.html`, componente **Diagramas** de `0_Assets/requisitos/tema.html`):

```html
<figure class="diagrama">
  <a href="../2_Diagramas/<arquivo>.svg"><img src="../2_Diagramas/<arquivo>.svg" alt="<o que o diagrama mostra>"></a>
  <figcaption><strong><Título></strong><code>2_Diagramas/<arquivo>.svg</code></figcaption>
</figure>
```
