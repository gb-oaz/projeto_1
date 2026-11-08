---
projeto: "boaz"
documento: "05_cd-implantacao"
tipo: "Transversal"
titulo: "CD e implantação na Azure"
versao: "v12"
data: "2026-11-07 16:15"
status: "Em revisão"
objetivo: "definir como as imagens publicadas pelo CI chegam, após aprovação manual, aos ambientes de homologação e produção na Azure, descritos em Terraform e com custo mínimo."
---

> **boaz · Transversal** · `05_cd-implantacao` · **v12** · 2026-11-07 16:15 · **Em revisão**

# CD e implantação na Azure

**Objetivo:** definir como as imagens publicadas pelo CI chegam, após aprovação manual, aos ambientes de homologação e produção na Azure, descritos em Terraform e com custo mínimo.

---

<a id="cenarios"></a>

## Cenários

<a id="premissas"></a>

### Premissas

```gherkin
Cenário: segunda aprovação e Azure
  Dado as imagens publicadas no Docker Hub
  Quando o CD pede a segunda aprovação e ela é dada
  Então o Terraform implanta na Azure, em homologação ou em produção

Esquema do Cenário: serviço local e na Azure
  Quando o serviço é <serviço>
  Então localmente é <local> e na Azure é <azure>

  Exemplos:
    | serviço | local   | azure                 |
    | dados   | MongoDB | Cosmos DB for MongoDB |
    | cache   | Redis   | Azure Managed Redis   |

Cenário: o que fica na VNet
  Então na VNet ficam só o Container Apps e os private endpoints
  E, em produção, o Application Gateway
  E Log Analytics, Application Insights e orçamentos não ocupam subnet

Cenário: homologação sem Application Gateway (decisão de 2026-10-13)
  Então homologação usa a entrada HTTPS do Container Apps, sem custo fixo
  E em produção o Application Gateway com WAF é o único ponto público
```

<a id="RN-17"></a>

### `RN-17` Dois ambientes

```gherkin
Esquema do Cenário: assinatura de cada ambiente
  Então <ambiente> fica na assinatura <assinatura>, no grupo <grupo>

  Exemplos:
    | ambiente    | assinatura       | grupo               |
    | homologação | deb-boaz-app-hml | deb-boaz-app-hml_rg |
    | produção    | deb-boaz-app-prd | deb-boaz-app-prd_rg |
```

<a id="RN-18"></a>

### `RN-18` Merge vai a homologação

```gherkin
Cenário: merge do commit a1b2c3d
  Quando o commit a1b2c3d entra na master
  Então homologação recebe as imagens "sha-a1b2c3d"
```

<a id="RN-19"></a>

### `RN-19` Tag vai aos dois

```gherkin
Cenário: tag v1.3.0
  Quando o time cria a tag "v1.3.0"
  Então as imagens "1.3.0" vão primeiro a homologação e depois a produção
```

<a id="RN-26"></a>

### `RN-26` Ligar e desligar homologação

```gherkin
Cenário: homologação desligada
  Dado homologação desligada
  Quando um merge implanta imagens novas
  Então as imagens são atualizadas sem religar homologação
  E dados e configuração continuam guardados
```

<a id="RN-25"></a>

### `RN-25` Produção depois de homologação

```gherkin
Cenário: homologação negada
  Dado a tag "v1.3.0" com a etapa de homologação negada
  Então a tag não chega a produção
```

<a id="RN-20"></a>

### `RN-20` Aprovação por ambiente

```gherkin
Cenário: tag com duas aprovações
  Quando a tag "v1.3.0" é implantada
  Então homologação pede uma aprovação e produção pede outra
  E nenhuma é pedida antes de as imagens estarem publicadas
```

<a id="RN-21"></a>

### `RN-21` Sem imagem nova

```gherkin
Cenário: só o backend mudou
  Quando a execução traz só backend-mono novo
  Então as três aplicações web continuam com a imagem implantada
```

<a id="RN-22"></a>

### `RN-22` Homologação mínima

```gherkin
Cenário: uma unidade de cada
  Dado homologação ligada
  Então cada componente tem pelo menos uma réplica, nunca zero
  E homologação não é dimensionada para carga
```

<a id="RN-23"></a>

### `RN-23` Só pelo Terraform

```gherkin
Cenário: mudança no portal
  Dado alguém mudando a CPU do backend pelo portal da Azure
  Quando a implantação seguinte roda
  Então a mudança manual é desfeita
```

<a id="RN-24"></a>

### `RN-24` Segredo no Key Vault

```gherkin
Esquema do Cenário: onde o segredo não pode ficar
  Quando alguém põe a cadeia de conexão do Cosmos DB <lugar>
  Então a revisão recusa e o segredo vai para o Key Vault do ambiente

  Exemplos:
    | lugar                |
    | no código            |
    | num arquivo .tfvars  |
    | em texto no workflow |
```

<a id="RNF-47"></a>

### `RNF-47` Disparo do CD

```gherkin
Esquema do Cenário: quando o CD roda
  Dado o job de publicação <resultado> em <origem>
  Então o cd.yml <dispara>

  Exemplos:
    | resultado | origem             | dispara  |
    | aprovado  | a master           | dispara  |
    | aprovado  | a tag v1.3.0       | dispara  |
    | reprovado | a master           | não roda |
    | aprovado  | tarefa_42-cadastro | não roda |
```

<a id="RNF-91"></a>

### `RNF-91` Disparo manual

```gherkin
Esquema do Cenário: ações manuais em homologação
  Quando alguém dispara o cd.yml na master com <ação>
  Então homologação <efeito>, depois da aprovação

  Exemplos:
    | ação      | efeito              |
    | implantar | recebe as imagens   |
    | ligar     | volta a funcionar   |
    | desligar  | para de gerar custo |
```

<a id="RNF-92"></a>

### `RNF-92` O que desligar remove

```gherkin
Cenário: desligar homologação
  Quando homologação é desligada
  Então o Redis e os private endpoints são removidos e as aplicações vão a zero réplica
  E Cosmos DB, Key Vault, Log Analytics e a rede continuam
```

<a id="RNF-228"></a>

### `RNF-228` Esvaziar o banco de homologação

> Índices criados na partida: [17_modulo-usuario `RNF-227`](17_modulo-usuario.md#RNF-227)

```gherkin
Cenário: esvaziamento confirmado em homologação
  Dado o dono dispara o cd.yml na master para homologação
  E marca "esvaziar_banco" e digita "esvaziar hml" em "confirmacao"
  Quando a issue de aprovação abre
  Então ela traz em destaque o aviso de que o banco de homologação será esvaziado
  Quando o dono comenta "approve"
  Então todas as coleções do Cosmos DB de homologação são apagadas antes do terraform apply
  E o acesso público ao Cosmos DB continua desabilitado
  E depois do apply a revisão atual do backend-mono é reiniciada
  E a partida recria as coleções, os índices e as contas iniciais
  E a execução falha se o backend-mono não chegar a Running e Healthy em até 5 min

Esquema do Cenário: confirmação errada ou ausente
  Dado o dono dispara o cd.yml na master para homologação
  E <opção> com <texto> em "confirmacao"
  Então <resultado>
  E nenhuma coleção é apagada

  Exemplos:
    | opção                      | texto          | resultado                                   |
    | marca "esvaziar_banco"     | "esvaziar"     | o job preparar falha antes da aprovação     |
    | marca "esvaziar_banco"     | "ESVAZIAR HML" | o job preparar falha antes da aprovação     |
    | marca "esvaziar_banco"     | o campo vazio  | o job preparar falha antes da aprovação     |
    | não marca "esvaziar_banco" | "esvaziar hml" | segue a ação escolhida, sem esvaziar        |

Cenário: tentativa em produção
  Dado uma implantação em produção que pede o esvaziamento do banco
  Então ela falha antes da issue de aprovação
  E aparece "Esvaziar o banco só é permitido em homologação"
  E o banco de produção não é tocado
  E o merge na master e a tag vX.Y.Z nunca esvaziam banco algum
```

<a id="RNF-48"></a>

### `RNF-48` Plano no executor

```gherkin
Cenário: terraform plan
  Então o CD roda terraform plan antes de pedir aprovação
  E o plano fica só no executor, no mesmo job, sem virar artefato, porque tem segredos
```

<a id="RNF-49"></a>

### `RNF-49` Issue por ambiente

```gherkin
Cenário: pedido de aprovação
  Então a issue traz o ambiente, as imagens e tags, o resumo do plano e o link da execução
```

<a id="RNF-50"></a>

### `RNF-50` Aprovar ou negar

```gherkin
Esquema do Cenário: resposta na issue
  Quando <resposta>
  Então <resultado>

  Exemplos:
    | resposta                          | resultado                       |
    | o dono comenta "approve"          | a implantação segue             |
    | o dono comenta "deny"             | cancela sem mudar nada na Azure |
    | a issue fica 2 horas sem resposta | cancela sem mudar nada na Azure |
```

<a id="RNF-51"></a>

### `RNF-51` Aplicar o plano aprovado

```gherkin
Cenário: infraestrutura mudou depois do plano
  Dado o plano aprovado
  E alguém mudando a infraestrutura antes da aplicação
  Então a execução falha sem aplicar
```

<a id="RNF-52"></a>

### `RNF-52` OIDC

```gherkin
Cenário: credencial sem segredo
  Então o CD entra na Azure por OIDC, com uma identidade por ambiente
  E cada identidade só tem permissão na assinatura do seu ambiente
  E fica nos environments azure-hml e azure-prd, restritos à master
```

<a id="RNF-53"></a>

### `RNF-53` Uma implantação por vez

```gherkin
Cenário: dois merges seguidos
  Dado uma implantação em homologação em andamento
  Quando outro merge pede implantação
  Então a segunda entra na fila sem cancelar a primeira
```

<a id="RNF-54"></a>

### `RNF-54` Prontidão

```gherkin
Cenário: revisão que não fica pronta
  Dado uma revisão nova do backend que não responde /health em 5 min
  Então a revisão anterior continua atendendo e a execução falha
```

<a id="RNF-55"></a>

### `RNF-55` Pastas do Terraform

```gherkin
Cenário: estrutura
  Então 4_CI_CD/terraform/infra/ é o módulo raiz, sem módulos próprios
  E ambientes/<amb>/ tem variaveis.tfvars e backend.hcl, e scripts/ tem o bootstrap
  E nenhum segredo nem ID da Azure fica no repositório
```

<a id="RNF-56"></a>

### `RNF-56` Estado do Terraform

```gherkin
Esquema do Cenário: estado de cada ambiente
  Então o estado de <ambiente> fica em <storage>, com trava contra execução simultânea

  Exemplos:
    | ambiente    | storage              |
    | homologação | debboazapphmltfstate |
    | produção    | debboazappprdtfstate |
```

<a id="RNF-57"></a>

### `RNF-57` Versões do Terraform

```gherkin
Cenário: versões fixas
  Então as versões do Terraform e do azurerm ficam fixadas no repositório
```

<a id="RNF-58"></a>

### `RNF-58` Escala por variável

```gherkin
Cenário: mudar réplicas
  Quando o time muda o máximo de réplicas do backend
  Então muda só o .tfvars, sem alterar código
```

<a id="RNF-86"></a>

### `RNF-86` Região

```gherkin
Cenário: Brazil South
  Então os ambientes, o estado e todos os recursos ficam em Brazil South
```

<a id="RNF-59"></a>

### `RNF-59` VNets

```gherkin
Esquema do Cenário: VNet de cada ambiente
  Então <ambiente> tem a VNet <faixa>, sem conexão com a outra

  Exemplos:
    | ambiente    | faixa        |
    | homologação | 10.10.0.0/16 |
    | produção    | 10.20.0.0/16 |
```

<a id="RNF-60"></a>

### `RNF-60` Subnets

```gherkin
Esquema do Cenário: subnets
  Então a subnet <subnet> (<tamanho>) serve para <uso>

  Exemplos:
    | subnet     | tamanho | uso                              |
    | snet-aca   | /23     | o ambiente do Container Apps     |
    | snet-pe    | /27     | os private endpoints             |
    | snet-appgw | /24     | o Application Gateway, só em prd |
```

<a id="RNF-61"></a>

### `RNF-61` Private endpoints

```gherkin
Esquema do Cenário: acesso privado
  Então as aplicações acessam <serviço> só por private endpoint, pela zona <zona>

  Exemplos:
    | serviço   | zona                               |
    | Cosmos DB | privatelink.mongo.cosmos.azure.com |
    | Redis     | privatelink.redis.azure.net        |
    | Key Vault | privatelink.vaultcore.azure.net    |
```

<a id="RNF-62"></a>

### `RNF-62` Sem acesso público

```gherkin
Cenário: banco e cache fechados
  Então Cosmos DB e Redis têm o acesso público desligado
```

<a id="RNF-63"></a>

### `RNF-63` NSGs

```gherkin
Cenário: entrada de snet-pe
  Então o NSG de snet-pe aceita entrada só de snet-aca

Cenário: entrada de snet-appgw
  Então em produção o NSG de snet-appgw aceita só 443 da internet e as portas de gerenciamento
```

<a id="RNF-64"></a>

### `RNF-64` Container Apps

```gherkin
Cenário: quatro aplicações
  Então as quatro imagens rodam num ambiente do Container Apps Consumption, em snet-aca
```

<a id="RNF-65"></a>

### `RNF-65` Réplicas em homologação

```gherkin
Cenário: tamanho em homologação
  Então cada aplicação tem 1 a 2 réplicas de 0,25 vCPU e 0,5 GiB
```

<a id="RNF-66"></a>

### `RNF-66` Réplicas em produção

```gherkin
Cenário: escala em produção
  Dado o backend com 1 réplica e 31 requisições simultâneas
  Então uma réplica nova é criada, até o máximo de 5
```

<a id="RNF-67"></a>

### `RNF-67` Download das imagens

```gherkin
Cenário: sem limite anônimo
  Então as imagens são baixadas com o usuário gustavoboaz e um token só de leitura do Key Vault
```

<a id="RNF-68"></a>

### `RNF-68` Key Vault por RBAC

```gherkin
Cenário: leitura dos segredos
  Então cada ambiente tem um Key Vault Standard com RBAC
  E as aplicações leem os segredos pela identidade gerenciada com Key Vault Secrets User
```

<a id="RNF-69"></a>

### `RNF-69` Conexões gravadas pelo Terraform

```gherkin
Cenário: ninguém digita
  Quando o Terraform cria o Cosmos DB e o Redis
  Então grava as cadeias de conexão no Key Vault
```

<a id="RNF-70"></a>

### `RNF-70` Cosmos DB

```gherkin
Esquema do Cenário: modo do Cosmos DB
  Então em <ambiente> o Cosmos DB roda <modo>

  Exemplos:
    | ambiente    | modo                                                   |
    | homologação | serverless                                             |
    | produção    | autoscale compartilhado no banco, máximo em prd.tfvars |
```

<a id="RNF-87"></a>

### `RNF-87` Versões na Azure

```gherkin
Cenário: compatibilidade
  Então o Cosmos DB usa a versão de protocolo MongoDB mais alta e o Redis a mais recente
  E a compatibilidade com o MongoDB 8.0 e o Redis 8 é validada em homologação
```

<a id="RNF-71"></a>

### `RNF-71` Azure Managed Redis

```gherkin
Cenário: SKU do cache
  Então o cache usa o Azure Managed Redis no menor SKU
  E o Azure Cache for Redis não é usado, porque está em aposentadoria
```

<a id="RNF-72"></a>

### `RNF-72` Acesso em homologação

```gherkin
Cenário: domínio padrão
  Então web e backend de homologação abrem de qualquer IP pela entrada HTTPS do Container Apps
```

<a id="RNF-73"></a>

### `RNF-73` Documentação da API

```gherkin
Cenário: /doc/api em homologação
  Quando alguém abre a raiz do backend de homologação
  Então vai para /doc/api, com a capa do catálogo de APIs e a identidade deboaz
  E o seletor do canto abre a API do ms_mono para testar sem a web

Cenário: produção
  Então em produção nem a raiz nem /doc/api existem
```

<a id="RNF-74"></a>

### `RNF-74` Application Gateway

```gherkin
Cenário: único ponto público
  Então produção tem um Application Gateway WAF_v2 com IP estático
  E autoscale de 0 a 2 unidades e HTTP redirecionado para HTTPS
```

<a id="RNF-75"></a>

### `RNF-75` Container Apps interno

```gherkin
Cenário: sem IP público
  Então nenhuma aplicação de produção tem IP público
  E o Application Gateway as alcança pela VNet, com o domínio numa zona Private DNS
```

<a id="RNF-76"></a>

### `RNF-76` Backend pela web

```gherkin
Cenário: chamada do navegador
  Quando o navegador chama /api/usuarios no frontend-hub
  Então o frontend-hub encaminha ao endereço interno do backend
  E o backend não tem rota própria para a web no Application Gateway
```

<a id="RNF-77"></a>

### `RNF-77` Rota do mobile

```gherkin
Cenário: sem atestação
  Quando o app chama /mobile/* sem o cabeçalho X-App-Attestation
  Então a WAF bloqueia
```

<a id="RNF-78"></a>

### `RNF-78` Atestação inválida

```gherkin
Cenário: token de atestação falso
  Quando o app chama /mobile/* com Play Integrity ou App Attest inválido
  Então o backend responde 401
```

<a id="RNF-79"></a>

### `RNF-79` Política de WAF

```gherkin
Cenário: IP com muitas requisições
  Quando um IP passa de 100 requisições por minuto
  Então a WAF bloqueia o IP

Cenário: regras da WAF
  Então a WAF roda em Prevenção, com o DRS da Microsoft, e bloqueia /doc/api
```

<a id="RNF-80"></a>

### `RNF-80` Certificado

```gherkin
Cenário: TLS de produção
  Então o certificado fica no Key Vault, lido pelo Application Gateway por identidade gerenciada
```

<a id="RNF-81"></a>

### `RNF-81` Log Analytics

```gherkin
Cenário: logs
  Então cada ambiente tem um Log Analytics com retenção de 30 dias
  E recebe os logs do Container Apps e, em produção, do Application Gateway
```

<a id="RNF-82"></a>

### `RNF-82` Application Insights

```gherkin
Cenário: telemetria
  Então cada ambiente tem um Application Insights ligado ao workspace, com a telemetria do backend
```

<a id="RNF-83"></a>

### `RNF-83` Limite de ingestão

```gherkin
Cenário: homologação
  Então o workspace de homologação recebe no máximo 1 GB por dia
```

<a id="RNF-84"></a>

### `RNF-84` Orçamentos

```gherkin
Esquema do Cenário: alerta de custo
  Dado o orçamento de <ambiente> de <valor> por mês
  Quando o gasto chega a <percentual>
  Então o dono do repositório recebe um e-mail

  Exemplos:
    | ambiente    | valor       | percentual |
    | homologação | R$ 300,00   | 80 %       |
    | homologação | R$ 300,00   | 100 %      |
    | produção    | R$ 1.500,00 | 80 %       |
    | produção    | R$ 1.500,00 | 100 %      |
```

<a id="RNF-85"></a>

### `RNF-85` Tags

```gherkin
Cenário: filtrar custos
  Então todo recurso tem projeto=boaz, ambiente=hml ou prd e gerenciado-por=terraform
```

<a id="RNF-88"></a>

### `RNF-88` Nomes dos recursos

```gherkin
Esquema do Cenário: nome em homologação
  Então <recurso> se chama "<nome>"

  Exemplos:
    | recurso                 | nome                         |
    | a VNet                  | deb-boaz-app-hml_vnet        |
    | o NSG dos endpoints     | deb-boaz-app-hml_nsg-pe      |
    | o endpoint do Key Vault | deb-boaz-app-hml_pe-kv       |
    | o ambiente de apps      | deb-boaz-app-hml-cae         |
    | a identidade            | deb-boaz-app-hml_id          |
    | o Key Vault             | deb-boaz-app-hml-kv          |
    | o Cosmos DB             | deb-boaz-app-hml-cosmos      |
    | o Redis                 | deb-boaz-app-hml-redis       |
    | o Log Analytics         | deb-boaz-app-hml-log         |
    | o Application Insights  | deb-boaz-app-hml_appi        |
    | o orçamento             | deb-boaz-app-hml_budget      |
    | o estado do Terraform   | debboazapphmltfstate         |
    | o Application Gateway   | deb-boaz-app-prd_agw, só prd |

Cenário: caracteres aceitos
  Então quando a Azure não aceita "_" o nome usa "-"
  E quando não aceita nem "-" junta tudo em minúsculas
```

<a id="RNF-89"></a>

### `RNF-89` Nome das aplicações

```gherkin
Cenário: limite de 32 caracteres
  Então as aplicações se chamam backend-mono, frontend-hub, frontend-landing-page e frontend-quest
```

<a id="RNF-90"></a>

### `RNF-90` Assinaturas fora do Terraform

```gherkin
Cenário: grupo existente
  Então o Terraform lê a assinatura e o grupo de recursos como existentes
  E nunca os cria nem os apaga

Cenário: Key Vault aberto ao CD
  Dado o CD rodando fora da VNet
  Então o Key Vault mantém o acesso público, restrito por RBAC à identidade do CD
  E fechá-lo exige um executor dentro da VNet
```

---

<a id="diagramas"></a>

## Diagramas

**Implantação · homologação** — `3_Arquitetura/implantacao-hml_v06_20261107-0407.svg`

[![Ambiente de homologação na Azure: assinatura deb-boaz-app-hml, grupo de recursos deb-boaz-app-hml_rg, com VNet 10.10.0.0/16; na subnet snet-aca, o ambiente externo do Container Apps com backend-mono e as três aplicações web, 1 a 2 réplicas, acessadas por HTTPS pelo usuário web, pela pessoa desenvolvedora via documentação da API em /doc/api e pelos apps móveis; na subnet snet-pe, private endpoints de Key Vault, Cosmos DB for MongoDB serverless e Azure Managed Redis, com zonas Private DNS; Log Analytics, Application Insights e orçamento; imagens baixadas do Docker Hub e infraestrutura aplicada pelo CD com Terraform; segredos das contas iniciais (admin, contas qa e módulos de teste) lidos do Key Vault pelo backend-mono](../3_Arquitetura/implantacao-hml_v06_20261107-0407.svg)](../3_Arquitetura/implantacao-hml_v06_20261107-0407.svg)

**Implantação · produção** — `3_Arquitetura/implantacao-prd_v05_20261106-2318.svg`

[![Ambiente de produção na Azure: assinatura deb-boaz-app-prd, grupo de recursos deb-boaz-app-prd_rg, com VNet 10.20.0.0/16; Application Gateway WAF_v2 com IP público em snet-appgw como único ponto público, roteando a web para as aplicações web e /mobile/* para o backend, com política de WAF; ambiente interno do Container Apps em snet-aca com 1 a 5 réplicas, em que as aplicações web encaminham /api/* ao backend; private endpoints de Key Vault, Cosmos DB autoscale e Azure Managed Redis em snet-pe com zonas Private DNS; Log Analytics, Application Insights e orçamento](../3_Arquitetura/implantacao-prd_v05_20261106-2318.svg)](../3_Arquitetura/implantacao-prd_v05_20261106-2318.svg)

**Pipeline de CD** — `3_Arquitetura/pipeline-cd_v07_20261107-1614.svg`

[![Pipeline de CD no GitHub Actions: o fim do CI com as imagens publicadas dispara o cd.yml; login na Azure por OIDC, terraform plan com estado numa storage account de cada assinatura, aprovação manual por issue pelo dono do repositório, terraform apply em deb-boaz-app-hml_rg no merge na master e na tag vX.Y.Z, e depois em deb-boaz-app-prd_rg na tag, e verificação de /health; o Container Apps baixa as imagens do Docker Hub; no disparo manual em homologação, o esvaziamento do banco pelo plano de controle depois da aprovação e antes do apply, com o backend-mono reiniciado em seguida](../3_Arquitetura/pipeline-cd_v07_20261107-1614.svg)](../3_Arquitetura/pipeline-cd_v07_20261107-1614.svg)

---

<a id="em-aberto"></a>

## Em aberto

- Domínio público de produção e origem do certificado TLS; até lá, o domínio fica como variável vazia no arquivo de variáveis de produção — **Produto**

---

<a id="historico"></a>

## Histórico

| Versão | Data | Mudança |
|---|---|---|
| **v12** | 2026-11-07 16:15 | Esvaziar o banco de homologação pelo disparo manual (`RNF-228` incluído); diagrama `pipeline-cd` atualizado para v07 |
| v11 | 2026-11-06 23:37 | Diagramas de implantação de homologação e de produção atualizados para v05 |
| v10 | 2026-11-05 12:34 | Formato `.md`: migrado de `.html` sem mudar cenários, telas nem diagramas; links entre documentos apontam para `.md` |
| v09 | 2026-11-01 18:28 | Só Gherkin e diagramas: premissas, RN, RNF e nomes dos recursos viram cenários (mesmos IDs como âncora); Em aberto mantido; status → Em revisão |
| v08 | 2026-10-27 00:49 | Status → Aprovado |
| v07 | 2026-10-27 00:15 | Documentação da API em `/doc/api` (antes `/swagger`), com a identidade deboaz, capa do catálogo, seletor de APIs e a raiz levando ao catálogo (`RNF-73`); WAF bloqueia `/doc/api` (`RNF-79`); diagramas de implantação atualizados para v04 |
| v06 | 2026-10-15 23:58 | Ligar e desligar homologação sob demanda: `RN-26`, `RNF-91` e `RNF-92` incluídos, `RN-22` alterado; diagrama `pipeline-cd` atualizado para v06 |
| v05 | 2026-10-15 01:16 | Estrutura do Terraform: módulo raiz em `infra/`, valores em `ambientes/<amb>/` e bootstrap em `scripts/`, sem IDs da Azure no repositório (`RNF-55` alterado); diagrama `pipeline-cd` atualizado para v05 |
| v04 | 2026-10-15 01:00 | Ajustes da implementação: plano guardado só no executor, sem artefato (`RNF-48` e `RNF-51` alterados); environments `azure-hml` e `azure-prd` restritos à branch principal (`RNF-52` alterado); diagrama `pipeline-cd` atualizado para v04 |
| v03 | 2026-10-14 23:15 | Assinaturas `deb-boaz-app-hml` e `deb-boaz-app-prd` e grupos `deb-boaz-app-<amb>_rg` (`RN-17` e `RNF-52` alterados); estado do Terraform em cada assinatura (`RNF-56` alterado); nomenclatura dos recursos (`RNF-88` a `RNF-90` incluídos); diagramas atualizados para v03 |

---

<sub>boaz · v12 · 2026-11-07 16:15 · `1_Requisitos/05_cd-implantacao.md`</sub>
