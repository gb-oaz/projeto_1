# Terraform e Microsoft Azure

Fonte: `1_Requisitos/05_cd-implantacao.md` (premissas e RN-17 a RNF-90). Código: `4_CI_CD/terraform/` (`infra/` módulos, `ambientes/` por ambiente, `scripts/`).

## Terraform

- Pastas e estado: RNF-55, RNF-56 (estado remoto, um por ambiente, com lock); versões de Terraform e providers fixadas (RNF-57).
- Módulos pequenos por responsabilidade (rede, dados, cache, apps, monitoramento); ambiente só compõe módulos e define variáveis.
- Escala, réplicas e SKUs por variável (RNF-58); nada fixo dentro do módulo.
- Nomes (RNF-88, RNF-89) e tags (RNF-85) por convenção central (`locals`).
- `terraform fmt -check`, `terraform validate` antes de entregar; plano no executor e issue de aprovação por ambiente (RNF-48 a RNF-51).
- Conexões e segredos gerados pelo Terraform vão para o Key Vault (RNF-69); outputs sensíveis com `sensitive = true`.

## Topologia na Azure (resumo do requisito)

| Peça | Serviço | IDs |
|---|---|---|
| Contêineres | Azure Container Apps (ambiente interno em produção) | RNF-64 a RNF-67, RNF-75 |
| Dados | Cosmos DB for MongoDB | RNF-70, RNF-87 |
| Cache | Azure Managed Redis | RNF-71 |
| Rede | VNets, subnets, NSGs, private endpoints, sem acesso público | RNF-59 a RNF-63 |
| Entrada | produção: Application Gateway + WAF; homologação: entrada HTTPS do Container Apps | RNF-74, RNF-79, RNF-80 |
| Segredos | Key Vault por RBAC | RN-24, RNF-68 |
| Monitoramento | Log Analytics, Application Insights, limite de ingestão | RNF-81 a RNF-83 |
| Custo | orçamentos e tags | RNF-84, RNF-85 |
| Região | RNF-86 | |

## Azure Container Apps — boas práticas

- Identidade gerenciada para puxar imagem e ler Key Vault; segredos referenciados do Key Vault, não copiados.
- Sondas de prontidão/vida na rota de saúde (RNF-54, RNF-171).
- Réplicas mínimas: homologação pode escalar a zero quando o requisito permitir (RNF-65); produção conforme RNF-66.
- Revisões: uma ativa por padrão; rollback = ativar a revisão anterior.

## Segurança e custo

- Princípio do menor privilégio no RBAC; OIDC do GitHub com escopo por ambiente (RNF-52).
- Desligar homologação remove o que gera custo (RN-26, RNF-92) e o esvaziamento do banco segue RNF-228.
- Toda adição de recurso pago: estimativa de custo mensal na resposta e na ADR.
