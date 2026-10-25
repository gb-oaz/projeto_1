# Imagem frontend-<app> (14_estrutura-projetos RNF-181, 04_publicacao-imagens
# RNF-27): um Dockerfile para os três apps, com o app no argumento APP
# (landing_page, hub ou quest). O build gera o pacote estático do app e o
# nginx o serve na porta 8080, com 200 em /health (03_ci-qualidade RNF-162).
#   docker build --build-arg APP=hub -t frontend-hub .

# Node.js na versão exata do .nvmrc da raiz (01_stack-desenvolvimento RNF-168):
# o CI passa o NODE_VERSION lido de lá; o padrão vale para o build à mão
ARG NODE_VERSION=24.22.1

FROM node:${NODE_VERSION}-bookworm AS build
ARG APP
RUN test -n "$APP" || { echo "informe --build-arg APP=landing_page|hub|quest"; exit 1; }
ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0 CI=true
RUN corepack enable
WORKDIR /src
COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY . .
RUN APP=$APP pnpm run build && mv dist/$APP /app

# nginx sem root, que já escuta na 8080
FROM nginxinc/nginx-unprivileged:1.31-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app /usr/share/nginx/html
