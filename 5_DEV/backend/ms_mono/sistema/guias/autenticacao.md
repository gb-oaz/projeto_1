Quem é a pessoa e por quanto tempo ela fica conectada. Requisito: `1_Requisitos/17_modulo-usuario-a-autenticacao.md` · contrato: `autenticacao-contrato-api`.

## Como usar

Os exemplos de pedido e de cada resposta, inclusive os erros, estão dentro de cada endpoint, logo abaixo deste guia: abra o endpoint e veja **Request body** e **Responses**.

### 1. Entrar

Há uma entrada por frente, e cada uma devolve um **token de acesso** (JWT RS256, válido por 15 min) e um **token de renovação**:

| Frente | Endereço | Quem entra | Com o quê |
|---|---|---|---|
| Hub | `POST /auth/hub/entrar` | conta master, autônomo, staff, membros do time, administrador | entrada e senha |
| Quest | `POST /auth/quest/entrar` | participante ou um responsável dele | código de acesso e quem está entrando |
| Sala | `POST /auth/sala/entrar` | qualquer pessoa, sem cadastro | número da sala ao vivo |

- `"aparelho": "NAVEGADOR"` não traz o `tokenRenovacao` no corpo: ele vai no cookie `deboaz_renovacao` (HttpOnly, Secure, SameSite=Strict, só para `/auth/renovar`).
- No quest, `quem` diz quem está com o aparelho: o `PARTICIPANTE` ou um `RESPONSAVEL`, com o parentesco.
- A sala ainda responde `404 SALA_FECHADA`: o módulo de salas ao vivo não existe ainda.

### 2. Primeiro acesso

Quem ganhou uma senha nova (conta criada em `POST /contas` ou senha gerada em `POST /usuarios/gerar-senha`, na API **Usuários**) confirma que anotou em `POST /auth/hub/confirmar-senha` com o `comprovante`. A confirmação já abre a sessão; o comprovante vale uma vez.

### 3. Chamar a API

Mande o token em todo pedido com login:

```
Authorization: Bearer eyJhbGciOiJSUzI1NiIs…
```

Aqui no Swagger, toque **Authorize** e cole só o token: os endpoints com cadeado passam a mandá-lo.

### 4. Renovar

Com o token de acesso vencido (`401 TOKEN_INVALIDO`) ou com permissões que mudaram (`401 PERMISSOES_DESATUALIZADAS`), troque em `POST /auth/renovar`, mandando o `tokenRenovacao` (no app) ou só o cookie (no navegador).

A resposta traz um token de acesso novo **e** uma renovação nova; a anterior deixa de valer. Usar de novo uma renovação já trocada é sinal de cópia: a sessão inteira termina (`401 SESSAO_TERMINADA`) e a pessoa entra outra vez.

### 5. Sair e aparelhos

- `POST /auth/sair`: encerra a sessão deste aparelho.
- `GET /auth/sessoes`: lista os aparelhos conectados (só no hub), com `esteAparelho` marcando o atual.
- `DELETE /auth/sessoes/{sid}`: sai de um aparelho da lista; `DELETE /auth/sessoes`: sai de todos, inclusive este.

### Chave pública

`GET /auth/chaves` publica a chave que confere a assinatura do token (JWKS), para outros serviços validarem o token sem chamar o backend.

## Erros que mais aparecem

| Status | Código | O que fazer |
|---|---|---|
| 401 | `ENTRADA_INVALIDA` | Entrada ou senha errada; a 5ª seguida bloqueia |
| 429 | `ENTRADA_BLOQUEADA` | Esperar até `tentarEm`; o bloqueio dobra a cada vez |
| 401 | `TOKEN_INVALIDO` | Token vencido ou adulterado: renovar |
| 401 | `PERMISSOES_DESATUALIZADAS` | As permissões mudaram: renovar e montar a casca de novo |
| 401 | `SESSAO_TERMINADA` | A sessão acabou: entrar de novo |
| 422 | `DADOS_INVALIDOS` | O campo errado vem em `campo` |

Toda resposta de erro traz `codigo` e `mensagem`, além de `campo`, `tentarEm` ou limites quando a falha tem.

## Diagramas

Toque num diagrama para abrir em tamanho real. A versão de cada um está na marca d'água.

**Casos de uso**

[![Casos de uso da Autenticação](/doc/api/diagramas/autenticacao-casos-de-uso.svg)](/doc/api/diagramas/autenticacao-casos-de-uso.svg)

**Sequência: entrar, renovar e sair**

[![Sequência da Autenticação](/doc/api/diagramas/autenticacao-sequencia.svg)](/doc/api/diagramas/autenticacao-sequencia.svg)

**Contrato de API**

[![Contrato de API da Autenticação](/doc/api/diagramas/autenticacao-contrato-api.svg)](/doc/api/diagramas/autenticacao-contrato-api.svg)

**Classes do domínio**

[![Classes da Autenticação](/doc/api/diagramas/autenticacao-classes.svg)](/doc/api/diagramas/autenticacao-classes.svg)

**Documentos no Redis**

[![Documentos da Autenticação](/doc/api/diagramas/autenticacao-documentos.svg)](/doc/api/diagramas/autenticacao-documentos.svg)
