# web

Um único projeto Vite com três apps, `landing_page`, `hub` e `quest`, na
estrutura hexagonal de
[`14_estrutura-projetos`](../../../1_Requisitos/14_estrutura-projetos.html)
(RN-143 a RN-149, RNF-179 a RNF-182, RNF-193).

```
5_DEV/frontend/web/
├── package.json · pnpm-lock.yaml
├── core/                         domínio, casos de uso, portas e componentes, sem I/O
│   ├── commons/                  o que os contextos compartilham
│   │   ├── components/           componentes comuns (únicos para os três apps)
│   │   └── design/               cópia gerada do design system (não edite)
│   ├── landing_page/ ─┐
│   ├── hub/           ├─ importam só core/commons/ (o lint reprova o resto)
│   └── quest/        ─┘
│       └── <feature>/            components/ · domain/ · usecases/ · ports/in/ · ports/out/
├── apps/<app>/                   um app por pasta, imagem frontend-<app>
│   ├── index.html · main.ts      main.ts é o ponto de composição
│   ├── vite.config.ts            configDoApp('<app>') de vite.base.ts
│   └── infra/
│       ├── in/<contexto>/        páginas e rotas (chamam ports/in)
│       └── out/<contexto>/       clientes HTTP e armazenamento do navegador
├── tests/                        Vitest, sem navegador e sem backend
└── Dockerfile                    imagem frontend-<app> (APP), porta 8080, /health
```

O core é importado pelo alias `@core`. As features entram com o módulo que as
cria, em `snake_case`, com o mesmo nome nas três frentes. As cópias de
`core/commons/design/` vêm de `pnpm run gerar` em `0_Assets/design/`.

```bash
corepack enable           # pnpm do packageManager
pnpm install              # na pasta 5_DEV/frontend/web
APP=hub pnpm run dev      # http://localhost:5173
pnpm run lint && pnpm run tipos && pnpm test
APP=hub pnpm run build    # dist/hub/
docker build --build-arg APP=hub -t frontend-hub .
```
