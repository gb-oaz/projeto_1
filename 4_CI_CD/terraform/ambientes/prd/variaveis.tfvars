# Produção — escala parametrizada (RNF-66)
ambiente  = "prd"
vnet_cidr = "10.20.0.0/16"

escala = {
  min_replicas            = 1
  max_replicas            = 5
  cpu                     = 0.5
  memoria                 = "1Gi"
  requisicoes_simultaneas = 30
}

cosmos_max_ru = 1000 # autoscale de 100 a 1.000 RU/s no banco (RNF-70)

# Anexos do SAC no armazenamento de objetos, Standard LRS, Hot (RNF-235)
armazenamento_objetos     = true
armazenamento_redundancia = "LRS"

# clamd com recarga em série (RN-292); pico medido de ~1,1 GiB
verificacao_anexos = {
  cpu     = 1
  memoria = "2Gi"
}

orcamento_mensal      = 1500
waf_limite_por_minuto = 100
appgw_capacidade_max  = 2

# Em aberto: domínio público e certificado TLS no Key Vault
dominio                  = ""
certificado_kv_secret_id = ""
