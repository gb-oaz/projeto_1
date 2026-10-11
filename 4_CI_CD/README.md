# 4_CI_CD

Estruturas de entrega: pipelines de build/teste/deploy, manifests e scripts
de release.

## CI no GitHub Actions

Implementa [`03_ci-qualidade`](../1_Requisitos/03_ci-qualidade.html) (v13).
A publicação das imagens Docker ([`04_publicacao-imagens`](../1_Requisitos/04_publicacao-imagens.html))
ainda não está implementada.

O GitHub só lê workflows de `.github/workflows/` e o Dependabot de `.github/dependabot.yml`;
por isso esses dois arquivos ficam lá, finos, e a lógica fica aqui:

```
.github/
├── workflows/ci.yml                 orquestra os jobs (entrada do GitHub)
└── dependabot.yml                   RNF-21
4_CI_CD/github/
├── scripts/detectar-componentes.sh  decide o que roda (RNF-34, RNF-35, RNF-24)
└── actions/
    ├── dotnet/action.yml            backend, Android e iOS (.NET / .NET MAUI)
    └── web/action.yml               aplicações web (Vue + Vite)
```

### Componentes

Cada pasta é um componente; ela só entra no CI quando já tem projeto
(pastas vazias são ignoradas, então o CI passa enquanto não há código).

| Componente | Código | Testes | Executor | Quando roda |
|---|---|---|---|---|
| Backend | `5_DEV/backend/<servico>/` (`.slnx`, `.sln` ou um `.csproj`) | `5_DEV/qa/backend/<servico>/` | Linux | push e PR, qualquer branch |
| Web | `5_DEV/frontend/web_<app>/` (`package.json`) | `5_DEV/qa/frontend/web_<app>/` | Linux | push e PR, qualquer branch |
| Android | `5_DEV/frontend/mob_and_<app>/` (ou `mob_android_<app>/`) | `5_DEV/qa/frontend/mob_and_<app>/` | Linux | push e PR, qualquer branch |
| iOS | `5_DEV/frontend/mob_ios_<app>/` | `5_DEV/qa/frontend/mob_ios_<app>/` | macOS | só PR para a `master` e push (merge) na `master` |

Um componente roda quando o push/PR altera a pasta dele ou a pasta de testes dele.
Alterar um arquivo solto na raiz de `5_DEV/backend/`, `5_DEV/frontend/` ou das pastas de
`qa/` roda todos os componentes daquela área. Alterar arquivo comum roda **tudo** (RNF-35):
`.github/`, `4_CI_CD/`, `global.json`, `.nvmrc`, `docker-compose*.yml`/`compose*.yml`,
`.editorconfig`, `Directory.Build.*`, `Directory.Packages.props`, `NuGet.config`.

Pastas em `5_DEV/frontend/` fora dos prefixos `web_`, `mob_and`, `mob_ios_` geram aviso e são ignoradas.

### O que cada job verifica

**.NET (backend e mobile)** — SDK do `global.json` (ou 10.0.x enquanto ele não existir):
`dotnet format --verify-no-changes` (RNF-15) → `dotnet build -warnaserror` (RNF-22) →
`dotnet test` com xUnit + coverlet (`--collect:"XPlat Code Coverage"`, RNF-14) →
relatório do ReportGenerator no resumo do job.
- Backend: cobertura de linhas < 80 % reprova (RNF-25); sem projeto de teste também reprova.
  MongoDB 8.0 e Redis 8 sobem como contêineres do job (RNF-37), expostos em
  `ConnectionStrings__MongoDb=mongodb://localhost:27017` e `ConnectionStrings__Redis=localhost:6379`.
- Mobile: cobertura medida e publicada, sem reprovar (RNF-26). O app é compilado só no
  TargetFramework da plataforma (`net10.0-android` / `net10.0-ios`, iOS para o simulador,
  sem assinatura).

**Web** — Node do `.nvmrc` (ou 24), pnpm pelo Corepack (`packageManager` do `package.json`),
`pnpm install --frozen-lockfile` →
`eslint . --max-warnings 0`, `prettier --check .`, `vue-tsc --build` (RNF-17) →
`vitest run` com cobertura e mínimo de 80 % de linhas (RNF-16, RNF-25) →
`pnpm run build` → `pnpm run test:e2e` (Cypress, RNF-18).

Convenções que cada app web precisa seguir: `pnpm-lock.yaml` versionado, `@vitest/coverage-v8`
instalado, a config do Vitest incluindo os testes de `5_DEV/qa/frontend/web_<app>/`, e o script
`test:e2e` (o padrão do `create-vue`: `start-server-and-test preview http://localhost:4173 'cypress run --e2e'`).

Testes e cobertura de cada job ficam como artefato `testes-<componente>` da execução.

### Proteção da `master` (RNF-36, RNF-40)

Em *Settings → Branches* (ou *Rulesets*), exigir apenas o status check **`CI · resultado`**.
Ele falha se qualquer job disparado falhar ou for cancelado; jobs pulados por falta de
mudança não bloqueiam o merge.

### Testar a detecção localmente

```bash
EVENTO=pull_request REF_ALVO=master bash 4_CI_CD/github/scripts/detectar-componentes.sh master HEAD
```
