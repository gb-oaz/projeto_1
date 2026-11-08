# Redis no boaz

Local: Redis em contêiner (RNF-07). Na Azure: **Azure Managed Redis** (RNF-71).

## Usos aceitos

| Uso | Padrão | Cuidado |
|---|---|---|
| Cache de leitura | cache-aside: lê Redis → falhou → lê Mongo → grava com TTL | invalidar na escrita do agregado |
| Sessão / renovação de token | chave por sessão com TTL igual ao prazo do requisito | revogação apaga a chave |
| Limite de taxa | contador com `INCR` + `EXPIRE` | janela e limite vêm de `RNF` |
| Bloqueio curto | `SET key val NX PX` | sempre com expiração |

Redis **não é fonte da verdade**: perder o cache não pode perder dado de negócio.

## Chaves

- Formato `<contexto>:<feature>:<id>[:<detalhe>]`, ex.: `commons:autenticacao:sessao:<id>`.
- Toda chave tem TTL, salvo decisão registrada; o modelo de documentos lista as chaves e seus TTLs.
- Valores pequenos; nada de objeto inteiro grande se só um campo é lido.

## Acesso

- Só em `infra/out/` do `ms_mono`, atrás de porta (`ICacheX`, `ISessoes`), com `IConnectionMultiplexer` singleton.
- Falha do Redis em cache → degrada para o banco e loga; falha em sessão → comportamento definido no requisito.
- Sem `KEYS *` em produção; use `SCAN` ou estrutura indexada.
