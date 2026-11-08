---
name: engenharia-software
description: Princípios de engenharia de software compartilhados pelos agentes do boaz — POO, SOLID, DDD (estratégico e tático), arquitetura hexagonal (portas e adaptadores) e Clean Architecture como aplicadas em 1_Requisitos/14_estrutura-projetos.md, ciclo TDD/BDD, pirâmide de testes, modelo espiral e documentação técnica. Use ao modelar domínio, desenhar classes ou camadas, decidir onde um código ou teste mora, revisar dependências entre camadas, refatorar, ou quando precisar justificar uma decisão técnica com um princípio.
---

# Engenharia de software → como o boaz constrói

Base comum de arquitetura, back, front e qa (e do PO, na parte estratégica de DDD e no modelo espiral). O **requisito `1_Requisitos/14_estrutura-projetos.md` prevalece** sobre qualquer texto genérico daqui; em conflito, siga o requisito e reporte a divergência.

## Princípios que valem em todo código

1. **Domínio no centro, sem I/O** (RN-147): `core/` não conhece banco, HTTP, cache, framework de UI em `usecases/`. Pacotes de terceiros só no runtime (`ms_mono` no back; adaptadores no front).
2. **Portas e adaptadores** (RN-148): entrada chama a feature só por `ports/in/`; `usecases/` implementa `ports/in/` e usa `ports/out/`; adaptadores em `infra/in|out/<contexto>/<feature>/` implementam as portas.
3. **Composição num lugar só** (RN-149): `Program.cs` no back, `main.ts` de cada app na web, `MauiProgram.cs` no mobile.
4. **Contexto importa só `commons`** (RN-146): contextos (bounded contexts) não se enxergam entre si.
5. **Um caso de uso por classe**, com a interface em `ports/in/`; `dtos/` carregam e validam o que entra e sai.
6. **Linguagem ubíqua**: nomes de classes, métodos, campos e testes vêm do Gherkin e do `99_glossario.md`.
7. **TDD**: vermelho (teste a partir do cenário) → verde (o mínimo) → refatora (sem mudar comportamento). Nenhum código sem um teste que o exija (skill `desenvolvimento`).
8. **Simples antes de esperto**: sem abstração especulativa (YAGNI), sem duplicação de regra (DRY no domínio, não em teste), nomes que dispensam comentário.

## Mapa das camadas

```
infra/in  (HTTP, tela)  ──► ports/in  ◄── usecases ──► ports/out ◄── infra/out (Mongo, Redis, HTTP)
                                         │
                                       domain   (entidades, objetos de valor, regras)
```

Dependências apontam **para dentro**. Domínio não depende de nada; `usecases` dependem de `domain` e das portas; adaptadores dependem das portas.

## Onde está o detalhe (leia só o que a tarefa pedir)

| Assunto | Arquivo |
|---|---|
| POO e SOLID com exemplos em C# e TypeScript | `referencias/poo-solid.md` |
| DDD estratégico (contextos, linguagem ubíqua) e tático (entidade, agregado, VO, evento) | `referencias/ddd.md` |
| Hexagonal × Clean Architecture, regras de dependência e checagem | `referencias/hexagonal-clean.md` |
| TDD, BDD, pirâmide de testes do projeto, modelo espiral, documentação | `referencias/testes-processo.md` |

## Critérios de qualidade (revisão)

- [ ] Nenhum `using`/`import` de infraestrutura em `core/` (veja o comando em `referencias/hexagonal-clean.md`).
- [ ] Cada classe com uma razão para mudar; dependências por interface injetada no construtor.
- [ ] Regra de negócio no domínio ou no caso de uso — nunca no adaptador, no controller/endpoint ou no componente visual.
- [ ] Teste cita o ID do cenário que cobre.
- [ ] Nome igual ao do glossário.

## Skills relacionadas

`gherkin-bdd` (de onde vem o comportamento) · `arquitetura-software` (decisões e contratos) · `frontend-vue` · `estrategia-qa` · `desenvolvimento` (fluxo da tarefa).
