# TDD, BDD, pirâmide de testes, modelo espiral e documentação

## BDD → TDD no boaz

1. **Descobrir** (PO): conversa vira exemplos concretos → cenários Gherkin no `.md`.
2. **Formular**: vistas (UX/UI) e diagramas (arquiteto) ilustram os cenários.
3. **Automatizar** (qa): cenários viram testes vermelhos em `5_DEV/qa/` (`.feature` com a tag do ID).
4. **Implementar** (back/front): TDD interno — unitário vermelho → verde → refatora — até o teste do qa ficar verde.

## TDD

- Um teste por vez; o teste falha **pelo motivo certo** (falta comportamento, não erro de compilação ou de fixture).
- Verde com o mínimo; refatore com os testes verdes.
- Nomeie o teste pelo comportamento: `EncerrarSala_sem_permissao_responde_403_RN_xx`.
- Arrange-Act-Assert; um comportamento por teste; nada de lógica (`if`, laço) no teste.
- Dublês: falso/stub nas portas de saída nos unitários; banco e cache reais nos testes de integração (RNF-37).

## Pirâmide do projeto

| Nível | Onde | Dono | Base |
|---|---|---|---|
| Unitário | `5_DEV/backend/tests`, `5_DEV/frontend/web/tests`, mobile | back / front | regras de domínio e casos de uso |
| Integração entre poucos componentes | dentro de cada projeto | back / front | adaptadores |
| Integração da API (caixa-preta) | `5_DEV/qa/backend/integracao` | qa | Cenários de API |
| Funcional / ponta a ponta | `5_DEV/qa/frontend/web|mobile/funcional` | qa | Cenários de tela |
| Carga e desempenho | `5_DEV/qa/backend/carga` | qa | `RNF` com número |

Muitos unitários, menos integração, poucos ponta a ponta. O qa cobre o comportamento visível (RN-142); detalhe interno fica no unitário. Cobertura mínima e ferramentas: `1_Requisitos/03_ci-qualidade.md`.

## Modelo espiral

Cada volta da espiral = **objetivos → análise de riscos → desenvolvimento e verificação → planejamento da próxima volta**. No boaz cada volta é um módulo ou uma fatia (uma tarefa `tarefa_<NN>`), passando por PO → UX/UI → arquiteto → qa → back/front. Use-o para:
- atacar primeiro o que tem mais risco (regra incerta, integração nova, limite de desempenho);
- transformar risco não resolvido em item de **Em aberto** ou em protótipo (vista) antes de codar;
- revisar o requisito ao fim de cada volta com o que se aprendeu (nova versão, nunca edição silenciosa).

## Documentação técnica

- O que é decisão de arquitetura → diagrama (`2_Diagramas/`, `3_Arquitetura/`) e, quando houver escolha entre alternativas, ADR (skill `arquitetura-software`).
- O que é comportamento → Gherkin no requisito.
- O que é operação de uma frente → `README.md` da frente.
- Comentário no código só para o *porquê* que o código não diz.
