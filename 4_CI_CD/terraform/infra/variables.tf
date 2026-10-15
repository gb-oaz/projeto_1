# =============================================================================
# Variáveis — os valores de cada ambiente ficam em hml.tfvars e prd.tfvars
# (RNF-55, RNF-58). Segredos chegam só por TF_VAR_* no CD (RN-24).
# =============================================================================

# ── ambiente ──────────────────────────────────────────────────────────────────

variable "ambiente" {
  description = "hml ou prd (RN-17)"
  type        = string
  validation {
    condition     = contains(["hml", "prd"], var.ambiente)
    error_message = "ambiente deve ser hml ou prd."
  }
}

variable "regiao" {
  description = "Região de todos os recursos (RNF-86)"
  type        = string
  default     = "brazilsouth"
}

# ── rede (RNF-59, RNF-60) ─────────────────────────────────────────────────────

variable "vnet_cidr" {
  description = "Faixa da VNet: 10.10.0.0/16 (hml) ou 10.20.0.0/16 (prd)"
  type        = string
}

# ── aplicações (RNF-64 a RNF-66, RN-21) ───────────────────────────────────────

variable "imagens" {
  description = "Tag do Docker Hub por aplicação; vazia = imagem provisória até a primeira publicação"
  type        = map(string)
  default     = {}
}

variable "escala" {
  description = "Réplicas e recursos de cada aplicação (RNF-58)"
  type = object({
    min_replicas            = number
    max_replicas            = number
    cpu                     = number
    memoria                 = string
    requisicoes_simultaneas = number # nova réplica acima deste número (RNF-66)
  })
}

variable "porta" {
  description = "Porta HTTP em que cada imagem escuta"
  type        = map(number)
  default = {
    "backend-mono"          = 8080
    "frontend-hub"          = 8080
    "frontend-landing-page" = 8080
    "frontend-quest"        = 8080
  }
}

variable "dockerhub_usuario" {
  description = "Conta do Docker Hub dona das imagens (RNF-67)"
  type        = string
  default     = "gustavoboaz"
}

variable "dockerhub_token" {
  description = "Token só de leitura do Docker Hub (secret DOCKERHUB_READ_TOKEN); vai para o Key Vault"
  type        = string
  sensitive   = true
}

# ── dados (RNF-70, RNF-71, RNF-87) ────────────────────────────────────────────

variable "cosmos_max_ru" {
  description = "Máximo de RU/s do autoscale do banco; 0 = serverless (hml)"
  type        = number
  default     = 0
}

variable "mongo_versao" {
  description = "Versão de protocolo MongoDB do Cosmos DB (a mais alta oferecida)"
  type        = string
  default     = "7.0"
}

variable "redis_sku" {
  description = "SKU do Azure Managed Redis (o menor)"
  type        = string
  default     = "Balanced_B0"
}

# ── borda de produção (RNF-74 a RNF-80) ───────────────────────────────────────

variable "dominio" {
  description = "Domínio público de produção (Em aberto); vazio = só HTTP pelo IP público"
  type        = string
  default     = ""
}

variable "certificado_kv_secret_id" {
  description = "ID (versionless) do certificado TLS no Key Vault; obrigatório quando há domínio"
  type        = string
  default     = ""
}

variable "waf_limite_por_minuto" {
  description = "Requisições por minuto por IP antes do bloqueio (RNF-79)"
  type        = number
  default     = 100
}

variable "appgw_capacidade_max" {
  description = "Máximo de unidades do autoscale do Application Gateway (RNF-74)"
  type        = number
  default     = 2
}

# ── monitoramento e custos (RNF-81 a RNF-85) ──────────────────────────────────

variable "log_limite_diario_gb" {
  description = "Limite diário de ingestão do Log Analytics; -1 = sem limite (RNF-83)"
  type        = number
  default     = -1
}

variable "orcamento_mensal" {
  description = "Orçamento mensal do grupo de recursos, em reais (RNF-84)"
  type        = number
}

variable "alerta_email" {
  description = "E-mail que recebe os alertas de orçamento (variável ALERTA_EMAIL do GitHub)"
  type        = string
}
