# =============================================================================
# Armazenamento de objetos dos anexos do SAC
#   05 RNF-235  ligado ou desligado pela variável armazenamento_objetos
#   05 RNF-236  ciclo de vida dos arquivos, aplicado só pela implantação
#   05 RNF-61   private endpoint (privatelink.blob.core.windows.net), em rede.tf
#   05 RNF-62   sem acesso público e sem chave de conta: só a identidade
#   05 RNF-88   nome debboazapp<amb>anexos (a Azure não aceita "-" nem "_")
#   05 RNF-92   desligar homologação tira o private endpoint; a conta fica
#   18 RNF-233  cada anexo guarda onde foi gravado e é lido de lá
#   18 RNF-234  descarte: 24 h sem uso; hml 30 dias; prd 5 anos do fechamento
# =============================================================================

# A conta já criada continua mesmo com a variável desligada (RNF-235): os
# anexos antigos seguem sendo lidos dela até o ciclo de vida esvaziá-la. A
# tag uso = "anexos-sac" é como o plano a reconhece; apagar a conta é uma
# remoção explícita, feita à parte da implantação (tirar o prevent_destroy,
# terraform state rm e apagar no portal ou pela CLI).
data "azurerm_resources" "arquivos" {
  resource_group_name = data.azurerm_resource_group.rg.name
  type                = "Microsoft.Storage/storageAccounts"
  required_tags       = { uso = "anexos-sac" }
}

locals {
  arquivos_conta     = var.armazenamento_objetos || length(data.azurerm_resources.arquivos.resources) > 0
  arquivos_nome      = "${replace(local.n, "-", "")}anexos" # debboazapphmlanexos
  arquivos_conteiner = "anexos-sac"
}

# Standard (padrão) com LRS e camada Hot nos dois ambientes (RNF-235): o
# volume é pequeno, a leitura acontece na conversa e a guarda de 5 anos de
# produção fica numa região só, como o Cosmos DB e o Redis.
resource "azurerm_storage_account" "arquivos" {
  count                           = local.arquivos_conta ? 1 : 0
  name                            = local.arquivos_nome
  location                        = var.regiao
  resource_group_name             = data.azurerm_resource_group.rg.name
  account_kind                    = "StorageV2"
  account_tier                    = "Standard"
  account_replication_type        = var.armazenamento_redundancia
  access_tier                     = "Hot"
  https_traffic_only_enabled      = true
  min_tls_version                 = "TLS1_2"
  public_network_access           = "Disabled" # RNF-62: só o private endpoint
  allow_nested_items_to_be_public = false
  shared_access_key_enabled       = false # só Entra ID: a identidade da aplicação
  default_to_oauth_authentication = true
  tags                            = merge(local.tags, { uso = "anexos-sac" })

  blob_properties {
    versioning_enabled = false

    # Produção: 7 dias para desfazer uma remoção por engano da aplicação
    dynamic "delete_retention_policy" {
      for_each = local.prd ? [7] : []
      content {
        days = delete_retention_policy.value
      }
    }
  }

  # RNF-235: protegida contra destruição pelo Terraform
  lifecycle {
    prevent_destroy = true
  }
}

# Contêiner privado pelo plano de controle (storage_account_id): o CD roda
# fora da VNet e a conta não tem acesso público
resource "azurerm_storage_container" "anexos" {
  count                 = local.arquivos_conta ? 1 : 0
  name                  = local.arquivos_conteiner
  storage_account_id    = azurerm_storage_account.arquivos[0].id
  container_access_type = "private"
}

# Ciclo de vida (RNF-236). A aplicação marca cada blob com a tag de índice
# "uso": pendente no envio, chamado ao vincular (e ao reabrir), fechado no
# FECHADO, junto com o metadado fechadoem, que renova o Last-Modified. O
# vencimento exato é da aplicação (expiraEm em anexosSac, 404 depois dele);
# a regra roda uma vez por dia e garante que o arquivo sai da conta.
resource "azurerm_storage_management_policy" "arquivos" {
  count              = local.arquivos_conta ? 1 : 0
  storage_account_id = azurerm_storage_account.arquivos[0].id

  # Anexo não usado num chamado ou mensagem em 24 h (18 RN-292, RNF-234)
  rule {
    name    = "anexo-nao-usado"
    enabled = true
    filters {
      blob_types   = ["blockBlob"]
      prefix_match = ["${local.arquivos_conteiner}/"]
      match_blob_index_tag {
        name      = "uso"
        operation = "=="
        value     = "pendente"
      }
    }
    actions {
      base_blob {
        delete_after_days_since_creation_greater_than = 1
      }
    }
  }

  # Homologação: todo arquivo 30 dias depois do envio
  dynamic "rule" {
    for_each = local.prd ? [] : [30]
    content {
      name    = "hml-30-dias"
      enabled = true
      filters {
        blob_types   = ["blockBlob"]
        prefix_match = ["${local.arquivos_conteiner}/"]
      }
      actions {
        base_blob {
          delete_after_days_since_creation_greater_than = rule.value
        }
      }
    }
  }

  # Produção: o arquivo do chamado fechado há 5 anos (18 RNF-231), contados
  # da última modificação, que o fechamento renova (5 × 365 + 1 = 1.826 dias)
  dynamic "rule" {
    for_each = local.prd ? [1826] : []
    content {
      name    = "prd-guarda-5-anos"
      enabled = true
      filters {
        blob_types   = ["blockBlob"]
        prefix_match = ["${local.arquivos_conteiner}/"]
        match_blob_index_tag {
          name      = "uso"
          operation = "=="
          value     = "fechado"
        }
      }
      actions {
        base_blob {
          delete_after_days_since_modification_greater_than = rule.value
        }
      }
    }
  }
}

# A identidade das aplicações lê, grava, apaga e marca a tag "uso" só no
# contêiner dos anexos. Storage Blob Data Owner porque o Contributor não
# tem blobs/tags/write.
resource "azurerm_role_assignment" "arquivos_aplicacoes" {
  count                = local.arquivos_conta ? 1 : 0
  scope                = "${azurerm_storage_account.arquivos[0].id}/blobServices/default/containers/${local.arquivos_conteiner}"
  role_definition_name = "Storage Blob Data Owner"
  principal_id         = azurerm_user_assigned_identity.id.principal_id
  depends_on           = [azurerm_storage_container.anexos]
}

# Configuração passada ao backend-mono (em aplicacoes.tf). Destino diz onde
# os anexos novos são gravados; Blob é o endereço da conta, presente enquanto
# ela existir, para ler os anexos guardados nela. AZURE_CLIENT_ID escolhe a
# identidade atribuída pelo usuário no DefaultAzureCredential.
locals {
  arquivos_config = merge({
    "Sac__Arquivos__Destino"   = var.armazenamento_objetos ? "BLOB" : "GRIDFS"
    "Sac__Arquivos__Conteiner" = local.arquivos_conteiner
    "AZURE_CLIENT_ID"          = azurerm_user_assigned_identity.id.client_id
    }, local.arquivos_conta ? {
    "Sac__Arquivos__Blob" = azurerm_storage_account.arquivos[0].primary_blob_endpoint
  } : {})
}
