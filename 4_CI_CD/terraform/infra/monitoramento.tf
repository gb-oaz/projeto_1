# =============================================================================
# Monitoramento e custos — RNF-81 a RNF-85
# =============================================================================

resource "azurerm_log_analytics_workspace" "log" {
  name                = "${local.n}-log"
  location            = var.regiao
  resource_group_name = data.azurerm_resource_group.rg.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  daily_quota_gb      = var.log_limite_diario_gb
  tags                = local.tags
}

resource "azurerm_application_insights" "appi" {
  name                = "${local.n}_appi"
  location            = var.regiao
  resource_group_name = data.azurerm_resource_group.rg.name
  workspace_id        = azurerm_log_analytics_workspace.log.id
  application_type    = "web"
  tags                = local.tags
}

# Orçamento mensal do grupo de recursos, com alertas em 80 % e 100 % (RNF-84)
resource "azurerm_consumption_budget_resource_group" "orcamento" {
  name              = "${local.n}_budget"
  resource_group_id = data.azurerm_resource_group.rg.id
  amount            = var.orcamento_mensal
  time_grain        = "Monthly"

  time_period {
    start_date = formatdate("YYYY-MM-01'T'00:00:00Z", timestamp())
  }

  dynamic "notification" {
    for_each = [80, 100]
    content {
      enabled        = true
      threshold      = notification.value
      operator       = "GreaterThanOrEqualTo"
      threshold_type = "Actual"
      contact_emails = [var.alerta_email]
    }
  }

  # A data de início é fixada na criação; não muda a cada execução
  lifecycle {
    ignore_changes = [time_period]
  }
}

# prd: logs do Application Gateway e da WAF (RNF-81)
resource "azurerm_monitor_diagnostic_setting" "appgw" {
  count                      = local.prd ? 1 : 0
  name                       = "logs"
  target_resource_id         = azurerm_application_gateway.agw[0].id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.log.id

  enabled_log {
    category_group = "allLogs"
  }
}
