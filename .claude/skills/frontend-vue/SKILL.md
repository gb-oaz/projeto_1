---
name: frontend-vue
description: Competência de desenvolvimento frontend do boaz — Vue 3 com TypeScript e Vite (web e PWA, três apps num projeto), arquitetura hexagonal no front (core com domain, usecases, ports e components; adaptadores em apps/<app>/infra), componentização sobre o design system Material Design 3, estado com a reatividade do Vue, integração com a API REST pelo contrato, autenticação e autorização no cliente, testes unitários e de componente com Vitest e Vue Test Utils em TDD, acessibilidade, responsividade e o app .NET MAUI. Use ao implementar, corrigir, revisar ou testar telas, componentes, fluxos e integrações em 5_DEV/frontend/.
---

# Frontend → `5_DEV/frontend/web` (Vue) e `5_DEV/frontend/mobile` (.NET MAUI)

O `deboaz-frontend` **implementa**: segue o Gherkin (`gherkin-bdd`), a vista aprovada do `deboaz-uxui`, o contrato do `deboaz-arc` e o fluxo TDD da skill `desenvolvimento`. Divergência visual → uxui; de contrato → arc; de regra → po. Nunca decide sozinho.

Prevalência: `14_estrutura-projetos` (RN-143 a RNF-198), `06_design-system-telas`, `03_ci-qualidade` (RNF-16, RNF-17, RNF-25), `README.md` da frente → esta skill.

## 1 · Estrutura (web)

```
5_DEV/frontend/web/
├── core/<contexto>/<feature>/      ← sem fetch, sem storage; Vue só em components/ (RN-147)
│   ├── domain/        regras e tipos do domínio (TS puro)
│   ├── usecases/      orquestram o domínio pelas portas (TS puro)
│   ├── ports/         in/ (o que a feature oferece) e out/ (API, armazenamento, relógio)
│   └── components/    componentes Vue da feature
├── core/commons/design/ e components/   tokens gerados e componentes comuns (RNF-109)
├── apps/<app>/infra/in/<contexto>/  telas/rotas que dirigem a feature
├── apps/<app>/infra/out/<contexto>/ adaptadores: cliente HTTP do contrato, storage
├── apps/<app>/main.ts               único ponto de composição (RN-149)
└── tests/core/… e tests/apps/…      Vitest, sem navegador e sem backend (RNF-193)
```

Três apps (`landing_page`, `hub`, `quest`) num projeto Vite com um build por app (`APP=hub pnpm dev`). Contexto importa só `commons` (RN-146).

## 2 · Como implementar uma tela

1. Leia o cenário de tela (ID), a vista `-web`/`-pwa` mais recente (`./boaz.sh diagramas`) e o contrato.
2. Teste vermelho: unitário do caso de uso (`tests/core/...`) e/ou de componente; o funcional do qa (Cypress) é a meta de aceitação.
3. Domínio e caso de uso em TS puro → porta de saída → adaptador HTTP em `apps/<app>/infra/out/` → componente/tela → composição no `main.ts`.
4. Todos os estados do cenário e da vista: vazio, carregando, erro (por código do contrato), sucesso, sem permissão.
5. Textos **idênticos** ao Gherkin.
6. Verde → refatora → lint, formatação, tipos e cobertura.

## 3 · Regras

- **Só tokens do design system** (RNF-96) e **nenhuma biblioteca visual** (RNF-97); nada de cor, fonte, tamanho ou espaçamento soltos. Componente comum novo nasce no design system com vista (RN-29, RNF-100).
- Implementação igual à vista (RNF-105); web e app com a mesma tarefa possível (RN-32).
- Sem dependência nova sem necessidade clara: hoje a web usa só `vue`; roteador, gerenciador de estado ou plugin de PWA são decisão do arquiteto (ADR), não do componente.
- Erro da API tratado pelo **código** do contrato, nunca pela mensagem.
- Nada de regra de negócio no componente: ele chama o caso de uso.

## 4 · Referências (leia só o que a tarefa pedir)

| Assunto | Arquivo |
|---|---|
| Vue 3 + TypeScript: Composition API, componentes, props/emits, slots, composables, estado | `referencias/vue-ts.md` |
| Integração com a API REST, autenticação e autorização no cliente | `referencias/api-auth.md` |
| Testes: Vitest, Vue Test Utils, componentes, cobertura, TDD | `referencias/testes-web.md` |
| PWA técnico (manifest, service worker, atualização) | `referencias/pwa.md` |
| .NET MAUI (mobile): MVVM, estrutura, acessibilidade, testes | `referencias/maui.md` |
| HTML5 semântico, CSS3 e acessibilidade | skill `ux-usabilidade`, `referencias/acessibilidade.md` |
| Docker da web, CI | skill `devops-cloud` |

## 5 · Verificação

Comandos, na pasta `5_DEV/frontend/web` do worktree: `pnpm lint`, `pnpm tipos`, `pnpm test` (cobertura ≥ 80 %). Mobile: build com avisos como erro e testes do `tests/`. **Quem roda** segue a etapa 4 da skill `desenvolvimento` e a orientação do usuário (que pode preferir validar ele mesmo): diga sempre o que rodou e o que ficou para ele.

## Critérios de qualidade

- [ ] Cada tela/componente cita o ID do cenário no teste.
- [ ] Nenhum valor visual solto; nenhum `fetch` em `core/`.
- [ ] Todos os estados do cenário presentes, com o texto exato.
- [ ] Acessível por teclado e leitor de tela; responsivo nas classes de janela da vista.
- [ ] Lint, tipos e cobertura sem erro.

## Skills relacionadas

`desenvolvimento` (fluxo TDD e worktree) · `design-system` (tokens e componentes) · `gherkin-bdd` · `engenharia-software` · `ux-usabilidade` (acessibilidade) · `arquitetura-software` (`referencias/api-rest.md`).
