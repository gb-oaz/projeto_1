# Imagem das etapas de verificação e de qa da web no CI local (RNF-161).
# Node.js na versão do .nvmrc (passada pelo ci.sh) + as bibliotecas de sistema
# que o Cypress precisa para rodar sem tela (Xvfb), como no ubuntu-latest do
# GitHub Actions. O próprio Cypress é instalado pelo pnpm de cada app.
ARG NODE_VERSION
FROM node:${NODE_VERSION}-bookworm

RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      libgtk2.0-0 libgtk-3-0 libgbm-dev libnotify-dev libnss3 libxss1 \
      libasound2 libxtst6 xauth xvfb \
 && rm -rf /var/lib/apt/lists/* \
 && corepack enable

# Store do pnpm num volume (cache entre execuções)
# CI=true: as ferramentas rodam sem interação, como no GitHub Actions;
# o Corepack baixa o pnpm do packageManager sem perguntar.
ENV npm_config_store_dir=/pnpm-store \
    COREPACK_ENABLE_DOWNLOAD_PROMPT=0 \
    CI=true
