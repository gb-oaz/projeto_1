# Terraform · Azure

Infraestrutura dos ambientes de homologação (`hml`) e produção (`prd`), conforme
[`1_Requisitos/05_cd-implantacao.html`](../../1_Requisitos/05_cd-implantacao.html).
Diagramas em `3_Arquitetura/implantacao-hml_*`, `implantacao-prd_*` e `pipeline-cd_*`.

```
4_CI_CD/terraform/
├── infra/                    módulo raiz único, sem módulos próprios (RNF-55)
│   ├── versions.tf           Terraform, providers e backend
│   ├── variables.tf          variáveis (valores em ambientes/<amb>/)
│   ├── main.tf               nomes, tags e o grupo de recursos existente
│   ├── rede.tf               VNet, subnets, NSGs, Private DNS e private endpoints
│   ├── dados.tf              Cosmos DB for MongoDB e Azure Managed Redis
│   ├── segredos.tf           Key Vault, identidade gerenciada e segredos
│   ├── aplicacoes.tf         ambiente e aplicações do Container Apps
│   ├── borda.tf              Application Gateway, WAF e IP público (só prd)
│   ├── monitoramento.tf      Log Analytics, Application Insights e orçamento
│   ├── outputs.tf            saídas lidas pelo CD
│   └── .terraform.lock.hcl   versões exatas dos providers (Linux e macOS)
├── ambientes/                o que muda entre os ambientes (RNF-58)
│   ├── hml/
│   │   ├── backend.hcl       onde fica o estado (RNF-56)
│   │   └── variaveis.tfvars  escala, rede, orçamento
│   └── prd/
│       ├── backend.hcl
│       └── variaveis.tfvars
└── scripts/
    └── bootstrap.sh          preparação única de cada assinatura
```

Nenhum segredo nem ID da Azure fica no repositório (público): a assinatura vem
de `ARM_SUBSCRIPTION_ID`, os segredos de variáveis `TF_VAR_*` e o bootstrap
descobre os IDs pelo `az login`. Valores locais vão num `*.local.tfvars`, que o
`.gitignore` da raiz mantém fora do git.

## Preparação única

Assinaturas `deb-boaz-app-<amb>` e grupos `deb-boaz-app-<amb>_rg` criados à mão
(RNF-90). Depois, com `az login`:

```bash
./4_CI_CD/terraform/scripts/bootstrap.sh hml
./4_CI_CD/terraform/scripts/bootstrap.sh prd
```

O script cria a storage account do estado, a identidade do CD com login OIDC e
os papéis, e imprime as variáveis do environment `azure-<amb>` do GitHub.

No GitHub (Settings → Environments → `azure-hml` e `azure-prd`):

- variáveis `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_CLIENT_ID`
- secret `DOCKERHUB_READ_TOKEN` (token só de leitura do Docker Hub)
- Deployment branches: `master`

E no repositório: variável `ALERTA_EMAIL` (alertas de orçamento) e, opcional,
`APROVADORES` (quem pode aprovar a implantação; padrão `GustavoBoaz`).

## Rodar localmente

O CD é o caminho normal (`.github/workflows/cd.yml`). Para conferir um plano:

```bash
AMB=hml
export ARM_SUBSCRIPTION_ID=$(az account list --query "[?name=='deb-boaz-app-$AMB'].id | [0]" -o tsv)
export TF_VAR_dockerhub_token=...  TF_VAR_alerta_email=...

terraform -chdir=4_CI_CD/terraform/infra init -reconfigure -backend-config=../ambientes/$AMB/backend.hcl
terraform -chdir=4_CI_CD/terraform/infra plan -var-file=../ambientes/$AMB/variaveis.tfvars
```

## O que as imagens precisam oferecer

- escutar HTTP na porta de `var.porta` (8080 por padrão)
- responder `200` em `GET /health`: é a verificação de prontidão (RNF-54) e a
  sonda do Application Gateway
- web: encaminhar `/api/*` para `BACKEND_URL` (RNF-76)
- backend: ler `ConnectionStrings__Mongo`, `ConnectionStrings__Redis`,
  `APPLICATIONINSIGHTS_CONNECTION_STRING` e publicar o Swagger só quando
  `Swagger__Habilitado=true` (RNF-73); validar o token de `X-App-Attestation`
  em `/mobile/*` (RNF-78)

Enquanto uma imagem não foi publicada, a aplicação roda uma imagem provisória da
Microsoft, na porta 80.

## Domínio de produção (Em aberto)

Sem `dominio`, o Application Gateway atende HTTP no IP público e encaminha
`/mobile/*` ao backend e o resto à landing page. Para HTTPS, importe o
certificado no Key Vault de produção e preencha `dominio` e
`certificado_kv_secret_id` em `ambientes/prd/variaveis.tfvars`: surgem
`<dominio>`, `hub.<dominio>` e `quest.<dominio>`, com HTTP redirecionado para
HTTPS.
