# boaz

Monorepo organizado pelo boaz. Cada pasta tem uma única responsabilidade:

| Pasta | Conteúdo | Formato |
|---|---|---|
| `0_Assets/` | Recursos reutilizáveis: `design/` (identidade visual e design system), `projeto/` (mockups e referências), `requisitos/` (tema.html) e `diagramas/` (tema, ícones, logos) | livre |
| `1_Requisitos/` | O produto desejado — sem backend, frontend ou tecnologia | `.html` versionado (+ `pdf/`) |
| `2_Diagramas/` | Diagramas de desenvolvimento (backend e frontend) | `.svg` versionado |
| `3_Arquitetura/` | Diagramas de alto nível: cloud, tecnologias, integrações | `.svg` versionado |
| `4_CI_CD/` | Estruturas de entrega (pipelines, deploy) | livre |
| `5_DEV/` | Código: `backend/`, `frontend/`, `qa/backend/`, `qa/frontend/` | livre |

- Requisitos são documentos `.html` autocontidos, criados/alterados pela skill `requisito` no padrão de `0_Assets/requisitos/tema.html` (marca d'água de topo com projeto, documento, versão, data/hora e status; histórico no rodapé; temas claro/sistema/escuro; botão de PDF). A skill `requisito-estilo` cuida do visual, da migração de `.md` antigos e do PDF. `./boaz.sh requisitos` lista os documentos; `./boaz.sh pdf <documento>` exporta o PDF versionado para `1_Requisitos/pdf/`.
- A identidade visual e o design system (Material Design 3: tokens de cor, tipografia, forma, layout responsivo, componentes; logos, ícones, imagens e animações da marca; mascotes — personagens com rosto expressivo gerados por `scripts/mascotes.mjs`) ficam só em `0_Assets/design/`, cuidados pela skill `design-system`. Telas e componentes de `5_DEV/` usam apenas os tokens gerados, que `pnpm run gerar` copia para `core/commons/` de cada frente (`design/` na web, `Resources/Styles/Design/` no mobile) — nada de cor, fonte ou tamanho solto, nem biblioteca externa de estilização. Toda tela começa por uma vista aprovada em PR (`2_Diagramas/vista-<aplicacao>-<tela>_*.svg`, skill `diagrama-dev`). Regras completas em `1_Requisitos/06_design-system-telas.html`.
- Desenvolvimento (funcionalidade ou bug) segue a skill `desenvolvimento`, em TDD: requisito em `1_Requisitos/` → cenários vermelhos em `5_DEV/qa/` (Gherkin quando a suíte tiver BDD) → código em `5_DEV/backend/` ou `5_DEV/frontend/` até ficar verde, sempre na branch `tarefa_<NN>-…`/`fix_<NN>-…` num worktree próprio em `.claude/worktrees/`, sem trocar a branch da cópia principal.
- Diagramas são criados/alterados somente pelas skills `diagrama-dev` (2_) e `diagrama-arquitetura` (3_).
- Todo diagrama segue o tema dark de `0_Assets/diagramas/tema.svg` e embute (inline) os assets que usar de `0_Assets/`.
- Nome do diagrama: `<legenda>_v<NN>_<AAAAMMDD-HHMM>.svg`. Versões nunca são sobrescritas; cada alteração gera um novo arquivo.
- `./boaz.sh svg <pasta> <legenda>` devolve o próximo nome; `./boaz.sh diagramas` lista a versão mais recente de cada um.
