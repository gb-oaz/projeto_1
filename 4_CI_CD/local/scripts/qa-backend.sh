# Etapa 3 · qa do backend contra a imagem dele em contêiner (RNF-162).
# Roda os testes de 5_DEV/qa/backend/api/ (xUnit, fora das solutions dos
# serviços, por isso não entram na etapa 1) com API_URL apontando para o
# contêiner do backend.
# Uso (pelo ci.sh): docker compose run --rm --no-deps qa-backend
source /ci/comum.sh

copiar
cd /trabalho/5_DEV/qa/backend/api
passo dotnet test -c Release
