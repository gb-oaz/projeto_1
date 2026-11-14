---
name: engenharia-requisitos
description: Competência de requisitos do boaz (dono: deboaz-arc) para decidir o conteúdo dos requisitos — transformar necessidade de negócio em regras RF/RN/RNF testáveis, levar história de usuário e critério de aceitação para Gherkin, modelar o processo de negócio em cenários, achar conflitos e ambiguidades, avaliar o impacto de uma mudança em vistas, diagramas, testes e código, priorizar (valor × risco, modelo espiral) e manter backlog e rastreabilidade. Use ao planejar ou revisar um módulo ou uma mudança de requisito, ao responder dúvida de comportamento vinda de outro agente, ou ao priorizar o que entra na próxima tarefa. O formato do .md é da skill requisito.
---

# Engenharia de requisitos → o que entra no `.md`

A skill `requisito` diz **como** o documento é escrito (formato, versão, PDF). Esta diz **o que** escrever e **como decidir**. Base de leitura: skill `gherkin-bdd`.

O `deboaz-arc` **define e valida** as regras de negócio; nenhum outro agente decide regra. Nada de tecnologia no requisito: endereço, status e código só em `cenarios-api`, e só o que o contrato (também do `deboaz-arc`) traz.

## 1 · Da necessidade ao cenário

1. **Entenda o objetivo**: quem (persona de `00_visao-produto.md`), o que quer conseguir, por quê (valor), e como saberá que deu certo.
2. **História de usuário** é ferramenta de conversa, não seção do documento: *Como `<persona>`, quero `<ação>` para `<valor>`* vira o `**Objetivo:**` do documento ou o título do `###`, e seus critérios de aceitação viram cenários.
3. **Critério de aceitação → cenário**: cada critério verificável é um `Cenário` com dados concretos; regra em tabela é `Esquema do Cenário` + `Exemplos`.
4. **Classifique**: `RF` (a pessoa faz, o sistema responde) · `RN` (regra provada por um caso) · `RNF` (medida com número: tempo, volume, disponibilidade, segurança, acessibilidade). `RNF` sem número não é requisito: vira Em aberto.
5. **Modele o processo**: escreva o caminho feliz de ponta a ponta (grupo com âncora kebab-case), depois os desvios (negativos, alternativos, limites, estados de tela). Fluxo que fica claro desenhado → peça ao `deboaz-arc` o diagrama de atividades/sequência; não descreva em parágrafo.
6. **Telas e API**: regra que existe nas duas pontas tem cenário de tela (âncora do ID) e de API (âncora `-api`).

## 2 · Qualidade de um requisito (checklist antes de salvar)

| Atributo | Pergunta |
|---|---|
| Necessário | Alguém perde algo se sair? |
| Inequívoco | Duas pessoas leem igual? Termos estão no glossário? |
| Verificável | O `Então` pode ser testado sem interpretação? |
| Completo | Tem negativo, limite e estados de tela? Tem o par de API? |
| Consistente | Contradiz outro cenário, documento, vista ou contrato? |
| Atômico | Um comportamento por cenário? |
| Rastreável | Tem ID único e citação das dependências? |
| Livre de solução | Diz o *quê*, não o *como*? |

Não sabe a resposta de negócio? **Não invente**: registre em `## Em aberto` com quem responde.

## 3 · Conflitos, ambiguidades e dúvidas de outros agentes

- Pergunta de outro agente chega com ID e dúvida: responda com o cenário (novo ou alterado) em nova versão do documento, ou com a decisão registrada em Em aberto.
- Conflito entre documentos: decida qual vale, ajuste o outro na mesma tarefa (ambos ganham versão) e avise os afetados.
- Proposta de cenário vinda de outro agente é **sugestão**: valide valor e regra antes de aceitar.

## 4 · Impacto de uma mudança

Antes de mudar um cenário existente, meça o impacto e liste-o na resposta (procedimento em `referencias/impacto-priorizacao.md`):
- quem cita o ID (outros `.md`, vistas, diagramas, `.feature`, testes, código);
- quem precisa agir: `deboaz-uxui` (texto/estado de tela), `deboaz-arc` (contrato, classes, documentos), `deboaz-qa` (`.feature` e passos), `deboaz-backend`/`deboaz-frontend` (código);
- se o documento estava `Aprovado`, volta a `Em revisão`.

## 5 · Priorização, backlog e espiral

- O backlog do boaz é a fila de tarefas `tarefa_<NN>`/`fix_<NN>` (skill `desenvolvimento`) alimentada pelos módulos de `15_modulos.md` e pelos itens de **Em aberto**; o requisito não ganha tabela de backlog nem parágrafo de prioridade.
- Priorize por **valor × risco** (alto valor e alto risco primeiro, como manda o modelo espiral) e por dependência (o que outros módulos leem vem antes). Técnicas e formato da recomendação em `referencias/impacto-priorizacao.md`.
- A prioridade vai na **resposta** (para quem planeja as sessões), não no documento.

## 6 · Entrega

Documento e versão · IDs criados/alterados/removidos · impacto por agente · pendências de Em aberto · recomendação de prioridade (se pedida).

## Skills relacionadas

`requisito` (formato, versão, PDF) · `requisito-estilo` (apresentação) · `gherkin-bdd` (leitura e rastreio) · `engenharia-software` (`referencias/ddd.md`, parte estratégica; `referencias/testes-processo.md`, modelo espiral).
