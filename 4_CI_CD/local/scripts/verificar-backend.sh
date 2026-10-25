# Etapa 1 · verificação do backend — mesmos passos do job "Backend" de
# .github/workflows/ci.yml (RNF-161), sobre a solution de 5_DEV/backend/.
# Uso (pelo ci.sh): docker compose run --rm backend-ci
source /ci/comum.sh

copiar
cd /trabalho/5_DEV/backend

passo dotnet format --verify-no-changes                     # RNF-15
passo dotnet build -c Release -warnaserror                  # RNF-22
passo dotnet test -c Release --no-build \
  --collect:"XPlat Code Coverage" --results-directory /trabalho/cobertura   # RNF-14, RNF-37
passo cobertura_minima /trabalho/cobertura 80               # RNF-25
