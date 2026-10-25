# qa

Testes que olham o produto de fora, pelo que está rodando: a API do backend,
as aplicações web e os apps. Nenhuma suíte importa código de `5_DEV/backend/`
ou de `5_DEV/frontend/`. Regras completas em
[`14_estrutura-projetos`](../../1_Requisitos/14_estrutura-projetos.html)
(RN-151 a RN-156, RNF-190 a RNF-192 e RNF-196).

```
5_DEV/qa/
├── backend/
│   ├── integracao/         fluxos e regras pela API, com banco e cache reais
│   └── carga/              volume e tempo de resposta (sob demanda)
└── frontend/
    ├── web/funcional/      ponta a ponta pela tela, um app por vez (Cypress)
    └── mobile/funcional/   ponta a ponta no app (Appium, testes em C#/xUnit)
```

Dentro de cada suíte, os testes ficam em `<contexto>/<feature>/`, com os nomes
do core (`hub/salas/`), e cada teste cita o ID do requisito que cobre.

## Contrato de uma suíte em contêiner

A ferramenta é livre (xUnit, Newman, Cypress, k6…); o CI só conhece isto:

- um `Dockerfile` na raiz da pasta da suíte, que é todo o contexto do build e
  traz a ferramenta;
- a imagem roda todos os testes da suíte ao iniciar;
- o alvo vem das variáveis `API_URL` (backend), `APP_URL` e `APP` (app web,
  `landing_page`, `hub` ou `quest`; a suíte roda as pastas do contexto do app
  e de `commons/`);
- o resultado vai em JUnit XML para `/resultados`;
- código de saída 0 se tudo passou, diferente de 0 se algo falhou.

## Contrato do funcional do mobile

O Appium precisa do emulador e do simulador, então a suíte roda na máquina:
um `executar.sh` na raiz de `frontend/mobile/funcional/`, chamado uma vez por
app e plataforma com

- `PLATAFORMA` (`android` ou `ios`) e `APP` (`hub` ou `quest`);
- `APP_PACOTE`: o `.apk` (Debug) ou o `.app` (simulador) já compilado;
- `APPIUM_URL`: o servidor Appium no ar;
- `API_URL` (o backend visto da máquina) e `API_URL_DISPOSITIVO` (o mesmo
  backend visto do aparelho: `10.0.2.2` no emulador Android);
- `RESULTADOS`: a pasta do JUnit XML;

e termina com código 0 só se tudo passou.

O Appium e os drivers (UiAutomator2 e XCUITest) têm versão exata no
`package.json` da pasta; o CI os instala com `npm ci` e sobe esse Appium. O
pacote já vem compilado com o endereço do backend em `ApiUrl` (RNF-198). Como
o pacote muda a cada execução com a mesma versão, a suíte abre a sessão com
`appium:enforceAppInstall` ligado, para o app ser reinstalado.

## Onde roda

| Suíte | Pull request (job `qa` e `ci.sh`) | Sob demanda |
|---|---|---|
| `backend/integracao/` | sim | `ci.sh --qa backend` |
| `frontend/web/funcional/` | sim, uma vez por app | `ci.sh --qa frontend` |
| `backend/carga/` | não | `ci.sh --qa carga` (contêiner) e workflow `QA · carga` (homologação) |
| `frontend/mobile/funcional/` | só PR e merge na master, Android em emulador (job `qa-mobile`) | `ci.sh --qa mobile` (Android e iOS) |

Suíte sem `Dockerfile` (ou, no mobile, sem `executar.sh`) é pulada com aviso.
