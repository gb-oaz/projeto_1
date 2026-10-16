# design

Identidade visual e design system do boaz, no padrão do **Material Design 3**.
Abra [`design-system.html`](design-system.html) no navegador: é o catálogo vivo
(regras, cores, tipografia, layout responsivo e componentes de referência).
A skill `design-system` (`.claude/skills/design-system/`) cuida desta pasta.

```
design/
├── identidade/            a marca: marca.json (temas de cor, fontes, ícones) e
│                          logos/ icones/ imagens/ animacoes/ fontes/
├── tokens/
│   ├── fundamentos.json   tipografia, forma, elevação, espaçamento, janelas, movimento, estados
│   ├── componentes.json   tokens dos componentes (md.comp.*)
│   └── cores.json         gerado: paletas e esquemas claro/escuro/alto contraste de cada tema
├── plataformas/           gerado
│   ├── web/               tokens.css, tipografia.css, tokens.ts   (Vue)
│   └── maui/              Tokens.xaml + paletas/Paleta<Tema>.xaml (.NET MAUI)
├── design-system.html     catálogo (o bloco design:tokens é gerado)
├── tema.svg               gerado: quadro visual com cores, tipos, layout e componentes
└── scripts/               gerar.mjs (gerador) e tema-svg.mjs (desenho do tema.svg)
```

Para mudar a identidade ou um token, edite `identidade/marca.json` ou
`tokens/*.json` e gere de novo (Node 24):

```bash
cd 0_Assets/design
corepack enable && pnpm install
pnpm run gerar      # escreve cores.json, plataformas/*, tema.svg e o bloco do catálogo
pnpm run conferir   # falha se algum arquivo gerado estiver desatualizado
```

## Temas de cor

`identidade/marca.json` → `cores.temas` define as combinações de cor (hoje:
roxo (padrão), oceano, floresta, aurora, grafite, magenta, sol, girassol,
rubi, cereja, branco e preto), cada uma com
semente, terciária, harmonia e variante do M3; `cores.tema_padrao` escolhe o
padrão. Todas geram os mesmos papéis (`primary`, `surface`…), então trocar de
tema não muda componente nenhum:

- web: `<html data-paleta="oceano">` (sem o atributo, vale o padrão); num
  trecho, `class="md-paleta-oceano"` (+ `md-scheme-dark`);
- MAUI: `Tokens.xaml` traz o padrão; inclua `paletas/PaletaOceano.xaml` depois
  dele para usar outro.

Nunca edite à mão os arquivos gerados. A identidade atual é **provisória**
(semente de referência do M3 e Roboto) até a marca do boaz ser definida.
