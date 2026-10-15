# =============================================================================
# Base: nomes, tags e o grupo de recursos existente
#   RN-17   assinatura e grupo de recursos por ambiente
#   RNF-88  nome deb-boaz-app-<amb>_<sufixo>; "-" onde a Azure não aceita "_"
#   RNF-90  grupo de recursos criado fora do Terraform: só é lido
#   RNF-85  tags para filtrar os custos
# =============================================================================

locals {
  prd = var.ambiente == "prd"
  n   = "deb-boaz-app-${var.ambiente}" # prefixo de todos os nomes

  tags = {
    projeto        = "boaz"
    ambiente       = var.ambiente
    gerenciado-por = "terraform"
  }

  # Faixas das subnets dentro da VNet /16 (RNF-60)
  snet_aca   = cidrsubnet(var.vnet_cidr, 7, 0)   # x.x.0.0/23
  snet_pe    = cidrsubnet(var.vnet_cidr, 11, 16) # x.x.2.0/27
  snet_appgw = cidrsubnet(var.vnet_cidr, 8, 3)   # x.x.3.0/24
}

data "azurerm_client_config" "atual" {}

data "azurerm_resource_group" "rg" {
  name = "${local.n}_rg"
}
