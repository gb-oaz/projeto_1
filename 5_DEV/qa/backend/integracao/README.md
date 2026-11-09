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
│   ├── BancoDaPartida.cs           coleções e índices do banco dessa instância (RNF-227)
│   ├── Auditoria.cs                leitura das coleções "auditoria" e "auditoriaErros" (RN-178, RN-270, RN-271)
│   ├── Relogio.cs                  relógio de teste do backend e fechamento sob demanda (15 RN-278)
│   ├── CardGuardado.cs             leitura e histórico do card em "cardUsuarios" (17 RN-280, 15 RN-278)
│   ├── AoVivo.cs                   conexão SSE de GET /<modulo>/card/ao-vivo: eventos "card", "encerrado" e ": ping"
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
    ├── ContasIniciaisPassos.cs     contas iniciais (RN-179, RN-190, RN-265 a RN-268, RNF-225, RNF-226)
    ├── AuditoriaPassos.cs          auditoria do que muda estado, erros e alertas, traceId (RN-178, RN-270, RN-271)
    ├── IndicesPassos.cs            coleções e índices prontos na partida e GET /health (RNF-227, RNF-227-api)
    ├── modulos.feature             15_modulos.md: card comum, pela instância do Usuários (RN-276, RN-278, RN-279)
    ├── CardPassos.cs               GET /usuarios/card (RN-189, RN-280; 15 RN-276, RN-278)
    ├── AoVivoPassos.cs             GET /usuarios/card/ao-vivo (RN-281; 15 RN-279)
    └── PreferenciasPassos.cs       GET e PUT /usuarios/eu/preferencias (RN-283)
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
| `15_modulos.md` | `hub/usuarios/modulos.feature` | `Módulos · card do Usuários` |

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
da Presença e do Quiz, a caixa do SAC), é tela (fica no qa do
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
| `USUARIO_QA_ADMIN_*`, `USUARIO_QA_MEMBRO_*`, `USUARIO_QA_MASTER_*`, `USUARIO_QA_AUTONOMO_*`, `USUARIO_QA_STAFF_*` (`_ENTRADA`, `_SENHA`), `USUARIO_QA_PARTICIPANTE_CODIGO`, `USUARIO_QA_MODULOS` (JSON `{"staff": {"mods": {…}}, "time": {"mods": {…}}}`, RN-268) | as contas qa de homologação (RN-265), as mesmas do backend; nunca valor fixo no repositório | entradas do requisito; senha sem padrão (o cenário falha dizendo a variável) |
| — | os cenários usam o administrador qa (`USUARIO_QA_ADMIN_*`, RN-265) no lugar do administrador real; enquanto o CI não entrega `USUARIO_QA_ADMIN_SENHA`, a suíte usa as `USUARIO_ADMIN_*` | — |
| `API_COMANDO` | comando (por `/bin/sh -c`) que sobe o backend publicado, para os cenários de inicialização: primeira partida, segunda partida, produção, segredo ausente (RN-190, RN-266, RN-267, RNF-225, RNF-226). Sem ele, esses cenários ficam pendentes | vazia |
| `PARTIDA_*` | repassadas sem o prefixo à instância do `API_COMANDO` (conexões, chave e **um banco próprio**, diferente do `MONGO_BANCO`) | — |
| `CHAVE_PRIVADA` | a chave do token (PKCS#8 em base64), para montar os tokens recusados de RNF-216 | vazia |

**Como o CI entrega as variáveis** (`4_CI_CD/local/ci.sh`), no lugar do Key
Vault e novas a cada execução:

- o backend sobe com `AMBIENTE=hml`;
- o `ci.sh` gera a chave do token (`Autenticacao__ChaveId`,
  `Autenticacao__ChavePrivada`, que vira `CHAVE_PRIVADA` aqui) e as
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
  nos testes do backend. Única exceção: a RNF-227 (coleções e índices prontos
  na partida) promete o próprio esquema do banco, sem endereço na API; os
  passos leem só os metadados (coleções, chave e opções dos índices) e os `_id`
  do banco da instância de Partida (`PARTIDA_Mongo__Banco`, sem ela ficam
  pendentes), nunca o conteúdo nem os nomes internos dos índices.
  Segunda exceção: a auditoria (RN-178, RN-270, RN-271, RN-274 e, na
  Autenticação, RN-272 e RN-273) promete os registros das coleções
  "auditoria" e "auditoriaErros", sem endereço na API. Os passos
  (`suporte/Auditoria.cs`, com as frases comuns em
  `suporte/PassosDeAuditoria.cs` e os pedidos no `AuditoriaPassos.cs` de cada
  pasta) só leem essas duas coleções do `MONGO_BANCO`, pelos
  campos do modelo de documentos do requisito, e contam como "ganho" só o que
  apareceu depois do preparo do cenário. O traceId também é conferido no
  cabeçalho `X-Trace-Id` da resposta.
  Terceira exceção: o card guardado (17 exemplo "card guardado", 15 RN-278
  fechamento) promete o próprio documento de "cardUsuarios";
  `suporte/CardGuardado.cs` o lê pelo contaId e o perfil, pelos campos do
  modelo de documentos.
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
  no passo (`Mundo.CamposSemConferir`): o `ultimoPagamento` da conta, que vem
  da cobrança, e a `versao` do card do exemplo (418, o histórico da escola).
- **O que vem do banco, porque a API não cria**: o módulo contratado pela
  escola (o catálogo ainda não tem o endereço de contratar e descontratar:
  `Estado.Contratar`/`Descontratar`), a versão das permissões dos exemplos
  ("na versão 6": `Estado.DefinirVersao`), a hora do pedido do código de
  liberação ("pedido há 73 h": `Estado.PedidoHa`; "vence hoje":
  `Estado.CodigoVenceHoje`), o último acesso ("Sem acesso há 30 dias":
  `Estado.UltimoAcesso`, `Estado.UltimoAcessoMaster`), os meses anteriores do
  card guardado (`Estado.MesesAnterioresDoCard`), o tempo sem uso da sessão e
  os bloqueios seguidos.
- **Relógio do backend** (`suporte/Relogio.cs`, 15 RN-278): só com
  `AMBIENTE=hml` e `Teste__Relogio=sim`, o backend expõe `PUT /_teste/relogio`
  (`{"agora": "2027-02-28T23:50:00-03:00"}`, 204; o relógio anda a partir
  daí), `DELETE /_teste/relogio` (volta ao real) e `POST /_teste/fechamento`
  (roda o fechamento da 00:00 de Brasília na hora, 204). Sem eles, 404. O
  relógio volta ao real no fim do cenário que o mudou.
- **Card ao vivo** (`suporte/AoVivo.cs`): a conexão SSE fica aberta pela
  suíte e fecha no fim do cenário; os tempos (intervalo, ping de 30 s) são
  esperados de verdade, com 5 s de folga, por isso esses cenários levam até
  ~40 s cada.
- **Token desatualizado**: quem pede com um token de versão antiga recebe
  `401 PERMISSOES_DESATUALIZADAS`; no preparo, `Mundo.EnviarComo` renova e
  tenta de novo uma vez, como o cliente faz.
- **Comparação de JSON**: os campos do exemplo precisam estar na resposta com o
  mesmo valor; campo a mais é aceito; lista em qualquer ordem; data e hora do
  exemplo só pedem uma data e hora; `"eyJ…"` só pede o começo. Os `mods` de um
token são conferidos exatos (nenhum módulo a mais).
