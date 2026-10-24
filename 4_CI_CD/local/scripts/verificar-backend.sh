# Etapa 1 · verificação de um serviço do backend — mesmos passos do job
# "Backend · <servico>" de .github/workflows/ci.yml (RNF-161).
# Uso (pelo ci.sh): docker compose run --rm backend-ci <servico>
source /ci/comum.sh

servico=${1:?informe o serviço de 5_DEV/backend/}
copiar
cd "/trabalho/5_DEV/backend/$servico"

passo dotnet format --verify-no-changes                     # RNF-15
passo dotnet build -c Release -warnaserror                  # RNF-22
passo dotnet test -c Release --no-build \
  --collect:"XPlat Code Coverage" --results-directory /trabalho/cobertura   # RNF-14, RNF-37
passo cobertura_minima /trabalho/cobertura 80               # RNF-25
