# qa · backend · integração

Testes de aceitação da API: os **Cenários de API** dos requisitos
(`1_Requisitos/*.html`, seção `cenarios-api`) rodando contra o backend no ar.
Cada cenário em Gherkin vira um teste xUnit que faz pedidos HTTP de verdade.

Ferramentas: **Reqnroll** (Gherkin em português) sobre **xUnit** e o
`HttpClient` do .NET. Mais três pacotes, cada um por um motivo:
`JunitXml.TestLogger` (o CI pede o resultado em JUnit XML), `MongoDB.Driver` e
`StackExchange.Redis` (preparar o estado que a API não cria).

## 1. Como o Reqnroll funciona, em 4 passos

```
requisito (.md)    ──copiar-cenarios.py──►  autenticacao.feature   (Gherkin)
                                                  │  dotnet build
                                                  ▼
                                          autenticacao.feature.cs  (gerado, não vai para o git)
                                                  │  dotnet test
                                                  ▼
            cada linha Dado/Quando/Então  ──►  método [Given]/[When]/[Then] em *Passos.cs
```

1. **O `.feature`** é o texto dos cenários, copiado do requisito. Ninguém edita
   à mão: mudou no requisito, copie de novo (seção 3).
2. **O `.feature.cs`** é gerado sozinho pelo pacote `Reqnroll.xUnit` a cada
   `dotnet build`: uma classe xUnit com um teste por cenário (e um por linha de
   `Exemplos`). **Não há comando para gerá-lo**, só compilar. Ele aparece ao
   lado do `.feature` e está no `.gitignore`.
3. **Os passos** (`*Passos.cs`) dizem o que cada frase faz. O Reqnroll procura,
   para cada linha, o método cujo atributo tem uma expressão regular que casa
   com a frase inteira; cada grupo `(...)` vira um parâmetro:

   ```gherkin
   Quando envia POST /auth/hub/entrar com a entrada "maria.ebfga@deboaz.com" e uma senha errada
   ```
   ```csharp
   [When(@"^envia POST /auth/hub/entrar com a entrada ""(.+)"" e (uma senha errada|qualquer senha)$")]
   public async Task EntradaErrada(string entrada, string _) { … }
   ```

   - aspas dentro da expressão são dobradas (`""`), porque é string `@"…"`;
   - o `E` herda o tipo da linha de cima (um `E` depois de `Então` procura `[Then]`);
   - a doc string `"""…"""` chega como último parâmetro `string`;
   - no `Esquema do Cenário`, o `<valor>` é trocado pela coluna de `Exemplos`
     antes de procurar o passo.
4. **O mundo** (`suporte/Mundo.cs`) guarda o estado de um cenário: as pessoas
   criadas, as sessões abertas e a última resposta. O Reqnroll cria um por
   cenário e o entrega no construtor das classes de passos.

## 2. Pastas

```
integracao/
├── Boaz.Qa.Integracao.csproj       pacotes e versões
├── reqnroll.json                   língua pt; passo pendente = cenário pulado
├── xunit.runner.json               um cenário por vez (todos usam o mesmo banco)
├── Dockerfile                      a imagem que o CI roda (contrato das suítes)
├── scripts/copiar-cenarios.py      requisito → .feature
├── scripts/conferir-passos.py      toda frase tem um passo, e só um, sem rodar os testes
├── suporte/                        comum a todas as features
│   ├── Ambiente.cs                 variáveis de ambiente
│   ├── Api.cs                      HttpClient, cookies e a resposta
│   ├── Mundo.cs                    pessoas dos exemplos, sessões, troca de valores
│   ├── Respostas.cs                passos "a resposta é 401 ENTRADA_INVALIDA …"
│   ├── Comparacao.cs               confere o JSON do cenário com o da resposta
│   ├── Jwt.cs                      lê e assina tokens
│   ├── Estado.cs                   limpa e prepara o banco e o cache (menos as contas iniciais)
│   ├── ContasIniciais.cs           administrador e contas qa: ids e variáveis do Key Vault
│   ├── Partida.cs                  sobe uma instância própria do backend (cenários de inicialização)
│   └── Ganchos.cs                  antes de cada cenário: limpa tudo
├── commons/autenticacao/           17_modulo-usuario-a-autenticacao.md
│   ├── autenticacao.feature
│   └── AutenticacaoPassos.cs
├── commons/autorizacao/            17_modulo-usuario-b-autorizacao.md
│   ├── autorizacao.feature
│   └── AutorizacaoPassos.cs
└── hub/usuarios/                   17_modulo-usuario.md (contas, staff, participantes, time)
    ├── usuarios.feature
    ├── UsuariosPassos.cs
    └── ContasIniciaisPassos.cs     contas iniciais (RN-190, RN-265 a RN-267, RNF-225, RNF-226)
```

A pasta segue o core: `<contexto>/<feature>/` (`commons/autenticacao`,
`hub/usuarios`…), como pede o requisito 14 (RN-154).

## 3. Tarefas do dia a dia

**O requisito mudou: atualizar o `.feature`** (na raiz do repositório)

```bash
python3 5_DEV/qa/backend/integracao/scripts/copiar-cenarios.py \
  1_Requisitos/17_modulo-usuario-a-autenticacao.md \
  5_DEV/qa/backend/integracao/commons/autenticacao/autenticacao.feature \
  Autenticação
```

Depois rode `dotnet build`: frase nova sem passo aparece como cenário pulado,
com o texto `No matching step definition`.

Os outros requisitos, do mesmo jeito:

| Requisito | `.feature` | Funcionalidade |
|---|---|---|
| `17_modulo-usuario-a-autenticacao.md` | `commons/autenticacao/autenticacao.feature` | `Autenticação` |
| `17_modulo-usuario-b-autorizacao.md` | `commons/autorizacao/autorizacao.feature` | `Autorização` |
| `17_modulo-usuario.md` | `hub/usuarios/usuarios.feature` | `Usuários` |

Dois cenários com o mesmo nome no requisito: o segundo ganha o ID no fim
(`nome curto repetido · RF-153`), porque o Reqnroll não aceita nome repetido.

**Feature nova** (ex.: Cadastro)

1. Rode o script com o requisito e `<contexto>/<feature>/<feature>.feature`.
2. Crie `<contexto>/<feature>/<Feature>Passos.cs` com `[Binding]` e o
   construtor recebendo o `Mundo`.
3. Antes de criar um passo, procure se a frase já existe (`grep -rn "a resposta é" .`):
   passos de `suporte/` valem para todas as features.

**Frase que não dá para automatizar como está**: mude o requisito (versão
nova, skill `requisito`) e copie de novo. O `.feature` nunca diverge do
requisito.

**Passo que ainda não dá para testar**: `throw new PendingStepException("motivo")`.
O cenário sai como pulado, com o motivo na saída. Os casos de hoje: o
contrato ainda não tem o endereço (sala ao vivo, troca de chave, os pedidos
da Presença e do Quiz, a caixa do SAC, a auditoria), é tela (fica no qa do
frontend), só o banco mostra (fica nos testes do backend), é segredo do Key
Vault ou é fora de escopo.

**Conferir que toda frase tem passo**: o Reqnroll só acusa a frase sem passo,
a frase com dois passos ou o número errado de parâmetros quando o cenário
chega nela. Com o backend ainda em 404, o cenário para no preparo e nada
aparece. Rode, depois de mexer num `.feature` ou num `*Passos.cs`:

```bash
python3 scripts/conferir-passos.py     # sai com 1 se achar algum problema
```

**Grupo opcional não vira parâmetro**: com `(…)?` ou `(a|(b))`, o Reqnroll só
passa os grupos que casaram, e o método com um parâmetro a mais quebra. Escreva
um atributo para cada forma da frase, cada um no seu método.

## 4. Rodar

**No CI local**, tudo em contêiner (backend, banco, cache e esta suíte):

```bash
./4_CI_CD/local/ci.sh --qa backend           # build, testes e esta suíte
./4_CI_CD/local/ci.sh --so-qa --qa backend   # só esta suíte
```

**Na máquina**, contra o backend local e um banco e um cache **descartáveis**,
porque a suíte apaga tudo neles antes de cada cenário:

```bash
# 1. banco e cache só para o teste, em portas próprias
docker run -d --rm --name qa-mongo -p 127.0.0.1:37017:27017 mongo:8.0
docker run -d --rm --name qa-redis -p 127.0.0.1:16379:6379 redis:8.0

# 2. o backend (em outro terminal), na porta 8080
dotnet run --project 5_DEV/backend/ms_mono

# 3. a suíte (em 5_DEV/qa/backend/integracao/)
export API_URL=http://localhost:8080 MONGO_URL=mongodb://127.0.0.1:37017 \
       REDIS_URL=127.0.0.1:16379 QA_PODE_LIMPAR=sim
# as mesmas USUARIO_* que o backend recebeu (Key Vault de homologação), e,
# para os cenários de inicialização, o backend publicado e um banco próprio
export USUARIO_ADMIN_ENTRADA=… USUARIO_ADMIN_SENHA=… USUARIO_QA_…=…
export API_COMANDO="cd <pasta do publish> && exec dotnet Boaz.MsMono.dll" \
       PARTIDA_ConnectionStrings__MongoDb=mongodb://127.0.0.1:37017 \
       PARTIDA_ConnectionStrings__Redis=127.0.0.1:16379 PARTIDA_Mongo__Banco=boaz_partida
dotnet test                                        # tudo
dotnet test --filter "Category=RF-134"             # só os cenários de um ID
dotnet test --filter "DisplayName~sair de todos"   # pelo nome do cenário

# 4. no fim
docker rm -f qa-mongo qa-redis
```

Cada tag do `.feature` (`@RF-134`) vira uma `Category` do xUnit, por isso o
filtro por ID funciona.

**Lendo o resultado**: `Passed` = o backend faz o que o cenário diz; `Failed`
= não faz (a mensagem mostra o passo e a resposta, por exemplo
`preparo: POST /contas respondeu 404`); `Skipped` = passo pendente ou sem
método. No TDD, enquanto o endpoint não existe, o vermelho esperado é o 404.

## 5. Variáveis

| Variável | Para quê | Padrão na máquina |
|---|---|---|
| `API_URL` | o backend | `http://localhost:8080` |
| `MONGO_URL`, `REDIS_URL` | preparar o estado | `localhost` 27017 e 6379 |
| `MONGO_BANCO` | o banco do backend dentro do Mongo (contratações, versão das permissões) | `boaz` |
| `QA_PODE_LIMPAR=sim` | autoriza apagar o banco e o cache antes de cada cenário; sem ela, nada roda | — |
| `USUARIO_ADMIN_ENTRADA`, `USUARIO_ADMIN_SENHA` | o administrador do deboaz: as mesmas variáveis que o backend recebe do Key Vault (RN-190, RNF-225). `ADMIN_ENTRADA` e `ADMIN_SENHA` (nomes antigos) são lidas só se faltarem as `USUARIO_ADMIN_*` | `admin.dboaz@deboaz.com`, vazia |
| `USUARIO_QA_MEMBRO_*`, `USUARIO_QA_MASTER_*`, `USUARIO_QA_AUTONOMO_*`, `USUARIO_QA_STAFF_*` (`_ENTRADA`, `_SENHA`), `USUARIO_QA_PARTICIPANTE_CODIGO` | as contas qa de homologação (RN-265), as mesmas do backend; nunca valor fixo no repositório | entradas do requisito; senha sem padrão (o cenário falha dizendo a variável) |
| `API_COMANDO` | comando (por `/bin/sh -c`) que sobe o backend publicado, para os cenários de inicialização: primeira partida, segunda partida, produção, segredo ausente (RN-190, RN-266, RN-267, RNF-225, RNF-226). Sem ele, esses cenários ficam pendentes | vazia |
| `PARTIDA_*` | repassadas sem o prefixo à instância do `API_COMANDO` (conexões, chave e **um banco próprio**, diferente do `MONGO_BANCO`) | — |
| `CHAVE_PRIVADA` | a chave do token (PKCS#8 em base64), para montar os tokens recusados de RNF-216 | vazia |

**Como o CI entrega as variáveis** (`4_CI_CD/local/ci.sh`), no lugar do Key
Vault e novas a cada execução:

- o backend sobe com `AMBIENTE=hml`;
- o `ci.sh` gera a chave do token (`Autenticacao__ChaveId`,
  `Autenticacao__ChavePrivada`, que vira `CHAVE_PRIVADA` aqui) e as 11
  `USUARIO_*` (administrador e contas qa) e passa os mesmos valores ao backend
  e a esta suíte;
- a suíte roda numa imagem derivada (`qa-backend-integracao-api:ci`), com o
  publish do backend em `/api` e `API_COMANDO="cd /api && exec dotnet
  Boaz.MsMono.dll"`. As `PARTIDA_*` levam `ConnectionStrings__Mongo`,
  `ConnectionStrings__MongoDb`, `ConnectionStrings__Redis` (o mesmo Redis, com
  `defaultDatabase=1`), `Mongo__Banco=boaz_partida` e `Autenticacao__ChaveId`
  e `Autenticacao__ChavePrivada`;
- o Mongo do CI local é um replica set de um nó (`rs0`).

**Contas iniciais e a limpeza**: o backend cria o administrador e, em
homologação, as contas qa ao iniciar (RN-190, RN-265). Na primeira limpeza, a
suíte guarda os documentos desses `_id` (`ContasIniciais.Ids`) e os devolve,
como estavam, depois de cada limpeza: os cenários continuam começando do
banco "recém-iniciado", sem depender de o backend recriá-los. A limpeza do
Redis (`FlushAllDatabases`) também apaga o banco 1, o da instância de Partida,
e é isso mesmo que se quer: cada cenário começa com o cache dela vazio.

**Partida** (`suporte/Partida.cs`): os cenários que falam de iniciar a
aplicação sobem uma instância nova com o `API_COMANDO`, numa porta livre
(`ASPNETCORE_HTTP_PORTS`), com `AMBIENTE` (`hml` ou `prd`) e um Key Vault do
próprio cenário (entradas do requisito e senhas geradas na hora, RNF-202). Lê
só o HTTP, o código de saída e o log. As `USUARIO_*` da suíte não passam para
ela.

## 6. Regras

- **Caixa-preta**: nada de `5_DEV/backend/` é referenciado (RN-151).
- **Banco e cache só preparam**, nunca conferem: o que só eles mostram fica
  nos testes do backend.
- **Valores dos exemplos**: `usr-ebfga-maria`, `3f9c2a71` (sessão da Maria no
  navegador) e `EBFGA-LUCAS-0412` são trocados pelos valores reais na hora do
  pedido (`Mundo.Apelidos`); a pessoa é criada pela API na primeira vez que
  uma frase a cita.
- **Quem é quem**: a conta master da EBFGA cria as staffs (Maria, João, Rui) e
  dá as permissões pela API; a Ana é criada pelo João, por isso fica "abaixo"
  dele (RN-252); o Caio é do time deboaz e recebe permissões do administrador.
- **Quem o cenário cria fica anotado**: depois de `envia POST /usuarios com:`
  criar a Maria, `usr-ebfga-maria` é ela, e não uma Maria nova. Valores gerados
  (a senha, o código de liberação `482913`, o comprovante `c7e1…`) valem pelo
  formato: o do exemplo é trocado pelo que veio, se o que veio tem a forma certa.
- **Campos que a API não deixa reproduzir** saem da comparação, com o motivo
  no passo (`Mundo.CamposSemConferir`): o histórico do card (`selo`, `noMes`,
  `meses`) e o `ultimoPagamento` da conta, que vem da cobrança.
- **O que vem do banco, porque a API não cria**: o módulo contratado pela
  escola (o catálogo ainda não tem o endereço de contratar e descontratar:
  `Estado.Contratar`/`Descontratar`), a versão das permissões dos exemplos
  ("na versão 6": `Estado.DefinirVersao`), a hora do pedido do código de
  liberação ("pedido há 73 h": `Estado.PedidoHa`), o tempo sem uso da sessão e
  os bloqueios seguidos.
- **Token desatualizado**: quem pede com um token de versão antiga recebe
  `401 PERMISSOES_DESATUALIZADAS`; no preparo, `Mundo.EnviarComo` renova e
  tenta de novo uma vez, como o cliente faz.
- **Comparação de JSON**: os campos do exemplo precisam estar na resposta com o
  mesmo valor; campo a mais é aceito; lista em qualquer ordem; data e hora do
  exemplo só pedem uma data e hora; `"eyJ…"` só pede o começo. Os `mods` de um
token são conferidos exatos (nenhum módulo a mais).
