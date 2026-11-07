# =============================================================================
# Aplicações — RN-21, RN-22, RNF-54, RNF-64 a RNF-67, RNF-72, RNF-73,
# RNF-75, RNF-76, RNF-89
#   hml: ambiente externo, acessível de qualquer IP pelo domínio padrão
#   prd: ambiente interno, alcançado só pelo Application Gateway
# =============================================================================

resource "azurerm_container_app_environment" "cae" {
  name                           = "${local.n}-cae"
  location                       = var.regiao
  resource_group_name            = data.azurerm_resource_group.rg.name
  logs_destination               = "log-analytics" # azurerm 5: obrigatório junto com o workspace
  log_analytics_workspace_id     = azurerm_log_analytics_workspace.log.id
  infrastructure_subnet_id       = azurerm_subnet.aca.id
  internal_load_balancer_enabled = local.prd
  tags                           = local.tags

  workload_profile {
    name                  = "Consumption"
    workload_profile_type = "Consumption"
  }
}

locals {
  # Uma aplicação por imagem, com o nome da imagem (RNF-89)
  apps = toset(["backend-mono", "frontend-hub", "frontend-landing-page", "frontend-quest"])

  # Enquanto uma imagem não foi publicada, a aplicação roda uma imagem
  # provisória da Microsoft, na porta 80 e sem verificação de prontidão
  provisoria = "mcr.microsoft.com/k8se/quickstart:latest"
  tag        = { for a in local.apps : a => lookup(var.imagens, a, "") }
  imagem     = { for a in local.apps : a => local.tag[a] == "" ? local.provisoria : "docker.io/${var.dockerhub_usuario}/${a}:${local.tag[a]}" }
  porta      = { for a in local.apps : a => local.tag[a] == "" ? 80 : var.porta[a] }

  # Contas iniciais do backend (17: RN-190, RN-265 a RN-267, RNF-225, RNF-226).
  # Segredo do Key Vault → variável de ambiente lida no start. Os valores são
  # gravados à mão no Key Vault de cada ambiente (README) e nunca passam pelo
  # repositório, pela imagem nem pelo pipeline: aqui só há referências. As
  # contas qa existem só em homologação, então os segredos usuario-qa-* não
  # são referenciados em produção. Segredo ausente impede a revisão de subir.
  segredos_usuarios = merge({
    "usuario-admin-entrada" = "USUARIO_ADMIN_ENTRADA"
    "usuario-admin-senha"   = "USUARIO_ADMIN_SENHA"
    }, local.prd ? {} : {
    "usuario-qa-membro-entrada"      = "USUARIO_QA_MEMBRO_ENTRADA"
    "usuario-qa-membro-senha"        = "USUARIO_QA_MEMBRO_SENHA"
    "usuario-qa-master-entrada"      = "USUARIO_QA_MASTER_ENTRADA"
    "usuario-qa-master-senha"        = "USUARIO_QA_MASTER_SENHA"
    "usuario-qa-autonomo-entrada"    = "USUARIO_QA_AUTONOMO_ENTRADA"
    "usuario-qa-autonomo-senha"      = "USUARIO_QA_AUTONOMO_SENHA"
    "usuario-qa-staff-entrada"       = "USUARIO_QA_STAFF_ENTRADA"
    "usuario-qa-staff-senha"         = "USUARIO_QA_STAFF_SENHA"
    "usuario-qa-participante-codigo" = "USUARIO_QA_PARTICIPANTE_CODIGO"
  })
}

resource "azurerm_container_app" "app" {
  for_each                     = local.apps
  name                         = each.key
  container_app_environment_id = azurerm_container_app_environment.cae.id
  resource_group_name          = data.azurerm_resource_group.rg.name
  revision_mode                = "Single"
  workload_profile_name        = "Consumption"
  tags                         = local.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.id.id]
  }

  # Pull autenticado no Docker Hub, com o token só de leitura do Key Vault (RNF-67)
  registry {
    server               = "docker.io"
    username             = var.dockerhub_usuario
    password_secret_name = "dockerhub-token"
  }

  secret {
    name                = "dockerhub-token"
    identity            = azurerm_user_assigned_identity.id.id
    key_vault_secret_id = azurerm_key_vault_secret.segredo["dockerhub-token"].versionless_id
  }

  # Conexões só no backend, lidas do Key Vault (RN-24)
  dynamic "secret" {
    for_each = each.key == "backend-mono" ? ["cosmos-conexao", "redis-conexao"] : []
    content {
      name                = secret.value
      identity            = azurerm_user_assigned_identity.id.id
      key_vault_secret_id = azurerm_key_vault_secret.segredo[secret.value].versionless_id
    }
  }

  # Contas iniciais: referência sem versão ao segredo gravado à mão no Key
  # Vault, para a troca de senha valer no próximo start (RN-267)
  dynamic "secret" {
    for_each = each.key == "backend-mono" ? keys(local.segredos_usuarios) : []
    content {
      name                = secret.value
      identity            = azurerm_user_assigned_identity.id.id
      key_vault_secret_id = "${azurerm_key_vault.kv.vault_uri}secrets/${secret.value}"
    }
  }

  # hml: público (RNF-72). prd: "externo" vale só dentro da VNet, porque o
  # ambiente é interno (RNF-75); o Application Gateway alcança por aqui.
  ingress {
    external_enabled = true
    target_port      = local.porta[each.key]
    transport        = "auto"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    min_replicas = var.ligado ? var.escala.min_replicas : 0 # desligado: escala a zero (RN-26)
    max_replicas = var.escala.max_replicas

    http_scale_rule {
      name                = "requisicoes-simultaneas"
      concurrent_requests = tostring(var.escala.requisicoes_simultaneas)
    }

    container {
      name   = each.key
      image  = local.imagem[each.key]
      cpu    = var.escala.cpu
      memory = var.escala.memoria

      env {
        name  = "AMBIENTE"
        value = var.ambiente
      }

      # Backend: conexões, Application Insights e Swagger só em hml (RNF-73, RNF-79)
      dynamic "env" {
        for_each = each.key == "backend-mono" ? {
          "ConnectionStrings__Mongo" = "cosmos-conexao"
          "ConnectionStrings__Redis" = "redis-conexao"
        } : {}
        content {
          name        = env.key
          secret_name = env.value
        }
      }

      # Contas iniciais: cada segredo vira a variável de ambiente do start
      dynamic "env" {
        for_each = each.key == "backend-mono" ? local.segredos_usuarios : {}
        content {
          name        = env.value
          secret_name = env.key
        }
      }

      dynamic "env" {
        for_each = each.key == "backend-mono" ? {
          "APPLICATIONINSIGHTS_CONNECTION_STRING" = azurerm_application_insights.appi.connection_string
          "Swagger__Habilitado"                   = local.prd ? "false" : "true"
        } : {}
        content {
          name  = env.key
          value = env.value
        }
      }

      # Web: /api/* é encaminhado ao backend pelo endereço interno do ambiente (RNF-76)
      dynamic "env" {
        for_each = each.key == "backend-mono" ? [] : ["http://backend-mono"]
        content {
          name  = "BACKEND_URL"
          value = env.value
        }
      }

      # A revisão nova só recebe tráfego depois de pronta (RNF-54)
      dynamic "readiness_probe" {
        for_each = local.tag[each.key] == "" ? [] : [1]
        content {
          transport               = "HTTP"
          port                    = local.porta[each.key]
          path                    = "/health"
          interval_seconds        = 10
          failure_count_threshold = 30 # 30 × 10 s = 5 min
        }
      }
    }
  }

  depends_on = [time_sleep.rbac_kv]
}

# prd: o domínio interno do ambiente resolve para o IP estático dele (RNF-75)
resource "azurerm_private_dns_zone" "cae" {
  count               = local.prd ? 1 : 0
  name                = azurerm_container_app_environment.cae.default_domain
  resource_group_name = data.azurerm_resource_group.rg.name
  tags                = local.tags
}

resource "azurerm_private_dns_a_record" "cae" {
  count               = local.prd ? 1 : 0
  name                = "*"
  private_dns_zone_id = azurerm_private_dns_zone.cae[0].id
  ttl                 = 300
  records             = [azurerm_container_app_environment.cae.static_ip_address]
}

resource "azurerm_private_dns_zone_virtual_network_link" "cae" {
  count               = local.prd ? 1 : 0
  name                = "${local.n}_vnet"
  private_dns_zone_id = azurerm_private_dns_zone.cae[0].id
  virtual_network_id  = azurerm_virtual_network.vnet.id
  tags                = local.tags
}
