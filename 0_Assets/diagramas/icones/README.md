# Ícones de cloud

Gerenciado pelo `./boaz.sh init` (copia só o que falta; ícones que você adicionar aqui são mantidos).
Todos em `.svg`, nomes em kebab-case.

```
icones/
├── aws/                       # AWS Architecture Icons (pacote oficial 07/2025)
│   ├── servicos/<categoria>/  # um por serviço (64px) — amazon-ec2.svg, aws-lambda.svg
│   ├── recursos/<categoria>/  # recursos de um serviço (48px) — amazon-s3-bucket.svg
│   ├── grupos/                # fronteiras: region, aws-cloud, virtual-private-cloud-vpc, subnets
│   └── categorias/            # ícone por categoria — compute.svg, database.svg
└── gcp/                       # Google Cloud icons
    ├── produtos/              # ícones atuais dos produtos principais — bigquery.svg, gke.svg
    ├── categorias/            # ícone por categoria — compute.svg, databases.svg
    └── legado/                # conjunto completo antigo — cloud-functions.svg, pubsub.svg
└── azure/                     # Azure Public Service Icons (V24)
    └── <categoria>/           # compute/, databases/, networking/, general/… — virtual-machine.svg, kubernetes-services.svg
```

Procure por nome em vez de listar tudo: `find 0_Assets/diagramas/icones -iname '*lambda*'`.
