# Domain-Driven Design

## Estratégico (arquiteto)

- **Domínio e subdomínios**: núcleo (o que diferencia o produto), de suporte e genéricos (autenticação, e-mail). Investimento de modelagem vai para o núcleo.
- **Bounded context**: fronteira onde um termo tem um sentido só. No boaz os contextos são pastas de `core/` (`commons`, `hub`, `quest`, `landing_page`) e os módulos de `15_modulos.md`.
- **Linguagem ubíqua**: o vocabulário do `99_glossario.md`, igual em cenário, vista, contrato, classe, coleção e teste. Termo novo → proposta ao `deboaz-arc` para o glossário.
- **Mapa de contextos**: como os contextos se relacionam (cliente-fornecedor, conformista, camada anticorrupção). No projeto, contextos só falam por `commons` (RN-146); dado de outro contexto é **só lido** e aparece no modelo de documentos como tal.
- **Sinais para os requisitos**: o mesmo termo com dois sentidos em documentos diferentes indica dois contextos — registre em Em aberto em vez de unificar à força.

## Tático (arquiteto, back, front)

| Bloco | Definição | No projeto |
|---|---|---|
| **Entidade** | identidade que persiste no tempo | `Usuario`, `Sala` em `domain/` |
| **Objeto de valor** | definido pelos atributos, imutável, se autovalida | `Email`, `SiglaEscola`, `SalaId` (`record`/`readonly`) |
| **Agregado** | grupo consistente com uma raiz; só a raiz é referenciada de fora | um documento Mongo por agregado; embutidos = parte do agregado |
| **Repositório** | coleção do agregado, sem detalhes de banco | interface em `ports/out/`, implementação em `infra/out/` |
| **Serviço de domínio** | regra que não cabe numa entidade | classe em `domain/` sem I/O |
| **Caso de uso (application service)** | orquestra: carrega, chama o domínio, salva, publica | `usecases/` |
| **Evento de domínio** | fato passado relevante ao negócio | `SalaEncerrada`; integração (fila) só por porta |
| **Fábrica** | cria agregado válido com regra complexa | método estático ou classe em `domain/` |

## Regras práticas

- Invariantes do agregado protegidas dentro dele; transação = um agregado.
- Referência entre agregados por ID, não por objeto.
- Domínio anêmico (só getters/setters, regra no caso de uso) é sinal de alerta.
- Agregado pequeno: grande demais causa contenção e documentos enormes no Mongo.
