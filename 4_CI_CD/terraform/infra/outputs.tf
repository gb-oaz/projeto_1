# =============================================================================
# Saídas — lidas pelo CD
# =============================================================================

# Tag implantada de cada aplicação: o CD a reaproveita para quem não tem imagem
# nova na execução (RN-21)
output "imagens" {
  value = { for a in local.apps : a => local.tag[a] }
}

# Endereço de cada aplicação: público em hml, interno à VNet em prd
output "enderecos" {
  value = { for a in local.apps : a => "https://${azurerm_container_app.app[a].ingress[0].fqdn}" }
}

output "ip_publico" {
  description = "IP público do Application Gateway (prd)"
  value       = local.prd ? azurerm_public_ip.pip[0].ip_address : null
}
