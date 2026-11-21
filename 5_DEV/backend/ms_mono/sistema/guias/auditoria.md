Saúde dos serviços, tempo de resposta, registros e execuções para o time deboaz (módulo `MOD-17`). Requisito: `1_Requisitos/20_modulo-auditoria.md` · contrato: `auditoria-contrato-api`.

## Quem é quem

| Quem | O que faz |
|---|---|
| Administrador do deboaz | Usa todas as abas da Auditoria |
| Membro do time com `MOD-17` na aba Utilização (`U`) | Usa todas as abas da Auditoria |

Só o papel `ADMINISTRADOR` e o `MEMBRO_TIME` com `MOD-17` · `U` usam a Auditoria: a conta master, o staff e o membro do time sem o módulo recebem `403 ACESSO_NEGADO`. Todo endereço de `/auditoria` é `GET`; qualquer outro método responde `405`, sem corpo.

## Como usar

Os exemplos de resposta, inclusive os erros, estão dentro de cada endpoint, logo abaixo deste guia. Entre no hub com um membro do time (`POST /auth/hub/entrar`, na API **Autenticação**), cole o token em **Authorize** e abra os endpoints.

### 1. Saúde

- `GET /auditoria/saude`: os 8 serviços (aplicação, banco, cache, armazenamento e verificação dos anexos, intermediador de pagamento e os agendamentos do SAC e do Pagamento), os módulos e as instâncias no ar, lidos da última verificação desta instância, **sem o banco de dados**. Os serviços vêm na ordem `FORA_DO_AR`, `LENTO`, `SEM_VERIFICACAO`, `NO_AR` e `DESLIGADO`.
- `GET /auditoria/saude/{servico}/historico?periodo=24h|7d|30d`: as quedas, a disponibilidade e a série (por hora em 24 h, por dia em 7 e 30 dias).
- `GET /auditoria/saude/ao-vivo`: `text/event-stream` com o evento `saude` ao abrir e a cada verificação (30 s) e o evento `ocorrencia` a cada mudança de estado.

Cada serviço é verificado a cada 30 s, com até 3 s para responder: `NO_AR` até 1 s, `LENTO` até 3 s ou na 1ª e 2ª falha seguidas, `FORA_DO_AR` sem resposta em 3 s ou na 3ª falha seguida. O módulo fica em falha quando os erros 5xx dos últimos 5 min passam de 1 % ou o p95 passa de 3 s.

### 2. Tempo de resposta

`GET /auditoria/metricas?janela=1h|24h|7d|30d&modulo=MOD-NN` traz o resumo (pedidos, p50, p95, p99, erros) e a série; `GET /auditoria/metricas/enderecos` traz o tempo de cada endereço, sempre pelo modelo da rota, ordenado por `p95`, `pedidos` ou `erros`. O percentil é o fim da faixa do tempo em que cai (50, 100, 250, 500, 1000, 3000, 5000 e 10000 ms); acima de 10 s, `null`. Todo pedido aos módulos é medido da chegada ao fim da resposta, sem guardar quem pediu, a conta nem o corpo.

### 3. Registros

`GET /auditoria/registros` junta `auditoria` e `auditoriaErros`, o mais novo primeiro, 50 por página (até 200) com o cursor `proximo` em `depois`. As últimas 24 h por padrão, até 31 dias; `traceId` traz todos os registros da execução, sem período. Filtros: `tipo`, `nivel`, `modulo`, `acao`, `conta` (a sigla), `quem` e `codigo`. `GET /auditoria/registros/ao-vivo` mostra cada registro novo em até 5 s pelos mesmos filtros, e `GET /auditoria/acoes` traz o catálogo das 41 ações, com o nome, o módulo e o que quer dizer a falta de quem.

### 4. Execuções

`GET /auditoria/execucoes` lista as execuções (até 7 dias) com a contagem de cada resultado; `?resultado=FALHOU,INTERROMPIDA` pede só as com problema. `GET /auditoria/execucoes/{traceId}` mostra uma execução do começo ao fim: a ação principal é a do primeiro `INICIADO` e as outras são internas.

### 5. Ocorrências e card

`GET /auditoria/ocorrencias/abertas` traz a faixa (as abertas e as voltas dos últimos 10 min) pelo cache, sem o banco; `GET /auditoria/ocorrencias/ao-vivo` a acompanha; `GET /auditoria/ocorrencias` lista as do período (até 31 dias). `GET /auditoria/card` monta o card do time na tela Módulos, e `GET /auditoria/card/ao-vivo?intervalo=30` o acompanha.

### Conexões ao vivo

Cada usuário tem até 3 conexões ao vivo: a 4ª fecha a mais antiga com o evento `encerrado` (`LIMITE_CONEXOES`). Use `fetch` com o `Authorization`; o `EventSource` do navegador não manda o token.

## Login, permissões e auditoria

Todo endereço pede o token de acesso e `MOD-17` na aba Utilização. Sem sessão válida, `401 SESSAO_TERMINADA`. A leitura da Auditoria também é auditada: a consulta atendida traz o cabeçalho `X-Trace-Id` e fica em `auditoria` como `CONSULTAR_AUDITORIA` (`INICIADO` e `CONCLUIDO`), com o endereço lido como alvo; a mesma consulta da mesma pessoa em menos de 5 min não grava de novo. O card, a lista de ações e as ocorrências abertas e ao vivo não são auditados e não trazem o cabeçalho. A consulta recusada nunca é gravada.

## Erros que mais aparecem

| Status | Código | Quando |
|---|---|---|
| 403 | `ACESSO_NEGADO` | Sem o papel ou sem `MOD-17` · `U` |
| 404 | `SERVICO_INEXISTENTE`, `EXECUCAO_INEXISTENTE` | O serviço não é um dos 8 ou nenhum registro tem o `traceId` |
| 422 | `DADOS_INVALIDOS` | O campo errado vem em `campo` |
| 422 | `PERIODO_LONGO`, `PERIODO_INVERTIDO` | Mais de 31 dias (7 nas execuções) ou o fim antes do começo |
| 503 | `BANCO_DE_DADOS_FORA` | A leitura depende do banco de dados, fora do ar; a saúde e as ocorrências abertas seguem respondendo |

## Diagramas

Toque num diagrama para abrir em tamanho real. A versão de cada um está na marca d'água.

### Casos de uso

**Auditoria: o time, o relógio e as consultas**

[![Casos de uso da Auditoria](/doc/api/diagramas/auditoria-casos-de-uso.svg)](/doc/api/diagramas/auditoria-casos-de-uso.svg)

### Sequência

**Saúde, ocorrências, registros, execuções e métricas, com as telas**

[![Sequência da Auditoria](/doc/api/diagramas/auditoria-sequencia.svg)](/doc/api/diagramas/auditoria-sequencia.svg)

### Contrato de API

**Endereços, respostas e erros de /auditoria**

[![Contrato de API da Auditoria](/doc/api/diagramas/auditoria-contrato-api.svg)](/doc/api/diagramas/auditoria-contrato-api.svg)

### Classes e pacotes

**Classes do domínio, portas e adaptadores**

[![Classes da Auditoria](/doc/api/diagramas/auditoria-classes.svg)](/doc/api/diagramas/auditoria-classes.svg)

### Modelo de documentos

**Verificações, quedas, ocorrências, medições e card no MongoDB e no Redis**

[![Documentos da Auditoria](/doc/api/diagramas/auditoria-documentos.svg)](/doc/api/diagramas/auditoria-documentos.svg)
