---
name: ux-usabilidade
description: Competência de UX do boaz para projetar e avaliar a experiência a partir dos cenários Gherkin — arquitetura da informação, jornada e fluxo do usuário, prototipação por vistas, heurísticas de usabilidade de Nielsen, acessibilidade digital (WCAG 2.2 AA), design responsivo pelas classes de janela do Material Design 3 e experiência de PWA (instalação, offline, atualização). Use ao planejar as vistas de um módulo, ao revisar uma vista ou tela pronta, ao mapear a navegação, ou quando um cenário de tela parecer confuso para quem usa. Tokens, cores, componentes e mascotes ficam na skill design-system; o desenho do .svg, na diagrama-dev.
---

# UX e usabilidade → vistas que cumprem o Gherkin

O `deboaz-uxui` **define e valida** a experiência e o design. Não decide regra de negócio (`deboaz-po`) nem implementa (`deboaz-frontend`). Base de leitura: `gherkin-bdd`. Visual e tokens: `design-system`. Desenho: `diagrama-dev` (seção Vista de tela). Regras do projeto: `1_Requisitos/06_design-system-telas.md` (RN-27 a RNF-118).

## 1 · Do cenário ao fluxo

1. Leia os **Cenários de tela** do módulo e os documentos de UX da aplicação (`07_landing-page-ux`, `08_hub-ux`, `09_quest-ux`) e de componentes compostos (`10_*`).
2. **Atores e objetivos**: personas de `00_visao-produto.md`; para cada uma, o objetivo do módulo.
3. **Jornada** (macro): etapas, o que a pessoa pensa/sente, pontos de atrito — use para priorizar o que a vista deixa à mão.
4. **Fluxo** (micro): telas e transições que os cenários exigem, incluindo recusas e retornos. Cada `Quando` é uma ação visível; cada `Então` é um estado de tela.
5. **Inventário de estados por tela**: vazio, carregando, sucesso, erro, sem permissão, sem conexão — com o **texto exato** do Gherkin.
6. **Arquitetura da informação**: agrupamento, hierarquia, rótulos (do glossário), navegação pela casca da aplicação (`08_hub-ux` RF-17) — sem inventar menu que nenhum cenário pede.

Cenário que não fecha um fluxo (estado sem saída, ação sem resposta visível, texto ausente) é **lacuna** → `deboaz-po`, com a proposta em Gherkin.

## 2 · Prototipação = vistas

- Uma vista por plataforma (`-web`, `-pwa`, `-mobile`) e por tela; toda tela começa por vista aprovada em PR (RN-27, RN-33).
- Só tokens e componentes do design system (RN-28, RNF-96, RNF-97); componente novo nasce com vista própria (RN-29).
- Vista de um módulo mostra só o espaço genérico dos outros módulos.
- Anotações da vista (RNF-104) indicam estados, ordem de foco e comportamento responsivo.

## 3 · Avaliação (antes de entregar e ao revisar telas prontas)

- **Heurísticas de Nielsen**: passe as 10, com severidade 0–4 — `referencias/nielsen.md`.
- **Acessibilidade WCAG 2.2 AA**: contraste, alvo de toque, foco, rótulos, leitor de tela, movimento — `referencias/acessibilidade.md`.
- **Responsivo**: classes de janela do M3 (compact, medium, expanded…), nada cortado nem com rolagem horizontal; a mesma tarefa possível em todas as plataformas (RN-32).
- **PWA**: instalação, offline, atualização — `referencias/pwa-ux.md`.
- **Fidelidade ao Gherkin**: cada texto, estado e ação do cenário aparece na vista, com o mesmo texto.

Formato do achado: `Vista/tela · heurística ou critério · severidade · problema · proposta`. Problema que exige mudar regra → `deboaz-po`; que exige mudar código → `deboaz-frontend`; que exige token/componente → você mesmo, no design system.

## 4 · Material Design 3 e "Material UI 3"

O projeto aplica **Material Design 3** com tokens e componentes próprios, gerados de `0_Assets/design/` para Vue e MAUI. "Material UI" (biblioteca React) **não** é usada: RNF-97 proíbe biblioteca visual externa. Quando um pedido citar "Material UI 3", interprete como componentes M3 do design system.

## Critérios de qualidade

- [ ] Todo cenário de tela mapeado a uma vista e a um estado.
- [ ] Textos idênticos ao Gherkin; termos do glossário.
- [ ] Heurísticas sem achado de severidade ≥ 3 aberto.
- [ ] WCAG 2.2 AA atendido (contraste medido, não estimado).
- [ ] Vistas das três plataformas coerentes entre si.

## Skills relacionadas

`design-system` · `diagrama-dev` · `gherkin-bdd` · `engenharia-requisitos` (quando a lacuna é de comportamento).
