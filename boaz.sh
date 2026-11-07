#!/usr/bin/env bash
# ============================================================
#  boaz.sh — Monorepo Structure Manager
#  Uso: chmod +x boaz.sh && ./boaz.sh [comando]
# ============================================================

set -euo pipefail

CONFIG_FILE=".boaz"
ICONS_DIR="0_Assets/diagramas/icones"
ASSETS_TARBALL="https://codeload.github.com/gb-oaz/boaz/tar.gz/refs/heads/main"
DIAGRAM_DIRS="2_Diagramas 3_Arquitetura"
REQ_DIR="1_Requisitos"
REQ_THEME="0_Assets/requisitos/tema.md"
REQ_TOOL="0_Assets/requisitos/requisito.mjs"
LEAF_DIRS="0_Assets/projeto 0_Assets/diagramas 0_Assets/requisitos 1_Requisitos 2_Diagramas 3_Arquitetura 4_CI_CD
           5_DEV/backend 5_DEV/frontend 5_DEV/qa/backend 5_DEV/qa/frontend"

# ── TERMINAL ─────────────────────────────────────────────────
# Garante cores apenas quando o terminal suporta
if [ -t 1 ] && command -v tput &>/dev/null && tput colors &>/dev/null; then
  R=$(tput setaf 1)
  G=$(tput setaf 2)
  Y=$(tput setaf 3)
  B=$(tput setaf 4)
  C=$(tput setaf 6)
  W=$(tput bold)
  D=$(tput dim 2>/dev/null || echo "")
  NC=$(tput sgr0)
else
  R="" G="" Y="" B="" C="" W="" D="" NC=""
fi

# ── UTILITÁRIOS ──────────────────────────────────────────────
banner() {
  echo ""
  echo "${Y}╔══════════════════════════════╗${NC}"
  echo "${Y}║${NC}  🏗️  BOAZ — Monorepo Manager  ${Y}║${NC}"
  echo "${Y}╚══════════════════════════════╝${NC}"
  echo ""
}

info()    { echo "  ${C}ℹ${NC}  $1"; }
success() { echo "  ${G}✔${NC}  $1"; }
warn()    { echo "  ${Y}⚠${NC}  $1"; }
fail()    { echo "  ${R}✖${NC}  $1" >&2; exit 1; }
title()   { echo ""; echo "${B}▶ $1${NC}"; echo ""; }

mkd() {
  if [ ! -d "$1" ]; then
    mkdir -p "$1"
    success "Criado: ${D}$1/${NC}"
  fi
}

# Escreve stdin em $1 somente se o arquivo ainda não existir
write_once() {
  if [ -f "$1" ]; then
    cat > /dev/null
  else
    mkdir -p "$(dirname "$1")"
    cat > "$1"
    success "Criado: ${D}$1${NC}"
  fi
}

# Escreve stdin em $1 sempre (arquivos gerenciados pelo boaz)
write_always() {
  mkdir -p "$(dirname "$1")"
  cat > "$1"
  success "Atualizado: ${D}$1${NC}"
}

# "Fluxo de Cadastro" → fluxo-de-cadastro
slugify() {
  echo "$1" \
    | LC_ALL=en_US.UTF-8 sed 'y/áàâãäéèêëíìîïóòôõöúùûüçñÁÀÂÃÄÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇÑ/aaaaaeeeeiiiiooooouuuucnAAAAAEEEEIIIIOOOOOUUUUCN/' \
    | tr '[:upper:]' '[:lower:]' \
    | sed -E 's/[^a-z0-9]+/-/g; s/^-+//; s/-+$//'
}

# ── CONFIG ───────────────────────────────────────────────────
load_config() {
  PROJECT_NAME=""
  if [ -f "$CONFIG_FILE" ]; then
    PROJECT_NAME=$(sed -n 's/^PROJECT_NAME="\(.*\)"$/\1/p' "$CONFIG_FILE")
  fi
}

save_config() {
  printf 'PROJECT_NAME="%s"\n' "$PROJECT_NAME" > "$CONFIG_FILE"
}

# ── INIT ─────────────────────────────────────────────────────
cmd_init() {
  load_config
  if [ -n "${1:-}" ]; then
    PROJECT_NAME="$1"
  fi
  while [ -z "$PROJECT_NAME" ]; do
    read -rp "  Nome do projeto: " PROJECT_NAME
  done
  save_config

  title "Montando estrutura [${PROJECT_NAME}]"

  local leaf
  for leaf in $LEAF_DIRS; do
    mkd "$leaf"
  done

  create_readmes
  install_assets
  create_requirements_template
  create_diagram_skills
  create_requirements_skills

  # Mantém no git as pastas que ainda estão vazias
  for leaf in $LEAF_DIRS; do
    [ -n "$(ls -A "$leaf")" ] || touch "$leaf/.gitkeep"
  done

  echo ""
  success "Projeto ${C}${PROJECT_NAME}${NC} pronto."
  info "Requisitos: no Claude Code use ${W}/requisito${NC} (conteúdo) ou ${W}/requisito-estilo${NC} (visual, migração e PDF)."
  info "Diagramas: no Claude Code use ${W}/diagrama-dev${NC} ou ${W}/diagrama-arquitetura${NC}."
}

create_readmes() {
  write_once "CLAUDE.md" << EOF
# ${PROJECT_NAME}

Monorepo organizado pelo boaz. Cada pasta tem uma única responsabilidade:

| Pasta | Conteúdo | Formato |
|---|---|---|
| \`0_Assets/\` | Recursos reutilizáveis: \`projeto/\` (marca, imagens, fontes), \`requisitos/\` (tema.md, requisito.mjs, casca.html) e \`diagramas/\` (tema, ícones, logos) | livre |
| \`1_Requisitos/\` | O produto desejado — sem backend, frontend ou tecnologia | \`.md\` versionado (+ \`pdf/\`) |
| \`2_Diagramas/\` | Diagramas de desenvolvimento (backend e frontend) | \`.svg\` versionado |
| \`3_Arquitetura/\` | Diagramas de alto nível: cloud, tecnologias, integrações | \`.svg\` versionado |
| \`4_CI_CD/\` | Estruturas de entrega (pipelines, deploy) | livre |
| \`5_DEV/\` | Código: \`backend/\`, \`frontend/\`, \`qa/backend/\`, \`qa/frontend/\` | livre |

- Requisitos são documentos \`.md\`, criados/alterados pela skill \`requisito\` no padrão de \`0_Assets/requisitos/tema.md\` (front matter e carimbo de topo com projeto, documento, versão, data/hora e status; histórico no rodapé; diagramas e telas como imagens \`.svg\` versionadas). A skill \`requisito-estilo\` cuida do formato, do HTML/PDF gerado por \`0_Assets/requisitos/requisito.mjs\` com a \`casca.html\`, da migração de \`.html\` antigos e do PDF. \`./boaz.sh requisitos\` lista os documentos; \`./boaz.sh pdf <documento>\` exporta o PDF versionado para \`1_Requisitos/pdf/\`.
- Diagramas são criados/alterados somente pelas skills \`diagrama-dev\` (2_) e \`diagrama-arquitetura\` (3_).
- Todo diagrama segue o tema dark de \`0_Assets/diagramas/tema.svg\` e embute (inline) os assets que usar de \`0_Assets/\`.
- Nome do diagrama: \`<legenda>_v<NN>_<AAAAMMDD-HHMM>.svg\`. Cada alteração gera um novo arquivo com a versão seguinte e, na mesma tarefa, a versão anterior é apagada (\`git rm\`) depois que todas as referências (requisitos \`.md\` e outros arquivos) forem atualizadas para a nova; o histórico fica no git.
- \`./boaz.sh svg <pasta> <legenda>\` devolve o próximo nome; \`./boaz.sh diagramas\` lista a versão mais recente de cada um; \`./boaz.sh antigas\` lista as versões antigas já sem referência (\`--apagar\` as remove).
EOF

  write_once "0_Assets/README.md" << 'EOF'
# 0_Assets

Recursos reutilizáveis do projeto e dos diagramas.

```
0_Assets/
├── projeto/      # marca, logos, imagens, fontes, mockups — usados no produto e nos documentos
├── requisitos/   # tema.md (formato e componentes), requisito.mjs (HTML/PDF e conferência) e casca.html (visual do PDF)
└── diagramas/    # tema.svg (estilo padrão), ícones de cloud/tecnologias, logos de integrações
```

- Prefira `.svg`; raster (`.png`, `.jpg`, `.webp`) só quando não houver vetor.
- Nomes em kebab-case, sem acentos: `funcao-serverless.svg`, `logo-principal.svg`.
- `diagramas/icones/` traz os ícones oficiais de AWS, GCP e Azure (instalados pelo
  `./boaz.sh init`, veja o README da pasta). Use-os só quando a arquitetura for
  de um provedor específico; por padrão ela é neutra e vale para Azure, AWS e GCP.
- As skills de diagrama procuram aqui antes de desenhar e **embutem** o asset no SVG
  gerado (o diagrama continua autocontido e renderiza em qualquer lugar).
- `diagramas/tema.svg` define o visual de todos os diagramas: edite-o para mudar
  cores e tipografia dos próximos diagramas (os antigos não mudam).
- `requisitos/tema.md` define o padrão dos documentos `.md` de `1_Requisitos/` (marca
  d'água, formato e catálogo de componentes); `requisitos/requisito.mjs` gera o HTML
  do PDF com o visual de `requisitos/casca.html` e confere os documentos. Para mudar
  formato ou visual use a skill `/requisito-estilo`.
EOF

  write_once "1_Requisitos/README.md" << 'EOF'
# 1_Requisitos

Requisitos do **produto**: o que ele faz, para quem e com quais regras.
Aqui não se fala de backend, frontend ou tecnologia.

- Arquivos `<NN>_<nome>.md`, criados e alterados pela skill `/requisito`.
  Leia no GitHub ou no editor; o PDF sai com temas e paleta das vistas pelo `./boaz.sh pdf`.
- Padrão (marca d'água, formato, componentes): `0_Assets/requisitos/tema.md`.
- Cada documento abre com a marca de topo (projeto, documento, versão, data/hora, status)
  e fecha com o histórico. Toda alteração soma 1 na versão.
- `./boaz.sh requisitos` lista os documentos com versão, data e status.
- `./boaz.sh pdf <documento>` gera `pdf/<documento>_v<NN>_<AAAAMMDD-HHMM>.pdf` e apaga os PDFs de versões anteriores do mesmo documento.
- Para ilustrar, referencie um diagrama **com a versão explícita** (exemplo de formato, não é um arquivo real):
  `[![…](../2_Diagramas/<legenda>_v<NN>_<AAAAMMDD-HHMM>.svg)](../2_Diagramas/<legenda>_v<NN>_<AAAAMMDD-HHMM>.svg)`
- Quando o diagrama ganha versão nova, a referência passa para ela e a versão anterior é apagada.
- `node 0_Assets/requisitos/requisito.mjs conferir 1_Requisitos/*.md` confere marca d'água, links e imagens.
EOF

  write_once "2_Diagramas/README.md" << 'EOF'
# 2_Diagramas

Diagramas de desenvolvimento (backend e frontend): sequência, classes, ER,
fluxos, estados, componentes, navegação de telas, contratos de API.

- Somente `.svg`, gerados pela skill `/diagrama-dev`.
- Nome: `<legenda>_v<NN>_<AAAAMMDD-HHMM>.svg`. Cada alteração gera a versão seguinte;
  a anterior é apagada na mesma tarefa, depois de atualizadas as referências
  (o histórico fica no git). `./boaz.sh antigas` lista as que sobraram.
EOF

  write_once "3_Arquitetura/README.md" << 'EOF'
# 3_Arquitetura

Diagramas de alto nível: contexto do sistema, contêineres, cloud, rede,
ambientes, tecnologias escolhidas e integrações externas.

- Somente `.svg`, gerados pela skill `/diagrama-arquitetura`.
- Nome: `<legenda>_v<NN>_<AAAAMMDD-HHMM>.svg`. Cada alteração gera a versão seguinte;
  a anterior é apagada na mesma tarefa, depois de atualizadas as referências
  (o histórico fica no git). `./boaz.sh antigas` lista as que sobraram.
EOF

  write_once "4_CI_CD/README.md" << 'EOF'
# 4_CI_CD

Estruturas de entrega: pipelines de build/teste/deploy, manifests e scripts
de release. A ferramenta é escolhida pelo projeto.
EOF

  write_once "5_DEV/README.md" << 'EOF'
# 5_DEV

Ambiente de desenvolvimento.

```
5_DEV/
├── backend/
├── frontend/
└── qa/
    ├── backend/
    └── frontend/
```
EOF
}

# Escapa texto para string YAML entre aspas duplas (nome do projeto no front matter)
yaml_escape() {
  printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g'
}

# Visão do produto no padrão de 0_Assets/requisitos/tema.md — só se a pasta ainda não tiver documentos
create_requirements_template() {
  local now p y
  ls "$REQ_DIR"/*.md "$REQ_DIR"/*.html 2>/dev/null | grep -qv '/README\.md$' && return
  now=$(date '+%Y-%m-%d %H:%M')
  p=$PROJECT_NAME
  y=$(yaml_escape "$PROJECT_NAME")
  write_once "$REQ_DIR/00_visao-produto.md" << EOF
---
projeto: "${y}"
documento: "00_visao-produto"
tipo: "Visão do produto"
titulo: "Visão do produto"
versao: "v01"
data: "${now}"
status: "Rascunho"
objetivo: "definir o problema que ${y} resolve, para quem, e como saberemos que deu certo."
---

> **${p} · Visão do produto** · \`00_visao-produto\` · **v01** · ${now} · **Rascunho**

# Visão do produto

**Objetivo:** definir o problema que ${p} resolve, para quem, e como saberemos que deu certo.

**Sumário:** [Problema](#problema) · [Objetivos e métricas](#objetivos-e-metricas) · [Personas](#personas) · [Escopo](#escopo) · [Fora de escopo](#fora-de-escopo) · [Em aberto](#em-aberto) · [Histórico](#historico)

---

<a id="problema"></a>

## Problema

- …

---

<a id="objetivos-e-metricas"></a>

## Objetivos e métricas

### Objetivo

- **Indicador:** …
- **Meta:** …
- **Prazo:** …

---

<a id="personas"></a>

## Personas

### Persona

- **Quem é:** …
- **Precisa de:** …

---

<a id="escopo"></a>

## Escopo

- …

---

<a id="fora-de-escopo"></a>

## Fora de escopo

- …

---

<a id="em-aberto"></a>

## Em aberto

- … — **Produto**

---

<a id="historico"></a>

## Histórico

| Versão | Data | Mudança |
|---|---|---|
| **v01** | ${now} | Criação (modelo do boaz) |

---

<sub>${p} · v01 · ${now} · \`${REQ_DIR}/00_visao-produto.md\`</sub>
EOF
}

# ── ASSETS ───────────────────────────────────────────────────
# Copia de assets/ do boaz, sem sobrescrever (o projeto pode customizar os temas e adicionar ícones):
#   requisitos/ → 0_Assets/requisitos/ (tema.md, requisito.mjs e casca.html dos documentos de requisito)
#   tema.svg  → 0_Assets/diagramas/tema.svg   (estilo + catálogo de formas e notação)
#   icones/   → 0_Assets/diagramas/icones/
# Usa a cópia local (clone do boaz) se existir; senão baixa do GitHub.
install_assets() {
  local src tmp="" n
  src="$(cd "$(dirname "$0")" && pwd)/assets"
  if [ ! -f "$src/tema.svg" ]; then
    tmp=$(mktemp -d)
    if command -v curl &>/dev/null; then
      curl -fsSL "$ASSETS_TARBALL" | tar -xzf - -C "$tmp" boaz-main/assets 2>/dev/null || true
    elif command -v wget &>/dev/null; then
      wget -qO- "$ASSETS_TARBALL" | tar -xzf - -C "$tmp" boaz-main/assets 2>/dev/null || true
    fi
    src="$tmp/boaz-main/assets"
  fi
  if [ ! -f "$src/tema.svg" ]; then
    warn "Temas e ícones não baixados (sem rede?) — rode ./boaz.sh init de novo depois."
    [ -z "$tmp" ] || rm -rf "$tmp"
    return
  fi
  local r
  for r in tema.md requisito.mjs casca.html; do
    [ ! -f "$src/requisitos/$r" ] || write_once "0_Assets/requisitos/$r" < "$src/requisitos/$r"
  done
  write_once "0_Assets/diagramas/tema.svg" < "$src/tema.svg"
  # cp -Rn: copia a árvore de uma vez, sem sobrescrever (|| true: alguns cp saem com 1 ao pular arquivos)
  mkdir -p "$ICONS_DIR"
  n=$(find "$ICONS_DIR" -type f -name '*.svg' | wc -l)
  cp -Rn "$src/icones/." "$ICONS_DIR/" 2>/dev/null || true
  n=$(( $(find "$ICONS_DIR" -type f -name '*.svg' | wc -l) - n ))
  [ -z "$tmp" ] || rm -rf "$tmp"
  [ "$n" -eq 0 ] || success "Ícones de cloud: ${n} novos em ${D}${ICONS_DIR}/${NC}"

  write_always "$ICONS_DIR/README.md" << 'EOF'
# Ícones de cloud

Gerenciado pelo `./boaz.sh init` (copia só o que falta; ícones que você adicionar aqui são mantidos).
Todos em `.svg`, nomes em kebab-case.

```
icones/
├── aws/                       # AWS Architecture Icons (pacote oficial 07/2025)
│   ├── servicos/<categoria>/  # um por serviço (64px) — amazon-ec2.svg, aws-lambda.svg
│   ├── recursos/<categoria>/  # recursos de um serviço (48px) — amazon-s3-bucket.svg
│   ├── grupos/                # fronteiras: region, aws-cloud, virtual-private-cloud-vpc, subnets
│   └── categorias/            # ícone por categoria — compute.svg, database.svg
└── gcp/                       # Google Cloud icons
    ├── produtos/              # ícones atuais dos produtos principais — bigquery.svg, gke.svg
    ├── categorias/            # ícone por categoria — compute.svg, databases.svg
    └── legado/                # conjunto completo antigo — cloud-functions.svg, pubsub.svg
└── azure/                     # Azure Public Service Icons (V24)
    └── <categoria>/           # compute/, databases/, networking/, general/… — virtual-machine.svg, kubernetes-services.svg
```

Procure por nome em vez de listar tudo: `find 0_Assets/diagramas/icones -iname '*lambda*'`.
EOF
}

# ── SKILLS DE DIAGRAMA ───────────────────────────────────────
# Regras comuns às duas skills; __PASTA__ é substituído pela pasta de saída
diagram_skill_common() {
  sed "s|__PASTA__|$1|g" << 'EOF'

## Versionamento (obrigatório)

Nome do arquivo: `<legenda>_v<NN>_<AAAAMMDD-HHMM>.svg`

- **legenda**: kebab-case, sem acentos, curta. Identifica o diagrama e **não muda** entre versões (ex.: `fluxo-cadastro`).
- **NN**: versão com 2 dígitos, começa em `01` e soma 1 a cada alteração.
- **timestamp**: data/hora local em que aquela versão foi gerada.

Obtenha o caminho exato com:

```bash
./boaz.sh svg __PASTA__ <legenda>
```

Se o `boaz.sh` não estiver disponível, calcule pela regra acima.
Nunca sobrescreva nem renomeie uma versão: cada alteração gera um novo arquivo com a versão seguinte e, **na mesma tarefa**, a versão anterior é apagada (`git rm`; `rm` se nunca foi para o git) depois que todas as referências a ela (requisitos `.md` e outros arquivos) forem atualizadas para a nova. O histórico fica no git.

## Fluxo

**Diagrama novo**
1. Leia os documentos relevantes de `1_Requisitos/` (`.md`) e rode `./boaz.sh diagramas` para reaproveitar nomes e evitar duplicatas.
2. Se a ambiguidade mudar o desenho, faça no máximo 1–2 perguntas; caso contrário, assuma e declare as premissas na resposta.
3. Escolha a legenda, gere o caminho e escreva o SVG.

**Alteração** ("muda", "adiciona", "corrige" num diagrama existente)
1. Identifique a legenda e abra a **maior versão** (`ls __PASTA__/<legenda>_v*`).
2. Aplique só a mudança pedida, preservando layout, ids e o restante do desenho.
3. Salve como a próxima versão (`./boaz.sh svg __PASTA__ <legenda>`).
4. Rode `grep -rn "<legenda>_v" --exclude-dir=node_modules 1_Requisitos 0_Assets .claude 4_CI_CD 5_DEV CLAUDE.md boaz.sh` e, se houver referências à versão anterior, pergunte se devem apontar para a nova. Não altere sem confirmação; confirmado, troque o caminho na legenda e nos dois links da imagem (`[![…](…)](…)`), e cada documento alterado ganha uma versão nova (front matter, carimbo, histórico e rodapé, como na skill `requisito`).
5. Com nenhuma referência à versão anterior, apague-a (`git rm __PASTA__/<legenda>_v<NN-1>_*.svg`). Se alguma referência não puder ser trocada agora (requisito de outro dono, cópia em `5_DEV/`), mantenha a anterior e diga na resposta o que falta trocar; `./boaz.sh antigas` lista as versões antigas que já podem sair.

## Assets (`0_Assets/`)

Antes de desenhar, rode `ls 0_Assets/projeto 0_Assets/diagramas` e reaproveite o que existir: ícones de tecnologias, logos de integrações e da marca.
Ícones de cloud ficam em `0_Assets/diagramas/icones/{aws,gcp,azure}/` (centenas de arquivos — leia o `README.md` da pasta e busque por nome: `find 0_Assets/diagramas/icones -iname '*lambda*'`, nunca `ls -R`).

- **Embuta sempre** — nunca referencie o arquivo por caminho (`href="../0_Assets/..."` quebra no GitHub e em visualizadores de imagem).
  - `.svg`: copie o conteúdo gráfico para um `<symbol id="asset-<nome>" viewBox="...">` em `<defs>` (com o `viewBox` do original) e desenhe com `<use href="#asset-<nome>" x=".." y=".." width=".." height=".."/>`. Remova `<script>`, `<foreignObject>`, handlers `on*` e metadados de editor; prefixe ids internos com `<nome>-` para não colidirem. Se o ícone tiver `<style>` próprio (os do GCP trazem `.cls-1`, `.st0`…), converta cada regra em atributo (`fill="#4285f4"`) nos elementos e apague o `<style>` — senão as classes vazam e colidem com outros ícones. O `tema.svg` traz exemplos prontos em `#prov-*`.
  - `.png`/`.jpg`/`.webp`: `<image href="data:image/png;base64,..."/>` gerado com `base64 < arquivo | tr -d '\n'`. Evite arquivos acima de ~200 KB.
- Ícone é apoio, não substitui o texto: a caixa continua com nome e tecnologia escritos. Tamanho uniforme (24–32px), sempre no mesmo lugar da caixa (padrão do tema: à esquerda, nome e tecnologia alinhados depois dele).
- Faltou um ícone? Use a forma neutra `#ic-*` do tema que mais se aproxima (ou a forma simples da notação) e mencione na resposta qual asset poderia ser adicionado em `0_Assets/diagramas/`.

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

Responda curto: caminho do arquivo criado, premissas assumidas (se houver) e o trecho para referenciar no requisito (`.md`, componente **Diagramas e telas** de `0_Assets/requisitos/tema.md`):

```markdown
**<Título>** — `__PASTA__/<arquivo>.svg`

[![<o que o diagrama mostra>](../__PASTA__/<arquivo>.svg)](../__PASTA__/<arquivo>.svg)
```
EOF
}

create_diagram_skills() {
  {
    cat << 'EOF'
---
name: diagrama-dev
description: Cria ou altera diagramas de desenvolvimento (backend e frontend) como SVG versionado em 2_Diagramas/. Use para diagramas UML — classes, objetos, pacotes, componentes internos, estrutura composta, perfil, casos de uso, atividades, sequência, comunicação, visão geral de interação, estados e tempo — além de entidade-relacionamento, navegação de telas e contratos de API, ou quando pedirem para alterar um diagrama existente dessa pasta.
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
EOF
    diagram_skill_common "2_Diagramas"
  } | write_always ".claude/skills/diagrama-dev/SKILL.md"

  {
    cat << 'EOF'
---
name: diagrama-arquitetura
description: Cria ou altera diagramas de arquitetura de alto nível como SVG versionado em 3_Arquitetura/. Use para contexto do sistema, contêineres (C4), diagrama de implantação UML, componentes no nível de serviços/sistemas, cloud, rede, ambientes, deploy, tecnologias escolhidas e integrações externas, ou quando pedirem para alterar um diagrama existente dessa pasta.
---

# Diagrama de arquitetura → `3_Arquitetura/`

Você é arquiteto de soluções. A saída é **sempre um novo arquivo `.svg`** em `3_Arquitetura/` — nunca apenas texto, Mermaid ou PlantUML.

## Escopo

Pertence aqui a **visão de alto nível**: contexto do sistema e atores, contêineres/serviços e suas tecnologias, provedores de cloud e serviços gerenciados, rede (região, rede privada, sub-redes), ambientes, topologia de deploy, filas e integrações externas.

Não pertence: detalhe interno de código (classes, objetos, pacotes, sequência entre métodos, estados, telas e demais UML de código) → skill `diagrama-dev` (`2_Diagramas/`).

## Escolha do tipo

Se o pedido nomear o tipo, use-o. Senão, escolha pela pergunta e diga na resposta qual tipo usou:

| Pergunta | Tipo |
|---|---|
| Quem usa o sistema e com quais sistemas externos ele conversa? | C4 nível 1 — contexto |
| Quais aplicações, serviços e bancos compõem o sistema e com que tecnologia? | C4 nível 2 — contêiner |
| Quais serviços/sistemas existem e quais interfaces expõem e consomem? | Componentes UML (nível de serviço) |
| Onde cada artefato roda — nós, ambientes, rede? | Implantação UML |

## Neutralidade de provedor

A arquitetura deve valer para **Azure, AWS e GCP**. Nunca use nome de produto de um único provedor (`Lambda`, `EKS`, `S3`, `Cosmos DB`, `BigQuery`…): nomeie pela **categoria** do serviço gerenciado. Tecnologias abertas e portáveis (PostgreSQL, Kubernetes, Kafka, Redis, Docker) podem ser citadas. Só use o nome de um provedor se o pedido o exigir explicitamente — e então diga isso na resposta.

| Termo neutro | AWS | Azure | GCP |
|---|---|---|---|
| Rede privada / sub-rede | VPC / Subnet | VNet / Subnet | VPC / Subnet |
| Conta / assinatura / projeto | Account | Subscription | Project |
| Máquina virtual | EC2 | Virtual Machines | Compute Engine |
| Função serverless | Lambda | Functions | Cloud Functions / Cloud Run functions |
| Contêiner serverless | Fargate / App Runner | Container Apps | Cloud Run |
| Cluster Kubernetes gerenciado | EKS | AKS | GKE |
| Armazenamento de objetos | S3 | Blob Storage | Cloud Storage |
| Banco relacional gerenciado | RDS / Aurora | Azure Database / SQL Database | Cloud SQL / AlloyDB |
| Banco NoSQL gerenciado | DynamoDB | Cosmos DB | Firestore / Bigtable |
| Cache gerenciado | ElastiCache | Azure Cache for Redis | Memorystore |
| Fila de mensagens | SQS | Service Bus / Storage Queues | Pub/Sub / Cloud Tasks |
| Barramento / tópico de eventos | SNS / EventBridge | Event Grid / Event Hubs | Pub/Sub / Eventarc |
| API gateway | API Gateway | API Management | API Gateway / Apigee |
| Balanceador de carga | ELB / ALB | Load Balancer / Application Gateway | Cloud Load Balancing |
| CDN | CloudFront | Front Door / CDN | Cloud CDN |
| DNS gerenciado | Route 53 | Azure DNS | Cloud DNS |
| Identidade de usuários | Cognito | Entra ID / Entra External ID | Identity Platform |
| Identidade e acesso (IAM) | IAM | Entra ID + RBAC | IAM |
| Cofre de segredos | Secrets Manager | Key Vault | Secret Manager |
| Observabilidade (logs/métricas) | CloudWatch | Azure Monitor | Cloud Logging / Monitoring |
| Data warehouse | Redshift | Synapse / Fabric | BigQuery |
| Registro de contêineres | ECR | Container Registry | Artifact Registry |

Se o termo não estiver na tabela, crie um equivalente neutro no mesmo estilo (categoria + "gerenciado", quando couber).

## Notação

- **C4 contexto/contêiner**: como descrito abaixo nas regras gerais.
- **Componentes (nível de serviço)**: cada serviço ou sistema em retângulo com `«component»` (ou `«system»`) e o ícone `#uml-componente` no canto; interface fornecida = pirulito (linha + círculo `ink-ring`) rotulada com o contrato (`REST /pedidos`, `tópico pedido.criado`); requerida = soquete (meio-círculo). Conecte soquete ao pirulito.
- **Implantação UML**: nós como caixas 3D (retângulo `node` com face superior e lateral em paralelogramos `node-face`) com `«device»` ou `«executionEnvironment»` e o nome (`Cluster Kubernetes gerenciado`, `Máquina virtual`, `Navegador`); nós podem conter nós. Artefatos dentro dos nós como retângulos com `«artifact»`, o ícone `#uml-artefato` e nome do arquivo/imagem (`api:1.4.2` em `mono`). Caminhos de comunicação como linhas entre nós rotuladas com protocolo; dependência artefato→componente com `edge-async` + `seta-aberta` `«manifest»`.
- Cada caixa: nome em negrito, tecnologia entre colchetes (`[PostgreSQL 16]`, `[Função serverless]`) e uma linha de responsabilidade.
- Ícones: no diagrama neutro, use as formas neutras `#ic-*` do tema (seção 3 — `ic-banco-sql`, `ic-fila`, `ic-funcao`, `ic-gateway`, `ic-cdn`, `ic-objetos`, `ic-identidade`…) com 24px à esquerda do nome, como no exemplo da seção 4; nada de logo de produto. Ícones de tecnologias abertas e logos de integrações vêm de `0_Assets/diagramas/` (embutidos, ver **Assets**).
- Ícones de provedor (`icones/aws/`, `icones/gcp/`, `icones/azure/`) só quando o pedido fixar o provedor: substituem a forma neutra da mesma categoria (seção 5 do tema mostra a correspondência). Com provedor fixado, use `icones/aws/grupos/` para as fronteiras (região, VPC, sub-redes).
- Fronteiras (conta/assinatura/projeto, região, rede privada, cluster, ambiente) como retângulos tracejados rotulados no canto superior esquerdo.
- Setas rotuladas com o que trafega e o protocolo (`pedidos · HTTPS/JSON`, `eventos · AMQP`).
- Sistemas externos com `node-external`, para separar do que é do projeto.
EOF
    diagram_skill_common "3_Arquitetura"
  } | write_always ".claude/skills/diagrama-arquitetura/SKILL.md"
}

# ── SKILLS DE REQUISITOS ─────────────────────────────────────
create_requirements_skills() {
  write_always ".claude/skills/requisito/SKILL.md" << 'EOF'
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
1. Rode `./boaz.sh pdf <documento>` (ex.: `./boaz.sh pdf 02_cadastro-usuario`): o `requisito.mjs` gera o HTML com a casca e o Chrome imprime `1_Requisitos/pdf/<documento>_v<NN>_<AAAAMMDD-HHMM>.pdf` com a versão e a data do front matter, tema claro, A4, faixa de topo, histórico e cabeçalho/rodapé paginados; os PDFs de versões anteriores do mesmo documento são apagados (o histórico fica no git).
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
EOF

  write_always ".claude/skills/requisito-estilo/SKILL.md" << 'EOF'
---
name: requisito-estilo
description: Cuida da apresentação dos documentos de requisito (.md) de 1_Requisitos/ — o formato e o catálogo de componentes em Markdown de 0_Assets/requisitos/tema.md, o renderizador 0_Assets/requisitos/requisito.mjs (Markdown → HTML, conferência) e a casca 0_Assets/requisitos/casca.html do HTML/PDF (cores, tipografia, temas claro/sistema/escuro, paleta das vistas, impressão) — migra documentos .html antigos para .md e exporta PDF versionado. Use quando pedirem para mudar o visual ou o formato dos requisitos, criar um componente novo, ajustar o PDF, converter requisitos .html ou gerar PDFs.
---

# Apresentação dos requisitos → `0_Assets/requisitos/`

Você é designer de documentação técnica. O conteúdo dos requisitos é da skill `requisito`; aqui se cuida de **como eles aparecem**: o Markdown do catálogo, o HTML gerado para o PDF, a migração de `.html` antigos e o PDF.

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
EOF
}

# ── DIAGRAMAS ────────────────────────────────────────────────
check_diagram_dir() {
  case " $DIAGRAM_DIRS " in
    *" $1 "*) ;;
    *) fail "Pasta inválida: $1 (use: ${DIAGRAM_DIRS// / ou })" ;;
  esac
}

# Imprime "<legenda> <versão> <arquivo>" de cada SVG versionado em $1
list_versions() {
  local f base
  for f in "$1"/*_v[0-9]*_*.svg; do
    [ -e "$f" ] || continue
    base=$(basename "$f" .svg)
    echo "$base" | sed -nE "s|^(.+)_v0*([0-9]+)_[0-9]{8}-[0-9]{4}$|\1 \2 $f|p"
  done
}

# Próximo caminho livre para a legenda: <pasta>/<legenda>_v<NN>_<AAAAMMDD-HHMM>.svg
cmd_svg() {
  [ $# -eq 2 ] || fail "Uso: ./boaz.sh svg <2_Diagramas|3_Arquitetura> <legenda>"
  local dir=${1%/} slug last
  check_diagram_dir "$dir"
  slug=$(slugify "$2")
  [ -n "$slug" ] || fail "Legenda vazia."
  last=$(list_versions "$dir" | awk -v s="$slug" '$1 == s && $2 > m { m = $2 } END { print m + 0 }')
  printf '%s/%s_v%02d_%s.svg\n' "$dir" "$slug" $((last + 1)) "$(date +%Y%m%d-%H%M)"
}

cmd_diagramas() {
  local dir
  for dir in $DIAGRAM_DIRS; do
    title "$dir"
    if [ ! -d "$dir" ]; then
      warn "Pasta não existe — rode ./boaz.sh init"
      continue
    fi
    list_versions "$dir" \
      | sort -k1,1 -k2,2n \
      | awk '{ last[$1] = $3; count[$1]++ } END { for (s in last) printf "%s\t%d\n", last[s], count[s] }' \
      | sort \
      | while IFS="$(printf '\t')" read -r file n; do
          echo "  ${G}●${NC} ${file}  ${D}(${n} versão(ões))${NC}"
        done
    [ -n "$(list_versions "$dir")" ] || info "Nenhum diagrama ainda."
  done
}

# Versões antigas (não são a mais recente da legenda) que nenhum arquivo do
# repositório cita mais; com --apagar, remove (git rm se versionada, rm se não).
REF_PATHS="CLAUDE.md boaz.sh 0_Assets 1_Requisitos .claude 4_CI_CD 5_DEV .github"
cmd_antigas() {
  local apagar=0 dir f name ref refs kept=0 gone=0
  [ "${1:-}" = "--apagar" ] && apagar=1
  for dir in $DIAGRAM_DIRS; do
    [ -d "$dir" ] || continue
    title "$dir"
    list_versions "$dir" \
      | sort -k1,1 -k2,2n \
      | awk '{ if ($1 == prev) print pf; prev = $1; pf = $3 }' \
      | while read -r f; do
          name=$(basename "$f")
          # shellcheck disable=SC2086
          refs=$(grep -rlIF --exclude-dir=node_modules --exclude-dir=bin --exclude-dir=obj \
                   --exclude-dir=worktrees --exclude-dir=.git -e "$name" $REF_PATHS 2>/dev/null || true)
          if [ -n "$refs" ]; then
            ref=$(echo "$refs" | head -1)
            echo "  ${Y}●${NC} ${f}  ${D}(citada em ${ref}$( [ "$(echo "$refs" | wc -l)" -gt 1 ] && echo " e outros"))${NC}"
          elif [ $apagar -eq 1 ]; then
            if git ls-files --error-unmatch "$f" &>/dev/null; then git rm -q "$f"; else rm -f "$f"; fi
            echo "  ${R}✖${NC} ${f}  ${D}(apagada)${NC}"
          else
            echo "  ${G}●${NC} ${f}  ${D}(sem referência — pode sair)${NC}"
          fi
        done
  done
  [ $apagar -eq 1 ] || { echo ""; info "Rode ${W}./boaz.sh antigas --apagar${NC} para remover as sem referência (as citadas ficam)."; }
}

# ── REQUISITOS ───────────────────────────────────────────────
# Valor do campo $2 do front matter do documento $1 (valor entre aspas duplas, como no tema.md)
req_meta() {
  awk -v k="$2" 'NR == 1 && $0 != "---" { exit } NR > 1 && $0 == "---" { exit }
    index($0, k ":") == 1 { v = substr($0, length(k) + 2); sub(/^ +/, "", v); print v; exit }' "$1" \
    | sed 's/^"\(.*\)"$/\1/; s/\\"/"/g; s/\\\\/\\/g'
}

# Lista os documentos de 1_Requisitos com versão, data e status lidos do front matter
cmd_requisitos() {
  title "$REQ_DIR"
  [ -d "$REQ_DIR" ] || fail "Pasta não existe — rode ./boaz.sh init"
  local f n=0
  for f in "$REQ_DIR"/*.md; do
    [ -e "$f" ] || continue
    [ "$(basename "$f")" != "README.md" ] || continue
    n=$((n + 1))
    if [ -z "$(req_meta "$f" versao)" ]; then
      echo "  ${Y}●${NC} $(basename "$f")  ${D}(sem front matter boaz)${NC}"
      continue
    fi
    printf "  %s●%s %-38s %-5s %s%s · %s%s\n" "$G" "$NC" "$(basename "$f")" \
      "$(req_meta "$f" versao)" "$D" "$(req_meta "$f" data)" "$(req_meta "$f" status)" "$NC"
  done
  # Documentos .html da versão anterior do boaz
  for f in "$REQ_DIR"/*.html; do
    [ -e "$f" ] || continue
    n=$((n + 1))
    echo "  ${Y}●${NC} $(basename "$f")  ${D}(.html legado — migre com /requisito-estilo)${NC}"
  done
  [ "$n" -gt 0 ] || info "Nenhum documento ainda."
}

# Primeiro navegador Chromium encontrado (Chrome, Chromium, Edge, Brave), ou $BOAZ_CHROME
find_chrome() {
  local c
  for c in "${BOAZ_CHROME:-}" google-chrome google-chrome-stable chromium chromium-browser microsoft-edge brave-browser \
           "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" \
           "/Applications/Chromium.app/Contents/MacOS/Chromium" \
           "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge" \
           "/Applications/Brave Browser.app/Contents/MacOS/Brave Browser"; do
    [ -n "$c" ] || continue
    if [ -x "$c" ]; then echo "$c"; return; fi
    if command -v "$c" &>/dev/null; then command -v "$c"; return; fi
  done
}

# Exporta um documento para 1_Requisitos/pdf/<documento>_v<NN>_<AAAAMMDD-HHMM>.pdf
# (o requisito.mjs gera o HTML com a casca; o Chrome headless imprime)
pdf_one() {
  local f=$1 chrome=$2 doc ver data out tmp
  doc=$(basename "$f" .md)
  ver=$(req_meta "$f" versao)
  data=$(req_meta "$f" data)
  [ -n "$ver" ] && [ -n "$data" ] || { warn "$(basename "$f"): sem front matter boaz (versao/data) — ignorado"; return 1; }
  out="$REQ_DIR/pdf/${doc}_${ver}_$(echo "$data" | tr -d ':-' | tr ' ' '-').pdf"
  mkdir -p "$REQ_DIR/pdf"
  rm -f "$out"
  tmp=$(mktemp -d)
  if ! node "$REQ_TOOL" html "$f" "$tmp/$doc.html"; then
    rm -rf "$tmp"
    warn "Falha ao gerar o HTML de $(basename "$f")"
    return 1
  fi
  "$chrome" --headless --disable-gpu --no-pdf-header-footer --allow-file-access-from-files --virtual-time-budget=15000 \
    --print-to-pdf="$PWD/$out" "file://$tmp/$doc.html" &>/dev/null || true
  rm -rf "$tmp"
  if [ -s "$out" ]; then
    success "PDF: ${D}${out}${NC}"
    # só a versão atual fica na pasta; as anteriores ficam no histórico do git
    local antigo
    for antigo in "$REQ_DIR/pdf/${doc}"_v[0-9]*_*.pdf; do
      [ -e "$antigo" ] && [ "$antigo" != "$out" ] || continue
      if git ls-files --error-unmatch "$antigo" &>/dev/null; then git rm -q "$antigo"; else rm -f "$antigo"; fi
      info "Apagado: ${D}${antigo}${NC}"
    done
  else
    warn "Falha ao gerar o PDF de $(basename "$f")"
    return 1
  fi
}

cmd_pdf() {
  [ $# -eq 1 ] || fail "Uso: ./boaz.sh pdf <documento|todos>   ex.: ./boaz.sh pdf 02_cadastro-usuario"
  local chrome f arg=${1%.md} falhas=0
  command -v node &>/dev/null || fail "Node não encontrado: o PDF é gerado por ${REQ_TOOL}."
  [ -f "$REQ_TOOL" ] || fail "Sem ${REQ_TOOL} — rode ./boaz.sh init"
  chrome=$(find_chrome)
  [ -n "$chrome" ] || fail "Chrome, Chromium, Edge ou Brave não encontrado (defina BOAZ_CHROME)."
  title "PDF → $REQ_DIR/pdf"
  if [ "$arg" = "todos" ]; then
    for f in "$REQ_DIR"/*.md; do
      [ -e "$f" ] || continue
      [ "$(basename "$f")" != "README.md" ] || continue
      pdf_one "$f" "$chrome" || falhas=$((falhas + 1))
    done
  else
    arg=${arg#"$REQ_DIR"/}
    if [ -f "$REQ_DIR/$arg.md" ]; then
      f="$REQ_DIR/$arg.md"
    else
      # aceita só o número (02) ou só o nome (cadastro-usuario)
      f=$(ls "$REQ_DIR"/*.md 2>/dev/null | grep -E "/(${arg}_[^/]*|[0-9]{2}_${arg})\.md$" | head -1 || true)
    fi
    [ -n "$f" ] && [ -f "$f" ] || fail "Documento não encontrado: $1 (veja ./boaz.sh requisitos)"
    pdf_one "$f" "$chrome" || falhas=1
  fi
  [ "$falhas" -eq 0 ] || exit 1
}

# ── STATUS ───────────────────────────────────────────────────
cmd_status() {
  load_config
  title "Projeto [${PROJECT_NAME:-sem nome}]"
  local d
  for d in 0_Assets 1_Requisitos 2_Diagramas 3_Arquitetura 4_CI_CD 5_DEV; do
    if [ -d "$d" ]; then
      local n
      n=$(find "$d" -type f ! -name .gitkeep ! -name README.md | wc -l | tr -d ' ')
      echo "  ${G}✔${NC}  ${W}${d}/${NC}  ${D}(${n} arquivos)${NC}"
    else
      echo "  ${R}✖${NC}  ${W}${d}/${NC}  ${R}não encontrado${NC}"
    fi
  done
}

cmd_help() {
  cat << EOF

  ${W}Uso:${NC} ./boaz.sh [comando]

  ${C}init [nome]${NC}               Monta/completa a estrutura e (re)gera as skills
  ${C}status${NC}                    Mostra as pastas do projeto
  ${C}requisitos${NC}                Lista os documentos de requisito com versão, data e status
  ${C}pdf <documento|todos>${NC}     Exporta o requisito para 1_Requisitos/pdf/<documento>_v<NN>_<data>.pdf
  ${C}diagramas${NC}                 Lista a versão mais recente de cada diagrama
  ${C}antigas [--apagar]${NC}        Lista as versões antigas de diagramas (e quem ainda as cita);
                            --apagar remove as que nenhum arquivo cita mais
  ${C}svg <pasta> <legenda>${NC}     Imprime o próximo nome versionado de um diagrama
                            ex.: ./boaz.sh svg 2_Diagramas "Fluxo de cadastro"
  ${C}help${NC}                      Esta ajuda

  Sem comando: ${W}init${NC} na primeira execução, ${W}status${NC} nas seguintes.

EOF
}

# ── ENTRY POINT ──────────────────────────────────────────────
cmd=${1:-}
[ $# -gt 0 ] && shift

case "$cmd" in
  "")
    banner
    if [ -f "$CONFIG_FILE" ]; then cmd_status; cmd_help; else cmd_init; fi
    ;;
  init)      banner; cmd_init "$@" ;;
  status)    cmd_status ;;
  requisitos) cmd_requisitos ;;
  pdf)       cmd_pdf "$@" ;;
  diagramas) cmd_diagramas ;;
  antigas)   cmd_antigas "$@" ;;
  svg)       cmd_svg "$@" ;;
  help|-h|--help) cmd_help ;;
  *)         warn "Comando desconhecido: $cmd"; cmd_help; exit 1 ;;
esac
