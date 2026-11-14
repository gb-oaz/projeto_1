Atendimento da conta pelo time deboaz: chamados, conversa, anexos, prazos e avaliação (módulo `MOD-02`). Requisito: `1_Requisitos/18_modulo-sac.md` · contrato: `sac-contrato-api`.

## Quem é quem

| Lado | Quem | O que faz |
|---|---|---|
| Conta | Conta master e autônomo | Abre, acompanha, responde, reabre, encerra e avalia os chamados da própria conta |
| Time | Membro do time e administrador | Vê a fila de todas as contas, assume, responde, escreve nota interna, pede informação e resolve |
| Time | Administrador | Muda os prazos de primeira resposta de cada categoria |

O papel do token decide o lado. Staff e participante recebem `403`; chamado ou anexo de outra conta responde `404`, como o que não existe.

## Como usar

Os exemplos de pedido e de cada resposta, inclusive os erros, estão dentro de cada endpoint, logo abaixo deste guia: abra o endpoint e veja **Request body** e **Responses**.

### 1. Anexar um arquivo (opcional)

`POST /sac/anexos` em `multipart/form-data`, campo `arquivo`: PNG ou JPG de até 10 MB, conferido pelo conteúdo e pela verificação de segurança e guardado como veio, no armazenamento de objetos ou no banco, conforme o ambiente. A resposta `201` traz o `id` (`anx-7f3a21`), que vai na lista `anexos` do chamado ou da mensagem. O anexo não citado em 24 h é descartado; o usado fica 30 dias em homologação e, em produção, até 5 anos depois de o chamado fechar. Vencido, `GET /sac/anexos/{id}` responde `404`.

### 2. Abrir o chamado

Com o token da conta master ou do autônomo, `POST /sac/chamados` com a categoria, o assunto, a descrição, o chamado relacionado e os anexos. A resposta `201` traz o número (`SAC-000042`) e o `prazoResposta`, em horas úteis da categoria. A conta tem até 10 chamados em andamento.

### 3. Acompanhar e conversar

- `GET /sac/chamados?situacao=EM_ANDAMENTO`: a conta vê os dela; o time vê a fila de todas as contas, com os filtros `responsavel`, `atrasado` e `notaMaxima`, e cada item traz `atrasado` e `prazoPerto` (1 hora útil ou menos até o prazo). 50 por página.
- `GET /sac/chamados/{id}` abre o chamado com a conversa e a linha do tempo e marca como lido.
- `POST /sac/chamados/{id}/mensagens` responde; o time manda `"interna": true` para a nota interna, que a conta nunca vê. Nada muda depois de escrito: `PATCH`, `PUT` e `DELETE` de uma mensagem respondem `405`.
- `GET /sac/chamados/{id}/ao-vivo` é a conversa ao vivo, em `text/event-stream`: o evento `mensagem` chega em até 5 s. Use `fetch` com o `Authorization`; o `EventSource` do navegador não manda o token.

### 4. Atender (time)

`POST /sac/chamados/{id}/assumir` pega o chamado (`"substituir": true` toma de outro membro). `POST /sac/chamados/{id}/status` pede informação (`AGUARDANDO_CLIENTE`) ou resolve (`RESOLVIDO`), com a mensagem para a conta.

### 5. Fechar (conta)

Com o chamado resolvido, a conta confirma e avalia em `POST /sac/chamados/{id}/avaliacao` (nota de 1 a 5, uma vez só) ou, em até 7 dias, reabre em `POST /sac/chamados/{id}/reabrir`. `POST /sac/chamados/{id}/encerrar` fecha um chamado em andamento que a conta não precisa mais. Sem resposta da conta, o chamado fecha sozinho depois de 7 dias.

### 6. Prazos e card

`GET` e `PUT /sac/configuracao` (aba Configuração do SAC) leem e mudam os prazos de primeira resposta de cada categoria, de 1 a 80 horas úteis. `GET /sac/card` monta o card do SAC na tela Módulos, e `GET /sac/card/ao-vivo?intervalo=30` o acompanha ao vivo. Sem sessão válida, os dois respondem só `401 SESSAO_TERMINADA`.

### Pedido de senha sem login

O `POST /contas/esqueci-senha-master` da tela Entrar, na API **Usuários**, vira um chamado da categoria `SENHA_CONTA_MASTER`, sem conversa: o time escreve nota interna e resolve.

## Login, permissões e auditoria

Todo endereço com cadeado pede o token de acesso e o módulo **SAC** (`MOD-02`) na aba Utilização (`U`); salvar os prazos pede também a aba Configuração (`CU`). Abrir, enviar mensagem, assumir, mudar o status, reabrir, encerrar, avaliar e salvar os prazos são auditados: a resposta traz o cabeçalho `X-Trace-Id`, o mesmo `traceId` gravado na auditoria.

## Erros que mais aparecem

| Status | Código | Quando |
|---|---|---|
| 403 | `ACESSO_NEGADO` | Sem o módulo ou a aba, ou a ação é do outro lado |
| 404 | `CHAMADO_INEXISTENTE`, `ANEXO_INEXISTENTE` | Inclusive o de outra conta |
| 409 | `CHAMADO_FECHADO`, `JA_ASSUMIDO`, `TRANSICAO_INVALIDA` | O status do chamado não permite |
| 409 | `LIMITE_CHAMADOS` | 10 chamados em andamento na conta |
| 413, 415 | `ANEXO_GRANDE`, `TIPO_NAO_ACEITO` | Arquivo com mais de 10 MB ou que não é PNG nem JPG |
| 422 | `DADOS_INVALIDOS` | O campo errado vem em `campo` |
| 503 | `VERIFICACAO_INDISPONIVEL` | A verificação de segurança dos arquivos está fora do ar |

## Diagramas

Toque num diagrama para abrir em tamanho real. A versão de cada um está na marca d'água.

### Casos de uso

**SAC: a conta, o time e o administrador**

[![Casos de uso do SAC](/doc/api/diagramas/sac-casos-de-uso.svg)](/doc/api/diagramas/sac-casos-de-uso.svg)

### Sequência

**Do chamado aberto à avaliação, com as telas**

[![Sequência do SAC](/doc/api/diagramas/sac-sequencia.svg)](/doc/api/diagramas/sac-sequencia.svg)

**Inicialização: índices, verificação dos anexos e agendamento**

[![Sequência da inicialização do SAC](/doc/api/diagramas/sac-inicializacao-sequencia.svg)](/doc/api/diagramas/sac-inicializacao-sequencia.svg)

### Contrato de API

**Endereços, pedidos, respostas e erros de /sac**

[![Contrato de API do SAC](/doc/api/diagramas/sac-contrato-api.svg)](/doc/api/diagramas/sac-contrato-api.svg)

### Classes e pacotes

**Classes do domínio e portas de saída**

[![Classes do SAC](/doc/api/diagramas/sac-classes.svg)](/doc/api/diagramas/sac-classes.svg)

**Pacotes do módulo SAC (arquitetura hexagonal)**

[![Arquitetura hexagonal do SAC](/doc/api/diagramas/sac-hexagonal.svg)](/doc/api/diagramas/sac-hexagonal.svg)

### Modelo de documentos

**Chamados, mensagens, anexos, prazos e card no MongoDB e no Redis**

[![Documentos do SAC](/doc/api/diagramas/sac-documentos.svg)](/doc/api/diagramas/sac-documentos.svg)
