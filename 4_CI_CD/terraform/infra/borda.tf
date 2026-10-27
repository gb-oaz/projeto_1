# =============================================================================
# Borda de produção — RNF-74 a RNF-80
#   Application Gateway WAF_v2 com IP público: o único ponto público.
#   Sem domínio (Em aberto): um listener HTTP no IP público.
#   Com domínio: HTTPS por host (<dominio>, hub.<dominio>, quest.<dominio>),
#   certificado do Key Vault e HTTP → HTTPS.
#   Em todos os hosts, /mobile/* vai ao backend (RNF-77); o resto, à web.
# =============================================================================

locals {
  com_dominio = var.dominio != ""

  # site → aplicação web e host
  sites = local.com_dominio ? {
    landing = { app = "frontend-landing-page", host = var.dominio }
    hub     = { app = "frontend-hub", host = "hub.${var.dominio}" }
    quest   = { app = "frontend-quest", host = "quest.${var.dominio}" }
    } : {
    landing = { app = "frontend-landing-page", host = null }
  }
}

resource "azurerm_public_ip" "pip" {
  count               = local.prd ? 1 : 0
  name                = "${local.n}_pip"
  location            = var.regiao
  resource_group_name = data.azurerm_resource_group.rg.name
  allocation_method   = "Static"
  sku                 = "Standard"
  tags                = local.tags
}

# ── WAF (RNF-77, RNF-79) ──────────────────────────────────────────────────────

resource "azurerm_web_application_firewall_policy" "waf" {
  count               = local.prd ? 1 : 0
  name                = "${local.n}_waf"
  location            = var.regiao
  resource_group_name = data.azurerm_resource_group.rg.name
  tags                = local.tags

  policy_settings {
    enabled = true
    mode    = "Prevention"
  }

  managed_rules {
    managed_rule_set {
      type    = "Microsoft_DefaultRuleSet"
      version = "2.1"
    }
  }

  # Bloqueia o IP acima do limite de requisições por minuto
  custom_rules {
    name                 = "LimitePorIp"
    priority             = 10
    rule_type            = "RateLimitRule"
    action               = "Block"
    rate_limit_duration  = "OneMin"
    rate_limit_threshold = var.waf_limite_por_minuto
    group_rate_limit_by  = "ClientAddr"

    match_conditions {
      match_variables {
        variable_name = "RemoteAddr"
      }
      operator     = "IPMatch"
      match_values = ["0.0.0.0/0"]
    }
  }

  # A documentação da API (/doc/api) não existe em produção (RNF-73, RNF-79)
  custom_rules {
    name      = "BloqueiaDocumentacaoApi"
    priority  = 20
    rule_type = "MatchRule"
    action    = "Block"

    match_conditions {
      match_variables {
        variable_name = "RequestUri"
      }
      operator     = "Contains"
      match_values = ["/doc/api"]
      transforms   = ["Lowercase"]
    }
  }

  # /mobile/* sem o cabeçalho de atestação do app
  custom_rules {
    name      = "MobileSemAtestacao"
    priority  = 30
    rule_type = "MatchRule"
    action    = "Block"

    match_conditions {
      match_variables {
        variable_name = "RequestUri"
      }
      operator     = "BeginsWith"
      match_values = ["/mobile/"]
      transforms   = ["Lowercase"]
    }

    match_conditions {
      match_variables {
        variable_name = "RequestHeaders"
        selector      = "X-App-Attestation"
      }
      operator           = "Any"
      negation_condition = true
    }
  }
}

# ── Application Gateway (RNF-74, RNF-80) ──────────────────────────────────────

resource "azurerm_application_gateway" "agw" {
  count               = local.prd ? 1 : 0
  name                = "${local.n}_agw"
  location            = var.regiao
  resource_group_name = data.azurerm_resource_group.rg.name
  firewall_policy_id  = azurerm_web_application_firewall_policy.waf[0].id
  tags                = local.tags

  sku {
    name = "WAF_v2"
    tier = "WAF_v2"
  }

  autoscale_configuration {
    min_capacity = 0
    max_capacity = var.appgw_capacidade_max
  }

  # Lê o certificado TLS do Key Vault (RNF-80)
  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.id.id]
  }

  gateway_ip_configuration {
    name      = "gateway"
    subnet_id = azurerm_subnet.appgw[0].id
  }

  frontend_ip_configuration {
    name                 = "publico"
    public_ip_address_id = azurerm_public_ip.pip[0].id
  }

  frontend_port {
    name = "http"
    port = 80
  }

  frontend_port {
    name = "https"
    port = 443
  }

  dynamic "ssl_certificate" {
    for_each = local.com_dominio ? [1] : []
    content {
      name                = "dominio"
      key_vault_secret_id = var.certificado_kv_secret_id
    }
  }

  # Um pool, uma configuração HTTP e uma sonda por aplicação, pelo FQDN interno
  dynamic "backend_address_pool" {
    for_each = local.apps
    content {
      name  = backend_address_pool.value
      fqdns = [azurerm_container_app.app[backend_address_pool.value].ingress[0].fqdn]
    }
  }

  dynamic "backend_http_settings" {
    for_each = local.apps
    content {
      name                                = backend_http_settings.value
      protocol                            = "Https"
      port                                = 443
      cookie_based_affinity               = "Disabled"
      request_timeout                     = 30
      pick_host_name_from_backend_address = true
      probe_name                          = backend_http_settings.value
    }
  }

  dynamic "probe" {
    for_each = local.apps
    content {
      name                                      = probe.value
      protocol                                  = "Https"
      path                                      = local.tag[probe.value] == "" ? "/" : "/health"
      interval                                  = 30
      timeout                                   = 30
      unhealthy_threshold                       = 3
      pick_host_name_from_backend_http_settings = true
      match {
        status_code = ["200-399"]
      }
    }
  }

  # Listener de cada site: HTTPS com domínio, HTTP sem domínio
  dynamic "http_listener" {
    for_each = local.sites
    content {
      name                           = http_listener.key
      frontend_ip_configuration_name = "publico"
      frontend_port_name             = local.com_dominio ? "https" : "http"
      protocol                       = local.com_dominio ? "Https" : "Http"
      host_name                      = http_listener.value.host
      ssl_certificate_name           = local.com_dominio ? "dominio" : null
    }
  }

  # Com domínio: HTTP de cada site redireciona para o HTTPS (RNF-74)
  dynamic "http_listener" {
    for_each = local.com_dominio ? local.sites : {}
    content {
      name                           = "${http_listener.key}-http"
      frontend_ip_configuration_name = "publico"
      frontend_port_name             = "http"
      protocol                       = "Http"
      host_name                      = http_listener.value.host
    }
  }

  dynamic "redirect_configuration" {
    for_each = local.com_dominio ? local.sites : {}
    content {
      name                 = "${redirect_configuration.key}-https"
      redirect_type        = "Permanent"
      target_listener_name = redirect_configuration.key
      include_path         = true
      include_query_string = true
    }
  }

  # /mobile/* → backend; o resto → aplicação web do site (RNF-76, RNF-77)
  dynamic "url_path_map" {
    for_each = local.sites
    content {
      name                               = url_path_map.key
      default_backend_address_pool_name  = url_path_map.value.app
      default_backend_http_settings_name = url_path_map.value.app

      path_rule {
        name                       = "mobile"
        paths                      = ["/mobile/*"]
        backend_address_pool_name  = "backend-mono"
        backend_http_settings_name = "backend-mono"
      }
    }
  }

  dynamic "request_routing_rule" {
    for_each = { for i, k in keys(local.sites) : k => i }
    content {
      name               = request_routing_rule.key
      rule_type          = "PathBasedRouting"
      http_listener_name = request_routing_rule.key
      url_path_map_name  = request_routing_rule.key
      priority           = 100 + request_routing_rule.value
    }
  }

  dynamic "request_routing_rule" {
    for_each = local.com_dominio ? { for i, k in keys(local.sites) : k => i } : {}
    content {
      name                        = "${request_routing_rule.key}-http"
      rule_type                   = "Basic"
      http_listener_name          = "${request_routing_rule.key}-http"
      redirect_configuration_name = "${request_routing_rule.key}-https"
      priority                    = 200 + request_routing_rule.value
    }
  }

  lifecycle {
    precondition {
      condition     = !local.com_dominio || var.certificado_kv_secret_id != ""
      error_message = "Com domínio, informe certificado_kv_secret_id (certificado TLS no Key Vault)."
    }
  }

  depends_on = [
    azurerm_subnet_network_security_group_association.appgw,
    azurerm_private_dns_zone_virtual_network_link.cae,
    azurerm_private_dns_a_record.cae,
    time_sleep.rbac_kv,
  ]
}
