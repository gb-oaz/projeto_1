# Funções comuns aos scripts que rodam dentro dos contêineres do CI local.
# Carregado com: source /ci/comum.sh

set -euo pipefail

# Copia de /fonte (repositório, só leitura) para /trabalho só o que o build
# precisa, sem os artefatos da máquina (node_modules, bin, obj, dist…), para
# que nada do sistema da pessoa entre no contêiner e nada volte para ela (RNF-164).
copiar() {
  local itens=() i
  for i in global.json .nvmrc 5_DEV; do [ -e "/fonte/$i" ] && itens+=("$i"); done
  mkdir -p /trabalho
  tar -C /fonte \
    --exclude=node_modules --exclude=bin --exclude=obj --exclude=dist \
    --exclude=coverage --exclude=cobertura --exclude=.vite \
    -cf - "${itens[@]}" | tar -C /trabalho -xf -
}

# Mostra cada passo como no log do GitHub Actions e roda o comando.
passo() {
  printf '\n\033[1;34m▶ %s\033[0m\n' "$*"
  "$@"
}

# Soma a cobertura de linhas dos arquivos coverage.cobertura.xml do diretório
# e reprova abaixo do mínimo (RNF-25), como o CodeCoverageSummary do CI.
# Sem arquivo de cobertura (nenhum teste), também reprova.
cobertura_minima() {
  local dir=$1 min=$2 arqs
  arqs=$(find "$dir" -name coverage.cobertura.xml 2>/dev/null)
  if [ -z "$arqs" ]; then
    echo "✗ nenhum arquivo de cobertura em $dir (sem testes?)"
    return 1
  fi
  # shellcheck disable=SC2086
  grep -ho '<coverage [^>]*' $arqs | awk -v m="$min" '
    { match($0, /lines-covered="[0-9]+"/); c += substr($0, RSTART + 15, RLENGTH - 16)
      match($0, /lines-valid="[0-9]+"/);   v += substr($0, RSTART + 13, RLENGTH - 14) }
    END {
      if (v == 0) { print "✗ nenhuma linha medida"; exit 1 }
      p = 100 * c / v
      printf "%s cobertura de linhas: %.1f %% (mínimo %d %%)\n", (p >= m ? "✓" : "✗"), p, m
      exit (p >= m ? 0 : 1)
    }'
}

# Mede a cobertura de linhas sem reprovar (mobile, RNF-26)
cobertura_medida() {
  cobertura_minima "$1" 0 || true
}

# Nome da imagem/serviço de um app web: landing_page → frontend-landing-page
nome_frontend() {
  echo "frontend-${1//_/-}"
}
