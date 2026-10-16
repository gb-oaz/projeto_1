# =============================================================================
# Dados e cache — RNF-62, RNF-70, RNF-71, RNF-87
# =============================================================================

# Cosmos DB for MongoDB (RU): serverless em hml, autoscale no banco em prd
resource "azurerm_cosmosdb_account" "cosmos" {
  name                          = "${local.n}-cosmos"
  location                      = var.regiao
  resource_group_name           = data.azurerm_resource_group.rg.name
  offer_type                    = "Standard"
  kind                          = "MongoDB"
  mongo_server_version          = var.mongo_versao
  public_network_access_enabled = false # RNF-62
  minimal_tls_version           = "Tls12"
  tags                          = local.tags

  capabilities {
    name = "EnableMongo"
  }

  dynamic "capabilities" {
    for_each = var.cosmos_max_ru == 0 ? ["EnableServerless"] : []
    content {
      name = capabilities.value
    }
  }

  consistency_policy {
    consistency_level = "Session"
  }

  geo_location {
    location          = var.regiao
    failover_priority = 0
  }
}

resource "azurerm_cosmosdb_mongo_database" "boaz" {
  name                = "boaz"
  resource_group_name = data.azurerm_resource_group.rg.name
  account_name        = azurerm_cosmosdb_account.cosmos.name

  dynamic "autoscale_settings" {
    for_each = var.cosmos_max_ru > 0 ? [var.cosmos_max_ru] : []
    content {
      max_throughput = autoscale_settings.value
    }
  }
}

# Azure Managed Redis no menor SKU (RNF-71). Não tem pausa: desligado, é
# removido e volta vazio ao religar — é cache (RN-26)
resource "azurerm_managed_redis" "redis" {
  count                     = var.ligado ? 1 : 0
  name                      = "${local.n}-redis"
  location                  = var.regiao
  resource_group_name       = data.azurerm_resource_group.rg.name
  sku_name                  = var.redis_sku
  high_availability_enabled = false
  public_network_access     = "Disabled" # RNF-62
  tags                      = local.tags

  default_database {
    access_keys_authentication_enabled = true
    client_protocol                    = "Encrypted"
    clustering_policy                  = "EnterpriseCluster"
    eviction_policy                    = "VolatileLRU"
  }
}
