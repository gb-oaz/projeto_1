# Integração da API — Reqnroll + xUnit

Suíte: `5_DEV/qa/backend/integracao/` (`<contexto>/<feature>/<feature>.feature`, `suporte/`, `scripts/`). Pacotes: `Reqnroll.xUnit`, `xunit`, `MongoDB.Driver`, `StackExchange.Redis`, `JunitXml.TestLogger`.

## `.feature`

- `# language: pt` na primeira linha; cabeçalho com o documento e a **versão** do requisito copiado ("mudou lá, muda aqui").
- `Funcionalidade:` com o título do documento.
- **Cópia fiel** dos Cenários de API (mesmo texto, mesmos dados); cada cenário com a tag do ID (`@RF-134`) ou da âncora do grupo (`@formas-de-entrar`).
- Não reescreva o cenário para facilitar o passo: adapte o passo.

## Passos (step definitions)

- Expressões que casam com as frases do Gherkin; parâmetros tipados; um passo reaproveitado por várias frases iguais.
- Estado do cenário em contexto injetado (`ScenarioContext` ou classe de contexto via DI do Reqnroll), nunca em campo estático.
- Chamadas HTTP só pelo `API_URL`; corpo da doc string enviado como veio.
- **Preparo (`Dado`)**: pela própria API sempre que existir endereço; Mongo/Redis direto só para o que a API não consegue criar (ex.: conta master inicial) ou para verificar efeito não exposto (auditoria) — isso é exceção documentada no passo.
- Asserções do `Então`: status, código de erro, campos citados, cabeçalhos (`X-Trace-Id`) e cookies, exatamente como o cenário diz — nem mais, nem menos.
- Dados únicos por execução (sufixo) para rodar em paralelo e repetido; limpeza nos hooks `[AfterScenario]`.

## Vermelho pelo motivo certo

Antes do código, o esperado é 404/501 do endpoint inexistente ou a asserção do `Então` falhando — nunca exceção no passo, passo indefinido ou falha de conexão.

## Rodar

Contêiner (`Dockerfile` da suíte) contra o backend do CI local: `4_CI_CD/local/ci.sh --qa backend`.
