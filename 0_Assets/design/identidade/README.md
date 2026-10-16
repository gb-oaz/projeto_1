# identidade

A marca do boaz. `marca.json` define cores-semente, fontes e ícones; as pastas
guardam os arquivos visuais. Formatos e nomes esperados:

| Pasta | O que entra | Formato e nome |
|---|---|---|
| `logos/` | logo principal, negativo, monocromático, símbolo | `.svg` · `logo-principal.svg`, `logo-principal-negativo.svg`, `logo-mono.svg`, `simbolo.svg` |
| `icones/` | ícone do app (1024×1024, área segura de 66%) e favicon | `app-icone.svg`, `app-icone-fundo.svg`, `favicon.svg` |
| `imagens/` | ilustrações, estados vazios, onboarding | `.svg`; fotos em `.webp` (≤ 200 KB) |
| `animacoes/` | carregamento, sucesso, splash | Lottie `.json` (preferido), `.webp` animado ou `.gif` (≤ 500 KB) |
| `fontes/` | arquivos da fonte + licença | `<familia>-<peso>.ttf` e `LICENSE` |

Nomes em kebab-case, sem acentos. O repositório é público: só arquivos que
podem ser redistribuídos.
