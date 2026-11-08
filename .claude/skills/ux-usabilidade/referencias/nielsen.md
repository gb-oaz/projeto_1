# Heurísticas de usabilidade de Nielsen — checklist

Severidade: 0 não é problema · 1 cosmético · 2 menor · 3 maior (corrigir antes da entrega) · 4 catástrofe (bloqueia).

| # | Heurística | Perguntas na vista/tela |
|---|---|---|
| 1 | **Visibilidade do status do sistema** | Há retorno para cada ação (carregando, salvo, erro)? A pessoa sabe onde está (título, aba ativa)? |
| 2 | **Correspondência com o mundo real** | Rótulos do glossário e da linguagem da escola, não do sistema? Ordem natural das informações? |
| 3 | **Controle e liberdade** | Dá para cancelar, voltar, desfazer? Ação destrutiva tem confirmação ou desfazer (RN-248)? |
| 4 | **Consistência e padrões** | Mesmo componente para a mesma função em todos os módulos? Segue M3 e o design system? |
| 5 | **Prevenção de erros** | Restrições antes do envio (formato, obrigatório), padrões sensatos, botão desabilitado com motivo visível? |
| 6 | **Reconhecer em vez de lembrar** | Opções visíveis, dados anteriores reaproveitados, nada que a pessoa precise decorar entre telas? |
| 7 | **Flexibilidade e eficiência** | Atalhos para quem usa muito (busca, filtros, ações em lote) sem atrapalhar quem usa pouco? |
| 8 | **Estética e design minimalista** | Só o que a tarefa precisa; hierarquia clara; nada competindo com a ação principal? |
| 9 | **Ajudar a reconhecer, diagnosticar e recuperar erros** | Mensagem em linguagem simples, diz o problema e a saída, junto do campo? Mascote de retorno quando cabe (RN-34)? |
| 10 | **Ajuda e documentação** | Dica contextual onde a tarefa não é óbvia, sem depender de manual? |

## Registro

```
vista-hub-usuarios-web_v04 · H5 Prevenção de erros · 3
Problema: "Salvar" ativo com e-mail inválido; o erro só aparece após o envio.
Proposta: validar o formato ao sair do campo e mostrar "Informe um e-mail válido" sob ele.
Dono: deboaz-uxui (vista) → deboaz-po se o texto não existir no Gherkin.
```
