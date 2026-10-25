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
./4_CI_CD/local/ci.sh --qa backend # só a suíte de integração do backend
./4_CI_CD/local/ci.sh --qa frontend # só a suíte funcional da web (Cypress)
./4_CI_CD/local/ci.sh --qa todos   # as duas em conjunto (padrão)
./4_CI_CD/local/ci.sh --qa carga   # só a suíte de carga do backend (sob demanda)
./4_CI_CD/local/ci.sh --qa mobile  # só o funcional do mobile (Appium, sob demanda)
./4_CI_CD/local/ci.sh --sem-qa     # só a etapa 1 (verificação)
./4_CI_CD/local/ci.sh --so-qa      # só as etapas 2 e 3 (é o que o job qa do GitHub roda)
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

O funcional do mobile (`--qa mobile`) pede ainda o **Node.js** do `.nvmrc` e o
**emulador Android** (`adb devices`) ou o **simulador iOS**
(`xcrun simctl boot <aparelho>`) já ligado. O Appium e os drivers não precisam
estar instalados: o `qa-mobile.sh` instala os do `package.json` da suíte, nas
versões fixadas lá, e sobe esse Appium na porta 4724.

Faltando algum deles, a plataforma é pulada com aviso dizendo o que falta.

## Etapas

| Etapa | O que faz | Igual ao GitHub Actions |
|---|---|---|
| mudancas | backend, web, Android e iOS alterados desde a master, incluindo o que ainda não foi commitado; arquivo comum (`.github/`, `global.json`, `.xcode-version`, `.nvmrc`, compose, esta pasta) liga todos | job `mudancas` |
| 1 · verificação | `backend-ci` na solution de `5_DEV/backend/`: format, build sem avisos, testes com MongoDB e Redis, cobertura ≥ 80 %. `web-ci` no projeto de `5_DEV/frontend/web/`: install, ESLint, Prettier, vue-tsc, Vitest com cobertura ≥ 80 % e o build de cada app (`APP`). Na máquina, a solution de `5_DEV/frontend/mobile/`: Android com format, build sem avisos, testes e cobertura medida sem mínimo; iOS com format e build do `net10.0-ios` de cada app | jobs `backend`, `web`, `android` e `ios` |
| 2 · aplicações | sobe `backend` e `frontend-<app>` a partir dos Dockerfiles das imagens, com MongoDB e Redis, e espera `/health` responder 200 em cada um | — (o GitHub só gera a imagem para publicar) |
| 3 · qa | cada suíte de `5_DEV/qa/<alvo>/<tipo>/` vira imagem pelo `Dockerfile` dela e roda na rede das aplicações; conforme `--qa`: **backend**, `backend/integracao/` com `API_URL`; **frontend**, `frontend/web/funcional/` uma vez por app, com `APP` e `APP_URL`; **todos**, as duas sobre a mesma pilha; **carga**, `backend/carga/`; **mobile**, `frontend/mobile/funcional/` na máquina, pelo `executar.sh` com Appium, uma vez por app e plataforma, com o backend publicado na porta 18080. O JUnit XML de cada uma fica em `.gerado/resultados/` | jobs `qa` (sem carga e mobile) e `qa-mobile` (Android, só PR e merge na master) |
| resultado | resumo por componente e um veredito único (código de saída 1 se algo falhou) | job `resultado` |

As etapas 2 e 3 só rodam com a etapa 1 verde e só para backend e web (os apps
mobile não sobem em contêiner).

**Android e iOS** compilam numa cópia temporária, fora do Docker. O iOS roda
sempre que o mobile muda (no GitHub Actions, só em pull request para a master, por
custo do executor macOS).

A cobertura do mobile fica em `.gerado/cobertura/mobile/`, como o artefato do
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
- cada suíte de qa segue o contrato de
  [`14_estrutura-projetos`](../../1_Requisitos/14_estrutura-projetos.html)
  (RNF-191): `Dockerfile` na raiz da pasta da suíte, alvo em `API_URL`,
  `APP_URL` e `APP`, JUnit XML em `/resultados` e código de saída 0 só se
  tudo passou; suíte sem `Dockerfile` é pulada com aviso; o funcional do
  mobile segue o contrato de `executar.sh` (RNF-196), chamado por
  `scripts/qa-mobile.sh`;
- a verificação Android define `MobileSemIos=true`, que tira o `net10.0-ios`
  dos projetos no macOS (`5_DEV/frontend/Directory.Build.props`), como no
  Linux do GitHub Actions.

## Arquivos

```
4_CI_CD/local/
├── ci.sh                     ponto de partida
├── compose.yml               MongoDB, Redis, backend-ci, web-ci e saude (as suítes de qa trazem a própria imagem)
├── imagens/web-ci.Dockerfile Node.js do .nvmrc
├── scripts/                  o que roda em cada etapa (verificar-mobile.sh e qa-mobile.sh, na máquina)
└── .gerado/                  compose das aplicações, cobertura do mobile e resultados do qa (fora do git)
```

Caches (pacotes NuGet e store do pnpm) ficam nos volumes `boaz-ci-local_*`;
para limpar: `docker volume rm boaz-ci-local_nuget boaz-ci-local_pnpm`.
