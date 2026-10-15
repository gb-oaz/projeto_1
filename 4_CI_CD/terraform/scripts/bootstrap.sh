#!/usr/bin/env bash
# =============================================================================
# Preparação única de um ambiente na Azure, antes do primeiro terraform apply
# Requisito: 1_Requisitos/05_cd-implantacao.html (RNF-52, RNF-56, RNF-90)
#
# Uso:   ./4_CI_CD/terraform/scripts/bootstrap.sh hml
#        ./4_CI_CD/terraform/scripts/bootstrap.sh prd
#
# Pré-requisito: az login com uma conta Owner da assinatura e com permissão de
# criar App Registrations no Entra ID. A assinatura e o grupo de recursos já
# existem (criados à mão, RNF-90).
#
# O que faz (pode ser rodado de novo: o que já existe é reaproveitado):
#   1. registra os resource providers que o Terraform usa
#   2. cria a storage account e o container do estado do Terraform (RNF-56)
#   3. cria a identidade do CD (App Registration) com credencial federada
#      para o environment azure-<amb> do GitHub — login OIDC, sem segredo
#   4. dá à identidade os papéis no grupo de recursos e no estado
#   5. imprime as variáveis a cadastrar no environment do GitHub
# =============================================================================
set -euo pipefail

AMB="${1:-}"
case "$AMB" in
  hml | prd) ;;
  *) echo "uso: $0 hml|prd" >&2; exit 1 ;;
esac

# Nenhum ID fica no repositório: tudo vem do az login e do git remote
SUB=$(az account list --query "[?name=='deb-boaz-app-$AMB'].id | [0]" -o tsv)
[ -n "$SUB" ] || { echo "assinatura deb-boaz-app-$AMB não encontrada no az login" >&2; exit 1; }
TENANT=$(az account list --query "[?id=='$SUB'].tenantId | [0]" -o tsv)
REPO=$(git config --get remote.origin.url | sed -E 's#^(git@github.com:|https://github.com/)##; s#\.git$##')
REGIAO=brazilsouth
RG="deb-boaz-app-${AMB}_rg"
ST="debboazapp${AMB}tfstate"
APP_NOME="deb-boaz-app-${AMB}_cd"
ENV_GH="azure-${AMB}"

az account set --subscription "$SUB"
az group show -n "$RG" -o none # falha se o grupo não existir
SCOPE="/subscriptions/$SUB/resourceGroups/$RG"

echo "▶ 1/4 resource providers"
for p in Microsoft.App Microsoft.DocumentDB Microsoft.Cache Microsoft.KeyVault \
         Microsoft.Network Microsoft.OperationalInsights Microsoft.Insights \
         Microsoft.ManagedIdentity Microsoft.Storage Microsoft.Consumption; do
  az provider register -n "$p" -o none 2>/dev/null
done
# Numa assinatura nova o registro leva minutos; sem Microsoft.Storage pronto,
# a criação da storage account falha com SubscriptionNotFound.
until [ "$(az provider show -n Microsoft.Storage --query registrationState -o tsv)" = Registered ]; do
  echo "  aguardando o registro de Microsoft.Storage…"; sleep 15
done

echo "▶ 2/4 estado do Terraform: $ST/tfstate"
if ! az storage account show -n "$ST" -g "$RG" -o none 2>/dev/null; then
  az storage account create -n "$ST" -g "$RG" -l "$REGIAO" --sku Standard_LRS \
    --kind StorageV2 --min-tls-version TLS1_2 --allow-blob-public-access false \
    --allow-shared-key-access false \
    --tags projeto=boaz ambiente="$AMB" gerenciado-por=bootstrap -o none
fi
ST_ID=$(az storage account show -n "$ST" -g "$RG" --query id -o tsv)
az storage account blob-service-properties update --account-name "$ST" -g "$RG" \
  --enable-versioning true --enable-delete-retention true --delete-retention-days 30 -o none

echo "▶ 3/4 identidade do CD: $APP_NOME (OIDC → environment $ENV_GH)"
APP_ID=$(az ad app list --display-name "$APP_NOME" --query "[0].appId" -o tsv)
if [ -z "$APP_ID" ]; then
  APP_ID=$(az ad app create --display-name "$APP_NOME" --query appId -o tsv)
fi
az ad sp show --id "$APP_ID" -o none 2>/dev/null || az ad sp create --id "$APP_ID" -o none
SP_ID=$(az ad sp show --id "$APP_ID" --query id -o tsv)
if [ -z "$(az ad app federated-credential list --id "$APP_ID" --query "[?name=='github-$ENV_GH'].name" -o tsv)" ]; then
  az ad app federated-credential create --id "$APP_ID" -o none --parameters "{
    \"name\": \"github-$ENV_GH\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:$REPO:environment:$ENV_GH\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }"
fi

echo "▶ 4/4 papéis"
papel() { # <papel> <escopo>
  az role assignment create --assignee-object-id "$SP_ID" --assignee-principal-type ServicePrincipal \
    --role "$1" --scope "$2" -o none 2>/dev/null || true
}
# Usuário que roda o bootstrap também lê/grava o estado (shared key desligada)
EU=$(az ad signed-in-user show --query id -o tsv)
az role assignment create --assignee-object-id "$EU" --assignee-principal-type User \
  --role "Storage Blob Data Contributor" --scope "$ST_ID" -o none 2>/dev/null || true
papel "Contributor" "$SCOPE"                                   # cria os recursos do grupo
papel "Role Based Access Control Administrator" "$SCOPE"       # dá papéis às identidades das aplicações
papel "Storage Blob Data Contributor" "$ST_ID"                 # lê e trava o estado

if ! az storage container show -n tfstate --account-name "$ST" --auth-mode login -o none 2>/dev/null; then
  echo "  aguardando a propagação do papel no estado…"
  for _ in $(seq 1 12); do
    az storage container create -n tfstate --account-name "$ST" --auth-mode login -o none 2>/dev/null && break
    sleep 10
  done
fi

cat <<FIM

✔ $AMB pronto. Cadastre no GitHub (Settings → Environments → $ENV_GH → Variables):
  AZURE_TENANT_ID       = $TENANT
  AZURE_SUBSCRIPTION_ID = $SUB
  AZURE_CLIENT_ID       = $APP_ID
FIM
