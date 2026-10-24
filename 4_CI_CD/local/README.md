# CI local

Repete na sua máquina o CI do GitHub Actions
([`.github/workflows/ci.yml`](../../.github/workflows/ci.yml)) antes do push e,
depois, sobe as aplicações em contêiner e roda o qa contra elas. Se passar aqui,
a mesma mudança deve passar no GitHub Actions. Requisito:
[`03_ci-qualidade`](../../1_Requisitos/03_ci-qualidade.html) (RNF-160 a RNF-166,
RN-140 a RN-142).

```bash
./4_CI_CD/local/ci.sh              # componentes alterados em relação à master
./4_CI_CD/local/ci.sh --tudo       # todos os componentes
./4_CI_CD/local/ci.sh --qa backend # qa só do backend (testes de API)
./4_CI_CD/local/ci.sh --qa frontend # qa só do frontend (Cypress)
./4_CI_CD/local/ci.sh --qa todos   # os dois em conjunto (padrão)
./4_CI_CD/local/ci.sh --sem-qa     # só a etapa 1 (verificação)
./4_CI_CD/local/ci.sh --manter     # deixa as aplicações no ar ao terminar
```

Backend e web precisam só de Docker com Compose v2, git e bash: SDK .NET,
Node.js, MongoDB e Redis rodam em contêiner, nas versões de `global.json`,
`.nvmrc` e do requisito 01. Android e iOS rodam direto na máquina, fora do
Docker, e precisam do SDK .NET de `global.json` e dos workloads que ele fixa
em `workloadVersion` (`sudo dotnet workload install maui`, rodado na raiz do
repositório), mais:

- **Android**: JDK 17 (o `JAVA_HOME`, se for o 17, ou o 17 registrado no
  macOS) e o SDK do Android (`ANDROID_HOME`, ou o local padrão do Android
  Studio, `~/Library/Android/sdk`);
- **iOS**: macOS com o Xcode de `.xcode-version`, selecionado com
  `sudo xcode-select -s`, e o runtime do simulador iOS desse Xcode
  (`xcodebuild -downloadPlatform iOS`). O workload iOS fixado no `global.json`
  compila contra o SDK iOS desse Xcode; como não há workload publicado com o
  mesmo número do Xcode, a checagem de versão do .NET fica desligada em
  [`5_DEV/frontend/Directory.Build.props`](../../5_DEV/frontend/Directory.Build.props).

Faltando algum deles, o app é pulado com aviso dizendo o que falta.

## Etapas

| Etapa | O que faz | Igual ao GitHub Actions |
|---|---|---|
| mudancas | backend, web, Android e iOS alterados desde a master, incluindo o que ainda não foi commitado; arquivo comum (`.github/`, `global.json`, `.nvmrc`, compose, esta pasta) liga todos | job `mudancas` |
| 1 · verificação | `backend-ci` por serviço: format, build sem avisos, testes com MongoDB e Redis, cobertura ≥ 80 %. `web-ci` por app: install, ESLint, Prettier, vue-tsc, Vitest com cobertura ≥ 80 %, build, Cypress. Android e iOS por app, na máquina: format, build sem avisos, testes, cobertura medida sem mínimo | jobs `backend`, `web`, `android` e `ios` |
| 2 · aplicações | sobe `backend` e `frontend-<app>` a partir dos Dockerfiles das imagens, com MongoDB e Redis, e espera `/health` responder 200 em cada um | — (o GitHub só gera a imagem para publicar) |
| 3 · qa | conforme `--qa`: **backend**, os testes de API de `5_DEV/qa/backend/api/` contra o contêiner do backend; **frontend**, por app web, `pnpm run test:e2e:imagem` (os testes de `5_DEV/qa/frontend/web_<app>/`) contra o contêiner do app, com o backend no ar; **todos**, os dois sobre a mesma pilha | — |
| resultado | resumo por componente e um veredito único (código de saída 1 se algo falhou) | job `resultado` |

As etapas 2 e 3 só rodam com a etapa 1 verde e só para backend e web (os apps
mobile não sobem em contêiner).

**Android e iOS** compilam numa cópia temporária, fora do Docker. O iOS roda
sempre que o app muda (no GitHub Actions, só em pull request para a master, por
custo do executor macOS).

A cobertura do mobile fica em `.gerado/cobertura/<app>/`, como o artefato do
GitHub Actions.

## O que o CI local não faz

- **Não publica imagem.** As imagens ficam só no seu Docker, com a tag
  `boaz-ci-local/<imagem>:ci`; nenhum serviço faz login em registro. Publicar
  continua exclusivo do GitHub Actions, com aprovação (requisito 04).
- **Não altera a sua cópia de trabalho.** O repositório entra nos contêineres
  só para leitura (`/fonte`) e é copiado para dentro deles, sem `node_modules`,
  `bin` e `obj` da sua máquina; Android e iOS compilam numa pasta temporária.
- **Não substitui o GitHub Actions.** O merge continua liberado pelo job
  `CI · resultado` de lá.

## Convenções que ele usa

As mesmas do `ci.yml` (cabeçalho do arquivo), mais:

- as imagens escutam na porta **8080** e respondem **200 em `/health`** (as
  mesmas da homologação e da produção, `terraform/infra/aplicacoes.tf`);
- o qa do backend é um projeto xUnit em **`5_DEV/qa/backend/api/`**, fora das
  solutions dos serviços (por isso não roda na etapa 1), que chama o backend
  em `API_URL`;
- cada app web com testes contra a imagem tem o script **`test:e2e:imagem`** no
  `package.json`: roda o Cypress em `CYPRESS_baseUrl` (o contêiner do app) sem
  subir servidor de desenvolvimento; o backend fica em `CYPRESS_API_URL`.

## Arquivos

```
4_CI_CD/local/
├── ci.sh                     ponto de partida
├── compose.yml               MongoDB, Redis, backend-ci, web-ci, saude, qa-backend, qa-web
├── imagens/web-ci.Dockerfile Node.js do .nvmrc + bibliotecas do Cypress
├── scripts/                  o que roda em cada etapa (verificar-mobile.sh, na máquina)
└── .gerado/                  compose das aplicações e cobertura do mobile (fora do git)
```

Caches (pacotes NuGet, store do pnpm e binário do Cypress) ficam nos volumes
`boaz-ci-local_*`; para limpar: `docker volume rm boaz-ci-local_nuget boaz-ci-local_pnpm boaz-ci-local_cypress`.
