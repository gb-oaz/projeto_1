#!/usr/bin/env bash
# Etapa 3 · qa funcional do mobile, direto na máquina, fora do Docker
# (14_estrutura-projetos RNF-196, 03_ci-qualidade RNF-172). Compatível com o
# bash 3.2 do macOS.
#
# Uso (pelo ci.sh): qa-mobile.sh <android|ios> <app> [<app>…]
#
# Numa cópia temporária: instala o Appium e os drivers do package.json da
# suíte (versões exatas) e sobe esse Appium; para cada app, compila o pacote
# de teste (Android: .apk Debug; iOS: .app do simulador) com o endereço do
# backend em ApiUrl (RNF-198) e roda o executar.sh de
# 5_DEV/qa/frontend/mobile/funcional/ com o contrato:
#   PLATAFORMA           android | ios
#   APP                  hub | quest
#   APP_PACOTE           caminho do .apk ou do .app compilado
#   APPIUM_URL           servidor Appium (http://127.0.0.1:4724)
#   API_URL              backend visto da máquina (vazio sem backend)
#   API_URL_DISPOSITIVO  backend visto do emulador ou do simulador
#   RESULTADOS           pasta do JUnit XML
# O emulador Android e o simulador iOS já precisam estar ligados (o ci.sh
# confere antes). O JUnit XML fica em .gerado/resultados/mobile-funcional-*.
set -euo pipefail

AQUI=$(cd "$(dirname "$0")/.." && pwd)
RAIZ=$(cd "$AQUI/../.." && pwd)
plataforma=${1:?informe android ou ios}
shift
[ $# -gt 0 ] || { echo "informe os apps de 5_DEV/frontend/mobile/apps/"; exit 2; }
case "$plataforma" in android|ios) ;; *) echo "plataforma inválida: $plataforma"; exit 2 ;; esac
# porta própria, para não usar outro Appium que esteja no ar na máquina
APPIUM_URL=${APPIUM_URL:-http://127.0.0.1:4724}
# backend publicado pelo ci.sh na porta 18080 da máquina; o emulador Android
# enxerga a máquina em 10.0.2.2, o simulador iOS em localhost
PORTA_API=${PORTA_API:-}
API_URL="" API_URL_DISPOSITIVO=""
if [ -n "$PORTA_API" ]; then
  API_URL="http://localhost:$PORTA_API"
  if [ "$plataforma" = android ]; then API_URL_DISPOSITIVO="http://10.0.2.2:$PORTA_API"
  else API_URL_DISPOSITIVO="http://localhost:$PORTA_API"; fi
fi

passo() { printf '\n\033[1;34m▶ %s\033[0m\n' "$*"; "$@"; }

trabalho=$(mktemp -d "${TMPDIR:-/tmp}/boaz-qa-$plataforma.XXXXXX")
APPIUM_PID=""
encerrar() {
  if [ -n "$APPIUM_PID" ]; then kill "$APPIUM_PID" 2>/dev/null; wait "$APPIUM_PID" 2>/dev/null; fi
  rm -rf "$trabalho"
}
trap encerrar EXIT
itens=""
for i in global.json .nvmrc 5_DEV; do [ -e "$RAIZ/$i" ] && itens="$itens $i"; done
# shellcheck disable=SC2086
tar -C "$RAIZ" --exclude=node_modules --exclude=bin --exclude=obj --exclude=dist \
  --exclude=coverage --exclude=cobertura -cf - $itens | tar -C "$trabalho" -xf -
cd "$trabalho/5_DEV/frontend/mobile"
# a suíte também roda na cópia: o dotnet test dela não deixa bin/ e obj/ no repositório
SUITE="$trabalho/5_DEV/qa/frontend/mobile/funcional"

# Appium e drivers do package.json da suíte (APPIUM_HOME = pasta da suíte),
# no ar até o fim deste script
if curl -fsS -o /dev/null "$APPIUM_URL/status" 2>/dev/null; then
  echo "✗ já há um servidor em $APPIUM_URL; libere a porta ou defina APPIUM_URL"; exit 1
fi
(cd "$SUITE" && passo npm ci --no-audit --no-fund)
export APPIUM_HOME="$SUITE"
"$SUITE/node_modules/.bin/appium" --address 127.0.0.1 --port "${APPIUM_URL##*:}" \
  --log "$AQUI/.gerado/appium-$plataforma.log" >/dev/null 2>&1 &
APPIUM_PID=$!
i=0
until curl -fsS -o /dev/null "$APPIUM_URL/status" 2>/dev/null; do
  i=$((i + 1))
  [ "$i" -ge 60 ] && { echo "✗ Appium não respondeu em $APPIUM_URL (log em .gerado/appium-$plataforma.log)"; exit 1; }
  sleep 1
done

falhou=0
for app in "$@"; do
  if [ "$plataforma" = android ]; then
    export MobileSemIos=true
    # EmbedAssembliesIntoApk: o Debug do Android deixa os assemblies fora do
    # .apk (fast deployment do IDE); instalado pelo Appium, o app não abriria
    passo dotnet build "apps/$app" -f net10.0-android -c Debug -p:EmbedAssembliesIntoApk=true \
      ${API_URL_DISPOSITIVO:+-p:ApiUrl=$API_URL_DISPOSITIVO} || { falhou=1; continue; }
    pacote=$(find "apps/$app/bin/Debug/net10.0-android" -name '*-Signed.apk' | head -1)
  else
    case "$(uname -m)" in arm64) rid=iossimulator-arm64 ;; *) rid=iossimulator-x64 ;; esac
    passo dotnet build "apps/$app" -f net10.0-ios -c Debug -p:RuntimeIdentifier=$rid ${API_URL_DISPOSITIVO:+-p:ApiUrl=$API_URL_DISPOSITIVO} || { falhou=1; continue; }
    pacote=$(find "apps/$app/bin/Debug/net10.0-ios/$rid" -maxdepth 1 -name '*.app' | head -1)
  fi
  [ -n "$pacote" ] || { echo "✗ pacote do app $app não encontrado"; falhou=1; continue; }
  pacote="$PWD/$pacote"
  resultados="$AQUI/.gerado/resultados/mobile-funcional-$plataforma-$app"
  rm -rf "$resultados"; mkdir -p "$resultados"
  printf '\n\033[1;34m▶ %s\033[0m\n' "suíte funcional · $plataforma · $app"
  (cd "$SUITE" && PLATAFORMA=$plataforma APP=$app APP_PACOTE="$pacote" \
     APPIUM_URL=$APPIUM_URL API_URL=$API_URL API_URL_DISPOSITIVO=$API_URL_DISPOSITIVO \
     RESULTADOS="$resultados" bash ./executar.sh) || falhou=1
done
exit $falhou
