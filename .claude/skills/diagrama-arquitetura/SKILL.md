---
name: diagrama-arquitetura
description: Cria ou altera diagramas de arquitetura de alto nível como SVG versionado em 3_Arquitetura/. Use para contexto do sistema, contêineres (C4), diagrama de implantação UML, componentes no nível de serviços/sistemas, cloud, rede, ambientes, deploy, tecnologias escolhidas e integrações externas, ou quando pedirem para alterar um diagrama existente dessa pasta.
---

# Diagrama de arquitetura → `3_Arquitetura/`

Você é arquiteto de soluções. A saída é **sempre um novo arquivo `.svg`** em `3_Arquitetura/` — nunca apenas texto, Mermaid ou PlantUML.

## Escopo

Pertence aqui a **visão de alto nível**: contexto do sistema e atores, contêineres/serviços e suas tecnologias, provedores de cloud e serviços gerenciados, rede (região, rede privada, sub-redes), ambientes, topologia de deploy, filas e integrações externas.

Não pertence: detalhe interno de código (classes, objetos, pacotes, sequência entre métodos, estados, telas e demais UML de código) → skill `diagrama-dev` (`2_Diagramas/`).

## Escolha do tipo

Se o pedido nomear o tipo, use-o. Senão, escolha pela pergunta e diga na resposta qual tipo usou:

| Pergunta | Tipo |
|---|---|
| Quem usa o sistema e com quais sistemas externos ele conversa? | C4 nível 1 — contexto |
| Quais aplicações, serviços e bancos compõem o sistema e com que tecnologia? | C4 nível 2 — contêiner |
| Quais serviços/sistemas existem e quais interfaces expõem e consomem? | Componentes UML (nível de serviço) |
| Onde cada artefato roda — nós, ambientes, rede? | Implantação UML |

## Neutralidade de provedor

A arquitetura deve valer para **Azure, AWS e GCP**. Nunca use nome de produto de um único provedor (`Lambda`, `EKS`, `S3`, `Cosmos DB`, `BigQuery`…): nomeie pela **categoria** do serviço gerenciado. Tecnologias abertas e portáveis (PostgreSQL, Kubernetes, Kafka, Redis, Docker) podem ser citadas. Só use o nome de um provedor se o pedido o exigir explicitamente — e então diga isso na resposta.

| Termo neutro | AWS | Azure | GCP |
|---|---|---|---|
| Rede privada / sub-rede | VPC / Subnet | VNet / Subnet | VPC / Subnet |
| Conta / assinatura / projeto | Account | Subscription | Project |
| Máquina virtual | EC2 | Virtual Machines | Compute Engine |
| Função serverless | Lambda | Functions | Cloud Functions / Cloud Run functions |
| Contêiner serverless | Fargate / App Runner | Container Apps | Cloud Run |
| Cluster Kubernetes gerenciado | EKS | AKS | GKE |
| Armazenamento de objetos | S3 | Blob Storage | Cloud Storage |
| Banco relacional gerenciado | RDS / Aurora | Azure Database / SQL Database | Cloud SQL / AlloyDB |
| Banco NoSQL gerenciado | DynamoDB | Cosmos DB | Firestore / Bigtable |
| Cache gerenciado | ElastiCache | Azure Cache for Redis | Memorystore |
| Fila de mensagens | SQS | Service Bus / Storage Queues | Pub/Sub / Cloud Tasks |
| Barramento / tópico de eventos | SNS / EventBridge | Event Grid / Event Hubs | Pub/Sub / Eventarc |
| API gateway | API Gateway | API Management | API Gateway / Apigee |
| Balanceador de carga | ELB / ALB | Load Balancer / Application Gateway | Cloud Load Balancing |
| CDN | CloudFront | Front Door / CDN | Cloud CDN |
| DNS gerenciado | Route 53 | Azure DNS | Cloud DNS |
| Identidade de usuários | Cognito | Entra ID / Entra External ID | Identity Platform |
| Identidade e acesso (IAM) | IAM | Entra ID + RBAC | IAM |
| Cofre de segredos | Secrets Manager | Key Vault | Secret Manager |
| Observabilidade (logs/métricas) | CloudWatch | Azure Monitor | Cloud Logging / Monitoring |
| Data warehouse | Redshift | Synapse / Fabric | BigQuery |
| Registro de contêineres | ECR | Container Registry | Artifact Registry |

Se o termo não estiver na tabela, crie um equivalente neutro no mesmo estilo (categoria + "gerenciado", quando couber).

## Notação

- **C4 contexto/contêiner**: como descrito abaixo nas regras gerais.
- **Componentes (nível de serviço)**: cada serviço ou sistema em retângulo com `«component»` (ou `«system»`) e o ícone `#uml-componente` no canto; interface fornecida = pirulito (linha + círculo `ink-ring`) rotulada com o contrato (`REST /pedidos`, `tópico pedido.criado`); requerida = soquete (meio-círculo). Conecte soquete ao pirulito.
- **Implantação UML**: nós como caixas 3D (retângulo `node` com face superior e lateral em paralelogramos `node-face`) com `«device»` ou `«executionEnvironment»` e o nome (`Cluster Kubernetes gerenciado`, `Máquina virtual`, `Navegador`); nós podem conter nós. Artefatos dentro dos nós como retângulos com `«artifact»`, o ícone `#uml-artefato` e nome do arquivo/imagem (`api:1.4.2` em `mono`). Caminhos de comunicação como linhas entre nós rotuladas com protocolo; dependência artefato→componente com `edge-async` + `seta-aberta` `«manifest»`.
- Cada caixa: nome em negrito, tecnologia entre colchetes (`[PostgreSQL 16]`, `[Função serverless]`) e uma linha de responsabilidade.
- Ícones: no diagrama neutro, use as formas neutras `#ic-*` do tema (seção 3 — `ic-banco-sql`, `ic-fila`, `ic-funcao`, `ic-gateway`, `ic-cdn`, `ic-objetos`, `ic-identidade`…) com 24px à esquerda do nome, como no exemplo da seção 4; nada de logo de produto. Ícones de tecnologias abertas e logos de integrações vêm de `0_Assets/diagramas/` (embutidos, ver **Assets**).
- Ícones de provedor (`icones/aws/`, `icones/gcp/`, `icones/azure/`) só quando o pedido fixar o provedor: substituem a forma neutra da mesma categoria (seção 5 do tema mostra a correspondência). Com provedor fixado, use `icones/aws/grupos/` para as fronteiras (região, VPC, sub-redes).
- Fronteiras (conta/assinatura/projeto, região, rede privada, cluster, ambiente) como retângulos tracejados rotulados no canto superior esquerdo.
- Setas rotuladas com o que trafega e o protocolo (`pedidos · HTTPS/JSON`, `eventos · AMQP`).
- Sistemas externos com `node-external`, para separar do que é do projeto.

## Versionamento (obrigatório)

Nome do arquivo: `<legenda>_v<NN>_<AAAAMMDD-HHMM>.svg`

- **legenda**: kebab-case, sem acentos, curta. Identifica o diagrama e **não muda** entre versões (ex.: `fluxo-cadastro`).
- **NN**: versão com 2 dígitos, começa em `01` e soma 1 a cada alteração.
- **timestamp**: data/hora local em que aquela versão foi gerada.

Obtenha o caminho exato com:

```bash
./boaz.sh svg 3_Arquitetura <legenda>
```

Se o `boaz.sh` não estiver disponível, calcule pela regra acima.
**Nunca** sobrescreva, renomeie ou apague uma versão existente — cada alteração é um arquivo novo.

## Fluxo

**Diagrama novo**
1. Leia os documentos relevantes de `1_Requisitos/` (`.html`) e rode `./boaz.sh diagramas` para reaproveitar nomes e evitar duplicatas.
2. Se a ambiguidade mudar o desenho, faça no máximo 1–2 perguntas; caso contrário, assuma e declare as premissas na resposta.
3. Escolha a legenda, gere o caminho e escreva o SVG.

**Alteração** ("muda", "adiciona", "corrige" num diagrama existente)
1. Identifique a legenda e abra a **maior versão** (`ls 3_Arquitetura/<legenda>_v*`).
2. Aplique só a mudança pedida, preservando layout, ids e o restante do desenho.
3. Salve como a próxima versão (`./boaz.sh svg 3_Arquitetura <legenda>`).
4. Rode `grep -rn "<legenda>_v" 1_Requisitos` e, se houver referências à versão anterior, pergunte se devem apontar para a nova. Não altere sem confirmação; confirmado, troque o caminho no `src`/`href` e na legenda da `<figure class="diagrama">`, e cada documento alterado ganha uma versão nova (marca d'água e histórico, como na skill `requisito`).

## Assets (`0_Assets/`)

Antes de desenhar, rode `ls 0_Assets/projeto 0_Assets/diagramas` e reaproveite o que existir: ícones de tecnologias, logos de integrações e da marca.
Ícones de cloud ficam em `0_Assets/diagramas/icones/{aws,gcp,azure}/` (centenas de arquivos — leia o `README.md` da pasta e busque por nome: `find 0_Assets/diagramas/icones -iname '*lambda*'`, nunca `ls -R`).

- **Embuta sempre** — nunca referencie o arquivo por caminho (`href="../0_Assets/..."` quebra no GitHub e em visualizadores de imagem).
  - `.svg`: copie o conteúdo gráfico para um `<symbol id="asset-<nome>" viewBox="...">` em `<defs>` (com o `viewBox` do original) e desenhe com `<use href="#asset-<nome>" x=".." y=".." width=".." height=".."/>`. Remova `<script>`, `<foreignObject>`, handlers `on*` e metadados de editor; prefixe ids internos com `<nome>-` para não colidirem. Se o ícone tiver `<style>` próprio (os do GCP trazem `.cls-1`, `.st0`…), converta cada regra em atributo (`fill="#4285f4"`) nos elementos e apague o `<style>` — senão as classes vazam e colidem com outros ícones. O `tema.svg` traz exemplos prontos em `#prov-*`.
  - `.png`/`.jpg`/`.webp`: `<image href="data:image/png;base64,..."/>` gerado com `base64 < arquivo | tr -d '\n'`. Evite arquivos acima de ~200 KB.
- Ícone é apoio, não substitui o texto: a caixa continua com nome e tecnologia escritos. Tamanho uniforme (24–32px), sempre no mesmo lugar da caixa (padrão do tema: à esquerda, nome e tecnologia alinhados depois dele).
- Faltou um ícone? Use a forma neutra `#ic-*` do tema que mais se aproxima (ou a forma simples da notação) e mencione na resposta qual asset poderia ser adicionado em `0_Assets/diagramas/`.

## Visual (obrigatório)

O diagrama precisa ser **bonito**, não só correto — algo que daria orgulho de colocar numa apresentação.

**Tema padrão (modo dark).** `0_Assets/diagramas/tema.svg` é o estilo e o catálogo visual: cores por papel, tipografia, tipos de comunicação, formas neutras de arquitetura, exemplo de arquitetura, ícones por provedor, um cartão por tipo de diagrama UML com os elementos que ele usa e o exemplo da marca d'água de topo e rodapé. Abra-o (`grep -n 'id="' 0_Assets/diagramas/tema.svg` lista seções e símbolos) e:
- copie **sem alterar** o bloco `<style>` e, de `<defs>`, todos os `<marker>`, o filtro `sombra` e o gradiente `marca-acento`;
- dos `<symbol>`, copie **só os que usar**: `#ic-*` (formas neutras de arquitetura — banco, fila, função, gateway…), `#uml-*` (ator, ícone de componente, artefato), `#prov-*` (ícones oficiais de exemplo);
- use o cartão do tipo de diagrama (seção 6, `<g id="uml-...">`, `er`, `c4`) como referência de composição e o exemplo da seção 7 (`<g id="exemplo-marca">`) para a marca d'água.
Se o arquivo não existir, rode `./boaz.sh init` para recriá-lo.
- Estilize **apenas por classe** do tema (`node-blue`, `edge`, `label-strong`, `muted`...). Nada de `fill`/`stroke`/`font-*` inline nem cores fora do tema.
- Fundo: primeiro elemento `<rect class="bg">` cobrindo todo o `viewBox`.
- Cor = papel: no máximo 4 classes `node-*` coloridas por diagrama, cada uma explicada na legenda; `node-external` para o que não é do projeto, `node-red` só para erro/risco. Destaque o fluxo principal com `edge-accent` + `seta-destaque`; o resto com `edge`. Assíncrono/retorno: adicione `edge-async`.
- Comunicação (seção 2 do tema): síncrono principal `edge-accent` + `seta-destaque`; síncrono `edge` + `seta`; resposta `edge edge-async` + `seta-aberta`; evento/mensagem `edge edge-async` + `seta`; stream bidirecional `seta` nas duas pontas; dados/lote `edge edge-data`. Protocolo no rótulo da aresta ou como `chip` + `chip-text`.
- Notação UML com os elementos do tema: markers `seta-aberta`, `heranca`, `extensao`, `composicao`, `agregacao`, `er-um`, `er-muitos` e classes `ink`, `ink-ring`, `node-note`, `node-face`, `uml-keyword`, `label-underline`, `activation`, `frame-tag`, `frame-op`. Se o tema do projeto for antigo e não tiver algum deles, declare-o no diagrama com as mesmas cores do tema (ou recrie o tema: apague `0_Assets/diagramas/tema.svg` e rode `./boaz.sh init`).

**Composição.**
- Grade de 8px: coordenadas e tamanhos múltiplos de 8; margem externa de 40px; espaço mínimo de 48px entre caixas.
- Leitura em uma direção (esquerda→direita ou cima→baixo); alinhe caixas em linhas e colunas e dê o mesmo tamanho a elementos do mesmo tipo.
- Caixas com `rx="10"`, padding interno ≥ 16px, `filter="url(#sombra)"` no `<g>` do nó. Texto centralizado: título em `label-strong`, tecnologia/detalhe em `mono` ou `muted` logo abaixo.
- Tamanho pelo texto: estime ~7,5px por caractere em 13–14px (~8px em `mono`) e dimensione a caixa para caber com folga. Nenhum texto pode sobrepor formas, linhas ou outro texto.
- Setas ortogonais (`<path>` com segmentos H/V, cantos retos) ou retas; terminam na borda da caixa, sem atravessá-la. Rótulos acima da linha, em `muted`; se encostarem em algo, ponha um `<rect class="edge-label-bg">` atrás. Minimize cruzamentos.
- Fronteiras: `<rect class="boundary" rx="14">` com rótulo `boundary-label` em MAIÚSCULAS no canto superior esquerdo.

**Estrutura do arquivo.**
- Um único arquivo autocontido: sem fontes, imagens ou CSS externos; sem `<script>` nem `<foreignObject>`.
- `xmlns="http://www.w3.org/2000/svg"`, `viewBox`, `width` e `height` definidos.
- `<title>` com o título do diagrama; `<desc>` com o que ele mostra e o histórico curto (`v01: criação`, `v02: adicionado ...`).
- Agrupe cada elemento lógico em `<g id="...">` com id kebab-case — facilita alterações futuras.
- Numa alteração, mantenha o tema da versão anterior, a menos que pedirem para atualizar o visual.

**Marca d'água (obrigatória).** Todo diagrama faz parte da documentação: abre com uma faixa de topo e fecha com uma de rodapé, ambas de largura total do `viewBox`, encostadas nas bordas (sem margem). Copie a estrutura do exemplo da seção 7 do tema trocando só os textos.
- **Topo** — `<g id="marca-topo">` em `y=0`, altura 112:
  - `<rect class="wm-band">` de `0,0` até a largura total, e `<rect class="wm-accent">` de 2px em `y=110` (gradiente);
  - à esquerda, em `x=40`: sobretítulo `wm-eyebrow` em `y=40` com `<PROJETO> · <TIPO DE DIAGRAMA>` em MAIÚSCULAS (projeto = `PROJECT_NAME` de `.boaz`), o nome do diagrama em `title` em `y=72` e uma linha de contexto em `subtitle` em `y=94`;
  - à direita, o carimbo: `DATA` em `x = largura − 192` (`wm-meta-label` em `y=44`; valor `wm-meta-value` em `y=72`: `AAAA-MM-DD · HH:MM`, o mesmo instante do nome do arquivo); um `wm-sep` vertical 24px antes; `VERSÃO` 96px antes da data (`wm-meta-label` + selo `<rect class="wm-badge" width="48" height="22" rx="11">` com `wm-badge-text` centralizado: `v<NN>`);
  - o título não pode invadir os ~300px do carimbo; se não couber, encurte o subtítulo ou alargue o `viewBox`.
- **Rodapé** — `<g id="marca-rodape">` encostado no fim do `viewBox`, altura 116 (+26 por linha extra de legenda):
  - `<rect class="wm-band">` com `wm-sep` de 1px na borda de cima;
  - `LEGENDA` em `wm-eyebrow` e, numa linha, **todos** os elementos com significado que aparecem no desenho e só eles: cada classe `node-*` (amostra 14×14 `rx="3"` com a classe + papel em `label`) e cada tipo de aresta (linha de 28px com a mesma classe e o mesmo marker + significado). 28px entre itens; se não couber, quebre em outra linha;
  - um `divider` interno e, abaixo, à esquerda em `footer`: `v<NN> · <o que o diagrama mostra>` (numa alteração, o que mudou); à direita, `text-anchor="end"` em `wm-file`, o nome do arquivo `.svg`.
- O desenho fica entre as faixas: começa 40px abaixo da marca de topo e termina 40px acima da de rodapé. A legenda vive só no rodapé — nada de caixa de legenda solta no desenho.
- Numa alteração, atualize versão, data, nota e nome do arquivo nas marcas, e a legenda se os elementos mudaram.

**Revisão antes de entregar.**
- Se houver `xmllint`, valide com `xmllint --noout <arquivo>` e corrija os erros.
- Se houver `rsvg-convert` (ou `qlmanage` no macOS), renderize um PNG temporário no scratchpad, **olhe a imagem** e corrija sobreposições, textos cortados, desalinhamentos e espaços vazios desequilibrados antes de responder. Confira também as marcas d'água: versão e data iguais às do nome do arquivo e legenda batendo com o desenho.

## Resposta

Responda curto: caminho do arquivo criado, premissas assumidas (se houver) e o trecho para referenciar no requisito (`.html`, componente **Diagramas** de `0_Assets/requisitos/tema.html`):

```html
<figure class="diagrama">
  <a href="../3_Arquitetura/<arquivo>.svg"><img src="../3_Arquitetura/<arquivo>.svg" alt="<o que o diagrama mostra>"></a>
  <figcaption><strong><Título></strong><code>3_Arquitetura/<arquivo>.svg</code></figcaption>
</figure>
```
