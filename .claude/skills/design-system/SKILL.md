---
name: design-system
description: Cuida da identidade visual e do design system do boaz em 0_Assets/design/ — tokens no padrão Material Design 3 (cor, tipografia, forma, elevação, layout responsivo por classe de janela, movimento, estados), componentes de referência, identidade da marca (cores-semente, fontes, logos, ícones, imagens e animações), os mascotes (personagens com rosto expressivo: expressões, formas, proporções, movimentos) e a geração dos tokens para web (Vue) e mobile (.NET MAUI). Use quando pedirem para mudar ou consultar cores, fontes, logos, mascotes, carinhas, expressões, tema claro/escuro, tamanhos, espaçamentos, breakpoints, responsividade, um componente visual, ou quando qualquer tela, componente ou estilo for criado em 5_DEV/frontend (web ou mobile).
---

# Design system → `0_Assets/design/`

Você é designer de produto e mantém o design system. A identidade visual do boaz mora **só** em `0_Assets/design/`: nada de cor, fonte, tamanho ou logo definido em outro lugar.

## Como está organizado

```
0_Assets/design/
├── identidade/                 a marca — o que muda quando a identidade chegar
│   ├── marca.json              temas de cor, fontes, ícones                         (edite)
│   ├── logos/ icones/ imagens/ animacoes/ fontes/                                   (arquivos)
├── tokens/
│   ├── fundamentos.json        tipografia, forma, elevação, espaçamento, layout, movimento, estados (edite)
│   ├── componentes.json        tokens md.comp.* — só referências {md.sys.*} ou medidas em dp      (edite)
│   └── cores.json              paletas e esquemas claro/escuro/alto contraste por tema          (gerado)
├── plataformas/                                                                                  (gerado)
│   ├── web/tokens.css  web/tipografia.css  web/tokens.ts                      Vue
│   ├── web/mascotes.mjs  web/mascotes-temas.json                              Vue (mascotes)
│   └── maui/Tokens.xaml  maui/paletas/Paleta<Tema>.xaml                         .NET MAUI
├── mascotes/                   exemplos prontos dos mascotes (estáticos, animados, só o rosto, …)  (gerado)
├── design-system.html          catálogo vivo; o bloco design:tokens é gerado, o resto é editado
├── tema.svg                    quadro visual de todos os componentes e mascotes, claro e escuro (gerado)
├── scripts/gerar.mjs           gerador (@material/material-color-utilities, a biblioteca do M3)
├── scripts/tema-svg.mjs        desenho do tema.svg: uma função por componente
├── scripts/mascotes.mjs        núcleo dos mascotes (sem Node: roda também no navegador)       (edite)
├── scripts/mascotes-arquivos.mjs  o que vai para mascotes/ e plataformas/web/mascotes.mjs
└── package.json                pnpm run gerar · pnpm run conferir
```

Leia `design-system.html` (regras e catálogo) e os JSON antes de mudar algo. Para ver o resultado, abra o `.html` no navegador.

## Regras do Material Design 3 que o projeto segue

- **Três camadas de token**: `md.ref.*` (paletas tonais, fontes) → `md.sys.*` (papéis: `primary`, `on-surface`, `body-large`, `corner-medium`…) → `md.comp.*` (`button-filled-container-color`). Componente consome `md.comp` ou `md.sys`, **nunca** `md.ref` nem valor solto (`#hex`, `px`, `ms`).
- **Cor por papel**: texto `on-X` sobre fundo `X`. O esquema sai da semente em HCT; claro, escuro e alto contraste são gerados juntos e têm contraste garantido nos pares.
- **Tipografia**: 15 papéis (display, headline, title, body, label × large/medium/small). Fonte `marca` em display/headline/title-large, fonte `texto` no resto. O tamanho não muda com a tela: muda o papel escolhido. Web em `rem` (acompanha a fonte da pessoa); MAUI em unidades independentes com `FontAutoScalingEnabled`.
- **Layout por classe de janela** (largura disponível, não o aparelho): compact < 600 · medium 600–839 · expanded 840–1199 · large 1200–1599 · extra-large ≥ 1600. Cada uma fixa margem, colunas, espaço entre colunas, painéis e a navegação (barra → trilho → gaveta). Desenhe primeiro o compact.
- **Grade de 4dp** para espaçamentos; alvo de toque mínimo de 48dp; foco visível de 3dp em `secondary`; durações zeradas com movimento reduzido.
- Componentes seguem a anatomia do M3 (baseline). Adaptações para a marca mudam tokens, não a estrutura.

## Mudar a identidade (cores, fontes, ícones)

1. Edite `identidade/marca.json`:
   - `cores.temas.<id>`: um tema de cor (paleta) por entrada, com `nome`, `descricao` (para que serve), `harmonia` (análoga, complementar, triádica…), `semente` (`#RRGGBB`, gera o esquema), `secundaria`/`terciaria`/`neutra`/`erro` opcionais (null = derivadas pelo M3; a `terciaria` define a combinação) e `variante`. `cores.tema_padrao` é o tema aplicado sem escolha: o id de um tema ou `{ "claro": <id>, "escuro": <id> }` (um tema em cada modo, gerado como o tema combinado `<claro>-<escuro>`). Hoje é branco no claro e preto no escuro.
   - `variante`: `fidelity` mantém a cor da marca reconhecível (a semente vira o `primary-container`); `tonal-spot` é o padrão discreto do M3 (dessatura a semente); `neutral` para temas sóbrios (grafite).
   - Todos os temas geram os **mesmos papéis**: componente nunca sabe o tema. Web troca com `<html data-paleta="<id>">` (ou `class="md-paleta-<id>"` num trecho); MAUI com `plataformas/maui/paletas/Paleta<Tema>.xaml` depois de `Tokens.xaml`.
   - Tema novo: escolha a combinação por harmonia, confira no catálogo (seção **Temas de cor**, claro e escuro) e no `tema.svg`; os pares `on-X`/`X` saem com contraste garantido, mas olhe se a cor da marca continua reconhecível.
   - Amarelo como semente: no claro o `primary` vira dourado-escuro (texto branco precisa de contraste); o amarelo vivo aparece em `primary-container` com `fidelity`. Vermelho como semente: defina `erro` com outra matiz (ex.: carmim `#8E0052`) para erro não se confundir com a marca.
   - Branco/preto: `variante: "monochrome"` e `ajustes.light|dark` para fixar papéis (ex.: `surface: #FFFFFF`, ou `#000000` para OLED); `modo_preferido` (`light`/`dark`) diz em qual modo o tema brilha. O gerador recusa ajuste que deixe um par `on-X`/`X` (ou `on-surface` sobre qualquer superfície) abaixo de 4,5:1 (7:1 no alto contraste).
   - `cores.extras`: cores fora dos papéis do M3 (ex.: `sucesso`, `aviso`), harmonizadas com a semente de cada tema; geram `X`, `on-X`, `X-container` e `on-X-container`.
   - `tipografia.marca` / `tipografia.texto`: família, alternativas, `google_fonts` (só a web carrega do Google; o app embute) e `arquivos` por peso em `identidade/fontes/` (aliases MAUI `MarcaRegular`, `TextoMedium`…).
   - `icones`: Material Symbols e seus eixos (preenchimento, peso, grau, tamanho ótico).
   - Ao receber a identidade definitiva, mude `status` para `"definida"`.
2. Rode em `0_Assets/design/`: `corepack enable && pnpm install && pnpm run gerar` (Node 24).
3. Abra o catálogo e confira cada tema (seção **Temas de cor**) nos dois esquemas: contraste dos pares, cores extras e tipografia.
4. Nova versão do catálogo (ver **Versionamento**).

## Adicionar arquivos de marca

| Pasta | O que entra | Formato e nome |
|---|---|---|
| `identidade/logos/` | logo principal, negativo (fundo escuro), monocromático, símbolo | `.svg` (obrigatório) · `logo-principal.svg`, `logo-principal-negativo.svg`, `logo-mono.svg`, `simbolo.svg` |
| `identidade/icones/` | ícone do app (1024×1024, área segura de 66%, sem transparência para iOS), favicon | `app-icone.svg` + `app-icone-fundo.svg` (adaptativo Android), `favicon.svg` |
| `identidade/imagens/` | ilustrações, estados vazios, onboarding, fundos | `.svg`; foto em `.webp` (≤ 200 KB, 2× a maior exibição) |
| `identidade/animacoes/` | carregamento, sucesso, splash | Lottie `.json` (preferido) ou `.webp` animado; `.gif` só se não houver outro (≤ 500 KB) |
| `identidade/fontes/` | arquivos da fonte, com a licença | `.ttf`/`.otf` · `<familia>-<peso>.ttf` + `LICENSE` |

- Nomes em kebab-case, sem acentos. Um arquivo por variação; nada de cor “embutida” que deveria seguir o tema (ícones monocromáticos usam `currentColor`).
- Imagens precisam funcionar no claro e no escuro (ou ter as duas versões: `-claro`, `-escuro`).
- Cada arquivo novo aparece no catálogo: troque o cartão `.vaga` correspondente da seção **Identidade** pela imagem (`<img src="identidade/...">`).
- Repositório público: nada de arquivo com licença que proíba redistribuição.

## Mudar fundamentos ou componentes

- Fundamento (escala tipográfica, cantos, espaçamento, janelas, movimento, estados): edite `tokens/fundamentos.json`. Só se afaste do M3 com motivo, e registre o motivo no histórico do catálogo.
- Componente novo ou alterado:
  1. tokens em `tokens/componentes.json` (`md.comp.<componente>.*`), só com `{md.sys.*}` ou medidas em dp — o gerador recusa referências inexistentes;
  2. implementação de referência no `<style id="design-componentes">` do catálogo (classes `md-<componente>`, só `var(--md-comp-…)`/`var(--md-sys-…)`, com estados hover/foco/pressionado/desabilitado e alvo de 48dp), mais um exemplo na seção **Componentes**;
  3. desenho no `tema.svg`: uma entrada em `COMPONENTES` de `scripts/tema-svg.mjs` (título, descrição, altura do conteúdo e a função que desenha num esquema), usando só os tokens (`num`, `cor`, `canto`, `tipoDe`, `nivel`) e as primitivas do arquivo; ícones novos entram em `ICONES` (Material Icons, viewBox 24);
  4. se for usado no app, um estilo em `Tokens.xaml` (no gerador, como os botões).
- Rode `pnpm run gerar`. Nunca edite `cores.json`, `plataformas/*`, `tema.svg` nem o bloco `design:tokens` à mão: `pnpm run conferir` falha se estiverem diferentes do gerado.

## Marca (símbolo, logo, ícone, favicon)

- O símbolo do deboaz é um b e um d: barriga redonda com uma antena de cada lado, separadas dela por um vão (`VAO`) que acompanha a curva. Variação `longo` (hastes longas) na landing page e no hub; `curto` (hastes curtas) no quest (`FRENTES` em `scripts/marca.mjs`).
- Tudo sai do núcleo `scripts/marca.mjs` (sem Node, roda no navegador) por `scripts/marca-arquivos.mjs`: `identidade/logos/` e `identidade/icones/` são **gerados** — nunca edite os `.svg` à mão. O nome deboaz é medido em `identidade/fontes/fredoka-medium.ttf` (opentype.js) e vira contorno.
- Oficial: monocromático (preto; `-negativo` em branco); ícone e favicon com fundo preto e símbolo branco. Num tema de cor, o logo usa `tertiary` no anel, `primary` na antena do b e em "boaz" e `secondary` na antena do d e em "de" (cores `{ anel, b, d, de, boaz }`), e o favicon `primary` com `on-primary` — tabela em `plataformas/web/marca-temas.json` (`coresDaMarca`). Nos temas monocromáticos tudo fica em `on-surface`.
- Web: `import { marca, FRENTES } from '@/design/marca.mjs'` e `marca.logo(FRENTES.hub, cores)`; em SVG embutido, as cores podem ser `var(--md-sys-color-…)` e acompanham tema e modo sozinhas (como faz o catálogo).
- Animações: `marca.abertura(variante, cores)` (o anel se desenha, as antenas brotam do vão, o nome entra; toca uma vez), `marca.fechamento` (o inverso: o nome sai, as antenas recolhem, o anel se apaga; uma vez), `marca.ciclo` (abertura + fechamento em laço) e `marca.carregamento(variante, cores)` (laço); os oficiais saem em `identidade/animacoes/`. Com movimento reduzido, use `marca.logo` / `marca.simbolo` parados.
- App (MAUI): `plataformas/maui/Marca.xaml` traz as peças como `PathGeometry` (`MarcaAnel`, `MarcaAntenaB/D[Curta]`, `MarcaNomeDe/Boaz`); desenhe cada uma num `Path` com a cor do tema (`AppThemeBinding`), num `Grid` de 540 × 793 escalado. É vetor nativo, sem custo de processar SVG. O ícone da tela inicial é sempre o oficial.
- Regras de uso: `1_Requisitos/90_identidade-visual.html`.
- Animação no app (decidido): o MAUI não toca SMIL, então a marca e os mascotes animam com a **API de animação do próprio MAUI** sobre os desenhos parados — `TranslateTo` (cima, baixo, lados, pular, flutuar), `RotateTo` (balançar, girar), `ScaleTo` (frente e trás, comemorar) e `FadeTo`, encadeados com `await` e repetidos em laço, com as mesmas durações e curvas de `fundamentos.json` (movimento). O equivalente de cada movimento está em `MOVIMENTOS` de `scripts/mascotes.mjs`. Antes de animar, consulte a preferência de movimento reduzido do aparelho e, se ativa, não anime. `WebView` com o `.svg` animado não é o padrão: só com motivo registrado na vista.

## Fundos

- Seis conceitos em `scripts/fundos.mjs` (sem Node; recebe os núcleos da marca e dos mascotes em `criarFundos`): `estampa`, `anel`, `antenas`, `ondas`, `recorte`, `halo`. Uso de cada um em `CONCEITOS` e na RN-56 do requisito 90.
- `fundos.desenhar(conceito, { largura, altura, cores, pecas, mascote, logo, quantidade, semente, origem, movimentoReduzido })`: `pecas` = `ambos` | `logo` | `mascotes` | `nenhuma` (só o desenho, para fundo de card: o card do catálogo usa as Ondas do vão e o destaque da landing o Anel gigante, na forma do padrinho); `origem` = `{ x, y }` de 0 a 1, centro do anel e das ondas; `raio` (px) fixa o tamanho do Anel gigante, para o card cortar só pela borda dele; `mascote` = `{ expressao, forma, proporcao, movimento }` (vazio = sortido, na estampa e nas antenas); `logo` = `''` | `abertura` | `fechamento` | `ciclo` | `carregamento`; `semente` nova a cada abertura da tela; `cores` de `plataformas/web/fundos-temas.json` (ou `var(--md-sys-color-…)` em SVG embutido, como no catálogo).
- Halo: `nome: true` (com `pecas: 'logo'`) desenha o logo inteiro, símbolo + nome, centrado na `origem` e com altura `raio`; com `logo: 'abertura'` ele se monta uma vez. `ciclo: [cores, …]` faz os brilhos passarem pelas cores desses temas em laço, suave (`cicloTempo` = `{ fica: 6, troca: 2 }` s), começando no primeiro; a superfície de baixo é sempre a de `cores`, para o painel de texto manter o contraste; com movimento reduzido, fica no primeiro tema e o logo aparece parado. `variante: 'curto'` usa o logo de hastes curtas (quest). O composto boas-vindas com o logo usa o Halo assim, com os temas coloridos (o ativo primeiro) e sem mascotes, na apresentação da landing page, na entrada do hub e no acesso do quest (no compact desses dois, numa faixa no alto que some antes do título).
- Fundo de tela: toda tela do hub, do quest e da landing page usa a Estampa abaixo da barra do topo, com `celula: 48` (rabiscos do mesmo tamanho em qualquer janela) e 30% mais leve (`opacity` 0.7); barras de navegação, menu e cards ficam por cima, limpos; os fundos próprios de um bloco (Anel gigante, Ondas do vão, Símbolo recortado) vão por cima dela. Componentes sem fundo próprio (campo outlined, botões outlined e de texto soltos na tela, segmentado, chip, controles do carrossel) usam os tokens `*-container-color` com o fundo da tela; o botão de texto fica transparente dentro de card, diálogo, snackbar e barras. Regra em `1_Requisitos/90_identidade-visual.html` (RN-56, RNF-119).
- Os oficiais (Branco e Preto, claro, parados) saem em `identidade/imagens/fundos/`; a web usa `plataformas/web/fundos.mjs`, já ligado a `./marca.mjs` e `./mascotes.mjs`.
- Texto por cima sempre num painel de superfície (RNF-119). Fundo novo ou peça nova entra no núcleo, com versão nova do catálogo.

## Mascotes

Personagens com rosto expressivo da identidade (regras de produto em `1_Requisitos/06_design-system-telas.html`, RN-34 a RN-38 e RNF-110 a RNF-115; catálogo em `design-system.html#mascotes`). Um mascote é a combinação de:

| Eixo | Onde (em `scripts/mascotes.mjs`) | Hoje |
|---|---|---|
| Expressão | `EXPRESSOES` (nome, uso, cor própria, duração, desenho) | 30, por situação: boas-vindas, acerto, erro, dúvida, espera, social |
| Forma | `FORMAS` (+ `GLIFOS` para números, letras e sinais) | planas, setas, 3D, glifos, `rosto` (só o rosto), `cartao`, `cartaoHorizontal` |
| Proporção | `PROPORCOES` | normal, alto, baixo, gordo, magro |
| Movimento | `MOVIMENTOS`; lista = sequência (`sequencia`) | direções, frente/trás, flutuar, pular, balançar, dançar, tremer, girar, comemorar |
| Cor | `CORES`, `#hex` ou a do tema (`coresDosTemas`) | tema colorido → cor do tema; monocromático → cor da expressão |

- **Código**: `animado(expressao, { forma, proporcao, cor, movimento })` e `estatico(…)` devolvem o SVG; `embutir(svg, prefixo, x, y, w, h)` coloca um mascote dentro de outro SVG (usado no `tema.svg`). Tudo que se move é transform/opacity de um grupo (`P(conteudo, mov, pivô)`), amostrado em laço SMIL.
- **Nova expressão**: uma entrada em `EXPRESSOES` com `rosto(c, conteudo, movCabeça, centro)` + enfeites (use `olho`, `sobrancelha`, `bocaAberta`, `traco`, `bochechas`, `gota`, `brilhinho`, `coracao`, `espiral`…), a linha na tabela "Quando usar" do catálogo e do requisito 06.
- **Nova forma**: em `FORMAS` com `unid` (polígono unitário) e `canto`; o encaixe acha a escala que contém o rosto (`livreX`/`livreY` deixam o rosto ir para o lado). 3D: `faces(pol)` com tons `claro`/`escuro`/`sombra`. Glifo: linha central em `GLIFOS` (caixa 100×140) e o ponto do traço onde fica o rosto.
- **Novo movimento**: em `MOVIMENTOS` com `corpo(t)` (t de 0 a 1, voltando ao repouso em t = 1) e `pivo` (`base` ou `centro`).
- Depois: `pnpm run gerar` (grava `mascotes/`, o `tema.svg` e as cópias), confira no catálogo (claro, escuro, um tema colorido, 390px) e faça versão nova do catálogo.
- Regras de uso: sempre com texto ao lado; erro com expressão acolhedora; um animado por vez; parado com movimento reduzido; mínimo 24dp (só o rosto) e 40dp (com corpo); decorativo para leitor de tela (`alt=""`).

## Usar no código (`5_DEV/`)

Regras completas em `1_Requisitos/06_design-system-telas.html` (vista antes do código, sem biblioteca externa de estilização, componentes comuns). O build de cada imagem só enxerga a pasta da aplicação, por isso `pnpm run gerar` também escreve **cópias** versionadas (`scripts/sincronizar.mjs`): `plataformas/web/*` → `5_DEV/frontend/web_<app>/src/design/`; `5_DEV/frontend/web_comum/**` (componentes comuns da web) → `web_<app>/src/comum/`; `plataformas/maui/*` → `5_DEV/frontend/mob_*/Resources/Styles/Design/`. As aplicações importam as cópias; ninguém as edita (o `conferir` acusa). Mudou um componente comum em `web_comum`? Rode `pnpm run gerar`.

- **Web (Vue)**: importar `tokens.css` e `tipografia.css` uma vez; componentes usam `var(--md-comp-…)`/`var(--md-sys-…)`; tema por `<html data-theme="light|dark">` (sem o atributo, segue o sistema); responsividade com as media queries das janelas (`windowMedia`/`windowClassOf` de `tokens.ts`) ou container queries com as mesmas larguras.
- **Mascotes (web)**: `import { animado, estatico } from './design/mascotes.mjs'` e `mascotes-temas.json` (cor por tema); `img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg)`; `estatico` com `prefers-reduced-motion`. No app, os SVG parados de `mascotes/estaticos/`.
- **Mobile (.NET MAUI)**: `Tokens.xaml` e `Marca.xaml` em `App.xaml` (`MergedDictionaries`); animações pela API do MAUI (ver **Marca**); cores com `AppThemeBinding` (`{StaticResource PrimaryLight}`/`PrimaryDark`), textos com os estilos `BodyLarge`, `TitleMedium`…, botões com `ButtonFilled`, `ButtonTonal`…; fontes registradas em `MauiProgram` com os aliases do comentário do arquivo.
- Ao revisar código de tela: valor solto de cor/tamanho/fonte é defeito — troque pelo token. Faltou token? Crie-o aqui primeiro.

## Versionamento

O catálogo é a documentação (o `tema.svg` herda dele a versão e a data): cada mudança no design system (identidade, tokens, componentes, regras) é uma versão nova de `design-system.html`:
- `vNN` + 1 e data/hora (`date '+%Y-%m-%d %H:%M'`) no `<title>`, nos `<meta name="design:versao|data">`, no carimbo (chips Versão e Data) e na linha final do rodapé;
- linha nova no **topo** do histórico (até 10 linhas) dizendo o que mudou;
- `<meta name="design:status">` e o chip de status: `Identidade provisória` até a marca ser definida, depois `Identidade definida`.
Arquivos gerados e o catálogo vão no mesmo commit que a mudança de origem.

## Revisão (antes de entregar)

- `pnpm run conferir` passa (nada desatualizado).
- `xmllint --noout plataformas/maui/*.xaml plataformas/maui/paletas/*.xaml tema.svg`.
- Renderize o `tema.svg` (Chrome headless, em fatias de ~1450px com `<img>` e `margin-top` negativo) e **olhe**: nada vazando dos painéis, textos sem sobreposição, claro e escuro coerentes.
- Sem cor solta fora do bloco gerado: `awk '/design:tokens:inicio/{s=1} /design:tokens:fim/{s=0} !s' design-system.html | grep -nE ':\s*#[0-9a-fA-F]{3,8}\b'` sai vazio.
- Mascotes: `for f in $(find mascotes -name '*.svg'); do xmllint --noout $f; done` e, se mexeu no núcleo, renderize as expressões ou formas tocadas (parado e alguns quadros de `quadros(…)`) e **olhe**.
- Capturas de uma seção do catálogo: esconda as outras (`section:not(#id){display:none}`) em vez de rolar — rolar no headless sai preto.
- Abra o catálogo no Chrome headless em 1440px e num iframe de 390px (o headless não desce de 500px de janela) e **olhe**: claro e escuro, nada cortado, sem rolagem horizontal da página.

## Resposta

Responda curto: o que mudou (identidade, tokens, componentes), a versão nova do catálogo, os arquivos gerados e, se houver, o que falta na identidade (logos, fontes, imagens).
