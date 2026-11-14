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

cosmos_max_ru = 0 # serverless (RNF-70)

# Anexos do SAC no armazenamento de objetos, Standard LRS (RNF-235)
armazenamento_objetos     = true
armazenamento_redundancia = "LRS"

# clamd com recarga em série (RN-292): as assinaturas ocupam ~1 GiB e o
# clamd morre por falta de memória com 1Gi; 1.5Gi é o mínimo com folga
verificacao_anexos = {
  cpu     = 0.75
  memoria = "1.5Gi"
}

log_limite_diario_gb = 1 # RNF-83
orcamento_mensal     = 300
