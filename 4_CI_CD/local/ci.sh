#!/usr/bin/env bash
# =============================================================================
# CI local — requisito 1_Requisitos/03_ci-qualidade.html (RNF-160 a RNF-166)
# =============================================================================
#
# Repete na máquina o CI do GitHub Actions (.github/workflows/ci.yml) e, depois,
# sobe as aplicações em contêiner e roda o qa contra elas. Não publica imagem:
# a publicação é só do GitHub Actions (RN-140).
#
#   ./4_CI_CD/local/ci.sh              componentes alterados em relação à master
#   ./4_CI_CD/local/ci.sh --tudo       todos os componentes
#   ./4_CI_CD/local/ci.sh --qa backend só o qa do backend (testes de API)
#   ./4_CI_CD/local/ci.sh --qa frontend só o qa do frontend (Cypress)
#   ./4_CI_CD/local/ci.sh --qa todos   os dois em conjunto (padrão)
#   ./4_CI_CD/local/ci.sh --sem-qa     só a etapa 1 (verificação)
#   ./4_CI_CD/local/ci.sh --manter     deixa as aplicações no ar ao terminar
#
#   mudancas ─► 1 · verificação ─► 2 · aplicações ─► 3 · qa ─► resultado
#               backend-ci          backend           qa-backend (API)
#               web-ci              frontend-<app>    qa-web (Cypress)
#               android e ios na máquina, fora do Docker
#
# Precisa de: Docker com Compose v2, git e bash; para o mobile, o SDK .NET do
# global.json e o workload do MAUI; Android: JDK 17 e SDK do Android; iOS:
# macOS com Xcode.
# =============================================================================
set -uo pipefail

AQUI=$(cd "$(dirname "$0")" && pwd)
RAIZ=$(cd "$AQUI/../.." && pwd)
GERADO="$AQUI/.gerado"
cd "$RAIZ"

TUDO=false SEM_QA=false MANTER=false QA=todos
while [ $# -gt 0 ]; do
  case "$1" in
    --tudo) TUDO=true ;;
    --sem-qa) SEM_QA=true ;;
    --manter) MANTER=true ;;
    --qa) QA=${2:-}; shift ;;
    --qa=*) QA=${1#--qa=} ;;
    -h|--help) sed -n '5,25p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "opção desconhecida: $1 (use --help)"; exit 2 ;;
  esac
  shift
done
case "$QA" in backend|frontend|todos) ;; *) echo "--qa aceita backend, frontend ou todos"; exit 2 ;; esac
QA_BACKEND=false QA_FRONTEND=false
[ "$QA" != frontend ] && QA_BACKEND=true
[ "$QA" != backend ] && QA_FRONTEND=true

titulo() { printf '\n\033[1m━━ %s ━━\033[0m\n' "$*"; }
aviso() { printf '\033[33m! %s\033[0m\n' "$*"; }

# Resultados: "componente|etapa|resultado" (passou, falhou, pulado)
RES=()
registrar() { RES+=("$1|$2|$3"); }

# -----------------------------------------------------------------------------
# mudancas — o que roda (RNF-163), com os mesmos filtros do CI (RNF-34, RNF-35)
# Compara com a master (o ponto em que a branch saiu dela) e inclui o que
# ainda não foi commitado nem adicionado ao git.
# -----------------------------------------------------------------------------
titulo "mudancas"
BACKEND_MUDOU=false WEB_MUDOU=false ANDROID_MUDOU=false IOS_MUDOU=false
if $TUDO; then
  BACKEND_MUDOU=true WEB_MUDOU=true ANDROID_MUDOU=true IOS_MUDOU=true
  echo "--tudo: todos os componentes"
else
  BASE=$(git merge-base HEAD origin/master 2>/dev/null || git merge-base HEAD master 2>/dev/null || echo HEAD)
  ALTERADOS=$( { git diff --name-only "$BASE"; git ls-files --others --exclude-standard; } | sort -u)
  while IFS= read -r f; do
    case "$f" in
      .github/*|global.json|.xcode-version|.nvmrc|*docker-compose*.yml|*compose*.yml|4_CI_CD/local/*)
        BACKEND_MUDOU=true WEB_MUDOU=true ANDROID_MUDOU=true IOS_MUDOU=true ;;
      5_DEV/backend/*|5_DEV/qa/backend/*) BACKEND_MUDOU=true ;;
      5_DEV/frontend/web/*|5_DEV/qa/frontend/web/*) WEB_MUDOU=true ;;
      5_DEV/frontend/Directory.Build.props|5_DEV/frontend/mobile/*|5_DEV/qa/frontend/mobile/*)
        ANDROID_MUDOU=true IOS_MUDOU=true ;;
    esac
  done <<<"$ALTERADOS"
  echo "base: $(git rev-parse --short "$BASE")  ·  arquivos alterados: $(grep -c . <<<"$ALTERADOS")"
fi

# Frente que mudou só roda se já tiver projeto; apps de apps/<app>/, como no
# CI (14_estrutura-projetos)
solution() { compgen -G "$1*.sln" >/dev/null || compgen -G "$1*.slnx" >/dev/null; }
BACKEND=false APPS=() ANDROID=() IOS=()
$BACKEND_MUDOU && solution 5_DEV/backend/ && BACKEND=true
if $WEB_MUDOU && [ -f 5_DEV/frontend/web/package.json ]; then
  for d in 5_DEV/frontend/web/apps/*/; do [ -d "$d" ] && APPS+=("$(basename "$d")"); done
fi
if solution 5_DEV/frontend/mobile/; then
  for d in 5_DEV/frontend/mobile/apps/*/; do
    compgen -G "$d*.csproj" >/dev/null || continue
    $ANDROID_MUDOU && ANDROID+=("$(basename "$d")")
    $IOS_MUDOU && IOS+=("$(basename "$d")")
  done
fi
echo "backend: $($BACKEND && echo 5_DEV/backend || echo —)"
echo "web:     ${APPS[*]:-—}"
echo "android: ${ANDROID[*]:-—}"
echo "ios:     ${IOS[*]:-—}"

if ! $BACKEND && [ ${#APPS[@]} -eq 0 ] && [ ${#ANDROID[@]} -eq 0 ] && [ ${#IOS[@]} -eq 0 ]; then
  titulo "resultado"
  echo "✓ nada a verificar (nenhum componente alterado)"
  exit 0
fi

# Versões do ambiente local e do CI (RN-02, RNF-19)
if $BACKEND || [ ${#ANDROID[@]} -gt 0 ] || [ ${#IOS[@]} -gt 0 ]; then
  DOTNET_SDK=$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' global.json 2>/dev/null | head -1)
  [ -n "$DOTNET_SDK" ] || { echo "✗ global.json sem a versão do SDK .NET"; exit 2; }
  export DOTNET_SDK
elif [ -f global.json ]; then
  # o qa do backend (imagem do SDK) pode rodar mesmo quando só a web mudou
  DOTNET_SDK=$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' global.json | head -1)
  export DOTNET_SDK
fi
if [ ${#APPS[@]} -gt 0 ]; then
  NODE_VERSION=$(tr -d ' \nv' < .nvmrc 2>/dev/null)
  [ -n "$NODE_VERSION" ] || { echo "✗ .nvmrc sem a versão do Node.js"; exit 2; }
  export NODE_VERSION
fi
# Docker só é preciso para backend e web (o mobile roda na máquina)
USA_DOCKER=false
if $BACKEND || [ ${#APPS[@]} -gt 0 ]; then
  USA_DOCKER=true
  command -v docker >/dev/null && docker compose version >/dev/null 2>&1 && docker info >/dev/null 2>&1 \
    || { echo "✗ Docker com Compose v2 não encontrado ou parado"; exit 2; }
fi

mkdir -p "$GERADO/cobertura"
COMPOSE=(docker compose -f "$AQUI/compose.yml")
# Sem terminal (saída redirecionada, IDE), o "run" não pode pedir um TTY
RUN=(run --rm); [ -t 0 ] && [ -t 1 ] || RUN+=(-T)

encerrar() {
  $USA_DOCKER || return 0
  if $MANTER; then
    aviso "aplicações no ar (--manter); para derrubar: docker compose -p boaz-ci-local down"
  else
    "${COMPOSE[@]}" down --remove-orphans >/dev/null 2>&1
  fi
}
trap encerrar EXIT

# -----------------------------------------------------------------------------
# Etapa 1 · verificação — os jobs backend e web do CI (RNF-161)
# Um componente que falha não interrompe os outros (fail-fast: false).
# -----------------------------------------------------------------------------
VERDE=true
[ ${#APPS[@]} -gt 0 ] && "${COMPOSE[@]}" --profile verificacao build web-ci
if $BACKEND; then
  titulo "1 · verificação · backend"
  if "${COMPOSE[@]}" --profile verificacao "${RUN[@]}" backend-ci; then registrar "backend" "verificação" "passou"
  else registrar "backend" "verificação" "falhou"; VERDE=false; fi
fi
if [ ${#APPS[@]} -gt 0 ]; then
  titulo "1 · verificação · web · ${APPS[*]}"
  if "${COMPOSE[@]}" --profile verificacao "${RUN[@]}" web-ci "${APPS[@]}"; then registrar "web" "verificação" "passou"
  else registrar "web" "verificação" "falhou"; VERDE=false; fi
fi

# Android e iOS direto na máquina, fora do Docker (RNF-166), sobre a solution
# de 5_DEV/frontend/mobile/. Faltando algum pré-requisito, a plataforma é
# pulada com aviso dizendo o que falta: o GitHub Actions verifica (o iOS, no
# pull request para a master).
WORKLOADS=$(sed -n 's/.*"workloadVersion"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' global.json 2>/dev/null | head -1)
falta_dotnet() {
  if ! command -v dotnet >/dev/null || ! dotnet --list-sdks | grep -q "^$DOTNET_SDK "; then echo "SDK .NET $DOTNET_SDK (global.json)"
  elif [ -n "$WORKLOADS" ] && [ "$(dotnet workload --version 2>/dev/null)" != "$WORKLOADS" ]; then
    echo "workloads $WORKLOADS do global.json (sudo dotnet workload install maui, na raiz do repositório)"
  elif ! dotnet workload list 2>/dev/null | grep -qE "^(maui|maui-$1) "; then echo "workload maui-$1 (dotnet workload install maui-$1)"
  # no macOS os projetos declaram Android e iOS: a restauração pede os dois
  elif [ "$1" = ios ] && ! dotnet workload list 2>/dev/null | grep -qE "^(maui|maui-android) "; then
    echo "workload maui-android (dotnet workload install maui-android)"
  fi
}
ANDROID_FALTA="" IOS_FALTA=""
if [ ${#ANDROID[@]} -gt 0 ]; then
  ANDROID_FALTA=$(falta_dotnet android)
  # JDK 17: JAVA_HOME, se for o 17, ou o 17 registrado no macOS
  JDK=""
  for j in "${JAVA_HOME:-}" "$( [ -x /usr/libexec/java_home ] && /usr/libexec/java_home -v 17 2>/dev/null )"; do
    [ -n "$j" ] && "$j/bin/java" -version 2>&1 | grep -q 'version "17' && { JDK=$j; break; }
  done
  # SDK do Android: ANDROID_HOME, ANDROID_SDK_ROOT ou o local padrão do Android Studio
  ASDK=${ANDROID_HOME:-${ANDROID_SDK_ROOT:-}}
  for d in "$HOME/Library/Android/sdk" "$HOME/Android/Sdk"; do [ -z "$ASDK" ] && [ -d "$d" ] && ASDK=$d; done
  if [ -z "$ANDROID_FALTA" ]; then
    if [ -z "$JDK" ]; then ANDROID_FALTA="JDK 17 (JAVA_HOME)"
    elif [ -z "$ASDK" ] || [ ! -d "$ASDK/platforms" ]; then ANDROID_FALTA="SDK do Android (ANDROID_HOME)"
    fi
  fi
  # o MSBuild lê variáveis de ambiente como propriedades
  export JavaSdkDirectory="$JDK" AndroidSdkDirectory="$ASDK"
fi
if [ ${#IOS[@]} -gt 0 ]; then
  if [ "$(uname -s)" != Darwin ]; then IOS_FALTA="macOS"
  elif ! xcodebuild -version >/dev/null 2>&1; then IOS_FALTA="Xcode (instalado e selecionado com xcode-select -s /Applications/Xcode.app)"
  else
    # o Xcode é o de .xcode-version (RNF-170), e o build do simulador
    # precisa do runtime iOS da versão desse Xcode
    XCODE=$(tr -d ' \n' < .xcode-version 2>/dev/null)
    XCODE_ATUAL=$(xcodebuild -version | sed -n 's/^Xcode \([0-9]*\.[0-9]*\).*/\1/p')
    SIM=$(xcrun --sdk iphonesimulator --show-sdk-version 2>/dev/null)
    if [ -n "$XCODE" ] && [ "$XCODE_ATUAL" != "$XCODE" ]; then
      IOS_FALTA="Xcode $XCODE de .xcode-version (o selecionado é o $XCODE_ATUAL; selecione-o com sudo xcode-select -s)"
    elif ! xcrun simctl list runtimes 2>/dev/null | grep -q "^iOS $SIM "; then
      IOS_FALTA="runtime do simulador iOS $SIM (xcodebuild -downloadPlatform iOS)"
    else IOS_FALTA=$(falta_dotnet ios)
    fi
  fi
fi
verificar_mobile() { # $1 = android|ios  $2 = o que falta  $3... = apps
  local p=$1 f=$2; shift 2
  [ $# -gt 0 ] || return 0
  titulo "1 · verificação · $p · $*"
  if [ -n "$f" ]; then
    aviso "falta $f nesta máquina; $p não verificado"
    registrar "$p" "verificação" "pulado"
  elif bash "$AQUI/scripts/verificar-mobile.sh" "$p" "$@"; then registrar "$p" "verificação" "passou"
  else registrar "$p" "verificação" "falhou"; VERDE=false; fi
}
verificar_mobile android "$ANDROID_FALTA" ${ANDROID[@]+"${ANDROID[@]}"}
verificar_mobile ios "$IOS_FALTA" ${IOS[@]+"${IOS[@]}"}

# -----------------------------------------------------------------------------
# Etapas 2 e 3 · aplicações em contêiner e qa (RNF-162)
# Só com a etapa 1 verde. As imagens saem dos mesmos Dockerfiles da publicação
# (5_DEV/backend/Dockerfile e 5_DEV/frontend/web/Dockerfile, este com o
# argumento APP de cada app), com a tag
# local boaz-ci-local/<imagem>:ci, e nunca são enviadas a um registro (RN-140).
# O qa (--qa) testa o backend sozinho (API), o frontend (Cypress, com o backend
# no ar) ou os dois em conjunto, sobre a mesma pilha de contêineres.
# -----------------------------------------------------------------------------
if $VERDE && ! $SEM_QA && { $BACKEND || [ ${#APPS[@]} -gt 0 ]; }; then
  # apps da web com imagem (o qa do frontend sobe todos, mesmo sem mudança)
  APPS_IMAGEM=()
  if [ -f 5_DEV/frontend/web/Dockerfile ]; then
    for d in 5_DEV/frontend/web/apps/*/; do [ -d "$d" ] && APPS_IMAGEM+=("$(basename "$d")"); done
  fi
  titulo "2 · aplicações"
  {
    echo "# gerado por ci.sh — não edite"
    echo "services:"
    if [ -f 5_DEV/backend/Dockerfile ]; then
      cat <<EOF
  backend:
    build: { context: ../../5_DEV/backend }
    image: boaz-ci-local/backend-mono:ci
    profiles: [aplicacoes]
    environment:
      AMBIENTE: local
      ConnectionStrings__Mongo: mongodb://mongodb:27017
      ConnectionStrings__MongoDb: mongodb://mongodb:27017
      ConnectionStrings__Redis: redis:6379
    depends_on:
      mongodb: { condition: service_healthy }
      redis: { condition: service_healthy }
EOF
    fi
    for app in ${APPS_IMAGEM[@]+"${APPS_IMAGEM[@]}"}; do
      $QA_FRONTEND || break
      n="frontend-${app//_/-}"
      cat <<EOF
  $n:
    build: { context: ../../5_DEV/frontend/web, args: { APP: $app } }
    image: boaz-ci-local/$n:ci
    profiles: [aplicacoes]
    environment:
      AMBIENTE: local
EOF
    done
  } > "$GERADO/compose.apps.yml"
  SAUDE=()
  grep -q '^  backend:' "$GERADO/compose.apps.yml" && SAUDE+=("http://backend:8080/health")
  for n in $(sed -n 's/^  \(frontend-[a-z0-9-]*\):$/\1/p' "$GERADO/compose.apps.yml"); do SAUDE+=("http://$n:8080/health"); done

  if [ ${#SAUDE[@]} -eq 0 ]; then
    aviso "nenhum Dockerfile em 5_DEV/backend/ ou 5_DEV/frontend/web/: etapas 2 e 3 puladas"
    registrar "aplicações" "qa" "pulado"
  else
    COMPOSE+=(-f "$GERADO/compose.apps.yml")
    if "${COMPOSE[@]}" --profile aplicacoes up -d --build \
       && "${COMPOSE[@]}" --profile qa "${RUN[@]}" --no-deps saude "${SAUDE[@]}"; then
      registrar "aplicações" "/health" "passou"
      titulo "3 · qa · $QA"
      # backend: testes de API de 5_DEV/qa/backend/api/ contra o contêiner
      if $QA_BACKEND; then
        if ! grep -q '^  backend:' "$GERADO/compose.apps.yml"; then
          aviso "sem 5_DEV/backend/Dockerfile: qa do backend pulado"; registrar "qa · backend" "qa" "pulado"
        elif ! projeto_dotnet 5_DEV/qa/backend/api/; then
          aviso "sem projeto de teste em 5_DEV/qa/backend/api/: qa do backend pulado"; registrar "qa · backend" "qa" "pulado"
        elif "${COMPOSE[@]}" --profile qa "${RUN[@]}" --no-deps qa-backend; then registrar "qa · backend" "qa" "passou"
        else registrar "qa · backend" "qa" "falhou"; VERDE=false; fi
      fi
      # frontend: Cypress de cada app web com imagem, contra o contêiner dele
      if $QA_FRONTEND; then
        for n in $(sed -n 's/^  frontend-\([a-z0-9-]*\):$/\1/p' "$GERADO/compose.apps.yml"); do
          a="${n//-/_}"
          if ! grep -q '"test:e2e:imagem"' 5_DEV/frontend/web/package.json 2>/dev/null; then
            aviso "web sem o script test:e2e:imagem: qa do frontend pulado"; registrar "qa · $a" "qa" "pulado"; continue
          fi
          if "${COMPOSE[@]}" --profile qa "${RUN[@]}" --no-deps qa-web "$a"; then registrar "qa · $a" "qa" "passou"
          else registrar "qa · $a" "qa" "falhou"; VERDE=false; fi
        done
      fi
    else
      registrar "aplicações" "/health" "falhou"; VERDE=false
      "${COMPOSE[@]}" --profile aplicacoes logs --tail 50
    fi
  fi
elif ! $VERDE; then
  aviso "etapa 1 com falha: aplicações e qa não rodam"
fi

# -----------------------------------------------------------------------------
# resultado — veredito único, como o job "CI · resultado" (RNF-165)
# -----------------------------------------------------------------------------
titulo "resultado"
for r in "${RES[@]}"; do
  IFS='|' read -r c e s <<<"$r"
  case "$s" in passou) m="✓" ;; falhou) m="✗" ;; *) m="–" ;; esac
  printf '  %s  %-34s %-13s %s\n' "$m" "$c" "$e" "$s"
done
if $VERDE; then
  if printf '%s\n' "${RES[@]}" | grep -q '|verificação|pulado$'; then
    echo "✓ CI local aprovado, exceto o que foi pulado: isso só o GitHub Actions confere"
  else
    echo "✓ CI local aprovado: a mesma mudança deve passar no GitHub Actions"
  fi
  exit 0
fi
echo "✗ CI local reprovado"
exit 1
