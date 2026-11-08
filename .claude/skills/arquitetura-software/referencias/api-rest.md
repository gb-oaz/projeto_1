# API REST — guia de contrato

O contrato do módulo (diagrama `contrato-*` em `2_Diagramas/`) é a fonte da verdade; os cenários de API só usam o que ele traz.

## Recursos e verbos

| Verbo | Uso | Sucesso típico | Idempotente |
|---|---|---|---|
| `GET` | ler recurso ou coleção | 200 | sim |
| `POST` | criar ou executar ação de negócio | 201 (com o id) / 200 | não |
| `PUT` | substituir o recurso | 200 / 204 | sim |
| `PATCH` | alterar parte | 200 / 204 | depende |
| `DELETE` | remover | 204 | sim |

- Substantivos no plural, contexto como prefixo (`/hub/salas/{id}`); ação que não é CRUD vira sub-recurso (`POST /hub/salas/{id}/encerrar`).
- Nomes em português, iguais ao glossário.

## Status e erros

| Status | Quando |
|---|---|
| 400 | requisição malformada (quando o requisito não diz 422) |
| 401 | sem autenticação ou token inválido/vencido |
| 403 | autenticado sem permissão |
| 404 | recurso não existe (ou não visível para quem pede) |
| 409 | conflito de estado ou duplicidade |
| 422 | dado válido na forma mas recusado (o projeto usa `DADOS_INVALIDOS` com o campo) |
| 429 | limite de taxa |
| 5xx | falha do servidor — nunca para regra de negócio |

- Todo erro tem **código estável em MAIÚSCULAS** (`ENTRADA_INVALIDA`, `ACESSO_NEGADO`) que o front e o qa usam; mensagem é secundária.
- Formato do corpo de erro, cabeçalhos (`X-Trace-Id`) e quando eles aparecem: siga o requisito do módulo (`17_*`, `RN-270`, `RN-271`) e o contrato.

## Coleções

- Paginação explícita (página/tamanho ou cursor) com limite máximo; filtros e ordenação por query string.
- Nunca devolva coleção sem limite.

## Evolução

- Mudança compatível: adicionar campo opcional, novo endereço, novo código de erro documentado.
- Mudança incompatível: remover/renomear campo, mudar tipo ou semântica → nova versão do contrato, aviso ao PO (cenários de API), qa e front.

## Segurança no contrato

- Autenticação por `Bearer` (token de acesso curto) e renovação por cookie `HttpOnly`/`Secure`/`SameSite=Strict` (requisito `17_modulo-usuario-a-autenticacao`).
- Nunca devolva senha, hash, token de renovação ou dado de outro locatário (escola).
- Imagens como SVG em texto.
