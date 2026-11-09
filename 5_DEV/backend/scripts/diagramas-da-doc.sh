#!/usr/bin/env bash
# Copia para a documentação da API (ms_mono/wwwroot/doc/api/diagramas/) a versão
# mais recente de cada diagrama que os guias de sistema/guias/ mostram. A cópia
# perde a versão no nome (usuarios-contrato-api.svg), para o link do guia não
# mudar a cada versão nova; o próprio desenho traz a versão na marca d'água.
#
# Rode depois de criar uma versão nova de um diagrama da lista:
#   ./5_DEV/backend/scripts/diagramas-da-doc.sh
# O teste DiagramasDaDocumentacaoTests falha se alguma cópia ficar para trás.
# A imagem do backend só enxerga 5_DEV/backend/, por isso a cópia é versionada.
set -euo pipefail

raiz=$(cd "$(dirname "$0")/../../.." && pwd)
origem="$raiz/2_Diagramas"
destino="$raiz/5_DEV/backend/ms_mono/wwwroot/doc/api/diagramas"

# legenda → módulo que a mostra (o guia de cada módulo cita as dele)
legendas=(
  autenticacao-casos-de-uso autenticacao-sequencia autenticacao-contrato-api autenticacao-classes autenticacao-documentos
  identidade-acesso usuarios-sequencia usuario-inicializacao-sequencia usuarios-contrato-api identidade-acesso-classes usuario-hexagonal identidade-acesso-documentos
  autorizacao-casos-de-uso autorizacao-sequencia autorizacao-contrato-api autorizacao-classes autorizacao-documentos
)

mkdir -p "$destino"
rm -f "$destino"/*.svg
for legenda in "${legendas[@]}"; do
  ultima=$(ls "$origem/${legenda}"_v*.svg 2>/dev/null | sort -V | tail -1)
  [ -n "$ultima" ] || { echo "✗ nenhuma versão de $legenda em 2_Diagramas/" >&2; exit 1; }
  cp "$ultima" "$destino/$legenda.svg"
  echo "✓ $(basename "$ultima") → doc/api/diagramas/$legenda.svg"
done
