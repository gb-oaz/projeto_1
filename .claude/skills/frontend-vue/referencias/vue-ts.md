# Vue 3 + TypeScript

## Componentes

- **SFC com `<script setup lang="ts">`** e Composition API.
- Props e eventos tipados: `defineProps<{ titulo: string; desabilitado?: boolean }>()`, `defineEmits<{ salvar: [dados: CadastroDto] }>()`; `withDefaults` para padrões.
- **Props para baixo, eventos para cima**; nunca mutar prop. `defineModel` para `v-model` de componente.
- Slots para composição (cabeçalho, ações) em vez de dezenas de props de layout.
- Componente **apresentacional** (só recebe e emite; testável isolado) × **contêiner/tela** (chama caso de uso, guarda estado da tela). Os de `core/*/components` tendem a apresentacionais.
- Nome em PascalCase, arquivo por componente; um componente por responsabilidade.
- Estilo `scoped`, só com variáveis CSS dos tokens gerados.

## Reatividade e estado

- `ref` para valores, `reactive` só para objeto que não será reatribuído, `computed` para derivados (sem efeitos), `watch` só para efeito colateral real.
- **Estado local** no componente-tela; **estado compartilhado** em composable com estado no escopo do módulo ou `provide/inject` tipado (`InjectionKey<T>`), criado no `main.ts`. Gerenciador de estado externo (Pinia) só com ADR.
- Composable (`useX`) encapsula estado + lógica de UI reutilizável; regra de negócio continua em `usecases/`.
- Estados assíncronos explícitos: `'ocioso' | 'carregando' | 'sucesso' | 'erro'` — mapeiam os estados da vista.

## TypeScript

- `strict`; nada de `any` (use `unknown` + estreitamento); tipos do contrato num lugar só (DTOs na porta).
- União discriminada para resultado: `{ ok: true; valor } | { ok: false; codigo: 'ACESSO_NEGADO' | ... }`.
- `vue-tsc` sem erro (RNF-17).

## HTML5 e CSS3

- Elementos semânticos (`main`, `nav`, `header`, `button`, `form`, `label`), nunca `div` clicável.
- Layout com flexbox/grid e container queries/media queries nas larguras das classes de janela do M3 (tokens de layout).
- Unidades relativas (`rem`) vindas dos tokens; respeitar `prefers-reduced-motion` e `prefers-color-scheme` via tokens de tema.

## Desempenho

- Carregamento sob demanda de telas pesadas (`defineAsyncComponent`/import dinâmico).
- `v-for` sempre com `:key` estável; `v-if` × `v-show` conforme frequência.
- Lista longa paginada pela API, não filtrada no cliente.
