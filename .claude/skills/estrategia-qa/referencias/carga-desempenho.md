# Carga e desempenho

Suíte: `5_DEV/qa/backend/carga/` (ferramenta livre, ex.: k6 — RN-153). Roda sob demanda: `ci.sh --qa carga` (contêiner) e workflow `QA · carga` contra homologação (RN-156, RNF-195). Não roda em PR.

## Regras

- **Limite vem do requisito** (RN-155): cada teste cita o `RNF` e usa o número dele (ex.: "200 pedidos por segundo em até 300 ms"); o qa não inventa limite. `RNF` sem número → lacuna para o PO.
- Thresholds da ferramenta = critério do `RNF` (ex.: `p(95)<300`, taxa de erro < 1 %); a saída é 0 só se todos passarem.

## Tipos

| Tipo | Pergunta | Perfil |
|---|---|---|
| Fumaça | o script funciona? | 1–2 usuários, 1 min |
| Carga | aguenta o volume do `RNF`? | rampa até o alvo, platô |
| Estresse | onde quebra e como se recupera? | acima do alvo, só sob pedido |
| Pico | aguenta rajada? | salto súbito |
| Resistência | degrada com o tempo? | alvo por longo período |

## Boas práticas

- Dados preparados antes (pela API), únicos, em volume realista; não meça o preparo.
- Autenticação feita uma vez por usuário virtual; token renovado como o cliente real.
- Métricas: p95/p99 de latência, vazão, taxa de erro por código; correlacione com Application Insights em homologação.
- Homologação mínima tem limites próprios: registre ambiente, réplicas e versão no resultado.
- Nunca rode carga em produção sem decisão humana explícita.
