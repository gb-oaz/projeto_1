Faturas mensais das contas do deboaz e o pagamento delas por PIX, cartão de crédito, cartão de débito ou boleto (módulo `MOD-01`). Requisito: `1_Requisitos/19_modulo-pagamento.md` · contrato: `pagamento-contrato-api`.

## Quem é quem

| Lado | Quem | O que faz |
|---|---|---|
| Quem paga | Conta master, autônomo e staff com `MOD-01` na aba Utilização (`U`) | Vê as faturas da conta e os avisos de atraso, paga, troca de forma, baixa o boleto e o comprovante |
| Quem recebe | Só o papel **ADMINISTRADOR** do deboaz (não se delega nem aparece em Permissões) | Vê os recebimentos, as faturas de todas as contas, cancela fatura, pede estorno e muda a configuração |
| Banco | O intermediador de pagamento | Avisa pagamento, recusa e estorno em `POST /pagamento/notificacoes`, conferido pela assinatura |

O administrador recebe `403` no lado de quem paga, e quem paga recebe `403` no de quem recebe. Staff sem `MOD-01`, membro do time e participante também recebem `403`. Fatura ou pagamento de outra conta responde `404`, como o que não existe.

## Como usar

Os exemplos de pedido e de cada resposta, inclusive os erros, estão dentro de cada endpoint, logo abaixo deste guia: abra o endpoint e veja **Request body** e **Responses**.

### 1. A fatura do mês

No dia 1, à 00:00 de Brasília, o agendamento fecha a competência anterior e emite uma fatura por conta (`FAT-000402`), com um item por módulo cobrado e o vencimento no dia 10. No dia seguinte ao vencimento, a fatura sem pagamento fica `VENCIDA` e passa a ter juros ao dia.

- `GET /pagamento/faturas?situacao=A_PAGAR`: as faturas da conta (`A_PAGAR`, `PAGAS` ou `TODAS`), 50 por página, com o resumo do topo.
- `GET /pagamento/faturas/{id}`: o detalhe, com os itens, os juros de hoje e todas as tentativas de pagamento, cada uma com o seu `traceId`.
- `GET /pagamento/avisos`: o aviso de atraso, só no módulo, depois da tolerância da configuração.

### 2. Pagar

`GET /pagamento/formas` diz as formas ligadas agora. Depois, `POST /pagamento/faturas/{id}/pagamentos` com a forma:

| Forma | Corpo | Resposta `201` |
|---|---|---|
| PIX | `{ "forma": "PIX" }` | `AGUARDANDO`, com `pix.copiaECola`, `pix.qrCodeSvg` e `expiraEm` (30 min) |
| Cartão de crédito | `{ "forma": "CARTAO_CREDITO", "cartao": { "token": "tok_…" } }` | `PAGO` na hora, `RECUSADO` com o `motivo` ou `PROCESSANDO` |
| Cartão de débito | `{ "forma": "CARTAO_DEBITO", "cartao": { "token": "tok_…" } }` | `AGUARDANDO`, com `debito.confirmacaoUrl` para confirmar no app do banco (10 min) |
| Boleto | `{ "forma": "BOLETO" }` | `AGUARDANDO`, com a linha digitável e o PDF em `boleto.pdf` |

O cartão vai só como o **token** gerado pela tela direto no intermediador: número, CVV ou validade no corpo são recusados. A fatura tem um pagamento em aberto por vez; para trocar de forma, `POST /pagamento/pagamentos/{id}/cancelar` antes. Sem resposta do banco em 15 s, a resposta é `503 BANCO_INDISPONIVEL` e nada foi cobrado.

### 3. Acompanhar e baixar

- `GET /pagamento/pagamentos/{id}`: o estado atual; `duplicidade: true` quando pagou uma fatura que já estava paga.
- `GET /pagamento/pagamentos/{id}/ao-vivo`: o pagamento ao vivo, em `text/event-stream`. Cada mudança de status chega em até 5 s como o evento `pagamento` (com o `traceId` de quem mudou), `: ping` a cada 30 s, e a conexão fecha no status final (`PAGO`, `RECUSADO`, `EXPIRADO` ou `CANCELADO`). Use `fetch` com o `Authorization`; o `EventSource` do navegador não manda o token.
- `GET /pagamento/pagamentos/{id}/boleto` e `GET /pagamento/pagamentos/{id}/comprovante`: os PDFs (`application/pdf`, baixados como `boleto-FAT-000402.pdf` e `comprovante-FAT-000402.pdf`). O comprovante só existe depois da confirmação do banco.

### 4. O aviso do banco

`POST /pagamento/notificacoes` não usa token: vale a assinatura do intermediador. Em produção, o Mercado Pago manda `x-signature` (`ts=…,v1=…`, HMAC-SHA256 em hex de `id:<data.id>;request-id:<x-request-id>;ts:<ts>;` com o segredo do webhook) e `x-request-id`; a situação é lida na API do banco. Na máquina e no qa, o intermediador falso manda `x-assinatura: sha256=<HMAC-SHA256 do corpo>`. Assinatura errada responde `401 ASSINATURA_INVALIDA` e fica registrada; aviso repetido ou de pagamento desconhecido responde `200` sem efeito. Quem paga nunca confirma o próprio pagamento.

### 5. Receber (administrador)

- `GET /pagamento/recebimentos/resumo?competencia=2026-10`: faturado, recebido, a receber, vencido, inadimplência e duplicidades, por forma e por tipo de conta.
- `GET /pagamento/recebimentos/competencias?ate=2026-10`: as 12 competências até a pedida, a mais nova primeiro.
- `GET /pagamento/recebimentos/faturas`: as faturas de todas as contas, com `competencia`, `situacao`, `busca` (sigla, nome ou número) e `pagina`.
- `POST /pagamento/recebimentos/faturas/{id}/cancelar` com o `motivo`: cancela a fatura em aberto e o pagamento que esperava.
- `POST /pagamento/recebimentos/pagamentos/{id}/estornos` com `valor` e `motivo`: estorno total ou parcial de cartão ou PIX, até o saldo, um por vez. Fica `PENDENTE` até o aviso do banco. O boleto é devolvido pelo SAC.

### 6. Configuração e card

`GET /pagamento/recebimentos/configuracao` lê as formas ligadas, os juros ao dia, a tolerância do aviso, o dia do vencimento e os prazos, com a `versao`. `PUT` no mesmo endereço salva com o cabeçalho `If-Match` da versão lida (`"4"`): sem ele ou com uma versão antiga, a resposta é `412 CONFIGURACAO_MUDOU`. Ao menos uma forma fica ligada.

`GET /pagamento/card` monta o card do Pagamento na tela Módulos (`DEBOAZ` para o administrador, `INSTITUICAO` ou `AUTONOMO` para quem paga), e `GET /pagamento/card/ao-vivo?intervalo=30` o acompanha ao vivo.

## Login, rastreio e auditoria

Todo endereço com cadeado pede o token de acesso do hub. Sem sessão válida, as leituras respondem `401 SESSAO_TERMINADA`.

**Toda resposta de `/pagamento/*`**, de sucesso ou de erro, traz o cabeçalho `X-Trace-Id` (`trc-` e 8 hexadecimais), um por execução, e todo corpo de erro traz o mesmo valor em `traceId`. Um `X-Trace-Id` mandado no pedido não substitui o da execução: vira o `traceIdOrigem`, que liga a nova tentativa à anterior. O `traceId` fica gravado na fatura, no pagamento, no estorno, no aviso do banco e na auditoria, e vai ao banco no cabeçalho e nos metadados.

Criar e cancelar pagamento, cancelar fatura, pedir estorno, salvar a configuração e o aviso do banco são auditados (`PAGAMENTO_CRIADO`, `PAGAMENTO_CANCELADO`, `FATURA_CANCELADA`, `ESTORNO_PEDIDO`, `CONFIGURACAO_SALVA`, `AVISO_DO_BANCO`); a virada para vencida, pelo agendamento, como `FATURA_VENCIDA`.

## Erros que mais aparecem

| Status | Código | Quando |
|---|---|---|
| 401 | `SESSAO_TERMINADA`, `ASSINATURA_INVALIDA` | Sem sessão válida; aviso do banco com assinatura errada |
| 403 | `ACESSO_NEGADO` | Sem `MOD-01` · `U`, ou o endereço é do outro lado |
| 404 | `FATURA_INEXISTENTE`, `PAGAMENTO_INEXISTENTE`, `BOLETO_INEXISTENTE` | Inclusive o de outra conta |
| 409 | `FATURA_PAGA`, `FATURA_CANCELADA`, `PAGAMENTO_EM_ABERTO` | A fatura não aceita outro pagamento agora |
| 409 | `PAGAMENTO_CONFIRMADO`, `PAGAMENTO_NAO_CONFIRMADO`, `ESTORNO_EM_ANDAMENTO`, `ESTORNO_PELO_SAC` | O status do pagamento não permite |
| 412 | `CONFIGURACAO_MUDOU` | `If-Match` ausente ou de versão antiga |
| 422 | `DADOS_INVALIDOS`, `FORMA_INDISPONIVEL`, `FORMA_OBRIGATORIA`, `ESTORNO_ACIMA_DO_SALDO` | O campo errado vem em `campo`; o estorno traz o `saldo` |
| 503 | `BANCO_INDISPONIVEL` | O banco não respondeu em 15 s; nada foi cobrado |

## Diagramas

Toque num diagrama para abrir em tamanho real. A versão de cada um está na marca d'água.

### Casos de uso

**Pagamento: quem paga, quem recebe e o banco**

[![Casos de uso do Pagamento](/doc/api/diagramas/pagamento-casos-de-uso.svg)](/doc/api/diagramas/pagamento-casos-de-uso.svg)

### Sequência

**Da fatura emitida ao pagamento confirmado, com as telas**

[![Sequência do Pagamento](/doc/api/diagramas/pagamento-sequencia.svg)](/doc/api/diagramas/pagamento-sequencia.svg)

### Contrato de API

**Endereços, pedidos, respostas e erros de /pagamento**

[![Contrato de API do Pagamento](/doc/api/diagramas/pagamento-contrato-api.svg)](/doc/api/diagramas/pagamento-contrato-api.svg)

### Classes e pacotes

**Classes do domínio e portas de saída**

[![Classes do Pagamento](/doc/api/diagramas/pagamento-classes.svg)](/doc/api/diagramas/pagamento-classes.svg)

**Pacotes do módulo Pagamento (arquitetura hexagonal)**

[![Arquitetura hexagonal do Pagamento](/doc/api/diagramas/pagamento-hexagonal.svg)](/doc/api/diagramas/pagamento-hexagonal.svg)

### Modelo de documentos

**Faturas, pagamentos, estornos, configuração e avisos no MongoDB e no Redis**

[![Documentos do Pagamento](/doc/api/diagramas/pagamento-documentos.svg)](/doc/api/diagramas/pagamento-documentos.svg)
