# Docker e Docker Compose

Requisitos: `01_stack` RNF-09, RNF-10, RNF-167, RNF-168, RNF-171; `03` RNF-162; `14` RNF-191 (contrato das suítes do qa).

## Dockerfile

- **Multi-stage**: estágio de build com o SDK (imagem de build de `RNF-168`), estágio final só com o runtime.
- Imagem base com versão fixada; usuário não root no estágio final.
- Copie primeiro os arquivos de dependência (`*.csproj`, `package.json` + `pnpm-lock.yaml`) e restaure, depois o código: aproveita o cache de camadas.
- `.dockerignore` com `node_modules`, `bin`, `obj`, `coverage`, `dist`.
- Porta e rota de saúde conforme RNF-171; `HEALTHCHECK` ou saúde no compose.
- Nada de segredo em `ARG`/`ENV` da imagem.

## Suítes do qa (RNF-191)

`Dockerfile` na raiz da suíte, alvo por `API_URL`/`APP_URL`/`APP`, JUnit XML em `/resultados`, saída 0 só se tudo passou.

## Docker Compose (`4_CI_CD/local/compose.yml`)

- Sobe as dependências (Mongo, Redis, RabbitMQ) e, no perfil app (RNF-20), os serviços.
- `depends_on` com `condition: service_healthy`; cada serviço com `healthcheck`.
- Variáveis por `.env` não versionado ou valores locais sem segredo real.
- Volumes nomeados para dados que precisam sobreviver; nada de bind mount do repositório em CI.
- Valide com `docker compose -f 4_CI_CD/local/compose.yml config`.

## Desenvolvimento local

Só as dependências em contêiner (RNF-10); o código roda na máquina. O CI local usa contêineres para o qa contra o que está rodando.
