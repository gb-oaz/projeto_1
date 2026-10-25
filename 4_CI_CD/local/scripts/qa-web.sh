# Etapa 3 · qa de um app web contra a imagem dele em contêiner (RNF-162).
# Roda os testes de ponta a ponta do app pelo script "test:e2e:imagem" do
# package.json de 5_DEV/frontend/web/, com o app em APP, CYPRESS_baseUrl
# apontando para o contêiner (sem subir servidor de desenvolvimento) e
# CYPRESS_API_URL para o backend.
# Uso (pelo ci.sh): docker compose run --rm --no-deps qa-web <app>
source /ci/comum.sh

app=${1:?informe o app de 5_DEV/frontend/web/apps/}
copiar
cd /trabalho/5_DEV/frontend/web

export APP=$app
export CYPRESS_baseUrl="http://$(nome_frontend "$app"):8080"
export CYPRESS_API_URL="$API_URL"
passo pnpm install --frozen-lockfile
passo pnpm run test:e2e:imagem
