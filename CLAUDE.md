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
- Nome do diagrama: `<legenda>_v<NN>_<AAAAMMDD-HHMM>.svg`. Cada alteração gera um novo arquivo com a versão seguinte e, na mesma tarefa, a versão anterior é apagada (`git rm`) depois que todas as referências (requisitos `.md` e outros arquivos) forem atualizadas para a nova; o histórico fica no git.
- `./boaz.sh svg <pasta> <legenda>` devolve o próximo nome; `./boaz.sh diagramas` lista a versão mais recente de cada um; `./boaz.sh antigas` lista as versões antigas já sem referência (`--apagar` as remove).

## Agentes

O time é formado por agentes especialistas em `.claude/agents/`. Cada um é dono de uma área e carrega as skills dela; delegue a tarefa ao dono e respeite a passagem de bastão entre eles.

| Agente | Responsável por | Skills |
|---|---|---|
| `deboaz-uxui` | vistas de tela (`vista-<app>-<tela>-web\|pwa\|mobile`) e design system em `0_Assets/design/` | `design-system`, `diagrama-dev`, `gherkin-bdd`, `ux-usabilidade` |
| `deboaz-arc` | requisitos em Gherkin em `1_Requisitos/` (poder total: cenários de tela e de API, status, PDF), diagramas UML em `2_Diagramas/` (menos as vistas), arquitetura em `3_Arquitetura/`, CI/CD em `4_CI_CD/` e `.github/workflows/`; conhece backend, frontend, qa e UX/UI | `requisito`, `requisito-estilo`, `gherkin-bdd`, `engenharia-requisitos`, `diagrama-dev`, `diagrama-arquitetura`, `engenharia-software`, `arquitetura-software`, `devops-cloud`, `desenvolvimento`, `estrategia-qa`, `frontend-vue`, `ux-usabilidade` |
| `deboaz-qa` | testes de integração, funcionais (web e mobile) e carga em `5_DEV/qa/`, a partir do Gherkin | `desenvolvimento`, `gherkin-bdd`, `engenharia-software`, `estrategia-qa` |
| `deboaz-backend` | `5_DEV/backend/`: domínio, casos de uso, API, banco e cache, a partir dos diagramas | `desenvolvimento`, `gherkin-bdd`, `engenharia-software` |
| `deboaz-frontend` | `5_DEV/frontend/` web e mobile, a partir das vistas e do design system | `desenvolvimento`, `design-system`, `gherkin-bdd`, `engenharia-software`, `frontend-vue` |

Skills de conhecimento: `gherkin-bdd` é a base comum (ler requisito em Gherkin dentro do `.md`, achar lacuna, rastrear ID → teste → código, relatar atendidos/pendentes); `engenharia-software` é compartilhada pelos que modelam ou constroem; as especializadas (`engenharia-requisitos`, `arquitetura-software`, `devops-cloud`, `ux-usabilidade`, `frontend-vue`, `estrategia-qa`) guardam o detalhe técnico em `referencias/`, lido sob demanda. Só o `deboaz-arc` escreve requisito; os demais propõem o cenário na resposta.

Ordem de uma funcionalidade: `deboaz-arc` (Gherkin) → `deboaz-uxui` (vistas) → `deboaz-arc` (diagramas e referências no requisito) → `deboaz-qa` (testes vermelhos) → `deboaz-backend` / `deboaz-frontend` (verde). Lacuna ou divergência volta ao dono da etapa anterior em vez de virar decisão silenciosa.

### Contexto isolado

- **Uma tarefa pontual por conversa.** Não tente resolver o projeto inteiro nem várias tarefas na mesma sessão: um documento, um diagrama, um conjunto de testes ou uma fatia de código por vez. Pedido com várias tarefas → faça a primeira e liste as outras para sessões novas.
- **Terminou a tarefa, encerre o chat** (`/clear` ou feche a sessão) e comece a próxima numa sessão limpa. O estado passa pelo disco e pelo git (arquivos, versões, commits), nunca pela memória da conversa; para retomar, use `git log` e `./boaz.sh diagramas`.
- **Cada agente roda num contexto isolado.** Ao delegar, nunca repasse o histórico da sessão nem use `fork`: o pedido leva só a tarefa pontual, os caminhos dos arquivos e o trecho exato necessário (IDs, âncoras, linhas, versão do `.svg`). O agente lê o resto do disco, só o que precisar.
- **Leia só o necessário**: `grep` pelo ID/âncora e leitura por intervalo de linhas; nada de varrer pastas ou ler documentos inteiros "por garantia" (exceto o que a skill manda ler inteiro).
- **Bastão curto**: a resposta de um agente traz caminhos, versões, IDs e resultado, nunca o conteúdo dos arquivos nem o histórico do trabalho.
