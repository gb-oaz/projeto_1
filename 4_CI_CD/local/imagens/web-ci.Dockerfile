# Imagem da etapa de verificação da web no CI local (RNF-161): Node.js na
# versão do .nvmrc (passada pelo ci.sh), como no setup-node do GitHub Actions.
# O Cypress não entra aqui: ele é da suíte 5_DEV/qa/frontend/web/funcional/,
# que traz a própria imagem.
ARG NODE_VERSION
FROM node:${NODE_VERSION}-bookworm

RUN corepack enable

# Store do pnpm num volume (cache entre execuções)
# CI=true: as ferramentas rodam sem interação, como no GitHub Actions;
# o Corepack baixa o pnpm do packageManager sem perguntar.
ENV npm_config_store_dir=/pnpm-store \
    COREPACK_ENABLE_DOWNLOAD_PROMPT=0 \
    CI=true
