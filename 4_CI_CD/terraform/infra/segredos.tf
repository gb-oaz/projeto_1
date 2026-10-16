# =============================================================================
# Segredos e identidade — RN-24, RNF-67 a RNF-69, RNF-80
# =============================================================================

# Identidade das aplicações (e do Application Gateway, para o certificado)
resource "azurerm_user_assigned_identity" "id" {
  name                = "${local.n}_id"
  location            = var.regiao
  resource_group_name = data.azurerm_resource_group.rg.name
  tags                = local.tags
}

# Key Vault Standard com RBAC. O acesso público fica ligado, limitado por RBAC,
# porque o CD roda fora da VNet; as aplicações usam o private endpoint
# (aviso do requisito 05).
resource "azurerm_key_vault" "kv" {
  name                          = "${local.n}-kv"
  location                      = var.regiao
  resource_group_name           = data.azurerm_resource_group.rg.name
  tenant_id                     = data.azurerm_client_config.atual.tenant_id
  sku_name                      = "standard"
  rbac_authorization_enabled    = true
  public_network_access_enabled = true
  soft_delete_retention_days    = local.prd ? 90 : 7
  purge_protection_enabled      = local.prd
  tags                          = local.tags
}

# Quem roda o Terraform (o CD) grava os segredos
resource "azurerm_role_assignment" "kv_terraform" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.atual.object_id
}

# As aplicações e o Application Gateway só leem (RNF-68)
resource "azurerm_role_assignment" "kv_aplicacoes" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.id.principal_id
}

# O papel leva alguns minutos para valer depois de criado
resource "time_sleep" "rbac_kv" {
  depends_on      = [azurerm_role_assignment.kv_terraform, azurerm_role_assignment.kv_aplicacoes]
  create_duration = "90s"
}

# Segredos gravados pelo Terraform a partir dos recursos que ele cria (RNF-69)
locals {
  segredos = {
    "cosmos-conexao" = azurerm_cosmosdb_account.cosmos.primary_mongodb_connection_string
    # Desligado, o segredo continua existindo para as aplicações (em zero réplica)
    "redis-conexao" = var.ligado ? join("", [
      azurerm_managed_redis.redis[0].hostname, ":", azurerm_managed_redis.redis[0].default_database[0].port,
      ",password=", azurerm_managed_redis.redis[0].default_database[0].primary_access_key,
      ",ssl=True,abortConnect=False",
    ]) : "desligado"
    "dockerhub-token" = var.dockerhub_token
  }
}

resource "azurerm_key_vault_secret" "segredo" {
  for_each     = nonsensitive(toset(keys(local.segredos)))
  name         = each.key
  value        = local.segredos[each.key]
  key_vault_id = azurerm_key_vault.kv.id
  depends_on   = [time_sleep.rbac_kv]
}
