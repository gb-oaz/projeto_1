# identidade

A marca do deboaz. O desenho do símbolo e do logo está em `scripts/marca.mjs`: para mudar, edite o núcleo e rode `pnpm run gerar`. `marca.json` define cores-semente, fontes e ícones; as pastas
guardam os arquivos visuais. Formatos e nomes esperados:

| Pasta | O que entra | Formato e nome |
|---|---|---|
| `logos/` | **gerado** por `scripts/marca-arquivos.mjs`: símbolo e logo principal, hastes longas (landing page e hub) e curtas (`-curto`, quest), com `-negativo` | `simbolo.svg`, `logo-principal.svg` e variações |
| `icones/` | **gerado**: ícone do app (1024×1024, área segura de 66%), camadas do adaptativo e favicon | `app-icone[-curto].svg`, `-frente`, `app-icone-fundo.svg`, `favicon[-curto].svg` |
| `imagens/` | **gerado** em `imagens/fundos/`: os seis fundos oficiais (`scripts/fundos-arquivos.mjs`); outras ilustrações em `.svg`, fotos em `.webp` (≤ 200 KB) | `fundos/<conceito>-<horizontal\|vertical\|quadrado>.svg` |
| `animacoes/` | **gerado**: abertura, fechamento, ciclo (abertura + fechamento em laço) e carregamento da marca, em SVG animado (toca também em `<img>`) | `<abertura\|fechamento\|ciclo\|carregamento>[-curto][-negativo].svg` |
| `fontes/` | arquivos da fonte + licença | `<familia>-<peso>.ttf` e `LICENSE` |

Nomes em kebab-case, sem acentos. O repositório é público: só arquivos que
podem ser redistribuídos.
