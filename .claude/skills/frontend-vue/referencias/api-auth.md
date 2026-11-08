# Integração com a API REST, autenticação e autorização

Fonte: contrato de API do módulo (`2_Diagramas/contrato-*`), requisitos `17_modulo-usuario-a-autenticacao` e `-b-autorizacao`, guia `arquitetura-software/referencias/api-rest.md`.

## Camadas

```
componente/tela → caso de uso (core/usecases) → porta de saída (core/ports/out)
                                     ↑ implementada por
               adaptador HTTP (apps/<app>/infra/out/<contexto>/<feature>)
```

- O adaptador é o **único** lugar com `fetch`, URL, cabeçalhos e conversão JSON ↔ DTO.
- Endereço base do backend vem da configuração do app (RNF-198 no mobile; variável de build na web), nunca fixo no código.
- Respostas convertidas em resultado tipado: sucesso com DTO ou falha com **código** do contrato (`ENTRADA_INVALIDA`, `ACESSO_NEGADO`, `DADOS_INVALIDOS` com o campo).
- Erro de rede/timeout vira falha própria (estado "sem conexão"), distinta de erro de negócio.
- Cancelamento com `AbortController` quando a tela sai.

## Autenticação no cliente

- Token de acesso (`Bearer`, curto) **em memória**; nunca em `localStorage`/`sessionStorage`.
- Renovação pelo cookie `HttpOnly` que o servidor manda só para `/auth/renovar` — o JavaScript não lê nem grava esse cookie; o pedido sai com `credentials: 'include'` só nesse endereço.
- 401 numa chamada → uma tentativa de renovar → repetir a chamada; falhou → tela de Entrar com o aviso do Gherkin (ex.: "Sua sessão terminou. Entre de novo.").
- Evite corrida: várias chamadas com 401 simultâneo compartilham a mesma renovação em andamento.
- Saída limpa o token em memória e chama o endereço de sair do contrato.

## Autorização no cliente

- O front **esconde ou desabilita** o que a pessoa não pode, conforme permissões/módulos que a API informa — é conveniência de UX; quem garante é o servidor.
- 403 de uma ação é sempre tratado (estado "sem permissão" com o texto do Gherkin), mesmo que o botão estivesse escondido.

## Testes

- Caso de uso testado com porta falsa (sem HTTP).
- Adaptador testado com `fetch` simulado (`vi.fn`/`vi.stubGlobal`), cobrindo cada código de erro do contrato.
- Ponta a ponta com backend real é do qa (Cypress).
