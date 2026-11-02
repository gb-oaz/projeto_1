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
requisito (.html)  ──copiar-cenarios.py──►  autenticacao.feature   (Gherkin)
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
├── suporte/                        comum a todas as features
│   ├── Ambiente.cs                 variáveis de ambiente
│   ├── Api.cs                      HttpClient, cookies e a resposta
│   ├── Mundo.cs                    pessoas dos exemplos, sessões, troca de valores
│   ├── Respostas.cs                passos "a resposta é 401 ENTRADA_INVALIDA …"
│   ├── Comparacao.cs               confere o JSON do cenário com o da resposta
│   ├── Jwt.cs                      lê e assina tokens
│   ├── Estado.cs                   limpa e prepara o banco e o cache
│   └── Ganchos.cs                  antes de cada cenário: limpa tudo
├── commons/autenticacao/           17_modulo-usuario-a-autenticacao.html
│   ├── autenticacao.feature
│   └── AutenticacaoPassos.cs
└── commons/autorizacao/            17_modulo-usuario-b-autorizacao.html
    ├── autorizacao.feature
    └── AutorizacaoPassos.cs
```

A pasta segue o core: `<contexto>/<feature>/` (`commons/autenticacao`,
`hub/usuarios`…), como pede o requisito 14 (RN-154).

## 3. Tarefas do dia a dia

**O requisito mudou: atualizar o `.feature`** (na raiz do repositório)

```bash
python3 5_DEV/qa/backend/integracao/scripts/copiar-cenarios.py \
  1_Requisitos/17_modulo-usuario-a-autenticacao.html \
  5_DEV/qa/backend/integracao/commons/autenticacao/autenticacao.feature \
  Autenticação
```

Depois rode `dotnet build`: frase nova sem passo aparece como cenário pulado,
com o texto `No matching step definition`.

Os outros requisitos, do mesmo jeito:

| Requisito | `.feature` | Funcionalidade |
|---|---|---|
| `17_modulo-usuario-a-autenticacao.html` | `commons/autenticacao/autenticacao.feature` | `Autenticação` |
| `17_modulo-usuario-b-autorizacao.html` | `commons/autorizacao/autorizacao.feature` | `Autorização` |

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
O cenário sai como pulado, com o motivo na saída. Hoje são três casos: o
contrato ainda não tem o endereço (sala ao vivo, troca de chave, os pedidos
da Presença e do Quiz), é tela (fica no qa do frontend), só o banco mostra
(fica nos testes do backend) ou é fora de escopo.

**Conferir que toda frase tem passo**: o Reqnroll só acusa a frase sem passo
quando o cenário chega nela. Com o backend ainda em 404, o cenário para no
preparo; procure `No matching step definition` na saída de `dotnet test
--logger "console;verbosity=normal"`.

## 4. Rodar

**No CI local**, tudo em contêiner (backend, banco, cache e esta suíte):

```bash
./4_CI_CD/local/ci.sh --qa backend
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
| `ADMIN_ENTRADA`, `ADMIN_SENHA` | o administrador do deboaz (no lugar do Key Vault) | `admin.dboaz@deboaz.com`, vazia |
| `CHAVE_PRIVADA` | a chave do token (PKCS#8 em base64), para montar os tokens recusados de RNF-216 | vazia |

No CI local, o `ci.sh` gera a chave e a senha do administrador a cada execução
e passa os mesmos valores ao backend (`Autenticacao__ChavePrivada`,
`Autenticacao__ChaveId`, `Administrador__Entrada`, `Administrador__Senha`) e a
esta suíte.

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
- **O que vem do banco, porque a API não cria**: o módulo contratado pela
  escola (o catálogo ainda não tem o endereço de contratar e descontratar:
  `Estado.Contratar`/`Descontratar`), a versão das permissões dos exemplos
  ("na versão 6": `Estado.DefinirVersao`), o tempo sem uso da sessão e os
  bloqueios seguidos.
- **Token desatualizado**: quem pede com um token de versão antiga recebe
  `401 PERMISSOES_DESATUALIZADAS`; no preparo, `Mundo.EnviarComo` renova e
  tenta de novo uma vez, como o cliente faz.
- **Comparação de JSON**: os campos do exemplo precisam estar na resposta com o
  mesmo valor; campo a mais é aceito; lista em qualquer ordem; data e hora do
  exemplo só pedem uma data e hora; `"eyJ…"` só pede o começo. Os `mods` de um
token são conferidos exatos (nenhum módulo a mais).
