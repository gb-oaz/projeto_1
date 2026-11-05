Contas assinantes e as pessoas de cada conta: staff, participantes e membros do time deboaz. Requisito: `1_Requisitos/17_modulo-usuario.md` · contrato: `usuarios-contrato-api`.

## Quem é quem

| Tipo | Entra em | Como |
|---|---|---|
| Conta master | hub | Nasce com a conta; administra o staff e os participantes |
| Autônomo | hub | A conta master de um educador sem instituição; só tem participantes |
| Staff | hub | Criado pela conta master ou por um staff com Administrar usuários |
| Participante | quest | Criado pelo staff; entra com o código de acesso, sem senha |
| Membro do time | hub | Do time deboaz, criado pelo administrador |

## Como usar

Os exemplos de pedido e de cada resposta, inclusive os erros, estão dentro de cada endpoint, logo abaixo deste guia: abra o endpoint e veja **Request body** e **Responses**.

### 1. Criar uma conta

`POST /contas` é público. A sigla tem 5 letras e não muda depois; `GET /contas/sigla-sugerida?nome=…` sugere uma. O pedido leva o tipo (`INSTITUICAO` ou `AUTONOMO`), nome, documento, responsável, telefone, domínio e sigla.

A resposta `201` traz a entrada da conta master, a senha gerada (mostrada uma vez) e o `comprovante`. A pessoa anota a senha e confirma em `POST /auth/hub/confirmar-senha`, na API **Autenticação**.

### 2. Cadastrar o staff

Com o token da conta master, `POST /usuarios` com `"tipo": "STAFF"`, nome, nome curto e matrícula.

A resposta traz o `codigoLiberacao` (mostrado uma vez, vale 72 h). A pessoa gera a própria senha em `POST /usuarios/gerar-senha`, que é público, com a entrada e o código de liberação, e confirma com o `comprovante` em `POST /auth/hub/confirmar-senha`. O staff começa sem módulos: as permissões são dadas na API **Autorização**.

### 3. Cadastrar participantes

`POST /usuarios` com `"tipo": "PARTICIPANTE"` e os `responsaveis`. O login dele é o **código de acesso** (`EBFGA-LUCAS-0412`), usado no quest; perdeu, `POST /usuarios/{id}/novo-codigo-acesso` troca.

### 4. Listar, buscar e cuidar

- `GET /usuarios?tipo=STAFF&busca=maria&estado=ATIVO&pagina=1`: 50 por página, de A a Z. Sem `tipo`, a busca percorre todos os tipos que quem pede pode listar, e cada item diz o seu.
- `GET /usuarios/{id}` abre a pessoa; `PATCH /usuarios/{id}` edita, mandando `responsaveis` inteiro. Nome curto novo troca a entrada do staff, que mantém a senha e tem as sessões encerradas; o participante recebe código de acesso novo.
- Esqueceu a senha: `POST /usuarios/{id}/nova-geracao-senha` gera um código de liberação novo.
- `POST /usuarios/{id}/desativar` tira o acesso sem apagar; `POST /usuarios/{id}/reativar` devolve, com as mesmas permissões.
- `GET /usuarios/card` monta o card do módulo no início do hub.

### 5. Tempo de sessão

`GET` e `PUT /contas/{id}/tempo-sessao` (aba Configuração de Usuários) definem quanto tempo o staff e os participantes ficam conectados, em duração ISO 8601 (`PT8H`, `P30D`).

### Time deboaz

Só para o administrador e os membros do time: `GET /contas` lista as contas assinantes, `GET /contas/{id}` mostra o cadastro, `POST /contas/{id}/estado` suspende, desativa, reativa ou encerra (`SUSPENSA`, `DESATIVADA`, `ATIVA`, `ENCERRADA`), `POST /contas/{id}/codigo-master` confere o cadastro e gera o código da conta master, e `/time/tempo-sessao` define o tempo dos membros e das contas master.

## Login e permissões

Todo endereço com cadeado pede o token de acesso e o módulo **Usuários** (`MOD-03`) na aba Utilização (`U`); o tempo de sessão pede a aba Configuração (`C`). Quem pede só age sobre quem está abaixo dele: a conta master sobre o staff e os participantes, o staff sobre os participantes e o staff que criou.

## Erros que mais aparecem

| Status | Código | Quando |
|---|---|---|
| 403 | `ACESSO_NEGADO` | Sem o módulo ou a aba, ou a pessoa não está abaixo |
| 404 | `USUARIO_INEXISTENTE` | Inclusive o usuário de outra conta |
| 409 | `SIGLA_EM_USO`, `NOME_CURTO_EM_USO` | O valor já é de outra conta ou pessoa |
| 409 | `ESTADO_IGUAL` | Desativar quem já está desativado |
| 422 | `DADOS_INVALIDOS` | O campo errado vem em `campo` |
| 429 | `ENTRADA_BLOQUEADA` | 5 códigos de liberação errados seguidos |

## Diagramas

Toque num diagrama para abrir em tamanho real. A versão de cada um está na marca d'água.

**Sequência: conta, staff e primeiro acesso**

[![Sequência de Usuários](/doc/api/diagramas/usuarios-sequencia.svg)](/doc/api/diagramas/usuarios-sequencia.svg)

**Contrato de API**

[![Contrato de API de Usuários](/doc/api/diagramas/usuarios-contrato-api.svg)](/doc/api/diagramas/usuarios-contrato-api.svg)

**Classes do domínio (identidade e acesso)**

[![Classes de identidade e acesso](/doc/api/diagramas/identidade-acesso-classes.svg)](/doc/api/diagramas/identidade-acesso-classes.svg)

**Documentos no MongoDB**

[![Documentos de identidade e acesso](/doc/api/diagramas/identidade-acesso-documentos.svg)](/doc/api/diagramas/identidade-acesso-documentos.svg)
