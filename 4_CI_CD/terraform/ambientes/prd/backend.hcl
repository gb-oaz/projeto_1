# Estado de prd (RNF-56) — terraform -chdir=infra init -backend-config=../ambientes/prd/backend.hcl
resource_group_name  = "deb-boaz-app-prd_rg"
storage_account_name = "debboazappprdtfstate"
container_name       = "tfstate"
key                  = "prd.tfstate"
use_azuread_auth     = true
