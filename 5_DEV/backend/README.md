# backend

Um único serviço, `ms_mono`, com a estrutura hexagonal de
[`14_estrutura-projetos`](../../1_Requisitos/14_estrutura-projetos.md)
(RN-143 a RN-149, RNF-175 a RNF-178).

```
5_DEV/backend/
├── Boaz.slnx
├── core/                         domínio, casos de uso e portas, sem I/O
│   ├── commons/                  Boaz.Core.Commons (o que os contextos compartilham)
│   ├── landing_page/             Boaz.Core.LandingPage ─┐
│   ├── hub/                      Boaz.Core.Hub          ├─ importam só o Commons
│   └── quest/                    Boaz.Core.Quest       ─┘
│       └── <feature>/            domain/ · usecases/ · ports/in/ · ports/out/
├── ms_mono/                      Boaz.MsMono: ASP.NET Core, imagem backend-mono
│   ├── Program.cs                ponto de composição (liga portas e adaptadores)
│   ├── appsettings.json          padrões de configuração (sem segredos)
│   ├── sistema/                  do runtime: SaudeController (/health) e a documentação em /doc/api
│   │   ├── guias/                guia de uso de cada API do Swagger (Markdown)
│   │   └── exemplos/             exemplos de cada endpoint (gerados dos contratos, não edite)
│   ├── wwwroot/doc/api/          swagger.css, design/ e diagramas/ (cópias geradas, não edite)
│   ├── primario/in/<contexto>/<feature>/    adaptadores primários: controllers HTTP finos, partida e
│   │                                        agendamento (chamam ports/in)
│   ├── primario/out/<contexto>/<feature>/   o que os primários devolvem: fluxo SSE, sessão na resposta
│   ├── secundario/out/<contexto>/<feature>/ adaptadores secundários: MongoDB e Redis (implementam ports/out)
│   └── secundario/in/<contexto>/<feature>/  o que volta dos secundários: canal aoVivo:encerrar do Redis
├── tests/                        espelha core/ e ms_mono/, um xUnit por projeto
├── scripts/diagramas-da-doc.sh   copia de 2_Diagramas/ os diagramas que os guias mostram
├── scripts/exemplos-da-doc.py    gera os exemplos de cada endpoint a partir dos contratos
└── Dockerfile                    imagem backend-mono, porta 8080, /health
```

As features entram com o módulo que as cria, em `snake_case`, com o mesmo nome
nas três frentes.

```bash
dotnet build              # na pasta 5_DEV/backend
dotnet test
dotnet run --project ms_mono   # http://localhost:8080/health
```

## Rodar local

Precisa de:

- **.NET 10 SDK** (`dotnet --version`);
- **Docker**, para o MongoDB 8.0 e o Redis 8.0 (as mesmas versões do CI);
- a **entrada e a senha do administrador** do deboaz, que você escolhe: elas
  não ficam no `appsettings.json` (são segredo, RNF-225) e valem só para o seu
  banco local. Sem elas o backend não sobe (RNF-226).

```bash
# 1. MongoDB, Redis e Azurite (Blob dos anexos do SAC, RNF-233) nas portas padrão
#    (uma vez; --rm apaga ao parar). Sem o Azurite, os testes de ArquivosDosAnexosTests falham
docker run -d --rm --name boaz-mongo -p 127.0.0.1:27017:27017 mongo:8.0
docker run -d --rm --name boaz-redis -p 127.0.0.1:6379:6379 redis:8.0
docker run -d --rm --name boaz-azurite -p 127.0.0.1:10000:10000 mcr.microsoft.com/azure-storage/azurite:3.34.0 \
  azurite-blob --blobHost 0.0.0.0 --inMemoryPersistence --skipApiVersionCheck --loose

# 2. backend, na pasta 5_DEV/backend, com a entrada e a senha do administrador
#    (senha da RNF-202: 8+ caracteres, com maiúscula, número e especial)
USUARIO_ADMIN_ENTRADA=admin.dboaz@deboaz.com USUARIO_ADMIN_SENHA='<escolha uma senha>' \
  dotnet run --project ms_mono
```

- `http://localhost:8080/health` responde 200 quando o backend está no ar;
  `http://localhost:8080/doc/api` abre a documentação (o `dotnet run` usa
  `appsettings.Development.json`, que liga o Swagger).
- Entrada do administrador: `admin.dboaz@deboaz.com`, com a senha escolhida, em
  `POST /auth/hub/entrar` (no Swagger, API **Autenticação**):

  ```bash
  curl -X POST http://localhost:8080/auth/hub/entrar \
    -H 'Content-Type: application/json' \
    -d '{
      "entrada": "admin.dboaz@deboaz.com",
      "senha": "<a senha escolhida>",
      "aparelho": "APP"
    }'
  ```

  Com `"aparelho": "APP"` a resposta traz o `tokenAcesso` e o `tokenRenovacao`
  no corpo; cole o `tokenAcesso` em **Authorize** no Swagger.
- MongoDB e Redis em outro lugar (os contêineres do qa, por exemplo): troque as
  portas por variável, nunca no `appsettings.json`:
  `ConnectionStrings__MongoDb='mongodb://localhost:37017' ConnectionStrings__Redis='localhost:16379'`.
- Sem `Autenticacao__ChavePrivada`, cada partida gera uma chave nova e os tokens
  emitidos antes deixam de valer; para mantê-los, gere uma chave RSA em PKCS#8
  base64 e passe-a nessa variável (tabela em **Configuração**).

### O primeiro usuário

Ninguém se cadastra como administrador: o time deboaz nasce da configuração.

1. **Na partida**, antes de aceitar o primeiro pedido (`/health` inclusive), o
   backend grava o registro de módulos, cria os índices do MongoDB
   (`secundario/out/commons/Indices.cs`, diagrama `identidade-acesso-documentos`) e
   garante as contas iniciais (`IInicializarContas`, chamado por
   `primario/in/commons/usuario/usuarios/Partida.cs`; RN-190, RN-265 a RN-267):
   - em todo ambiente, a conta do time (`conta-dboaz`, seção `Time`) e o
     administrador `usr-dboaz-admin`, com `USUARIO_ADMIN_ENTRADA` e o resumo
     (PBKDF2) de `USUARIO_ADMIN_SENHA`;
   - só com `AMBIENTE=hml`, as contas `conta-qains` (instituição) e
     `conta-qaaut` (autônomo) e as contas qa `usr-dboaz-qamembro`,
     `usr-qains-master`, `usr-qaaut-master`, `usr-qains-qastaff` e
     `usr-qains-qapart`, com as `USUARIO_QA_*`. Com outro `AMBIENTE` (`prd`,
     ou sem a variável) as `USUARIO_QA_*` são ignoradas (RN-266).
2. **Sem duplicar** (RN-267): cada conta é gravada pelo `_id`; o cadastro só no
   nascimento, e a cada partida a entrada (ou o código), o resumo da senha do
   Key Vault e o estado `ATIVO` valem de novo. Trocar a senha no Key Vault e
   reiniciar troca a senha; uma conta qa desativada por um teste volta ativa.
   Falta um segredo ou uma senha está fora da RNF-202: o log diz só o nome da
   variável e o processo termina com código 1, sem atender pedido (RNF-226).
   Banco fora do ar na partida também termina com código 1.
3. **Depois dele**, tudo nasce pela API:
   - o administrador cria os membros do time em `POST /usuarios` e dá as
     permissões deles em `PUT /usuarios/{id}/permissoes`;
   - uma escola ou um educador cria a própria conta em `POST /contas`, que é
     público: a resposta traz a entrada da conta master, a senha gerada (mostrada
     uma vez) e o `comprovante`, que confirma a senha em
     `POST /auth/hub/confirmar-senha`;
   - a conta master cria o staff e os participantes em `POST /usuarios`; o staff
     gera a própria senha com o código de liberação em
     `POST /usuarios/gerar-senha`, e o participante entra no quest com o código
     de acesso.

A documentação da API (Swagger UI com a identidade deboaz) fica em
`http://localhost:8080/doc/api`: abre na capa do catálogo, e o seletor do
topo, à direita, abre cada API — **Sistema**, **Autenticação**, **Usuários** e
**Autorização**, uma por módulo (`/doc/api/<modulo>.json`). Cada uma começa
pelo guia de uso de `ms_mono/sistema/guias/<modulo>.md` (fluxo passo a passo,
erros e os diagramas do módulo, na largura toda) e tem o botão **Authorize**
para o token; a raiz `/` leva ao catálogo (302). Os exemplos ficam dentro de
cada endpoint: o pedido em **Request body** e cada resposta, com os erros e as
mensagens exatas, em **Responses** (seletor **Examples**). O endereço da página
abre o endpoint (`#/<Tag>/<operationId>`), para compartilhar. Dentro de cada API
há um grupo (tag) por controller, e o resumo, a descrição, os parâmetros e as
respostas de cada endpoint vêm dos comentários XML da ação (`<summary>`,
`<remarks>`, `<param>`, `<response>`); o operationId é o `Name` da rota.
Ela existe quando `Swagger:Habilitado` é `true`: no `dotnet run`
(appsettings.Development.json), no CI local e em homologação. Em produção fica
desligada (a raiz responde 404) e o WAF bloqueia `/doc/api`. O banner (animado
e parado), o logo, o favicon e as cores de `wwwroot/doc/api/design/` vêm de
`pnpm run gerar` em `0_Assets/design/`.

Os diagramas dos guias são cópias da versão mais recente de `2_Diagramas/`, em
`ms_mono/wwwroot/doc/api/diagramas/<legenda>.svg`, porque a imagem do backend
só enxerga `5_DEV/backend/`. Depois de uma versão nova de um deles, rode
`./5_DEV/backend/scripts/diagramas-da-doc.sh`; o teste
`DiagramasDaDocumentacaoTests` falha enquanto a cópia estiver atrás.

Os exemplos de `ms_mono/sistema/exemplos/<modulo>.json` saem do contrato de API
mais recente de cada módulo (o PEDIDO e as RESPOSTAS de cada caixa), com as
mensagens das falhas do core. Depois de uma versão nova de um contrato, rode
`./5_DEV/backend/scripts/exemplos-da-doc.py`; o teste
`Todo_endpoint_traz_os_exemplos_do_contrato` falha se um endpoint ficar sem
exemplo ou sobrar um. Controller novo:
`[ApiExplorerSettings(GroupName = <modulo>)]`, `[Tags]` e o grupo dele em
`DocumentacaoApi.Apis`. Módulo novo: a constante do documento e uma linha em
`DocumentacaoApi.Apis`, o guia em `sistema/guias/`, as legendas no script dos diagramas e o módulo em
`MODULOS` do script dos exemplos.

## Configuração

Tudo que muda de um ambiente para outro vem da configuração, nunca do código:
`ms_mono/appsettings.json` traz os padrões e cada parâmetro pode ser trocado por
variável de ambiente, com `__` no lugar de `:`. Faltando um parâmetro
obrigatório, o backend não sobe e diz qual variável definir
(`ms_mono/secundario/out/commons/Configuracoes.cs`).

| Variável | Padrão (appsettings.json) | Para quê |
|---|---|---|
| `ConnectionStrings__MongoDb` | `mongodb://localhost:27017` | Servidor do MongoDB (`ConnectionStrings__Mongo` também vale, nome usado no Azure) |
| `ConnectionStrings__Redis` | `localhost:6379` | Servidor do Redis |
| `Mongo__Banco` | `boaz` | Banco dentro do MongoDB |
| `Time__Nome` | `Time deboaz` | Nome da conta do time deboaz |
| `Time__Documento` | vazio | CNPJ da conta do time |
| `Time__Responsavel` | vazio | Responsável pela conta do time |
| `Time__Telefone` | vazio | Telefone do responsável |
| `AMBIENTE` | — | `hml` cria também as contas qa (RN-265); qualquer outro valor só o administrador (RN-266) |
| `USUARIO_ADMIN_ENTRADA`, `USUARIO_ADMIN_SENHA` | — **segredo** | Entrada e senha do administrador do deboaz (Key Vault, RN-190); obrigatórias |
| `USUARIO_QA_MEMBRO_*`, `USUARIO_QA_MASTER_*`, `USUARIO_QA_AUTONOMO_*`, `USUARIO_QA_STAFF_*` (`_ENTRADA`, `_SENHA`), `USUARIO_QA_PARTICIPANTE_CODIGO` | — **segredo** | Contas qa; obrigatórias só com `AMBIENTE=hml` |
| `Autenticacao__ChaveId` | `deboaz-local` | O `kid` da chave que assina o token |
| `Autenticacao__ChavePrivada` | — **segredo** | Chave RSA em PKCS#8 base64; sem ela, uma chave nova a cada partida e os tokens caem quando o backend reinicia |
| `Swagger__Habilitado` | `false` (`true` em Development) | Liga `/doc/api` |

Os dados da conta do time são gravados só quando ela nasce no banco: mudar a
variável depois não altera o que já existe. A entrada e a senha das contas
iniciais, ao contrário, valem de novo a cada partida (RN-267).

Salvar permissões grava `usuarios` e `registrosPermissao` numa transação
quando o MongoDB é replica set (RNF-223); num MongoDB standalone (o do job
`backend` do GitHub Actions) grava em sequência, sem transação. A sigla `DBOAZ`, o id
`conta-dboaz` e o domínio `deboaz.com` das entradas são regras do produto
(requisito 17, RN-179 e RN-180) e ficam no core.
