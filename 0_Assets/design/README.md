# design

Identidade visual e design system do boaz, no padrão do **Material Design 3**.
Abra [`design-system.html`](design-system.html) no navegador: é o catálogo vivo
(regras, cores, tipografia, layout responsivo e componentes de referência).
A skill `design-system` (`.claude/skills/design-system/`) cuida desta pasta.

```
design/
├── identidade/            a marca: marca.json (cores-semente, fontes, ícones) e
│                          logos/ icones/ imagens/ animacoes/ fontes/
├── tokens/
│   ├── fundamentos.json   tipografia, forma, elevação, espaçamento, janelas, movimento, estados
│   ├── componentes.json   tokens dos componentes (md.comp.*)
│   └── cores.json         gerado: paletas e esquemas claro/escuro/alto contraste
├── plataformas/           gerado
│   ├── web/               tokens.css, tipografia.css, tokens.ts   (Vue)
│   └── maui/              Tokens.xaml                             (.NET MAUI)
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

Nunca edite à mão os arquivos gerados. A identidade atual é **provisória**
(semente de referência do M3 e Roboto) até a marca do boaz ser definida.
