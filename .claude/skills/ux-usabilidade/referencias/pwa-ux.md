# Experiência de PWA

No boaz, **PWA** é a web instalada no celular: vista própria `-pwa`, classe de janela compact, com anotações (`06_design-system-telas` RNF-103). Comportamento offline, instalação e atualização **só existem se um cenário pedir** — sem cenário, é lacuna para o `deboaz-po`, não decisão de UX.

## O que avaliar na vista `-pwa`

- **Modo standalone**: não há barra do navegador → a casca precisa de voltar, título e saída próprios.
- **Áreas seguras**: entalhe e barra de gestos (safe-area) não cobrem ação nem texto.
- **Polegar**: ações principais na metade inferior; navegação inferior na classe compact.
- **Teclado virtual**: campo focado não fica escondido; botão de enviar visível.
- **Orientação**: retrato como padrão; paisagem sem quebra.

## Quando houver cenário de offline/instalação/atualização

| Situação | Boa prática de UX |
|---|---|
| Sem conexão | aviso persistente e discreto ("Sem conexão"), ações que dependem da rede desabilitadas com motivo, dados já carregados continuam visíveis |
| Volta da conexão | aviso curto e sincronização sem perder o que a pessoa digitou |
| Ação feita offline | estado "pendente" visível até confirmar |
| Instalação | convite no momento de valor (depois de uma tarefa concluída), nunca na primeira tela; dá para dispensar |
| Atualização disponível | aviso "Nova versão disponível · Atualizar", sem recarregar no meio de uma tarefa |
| Ícone e splash | da identidade (`90_identidade-visual`, mesmo desenho do iOS) |

Cada linha dessas só entra na vista se o Gherkin tiver o texto e o estado correspondentes.
