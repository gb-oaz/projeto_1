# =============================================================================
# Verificação dos anexos do SAC — RN-292 (18_modulo-sac)
#   clamd do ClamAV numa aplicação interna do Container Apps, em hml e prd.
#   Só TCP 3310, sem ingresso externo: o backend-mono fala o INSTREAM em
#   Sac__ClamAv = "clamav:3310" (VerificacaoNoClamAv). Sem o clamd, prd
#   recusa todo envio (falha fechada) e hml usa o dublê EICAR.
#   A imagem oficial já traz as assinaturas e o freshclam as atualiza. A
#   configuração vai por variável de ambiente, que o /init da imagem grava
#   no clamd.conf e no freshclam.conf (CLAMD_CONF_* e FRESHCLAM_CONF_*), sem
#   imagem própria: recarga em série (ConcurrentDatabaseReload no), para não
#   ter duas cópias das assinaturas na memória, e sem o teste das assinaturas
#   novas no freshclam (TestDatabases no), que carregaria outra cópia.
#   Recursos por ambiente em var.verificacao_anexos (.tfvars).
# =============================================================================

locals {
  clamav = {
    nome   = "clamav"
    imagem = "docker.io/clamav/clamav:1.5.4"
    porta  = 3310
  }
  clamav_endereco = "${local.clamav.nome}:${local.clamav.porta}"
}

resource "azurerm_container_app" "clamav" {
  name                         = local.clamav.nome
  container_app_environment_id = azurerm_container_app_environment.cae.id
  resource_group_name          = data.azurerm_resource_group.rg.name
  revision_mode                = "Single"
  workload_profile_name        = "Consumption"
  tags                         = local.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.id.id]
  }

  # Pull autenticado no Docker Hub, como as outras aplicações (RNF-67)
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

  # Interno ao ambiente: só as aplicações dele alcançam clamav:3310
  ingress {
    external_enabled = false
    transport        = "tcp"
    target_port      = local.clamav.porta
    exposed_port     = local.clamav.porta

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    # Uma réplica sempre de pé; hml desligado escala a zero (RN-26)
    min_replicas = var.ligado ? 1 : 0
    max_replicas = 1

    container {
      name   = local.clamav.nome
      image  = local.clamav.imagem
      cpu    = var.verificacao_anexos.cpu
      memory = var.verificacao_anexos.memoria

      env {
        name  = "CLAMD_CONF_ConcurrentDatabaseReload"
        value = "no"
      }

      env {
        name  = "FRESHCLAM_CONF_TestDatabases"
        value = "no"
      }

      startup_probe {
        transport               = "TCP"
        port                    = local.clamav.porta
        interval_seconds        = 20
        failure_count_threshold = 30 # 30 × 20 s = 10 min para baixar as assinaturas
      }

      liveness_probe {
        transport               = "TCP"
        port                    = local.clamav.porta
        interval_seconds        = 30
        failure_count_threshold = 3
      }
    }
  }

  depends_on = [time_sleep.rbac_kv]
}
