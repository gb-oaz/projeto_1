O que cada pessoa pode fazer no hub: módulos e abas. Requisito: `1_Requisitos/17_modulo-usuario-b-autorizacao.html` · contrato: `autorizacao-contrato-api`.

## Permissões

Uma permissão é o ID do módulo no registro (`MOD-NN`) com as abas liberadas:

- `"C"`, **Configuração**: criar e configurar no módulo, como criar um Quiz;
- `"U"`, **Utilização**: usar o que foi criado, como ver e participar de um Quiz;
- `"CU"`: as duas.

O módulo **Usuários** é o `MOD-03`: com ele em `"CU"`, a pessoa administra usuários e concede permissões.

## Quem concede a quem

- O administrador do deboaz, aos membros do time.
- A conta master, ao staff da conta.
- O staff com Administrar usuários, ao staff que está abaixo dele (criado por ele, direta ou indiretamente). Ele só concede o que tem, com as abas que tem, Administrar usuários inclusive.

Ninguém mexe nas próprias permissões, nas de um par, nas de um superior ou nas de um participante.

## Como usar

Os exemplos de pedido e de cada resposta, inclusive os erros, estão dentro de cada endpoint, logo abaixo deste guia: abra o endpoint e veja **Request body** e **Responses**.

### 1. Ler

`GET /usuarios/{id}/permissoes` traz a versão, o que a pessoa tem e o que quem pede pode conceder a ela (`concediveis`).

### 2. Salvar

`PUT /usuarios/{id}/permissoes` salva o **conjunto inteiro**: o que não vier no mapa é retirado. Mande a versão lida no cabeçalho `If-Match`:

```
If-Match: "2"
```

A resposta `200` traz a versão nova. As sessões da pessoa recebem `401 PERMISSOES_DESATUALIZADAS` no próximo pedido, renovam e passam a ter as permissões novas.

- Sem `If-Match`: `428 VERSAO_AUSENTE`.
- Outra pessoa salvou antes: `412 VERSAO_DESATUALIZADA`, com a versão atual. Leia de novo, confira e salve.

### 3. Histórico

`GET /usuarios/{id}/permissoes/historico` mostra as mudanças, a mais recente primeiro: quando, quem, a versão e o antes e o depois. `?ate=2026-11-01T00:00:00Z` traz só até essa data.

## Erros que mais aparecem

| Status | Código | Quando |
|---|---|---|
| 403 | `ACESSO_NEGADO` | A pessoa não está abaixo de quem pede |
| 403 | `PERMISSAO_ACIMA` | Quem concede não tem o módulo ou a aba, com `mod` e `abas` |
| 404 | `USUARIO_INEXISTENTE` | Inclusive o usuário de outra conta |
| 412 | `VERSAO_DESATUALIZADA` | Outra pessoa salvou antes |
| 422 | `MODULO_NAO_CONTRATADO` | A conta não contratou o módulo |
| 422 | `DADOS_INVALIDOS` | Módulo que não existe, que a pessoa não usa, ou aba inválida |
| 428 | `VERSAO_AUSENTE` | Faltou o If-Match |

## Diagramas

Toque num diagrama para abrir em tamanho real. A versão de cada um está na marca d'água.

**Casos de uso**

[![Casos de uso da Autorização](/doc/api/diagramas/autorizacao-casos-de-uso.svg)](/doc/api/diagramas/autorizacao-casos-de-uso.svg)

**Sequência: conceder permissões**

[![Sequência da Autorização](/doc/api/diagramas/autorizacao-sequencia.svg)](/doc/api/diagramas/autorizacao-sequencia.svg)

**Contrato de API**

[![Contrato de API da Autorização](/doc/api/diagramas/autorizacao-contrato-api.svg)](/doc/api/diagramas/autorizacao-contrato-api.svg)

**Classes do domínio**

[![Classes da Autorização](/doc/api/diagramas/autorizacao-classes.svg)](/doc/api/diagramas/autorizacao-classes.svg)

**Documentos no MongoDB**

[![Documentos da Autorização](/doc/api/diagramas/autorizacao-documentos.svg)](/doc/api/diagramas/autorizacao-documentos.svg)
