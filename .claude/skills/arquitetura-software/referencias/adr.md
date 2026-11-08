# Architecture Decision Record (ADR)

## Quando registrar

Quando houver **escolha real entre alternativas** com impacto difícil de desfazer: novo serviço, nova tecnologia, troca de padrão (Controllers × Minimal APIs), transação multi-documento, cache novo, mudança de custo fixo, exceção a uma regra de `14_estrutura-projetos.md`.

Não precisa de ADR: o que o requisito já decide (a decisão já está no Gherkin com data, ex.: "decisão de 2026-10-24").

## Onde (decisão de 2026-11-08)

**Não há pasta de ADR.** O ADR vai na **resposta** do arquiteto, endereçado ao `deboaz-po`, que registra o resultado como **cenário datado** no requisito transversal certo (`01` stack, `05` implantação, `14` estrutura) — no padrão já usado, ex.: `Cenário: um runtime por frente (decisão de 2026-10-24)`. Os diagramas afetados ganham nova versão na mesma tarefa.

Cenário datado proposto pelo arquiteto:

```gherkin
Cenário: <decisão em poucas palavras> (decisão de AAAA-MM-DD)
  Então <o que passa a valer>
  E <a consequência que alguém precisa respeitar>
```

## Modelo

```markdown
# ADR-<NN> — <decisão em uma frase>

- Status: proposta | aceita | substituída por ADR-<NN>
- Data: AAAA-MM-DD
- Requisitos: <IDs que motivam>

## Contexto
<problema, forças, restrições (RNF, custo, prazo)>

## Alternativas
1. <A> — prós / contras / custo
2. <B> — prós / contras / custo

## Decisão
<a escolhida e por quê>

## Consequências
<o que melhora, o que piora, o que muda em diagramas, código, testes e infra>
```
