#!/usr/bin/env bash
# Detecta quais componentes de 5_DEV/ o CI deve compilar e testar.
#
# Requisito 03_ci-qualidade:
#   RNF-34  roda só os componentes cujo diretório (ou cujos testes em 5_DEV/qa/) mudou
#   RNF-35  arquivo comum a todos os componentes roda todos os componentes
#   RNF-24  iOS só em pull request para a principal e no merge nela
#
# Componentes:
#   backend  5_DEV/backend/<servico>/          testes em 5_DEV/qa/backend/<servico>/
#   web      5_DEV/frontend/web_<app>/         testes em 5_DEV/qa/frontend/web_<app>/
#   android  5_DEV/frontend/mob_and*_<app>/    testes em 5_DEV/qa/frontend/mob_and*_<app>/
#   ios      5_DEV/frontend/mob_ios_<app>/     testes em 5_DEV/qa/frontend/mob_ios_<app>/
#
# Um diretório só vira componente quando já tem projeto (.sln/.slnx/.csproj no
# .NET, package.json na web); pastas vazias são ignoradas.
#
# Uso: detectar-componentes.sh <base> <head>
#   <base> vazio ou só zeros => considera tudo alterado (primeiro push, branch nova sem base).
# Variáveis: EVENTO (push|pull_request), REF_ALVO (branch base do PR ou branch do push),
#            BRANCH_PRINCIPAL (padrão master), GITHUB_OUTPUT (opcional).
set -euo pipefail

BASE="${1:-}"
HEAD="${2:-HEAD}"
EVENTO="${EVENTO:-push}"
REF_ALVO="${REF_ALVO:-}"
BRANCH_PRINCIPAL="${BRANCH_PRINCIPAL:-master}"
DEV="5_DEV"
QA="$DEV/qa"

# Arquivos comuns: alterar qualquer um deles roda todos os componentes (RNF-35).
COMUNS_REGEX='^(\.github/|4_CI_CD/|global\.json$|\.nvmrc$|(.*/)?docker-compose[^/]*\.ya?ml$|(.*/)?compose[^/]*\.ya?ml$|\.editorconfig$|Directory\.(Build|Packages)\.[^/]+$|NuGet\.config$|nuget\.config$)'

tudo=false
if [[ -z "$BASE" || "$BASE" =~ ^0+$ ]] || ! git cat-file -e "${BASE}^{commit}" 2>/dev/null; then
  tudo=true
  alterados=""
else
  alterados="$(git diff --name-only "$BASE" "$HEAD")"
fi

if [[ "$tudo" == false ]] && grep -Eq "$COMUNS_REGEX" <<<"$alterados"; then
  tudo=true
fi

# alterou <prefixo>/<nome>/... ?
mudou() {
  local caminho="$1"
  [[ "$tudo" == true ]] && return 0
  grep -q "^${caminho}/" <<<"$alterados"
}

# arquivo solto na raiz de uma área (ex.: 5_DEV/backend/Directory.Build.props) afeta a área toda
mudou_raiz() {
  local area="$1"
  [[ "$tudo" == true ]] && return 0
  grep -Eq "^${area}/[^/]+$" <<<"$alterados"
}

tem_dotnet() { compgen -G "$1/*.sln" >/dev/null || compgen -G "$1/*.slnx" >/dev/null || [[ -n "$(find "$1" -name '*.csproj' -print -quit 2>/dev/null)" ]]; }
tem_node() { [[ -f "$1/package.json" ]]; }

# lista JSON a partir dos argumentos
json() {
  local saida="[" sep=""
  for item in "$@"; do saida+="${sep}\"${item}\""; sep=","; done
  echo "${saida}]"
}

backend=() web=() android=() ios=()

for dir in "$DEV"/backend/*/; do
  [[ -d "$dir" ]] || continue
  nome="$(basename "$dir")"
  tem_dotnet "$DEV/backend/$nome" || continue
  if mudou "$DEV/backend/$nome" || mudou "$QA/backend/$nome" || mudou_raiz "$DEV/backend" || mudou_raiz "$QA/backend"; then
    backend+=("$nome")
  fi
done

for dir in "$DEV"/frontend/*/; do
  [[ -d "$dir" ]] || continue
  nome="$(basename "$dir")"
  caminho="$DEV/frontend/$nome"
  afetado=false
  if mudou "$caminho" || mudou "$QA/frontend/$nome" || mudou_raiz "$DEV/frontend" || mudou_raiz "$QA/frontend"; then
    afetado=true
  fi
  [[ "$afetado" == true ]] || continue
  case "$nome" in
    web_*)     tem_node "$caminho" && web+=("$nome") ;;
    mob_and*)  tem_dotnet "$caminho" && android+=("$nome") ;;
    mob_ios_*) tem_dotnet "$caminho" && ios+=("$nome") ;;
    *) echo "::warning::$caminho não segue o padrão web_*, mob_and*_* ou mob_ios_* e foi ignorado pelo CI" ;;
  esac
done

# RNF-24: iOS só em PR para a principal e em push (merge) na principal.
if [[ ! ("$EVENTO" == "pull_request" && "$REF_ALVO" == "$BRANCH_PRINCIPAL") && ! ("$EVENTO" == "push" && "$REF_ALVO" == "$BRANCH_PRINCIPAL") ]]; then
  ios=()
fi

resultado="backend=$(json ${backend[@]+"${backend[@]}"})
web=$(json ${web[@]+"${web[@]}"})
android=$(json ${android[@]+"${android[@]}"})
ios=$(json ${ios[@]+"${ios[@]}"})"

echo "tudo alterado: $tudo"
echo "$resultado"
if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  echo "$resultado" >>"$GITHUB_OUTPUT"
fi
