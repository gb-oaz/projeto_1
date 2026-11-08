# MongoDB no boaz

Local: MongoDB em contêiner (RNF-06, RNF-09). Na Azure: **Cosmos DB for MongoDB** (`05_cd-implantacao`, RNF-70) — use só recursos compatíveis com ele.

## Modelagem

- **Um documento por agregado**; o que pertence ao agregado é embutido, o que tem vida própria é referenciado por ID.
- Embutir quando: lido junto, cresce pouco, não é compartilhado. Referenciar quando: cresce sem limite, é lido sozinho, é compartilhado.
- Documento com lista que cresce sem limite (ex.: histórico) → coleção própria.
- Dados de outro contexto aparecem como **cópia só leitura** (snapshot) ou ID, nunca como escrita cruzada.
- Nomes de coleção e campos = glossário; o modelo de documentos (`2_Diagramas/modelo-documentos-*`) é o contrato da persistência.

## Índices

- Todo filtro e ordenação frequente de cenário tem índice; unicidade de regra de negócio (e-mail, sigla) por índice único + tratamento do erro de duplicidade → código do contrato (ex.: 409).
- Índice composto na ordem igualdade → ordenação → intervalo.
- TTL index para dado que expira (quando o cenário define prazo).

## Acesso

- Só em `infra/out/` do `ms_mono`, implementando a porta de `ports/out/`; mapeamento documento ↔ domínio no adaptador.
- `IMongoClient` singleton; `CancellationToken` em toda operação.
- Atualizações atômicas por documento (`$set`, `$push`, filtro por versão para concorrência otimista); transação multi-documento só com ADR (custo e compatibilidade no Cosmos).
- Projeção: leia só os campos usados.

## Testes

Integração com Mongo real em contêiner (RNF-37); o qa usa o banco real pelo contrato, nunca o driver para preparar estado de negócio que a API consegue criar.
