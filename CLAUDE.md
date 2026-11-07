# boaz

Monorepo organizado pelo boaz. Cada pasta tem uma única responsabilidade:

| Pasta | Conteúdo | Formato |
|---|---|---|
| `0_Assets/` | Recursos reutilizáveis: `design/` (identidade visual e design system), `projeto/` (mockups e referências), `requisitos/` (tema.md, requisito.mjs, casca.html) e `diagramas/` (tema, ícones, logos) | livre |
| `1_Requisitos/` | O produto desejado em cenários Gherkin, com as telas e os diagramas | `.md` versionado (+ `pdf/`) |
| `2_Diagramas/` | Diagramas de desenvolvimento (backend e frontend) | `.svg` versionado |
| `3_Arquitetura/` | Diagramas de alto nível: cloud, tecnologias, integrações | `.svg` versionado |
| `4_CI_CD/` | Estruturas de entrega (pipelines, deploy) | livre |
| `5_DEV/` | Código: `backend/`, `frontend/`, `qa/backend/`, `qa/frontend/` | livre |

- Requisitos têm **só cenários em Gherkin, telas e diagramas** (nada de parágrafos nem tabelas de requisitos): cada regra `RF`/`RN`/`RNF` é um cenário com o ID como âncora; com contrato de API, os cenários se dividem em **Cenários de tela** (seção `cenarios-tela`) e **Cenários de API** (seção `cenarios-api`, endereços e códigos do contrato, âncora `<ID>-api` quando o ID está nas duas; base do `qa/backend/`); a ordem de trabalho é requisito em Gherkin → telas (UX/UI) → diagramas. Um módulo segue sempre esta ordem: Cenários de tela → Cenários de API → Telas (Web, PWA e Mobile, uma vista por plataforma: `vista-<app>-<tela>-web|pwa|mobile`) → Diagramas (casos de uso, sequência com as telas, contrato, classes, documentos).
- Requisitos são documentos `.md`, criados/alterados pela skill `requisito` no padrão de `0_Assets/requisitos/tema.md` (front matter e carimbo de topo com projeto, documento, versão, data/hora e status; histórico no rodapé; telas e diagramas como imagens `.svg` versionadas). A skill `requisito-estilo` cuida do formato, do HTML/PDF gerado por `0_Assets/requisitos/requisito.mjs` com a `casca.html` (temas claro/sistema/escuro, paleta das vistas), da migração de `.html` antigos e do PDF; `node 0_Assets/requisitos/requisito.mjs conferir <doc.md>` confere marca d'água, links e imagens. `./boaz.sh requisitos` lista os documentos; `./boaz.sh pdf <documento>` exporta o PDF versionado para `1_Requisitos/pdf/`.
- A identidade visual e o design system (Material Design 3: tokens de cor, tipografia, forma, layout responsivo, componentes; logos, ícones, imagens e animações da marca; mascotes — personagens com rosto expressivo gerados por `scripts/mascotes.mjs`) ficam só em `0_Assets/design/`, cuidados pela skill `design-system`. Telas e componentes de `5_DEV/` usam apenas os tokens gerados, que `pnpm run gerar` copia para `core/commons/` de cada frente (`design/` na web, `Resources/Styles/Design/` no mobile) — nada de cor, fonte ou tamanho solto, nem biblioteca externa de estilização. Toda tela começa por uma vista aprovada em PR (`2_Diagramas/vista-<aplicacao>-<tela>_*.svg`, skill `diagrama-dev`). Regras completas em `1_Requisitos/06_design-system-telas.md`.
- Desenvolvimento (funcionalidade ou bug) segue a skill `desenvolvimento`, em TDD: requisito em `1_Requisitos/` → cenários vermelhos em `5_DEV/qa/` (Gherkin quando a suíte tiver BDD) → código em `5_DEV/backend/` ou `5_DEV/frontend/` até ficar verde, sempre na branch `tarefa_<NN>-…`/`fix_<NN>-…` num worktree próprio em `.claude/worktrees/`, sem trocar a branch da cópia principal.
- Diagramas são criados/alterados somente pelas skills `diagrama-dev` (2_) e `diagrama-arquitetura` (3_).
- Todo diagrama segue o tema dark de `0_Assets/diagramas/tema.svg` e embute (inline) os assets que usar de `0_Assets/`.
- Nome do diagrama: `<legenda>_v<NN>_<AAAAMMDD-HHMM>.svg`. Versões nunca são sobrescritas; cada alteração gera um novo arquivo.
- `./boaz.sh svg <pasta> <legenda>` devolve o próximo nome; `./boaz.sh diagramas` lista a versão mais recente de cada um.

## Agentes

O time é formado por agentes especialistas em `.claude/agents/`. Cada um é dono de uma área e carrega as skills dela; delegue a tarefa ao dono e respeite a passagem de bastão entre eles.

| Agente | Responsável por | Skills |
|---|---|---|
| `deboaz-po` | requisitos em Gherkin em `1_Requisitos/` (cenários de tela e de API, PDF) | `requisito`, `requisito-estilo` |
| `deboaz-uxui` | vistas de tela (`vista-<app>-<tela>-web\|pwa\|mobile`) e design system em `0_Assets/design/` | `design-system`, `diagrama-dev` |
| `deboaz-arc` | diagramas UML em `2_Diagramas/` (menos as vistas), arquitetura em `3_Arquitetura/`, CI/CD em `4_CI_CD/` e `.github/workflows/` | `diagrama-dev`, `diagrama-arquitetura` |
| `deboaz-qa` | testes de integração, funcionais (web e mobile) e carga em `5_DEV/qa/`, a partir do Gherkin | `desenvolvimento` |
| `deboaz-backend` | `5_DEV/backend/`: domínio, casos de uso, API, banco e cache, a partir dos diagramas | `desenvolvimento` |
| `deboaz-frontend` | `5_DEV/frontend/` web e mobile, a partir das vistas e do design system | `desenvolvimento`, `design-system` |

Ordem de uma funcionalidade: `deboaz-po` (Gherkin) → `deboaz-uxui` (vistas) → `deboaz-arc` (diagramas) → `deboaz-qa` (testes vermelhos) → `deboaz-backend` / `deboaz-frontend` (verde). Lacuna ou divergência volta ao dono da etapa anterior em vez de virar decisão silenciosa.
