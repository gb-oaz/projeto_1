---
name: arquitetura-software
description: Competência de arquiteto do boaz para tomar e documentar decisões técnicas a partir dos requisitos Gherkin — limites de camadas e contextos na arquitetura hexagonal, contratos entre portas e adaptadores, desenho de API REST (endereços, DTOs, status e códigos de erro), backend .NET/ASP.NET Core com Minimal APIs, persistência no MongoDB e cache no Redis, segurança, escalabilidade, observabilidade, custo e ADR. Use ao desenhar contrato, classes, modelo de documentos ou sequência, ao avaliar uma decisão estrutural, ao revisar se o código respeita a arquitetura, ou quando outro agente pedir orientação técnica. O desenho do .svg é da skill diagrama-dev/diagrama-arquitetura; infraestrutura e pipeline, da devops-cloud.
---

# Arquitetura de software → decisões que suportam o requisito

O arquiteto **define e orienta** decisões técnicas; também é dono das regras de negócio (requisitos em `1_Requisitos/`); experiência é do `deboaz-uxui`. Toda decisão nasce de um cenário ou de um `RNF` — cite o ID. Base: `gherkin-bdd` e `engenharia-software`.

Ordem de prevalência: requisitos (`01_stack`, `03_ci-qualidade`, `05_cd-implantacao`, `14_estrutura-projetos`, módulos) → diagramas aprovados → esta skill. Divergiu? Siga o requisito e reporte.

## 1 · Do Gherkin à arquitetura

Para cada módulo, leia `cenarios-tela` e `cenarios-api` e extraia:

| No cenário | Decisão |
|---|---|
| `Quando envia POST /x` | endpoint, DTO de entrada, caso de uso (`ports/in`) |
| `Então a resposta é 4xx CODIGO` | código de erro do contrato e onde a recusa acontece (forma no endpoint, regra no domínio, permissão antes do caso de uso) |
| `Dado` com dados de outro módulo | dependência entre contextos: só por `commons` ou leitura (RN-146) |
| `RNF` com tempo/volume | cache, índice, réplica, teste de carga |
| `RNF` de segurança/sessão | autenticação, autorização, armazenamento de segredo |
| termo do glossário | nome de agregado, coleção, DTO, campo JSON |

Faltou informação para decidir → decida no requisito primeiro (comportamento) ou ADR com alternativas (técnica).

## 2 · Conjunto de diagramas de um módulo (nesta ordem)

casos de uso → sequência com as telas → contrato de API → classes → modelo de documentos (coleções, embutidos, chaves Redis, o que é só lido). Desenho e versionamento: skill `diagrama-dev`. Alto nível (C4, implantação, cloud neutra de provedor): `diagrama-arquitetura`.

## 3 · Regras estruturais do boaz

- Hexagonal: `core/<contexto>/<feature>/` com `domain/`, `dtos/`, `ports/in|out/`, `usecases/`; adaptadores em `infra/in|out/`; composição só no `Program.cs` (RN-147 a RN-149, RNF-175).
- **Core só .NET nativo**; MongoDB.Driver, StackExchange.Redis e afins só no `ms_mono` (RNF-177).
- Um serviço (`ms_mono`) com todos os contextos; separar serviço exige ADR e mudança de requisito.
- API em **Minimal APIs** (padrão atual do código); Controllers só com ADR.
- Imagens trafegam e são guardadas como SVG em texto; JSON dos contratos sempre expandido.

## 4 · Atributos de qualidade (avalie em toda decisão)

| Atributo | Perguntas |
|---|---|
| Segurança | quem pode? autenticado/autorizado onde? segredo fora do código? entrada validada? |
| Desempenho/escala | qual `RNF`? índice? cache com invalidação clara? réplicas? |
| Observabilidade | log estruturado com traceId? métrica? como achar o erro em produção? |
| Custo | serviço gerenciado necessário? escala a zero em homologação? orçamento (RNF-84)? |
| Testabilidade | porta substituível por falso? dá para testar caixa-preta pelo contrato? |
| Evolução | a mudança provável cabe sem quebrar contrato? |

## 5 · Referências (leia só o que a tarefa pedir)

| Assunto | Arquivo |
|---|---|
| C#, .NET, ASP.NET Core, Minimal APIs × Controllers, DTOs, DI, middleware | `referencias/dotnet-aspnet.md` |
| API REST: recursos, verbos, status, erros, paginação, idempotência, versão | `referencias/api-rest.md` |
| MongoDB: modelagem por agregado, índices, Cosmos DB for MongoDB | `referencias/mongodb.md` |
| Redis: cache-aside, sessões, TTL, chaves, invalidação | `referencias/redis.md` |
| Segurança, observabilidade, custo | `referencias/qualidade-operacional.md` |
| ADR: quando e como registrar | `referencias/adr.md` |
| Docker, CI/CD, Terraform, Azure | skill `devops-cloud` |

## 6 · Orientar os outros agentes

- Para `deboaz-backend`: endereços do contrato, DTOs, classes, portas, coleções e chaves — com a versão de cada `.svg`.
- Para `deboaz-frontend`: contrato (pedido, resposta, erros) e o que muda na integração.
- Para `deboaz-qa`: códigos de erro e cenários de limite que viram teste de integração ou carga.
- Desvio arquitetural achado em código: reporte ao dono da frente com arquivo, linha e regra violada; não corrija código de outra frente.

## Critérios de qualidade

- [ ] Cada decisão cita o cenário/`RNF` que a motiva.
- [ ] Contrato traz tudo que os cenários de API usam — e nada que nenhum cenário peça.
- [ ] Domínio independente de infraestrutura; dependências apontando para dentro.
- [ ] Segurança, observabilidade e custo considerados (mesmo que "não se aplica").
- [ ] Escolha entre alternativas reais tem ADR.

## Skills relacionadas

`diagrama-dev` · `diagrama-arquitetura` · `devops-cloud` · `engenharia-software` · `gherkin-bdd`.
