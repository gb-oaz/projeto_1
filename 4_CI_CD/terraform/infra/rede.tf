# =============================================================================
# Rede — RNF-59 a RNF-63
#   VNet própria por ambiente; subnets snet-aca, snet-pe e (prd) snet-appgw;
#   Private DNS para os private endpoints; NSGs.
# =============================================================================

resource "azurerm_virtual_network" "vnet" {
  name                = "${local.n}_vnet"
  location            = var.regiao
  resource_group_name = data.azurerm_resource_group.rg.name
  address_space       = [var.vnet_cidr]
  tags                = local.tags
}

# Ambiente do Container Apps com workload profiles: subnet delegada
resource "azurerm_subnet" "aca" {
  name                 = "${local.n}_snet-aca"
  resource_group_name  = data.azurerm_resource_group.rg.name
  virtual_network_name = azurerm_virtual_network.vnet.name
  address_prefixes     = [local.snet_aca]

  delegation {
    name = "container-apps"
    service_delegation {
      name    = "Microsoft.App/environments"
      actions = ["Microsoft.Network/virtualNetworks/subnets/join/action"]
    }
  }
}

resource "azurerm_subnet" "pe" {
  name                 = "${local.n}_snet-pe"
  resource_group_name  = data.azurerm_resource_group.rg.name
  virtual_network_name = azurerm_virtual_network.vnet.name
  address_prefixes     = [local.snet_pe]

  # Faz o NSG valer também para o tráfego dos private endpoints
  private_endpoint_network_policies = "Enabled"
}

resource "azurerm_subnet" "appgw" {
  count                = local.prd ? 1 : 0
  name                 = "${local.n}_snet-appgw"
  resource_group_name  = data.azurerm_resource_group.rg.name
  virtual_network_name = azurerm_virtual_network.vnet.name
  address_prefixes     = [local.snet_appgw]
}

# ── NSG dos private endpoints: entrada só de snet-aca (RNF-63) ───────────────

resource "azurerm_network_security_group" "pe" {
  name                = "${local.n}_nsg-pe"
  location            = var.regiao
  resource_group_name = data.azurerm_resource_group.rg.name
  tags                = local.tags

  security_rule {
    name                       = "permite-snet-aca"
    priority                   = 100
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_address_prefix      = local.snet_aca
    source_port_range          = "*"
    destination_address_prefix = local.snet_pe
    destination_port_ranges    = ["443", "10000", "10255"]
  }

  security_rule {
    name                       = "nega-resto-da-vnet"
    priority                   = 4000
    direction                  = "Inbound"
    access                     = "Deny"
    protocol                   = "*"
    source_address_prefix      = "VirtualNetwork"
    source_port_range          = "*"
    destination_address_prefix = "*"
    destination_port_range     = "*"
  }
}

resource "azurerm_subnet_network_security_group_association" "pe" {
  subnet_id                 = azurerm_subnet.pe.id
  network_security_group_id = azurerm_network_security_group.pe.id
}

# ── NSG do Application Gateway: HTTP/HTTPS e gerenciamento (RNF-63) ──────────

resource "azurerm_network_security_group" "appgw" {
  count               = local.prd ? 1 : 0
  name                = "${local.n}_nsg-appgw"
  location            = var.regiao
  resource_group_name = data.azurerm_resource_group.rg.name
  tags                = local.tags

  # 80 só redireciona para 443 (RNF-74)
  security_rule {
    name                       = "permite-web"
    priority                   = 100
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_address_prefix      = "Internet"
    source_port_range          = "*"
    destination_address_prefix = "*"
    destination_port_ranges    = ["80", "443"]
  }

  # Exigido pelo Application Gateway v2
  security_rule {
    name                       = "permite-gateway-manager"
    priority                   = 110
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_address_prefix      = "GatewayManager"
    source_port_range          = "*"
    destination_address_prefix = "*"
    destination_port_range     = "65200-65535"
  }

  security_rule {
    name                       = "permite-azure-load-balancer"
    priority                   = 120
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "*"
    source_address_prefix      = "AzureLoadBalancer"
    source_port_range          = "*"
    destination_address_prefix = "*"
    destination_port_range     = "*"
  }
}

resource "azurerm_subnet_network_security_group_association" "appgw" {
  count                     = local.prd ? 1 : 0
  subnet_id                 = azurerm_subnet.appgw[0].id
  network_security_group_id = azurerm_network_security_group.appgw[0].id
}

# ── Private DNS dos private endpoints (RNF-61) ────────────────────────────────

locals {
  zonas_dns = {
    kv     = "privatelink.vaultcore.azure.net"
    cosmos = "privatelink.mongo.cosmos.azure.com"
    redis  = "privatelink.redis.azure.net"
  }
}

resource "azurerm_private_dns_zone" "zona" {
  for_each            = local.zonas_dns
  name                = each.value
  resource_group_name = data.azurerm_resource_group.rg.name
  tags                = local.tags
}

resource "azurerm_private_dns_zone_virtual_network_link" "zona" {
  for_each            = local.zonas_dns
  name                = "${local.n}_vnet"
  private_dns_zone_id = azurerm_private_dns_zone.zona[each.key].id
  virtual_network_id  = azurerm_virtual_network.vnet.id
  tags                = local.tags
}

# Um private endpoint por serviço (RNF-61): _pe-kv, _pe-cosmos, _pe-redis.
# Desligado, nenhum: cobram por hora e as aplicações estão em zero (RN-26)
locals {
  private_endpoints = var.ligado ? {
    kv     = { recurso = azurerm_key_vault.kv.id, subrecurso = "vault" }
    cosmos = { recurso = azurerm_cosmosdb_account.cosmos.id, subrecurso = "MongoDB" }
    redis  = { recurso = azurerm_managed_redis.redis[0].id, subrecurso = "redisEnterprise" }
  } : {}
}

resource "azurerm_private_endpoint" "pe" {
  for_each            = local.private_endpoints
  name                = "${local.n}_pe-${each.key}"
  location            = var.regiao
  resource_group_name = data.azurerm_resource_group.rg.name
  subnet_id           = azurerm_subnet.pe.id
  tags                = local.tags

  private_service_connection {
    name                           = "${local.n}_pe-${each.key}"
    private_connection_resource_id = each.value.recurso
    subresource_names              = [each.value.subrecurso]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "padrao"
    private_dns_zone_ids = [azurerm_private_dns_zone.zona[each.key].id]
  }
}
