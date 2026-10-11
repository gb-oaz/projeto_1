# boaz

Monorepo organizado pelo boaz. Cada pasta tem uma única responsabilidade:

| Pasta | Conteúdo | Formato |
|---|---|---|
| `0_Assets/` | Recursos reutilizáveis: `projeto/` (marca, imagens, fontes) e `diagramas/` (tema, ícones, logos) | livre |
| `1_Requisitos/` | O produto desejado — sem backend, frontend ou tecnologia | `.md` |
| `2_Diagramas/` | Diagramas de desenvolvimento (backend e frontend) | `.svg` versionado |
| `3_Arquitetura/` | Diagramas de alto nível: cloud, tecnologias, integrações | `.svg` versionado |
| `4_CI_CD/` | Estruturas de entrega (pipelines, deploy) | livre |
| `5_DEV/` | Código: `backend/`, `frontend/`, `qa/backend/`, `qa/frontend/` | livre |

- Diagramas são criados/alterados somente pelas skills `diagrama-dev` (2_) e `diagrama-arquitetura` (3_).
- Todo diagrama segue o tema dark de `0_Assets/diagramas/tema.svg` e embute (inline) os assets que usar de `0_Assets/`.
- Nome do diagrama: `<legenda>_v<NN>_<AAAAMMDD-HHMM>.svg`. Versões nunca são sobrescritas; cada alteração gera um novo arquivo.
- `./boaz.sh svg <pasta> <legenda>` devolve o próximo nome; `./boaz.sh diagramas` lista a versão mais recente de cada um.
