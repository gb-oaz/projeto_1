#!/usr/bin/env bash
# Etapa 1 · verificação de um app mobile, direto na máquina, fora do Docker
# (RNF-161, RNF-166) — mesmos passos dos jobs "Android · <app>" e "iOS · <app>"
# de .github/workflows/ci.yml. Compatível com o bash 3.2 do macOS.
#
# Uso (pelo ci.sh): verificar-mobile.sh <android|ios> <app>
#   android  Release, com o JDK 17 e o SDK do Android da máquina
#            (JavaSdkDirectory e AndroidSdkDirectory, achados pelo ci.sh)
#   ios      Debug (simulador, sem certificado de assinatura), no macOS com Xcode
#
# O código é copiado para uma pasta temporária: bin/ e obj/ não aparecem na
# cópia de trabalho (RNF-164). A cobertura fica em .gerado/cobertura/<app>/.
set -euo pipefail

AQUI=$(cd "$(dirname "$0")/.." && pwd)
RAIZ=$(cd "$AQUI/../.." && pwd)
plataforma=${1:?informe android ou ios}
app=${2:?informe o app de 5_DEV/frontend/}
saida="$AQUI/.gerado/cobertura/$app"
case "$plataforma" in android) config=Release ;; ios) config=Debug ;; *) echo "plataforma inválida: $plataforma"; exit 2 ;; esac

passo() { printf '\n\033[1;34m▶ %s\033[0m\n' "$*"; "$@"; }

trabalho=$(mktemp -d "${TMPDIR:-/tmp}/boaz-ci-$plataforma.XXXXXX")
trap 'rm -rf "$trabalho"' EXIT
itens=""
for i in global.json .nvmrc 5_DEV; do [ -e "$RAIZ/$i" ] && itens="$itens $i"; done
# shellcheck disable=SC2086
tar -C "$RAIZ" --exclude=node_modules --exclude=bin --exclude=obj --exclude=dist \
  --exclude=coverage --exclude=cobertura -cf - $itens | tar -C "$trabalho" -xf -
cd "$trabalho/5_DEV/frontend/$app"

rm -rf "$saida"
passo dotnet format --verify-no-changes                     # RNF-15
passo dotnet build -c "$config" -warnaserror                # RNF-22
passo dotnet test -c "$config" --no-build \
  --collect:"XPlat Code Coverage" --results-directory "$saida"   # RNF-14

# RNF-26: mede a cobertura, sem reprovar
arqs=$(find "$saida" -name coverage.cobertura.xml 2>/dev/null || true)
if [ -n "$arqs" ]; then
  # shellcheck disable=SC2086
  grep -ho '<coverage [^>]*' $arqs | awk '
    { match($0, /lines-covered="[0-9]+"/); c += substr($0, RSTART + 15, RLENGTH - 16)
      match($0, /lines-valid="[0-9]+"/);   v += substr($0, RSTART + 13, RLENGTH - 14) }
    END { if (v) printf "cobertura de linhas: %.1f %% (medida, sem mínimo)\n", 100 * c / v }'
else
  echo "nenhum arquivo de cobertura (sem testes)"
fi
