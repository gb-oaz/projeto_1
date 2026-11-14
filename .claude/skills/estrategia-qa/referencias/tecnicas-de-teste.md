# Técnicas de derivação de casos

Use para **conferir** se o Gherkin cobre o necessário. Caso que falta vira proposta ao `deboaz-arc`; o qa não cria regra.

| Técnica | Quando | Exemplo |
|---|---|---|
| **Partição de equivalência** | entrada com classes válidas/inválidas | e-mail válido · sem `@` · vazio |
| **Valor-limite** | `RNF`/regra com número | sessão de 8 h: 7h59 · 8h00 · 8h01 |
| **Tabela de decisão** | combinação de condições | ativo × escola ativa × módulo contratado → entra / recusa (vira `Esquema do Cenário`) |
| **Transição de estados** | ciclo de vida | sala: criada → aberta → encerrada; ação inválida em cada estado |
| **Casos de uso / fluxos** | ponta a ponta | caminho feliz + desvios do diagrama de sequência |
| **Erro provável** | experiência | duplo clique, duplicidade, concorrência, sessão vencida no meio, dado de outra escola |
| **Pares (pairwise)** | muitas combinações | plataforma × perfil × permissão |

## Checklist por regra

- Positivo: o caminho do `Então` principal.
- Negativos: cada código de erro do contrato para aquele endereço (400/401/403/404/409/422).
- Autorização: sem autenticação, sem permissão, com permissão de outra escola (isolamento).
- Alternativos: outro ator/plataforma que o Gherkin cita.
- Limites: no limite e dos dois lados.
- Idempotência/concorrência quando o cenário fala de repetição.
- Estados de tela (funcional): vazio, carregando, erro, sucesso, sem conexão.

## Análise de risco (priorizar a cobertura)

`risco = probabilidade de falhar × impacto se falhar`, ambos 1–3. ≥ 6: teste automatizado em todo PR e exploratório; 3–4: automatizado; ≤ 2: cobertura mínima ou só unitário na frente.
