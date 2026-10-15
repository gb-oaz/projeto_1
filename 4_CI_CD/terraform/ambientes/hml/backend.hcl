# Estado de hml (RNF-56) — terraform -chdir=infra init -backend-config=../ambientes/hml/backend.hcl
resource_group_name  = "deb-boaz-app-hml_rg"
storage_account_name = "debboazapphmltfstate"
container_name       = "tfstate"
key                  = "hml.tfstate"
use_azuread_auth     = true
