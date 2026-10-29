---
name: desenvolvimento
description: Conduz o desenvolvimento de uma funcionalidade ou correção do boaz em fluxo TDD, sempre numa branch própria (tarefa_<NN>-<descricao> ou fix_<NN>-<descricao>) num git worktree isolado — requisito em 1_Requisitos/, depois os cenários de teste em 5_DEV/qa/ (em Gherkin/BDD quando a suíte tiver a ferramenta) e só então o código em 5_DEV/backend/ ou 5_DEV/frontend/, até os testes passarem. Use quando pedirem para desenvolver, implementar, codar, construir ou corrigir uma funcionalidade, uma tela, um endpoint, um caso de uso ou um bug, ou para "começar", "continuar" ou "entregar" uma tarefa.
---

# Desenvolvimento em TDD → requisito · qa · código

Você é a pessoa desenvolvedora do time. Uma tarefa sempre anda nesta ordem, e uma etapa só começa quando a anterior está pronta:

```
0 · branch e worktree ─► 1 · requisito ─► 2 · cenários (vermelho) ─► 3 · código (verde ─► refatora) ─► 4 · verificação ─► 5 · entrega
    tarefa_NN-…              1_Requisitos/     5_DEV/qa/ (+ unitários)     5_DEV/backend · 5_DEV/frontend      ci.sh               PR para a master
```

O teste vem antes do código, e o requisito vem antes do teste: **nenhuma linha de 5_DEV/backend/ ou 5_DEV/frontend/ é escrita sem um cenário vermelho que ela faça passar**, e nenhum cenário existe sem o critério de aceite que ele cobre.

## Regras que esta skill aplica

Leia, se ainda não leu nesta conversa, e siga mesmo quando divergirem deste resumo:
- `1_Requisitos/02_metodologia-git.html` — branch própria a partir da `master` (RN-05), nome `tarefa_<NN>-<descricao>` / `fix_<NN>-<descricao>` (RN-06), entrada na `master` só por PR com squash (RN-07, RNF-42).
- `1_Requisitos/03_ci-qualidade.html` — o que reprova o build: cobertura ≥ 80 % no backend e na web (RNF-25), `dotnet format` (RNF-15), avisos como erro (RNF-22), ESLint/Prettier/vue-tsc (RNF-17); o qa cobre comportamento visível, o detalhe interno fica nos unitários (RN-142).
- `1_Requisitos/14_estrutura-projetos.html` — `core/<contexto>/<feature>/` com `domain/`, `usecases/`, `ports/in/`, `ports/out/`; adaptadores em `infra/in|out/<contexto>/<feature>/`; contextos só importam `commons` (RN-143 a RN-149); qa caixa-preta, testes em `<contexto>/<feature>/` citando o ID (RN-151 a RN-156) e contratos das suítes (RNF-190 a RNF-196).
- `5_DEV/qa/README.md` e o `README.md` da frente que vai mudar.
- Tela nova ou alterada: `1_Requisitos/06_design-system-telas.html` e a skill `design-system`.

## 0 · Branch e worktree (sempre primeiro)

A tarefa vive numa branch própria **dentro de um worktree próprio**, para que a cópia principal, a `master` e as outras tarefas em andamento (suas, de outra sessão ou de outra pessoa) nunca fiquem bloqueadas, sujas ou trocadas de branch.

1. **Número e nome.** `<NN>` é o número do documento de requisito da funcionalidade (`08_hub-ux.html` → `08`); se o requisito ainda não existe, é o próximo número livre que a skill `requisito` vai usar (`ls 1_Requisitos`). `<descricao>` em snake_case, curta, sem acento. Funcionalidade → `tarefa_<NN>-<descricao>`; bug → `fix_<NN>-<descricao>`.
2. **Já existe?** `git worktree list` e `git branch --list 'tarefa_<NN>-*' 'fix_<NN>-*'`. Se a branch da tarefa já existe, continue nela (no worktree dela, ou crie um worktree para ela sem `-b`); não abra uma segunda branch para a mesma tarefa. Na dúvida entre duas branches parecidas, pergunte.
3. **Criar**, a partir da `master` remota atualizada:
   ```bash
   git fetch origin master
   git worktree add .claude/worktrees/<branch> -b <branch> origin/master
   ```
   Se a ferramenta `EnterWorktree` estiver disponível, prefira-a (com o nome da branch), porque ela também leva a sessão para dentro do worktree; depois confira `git rev-parse --abbrev-ref HEAD`.
4. **Trabalhe só dentro do worktree** daí em diante: todo caminho, comando, build e teste roda em `.claude/worktrees/<branch>/`. Nunca rode `git checkout`/`git switch` na cópia principal, nunca commite na `master`, nunca leve para a branch arquivos alheios à tarefa (como os `* 2.*` não rastreados da cópia principal).
5. O worktree não herda dependências: rode `pnpm install` em `5_DEV/frontend/web/` (e em `0_Assets/design/` se gerar tokens) e `dotnet restore` antes do primeiro build.
6. Tarefas em paralelo = um worktree por tarefa. Uma tarefa que depende de outra ainda não mergeada parte da branch dela (`… -b <branch> <branch-da-outra>`), e isso vai escrito no PR.

## 1 · Requisito → `1_Requisitos/`

Use a skill **`requisito`** (dentro do worktree) para criar ou alterar o documento da funcionalidade. Para o TDD, ele precisa ter:
- os requisitos (`RF-…`, `RN-…`, `RNF-…`) que a tarefa vai entregar, com IDs;
- um **critério de aceite em Gherkin** (`<article class="criterio">` + `<pre class="gherkin">`) para cada requisito `Deve` — é dele que sai o cenário de teste;
- os limites mensuráveis (tempo, volume, taxa de erro) se houver teste de carga (RN-155);
- nenhum item em **Em aberto** que mude o comportamento a testar. Se houver, pergunte antes de seguir; não invente a regra no teste nem no código.

Tela nova ou alterada também precisa da vista aprovada em `2_Diagramas/vista-<aplicacao>-<tela>_*.svg` (skill **`diagrama-dev`**); fluxo complexo ganha diagrama de sequência ou de atividades pela mesma skill.

Commit: `git commit -m "Requisito <NN> v<XX>: <o que mudou> (<IDs>)"`.

Correção de bug (`fix_…`): se o comportamento esperado já está no requisito, não crie versão; se o bug revelou uma regra que faltava, inclua-a antes do teste.

## 2 · Cenários de teste → `5_DEV/qa/` (vermelho)

**Escolha da suíte** pelo que o critério observa (RN-152, RNF-190):

| O critério observa… | Suíte |
|---|---|
| regra ou fluxo pela API, com banco e cache reais | `5_DEV/qa/backend/integracao/` |
| fluxo do usuário na tela de um app web | `5_DEV/qa/frontend/web/funcional/` (Cypress, RNF-192) |
| fluxo do usuário num app mobile | `5_DEV/qa/frontend/mobile/funcional/` (Appium em C#/xUnit, RNF-196) |
| tempo de resposta ou erros sob volume | `5_DEV/qa/backend/carga/` (k6 ou outra, RN-155) |
| detalhe interno sem efeito visível para quem usa | **não é qa**: vai para os unitários da frente (passo 3) |

Os testes ficam em `<suite>/<contexto>/<feature>/`, com os nomes do core (`hub/salas/`), e **cada cenário cita o ID** do requisito que cobre. A suíte só fala com o que está rodando: nunca importa código de `5_DEV/backend/` ou `5_DEV/frontend/` (RN-151). Suíte que ainda não existe nasce com o contrato completo (`Dockerfile`, variáveis `API_URL`/`APP_URL`/`APP`, JUnit XML em `/resultados`, código de saída; no mobile, o `executar.sh`).

**BDD com Gherkin — use quando a suíte tiver a ferramenta.** Detecte:
```bash
grep -rlsE 'Reqnroll|SpecFlow|cucumber' <suite> --include='*.csproj' --include='package.json' --include='*.config.*' ; find <suite> -name '*.feature' | head
```
- **Tem** → escreva `<suite>/<contexto>/<feature>/<feature>.feature`, em português (`# language: pt`), com `Funcionalidade:` apontando para o documento (`1_Requisitos/<NN>_<nome>.html`) e um `Cenário:` por critério de aceite, **copiado do `<pre class="gherkin">` do requisito** (desescape `&lt;` `&gt;` `&amp;`) e marcado com a tag do ID (`@RF-17`). Depois, os *step definitions* na mesma pasta, reaproveitando passos existentes antes de criar novos. Se o texto do requisito não der para automatizar como está, ajuste o requisito (nova versão) em vez de deixar o `.feature` divergir.
- **Não tem, mas a suíte está nascendo ou ainda não tem testes** → proponha adotar a ferramenta BDD da linguagem dela (C#/xUnit: Reqnroll com `Reqnroll.xUnit`; Cypress: `@badeball/cypress-cucumber-preprocessor`), na versão estável mais recente e fixada (RN-01, RNF-19). Trocar ou somar ferramenta de uma suíte não muda o CI se o contrato for mantido (RN-153). Pergunte uma vez antes de adotar.
- **Não tem e a suíte já tem testes em outro formato** → siga o formato dela: um teste por cenário, nome com o ID (`RF_17_criar_conta_de_educador_autonomo`, ou `it('RF-17 · …')`), um `Trait`/tag com o ID e o corpo dividido em `// Dado`, `// Quando`, `// Então` com as frases do critério.

Rode a suíte e **confirme o vermelho pelo motivo certo**: o teste falha porque o comportamento não existe (404, elemento ausente, valor errado), não por erro de compilação, de ambiente ou de seletor errado. Um cenário que já passa antes do código não testa nada: revise-o.

Commit: `git commit -m "QA: cenários <IDs> de <contexto>/<feature> (vermelho)"`.

## 3 · Código → `5_DEV/backend/` ou `5_DEV/frontend/` (verde → refatora)

Desenvolva de dentro para fora, cada peça no ciclo curto do TDD — **um teste unitário vermelho, o mínimo de código para ficar verde, refatorar com tudo verde**:

1. **core** (`core/<contexto>/<feature>/`): `domain/` → `ports/in/` e `ports/out/` → `usecases/`, com os unitários espelhados em `tests/` (`tests/core/<contexto>/…`; xUnit no backend e no mobile, Vitest na web). Sem I/O nem framework de tela no core (RN-147); só `commons` como dependência (RN-146).
2. **adaptadores**: `infra/in/` (endpoints, páginas, rotas) chamando só `ports/in/`; `infra/out/` (MongoDB, Redis, clientes HTTP, armazenamento) implementando `ports/out/` (RN-148), com testes de integração entre poucos componentes no projeto.
3. **composição**: ligue portas e adaptadores só em `Program.cs`, `main.ts` do app ou `MauiProgram.cs` (RN-149).
4. **tela**: só os tokens gerados do design system e os componentes de `core/commons/`, seguindo a vista aprovada — nada de cor, fonte ou tamanho solto (skill `design-system`).

Regras do ciclo:
- O código existe para fazer um teste passar; comportamento novo sem teste volta para o passo 2 (qa) ou para um unitário.
- **Não altere um cenário de qa para ele passar.** Se o cenário estiver errado, o erro está no requisito ou no teste: corrija a origem (requisito com nova versão) e diga isso na resposta.
- Mesmo nome de contexto e feature, em snake_case, nas três frentes (RN-145).
- Commits pequenos, cada um com a árvore compilando: `git commit -m "<Frente>: <o que passou a funcionar> (<IDs>)"`.

## 4 · Verificação (tudo verde, dentro do worktree)

Rode o que a frente alterada exige e só siga com tudo passando:
```bash
# backend (5_DEV/backend)
dotnet build -warnaserror && dotnet test --collect:"XPlat Code Coverage" && dotnet format --verify-no-changes
# web (5_DEV/frontend/web)
pnpm lint && pnpm exec prettier --check . && pnpm tipos && pnpm test   # cobertura ≥ 80 % já no script
APP=<app> pnpm build                                                   # cada app que a tarefa tocou
# mobile (5_DEV/frontend/mobile)
dotnet build -warnaserror && dotnet test && dotnet format --verify-no-changes
# tudo, como o GitHub Actions, com o qa contra os contêineres (na raiz do worktree)
./4_CI_CD/local/ci.sh                    # componentes alterados em relação à master
./4_CI_CD/local/ci.sh --qa mobile        # funcional do mobile, se a tarefa mexeu no app
./4_CI_CD/local/ci.sh --qa carga         # se a tarefa trouxe limites de carga
```
Os cenários do passo 2 têm de estar **verdes agora**, a cobertura de linhas ≥ 80 % no backend e na web, e nenhum teste anterior pode ter quebrado. Se o `ci.sh` pular uma plataforma por falta de pré-requisito, diga qual e que a verificação fica para o GitHub Actions.

## 5 · Entrega

- Rode `git status` no worktree: só arquivos da tarefa, nada de segredo (`.env`, chaves) nem artefato de build.
- **Push e PR só quando a pessoa pedir.** Então: `git push -u origin <branch>` e `gh pr create --base master` com título no padrão do repositório (`<Frente>: <resumo>`, ex.: `Backend: criação de salas no hub`) e corpo com o requisito e a versão, os IDs entregues, os cenários (suíte e arquivo), como verificar e o resultado do `ci.sh`. O merge é por squash, com o job agregador aprovado e 1 revisão (RNF-40 a RNF-42).
- Requisito entregue que estava `Rascunho`/`Em revisão`: pergunte se deve ir a `Aprovado` (skill `requisito`) no mesmo PR.
- Depois do merge: `git worktree remove .claude/worktrees/<branch>` e `git branch -d <branch>` — só com a pessoa confirmando que o PR foi mergeado.

## Retomar uma tarefa

"Continua a tarefa 12", "volta no hub de salas": ache o worktree e a branch (`git worktree list`), entre nele, leia `git log --oneline origin/master..HEAD` e descubra a etapa pela última mensagem de commit (`Requisito` → etapa 2; `QA … (vermelho)` → etapa 3; código → etapa 3 ou 4). Rode os testes para ver o que está vermelho e continue dali; se a `master` andou, `git rebase origin/master` antes de seguir (se houver conflito, resolva e conte o que mudou).

## Resposta

Curta, no fim de cada etapa ou da tarefa: branch e caminho do worktree; etapa em que a tarefa está; requisito e versão; cenários criados (arquivo e IDs) e se usou Gherkin; o que foi implementado por frente; resultado dos testes (vermelho/verde, cobertura) e do `ci.sh`; o que falta ou depende de alguém (pendências do requisito, aprovação da vista, PR).
