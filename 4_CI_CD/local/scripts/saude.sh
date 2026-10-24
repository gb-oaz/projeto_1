# Etapa 2 · espera cada aplicação responder 200 em <url> (o /health), até 120 s.
# POSIX sh: roda na imagem curlimages/curl.
# Uso (pelo ci.sh): docker compose run --rm --no-deps saude <url> [<url>…]
for url in "$@"; do
  printf '▶ %s ' "$url"
  i=0
  until curl -fsS -o /dev/null "$url"; do
    i=$((i + 1))
    if [ "$i" -ge 60 ]; then echo "✗ sem resposta 200 em 120 s"; exit 1; fi
    sleep 2
  done
  echo "✓"
done
