# Etapa 1 · verificação de um app web — mesmos passos do job "Web · <app>" de
# .github/workflows/ci.yml (RNF-161).
# Uso (pelo ci.sh): docker compose run --rm web-ci <app>
source /ci/comum.sh

app=${1:?informe o app de 5_DEV/frontend/web_*}
copiar
cd "/trabalho/5_DEV/frontend/$app"

passo pnpm install --frozen-lockfile
passo pnpm exec eslint . --max-warnings 0                   # RNF-17
passo pnpm exec prettier --check .                          # RNF-17
passo pnpm exec vue-tsc --build --force                     # RNF-17
passo pnpm exec vitest run --coverage.enabled --coverage.thresholds.lines=80   # RNF-16, RNF-25
passo pnpm run build
passo pnpm run test:e2e                                     # RNF-18
