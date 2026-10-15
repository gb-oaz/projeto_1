# Homologação — uma unidade de cada componente, sem escala para carga (RN-22)
ambiente  = "hml"
vnet_cidr = "10.10.0.0/16"

escala = {
  min_replicas            = 1 # nunca escala a zero (RN-22)
  max_replicas            = 2
  cpu                     = 0.25
  memoria                 = "0.5Gi"
  requisicoes_simultaneas = 30
}

cosmos_max_ru        = 0 # serverless (RNF-70)
log_limite_diario_gb = 1 # RNF-83
orcamento_mensal     = 300
