# Etapa 1 · verificação da web — mesmos passos do job "Web" de
# .github/workflows/ci.yml (RNF-161): lint, tipos e testes uma vez para o
# projeto de 5_DEV/frontend/web/ e build de cada app (APP). Os testes de
# ponta a ponta ficam no qa (5_DEV/qa/frontend/web/funcional/), na etapa 3.
# Uso (pelo ci.sh): docker compose run --rm web-ci <app> [<app>…]
source /ci/comum.sh

[ $# -gt 0 ] || { echo "informe os apps de 5_DEV/frontend/web/apps/"; exit 2; }
copiar
cd /trabalho/5_DEV/frontend/web

passo pnpm install --frozen-lockfile
passo pnpm exec eslint . --max-warnings 0                   # RNF-17
passo pnpm exec prettier --check .                          # RNF-17
passo pnpm exec vue-tsc --build --force                     # RNF-17
passo pnpm exec vitest run --coverage.enabled --coverage.thresholds.lines=80   # RNF-16, RNF-25
falhou=0
for app in "$@"; do
  APP=$app passo pnpm run build || falhou=1
done
exit $falhou
