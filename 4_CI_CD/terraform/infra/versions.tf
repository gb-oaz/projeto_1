# =============================================================================
# Versões e provider — requisito 1_Requisitos/05_cd-implantacao.html
#   RNF-55  um módulo raiz, sem módulos próprios; ambientes só nos .tfvars
#   RNF-56  estado na storage account do grupo de recursos de cada ambiente
#   RNF-57  versões fixadas (Stack de desenvolvimento RN-01)
# =============================================================================

terraform {
  required_version = "~> 1.16.6"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 5.9.0"
    }
    time = {
      source  = "hashicorp/time"
      version = "~> 0.14.2"
    }
  }

  # Configuração em ../ambientes/<amb>/backend.hcl (ver README.md)
  backend "azurerm" {}
}

# Login: localmente pelo az login; no CD por OIDC (ARM_USE_OIDC, ARM_CLIENT_ID,
# ARM_TENANT_ID e ARM_SUBSCRIPTION_ID vêm do environment azure-<amb> do GitHub).
# A assinatura vem sempre de ARM_SUBSCRIPTION_ID: nenhum ID fica no repositório.
# Numa assinatura errada o plano falha, porque o grupo deb-boaz-app-<amb>_rg
# só existe na assinatura do próprio ambiente.
provider "azurerm" {
  features {
    key_vault {
      # Em produção o Key Vault tem proteção contra expurgo (RNF-68)
      purge_soft_delete_on_destroy = false
    }
  }
  # Os providers são registrados pelo bootstrap.sh; o CD não tem permissão na assinatura
  resource_provider_registrations = "none"
  storage_use_azuread             = true
}
