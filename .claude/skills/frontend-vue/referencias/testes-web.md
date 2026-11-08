# Testes da web (Vitest + Vue Test Utils)

Requisitos: `03_ci-qualidade` RNF-16 (Vitest), RNF-17 (qualidade), RNF-25 (cobertura ≥ 80 %); `14` RNF-193 (sem navegador, sem backend). Ponta a ponta: qa (Cypress, RNF-18).

## Onde

`tests/core/<contexto>/<feature>/…` espelha `core/`; `tests/apps/…` para composição e adaptadores. `pnpm test` roda com cobertura e limite.

## Tipos

| Tipo | Alvo | Dublê |
|---|---|---|
| Unitário de domínio | regras em `domain/` | nenhum |
| Unitário de caso de uso | `usecases/` | portas falsas |
| Componente | `components/` montado com `mount` | props/eventos; caso de uso falso injetado |
| Adaptador | `infra/out` | `fetch` simulado |
| Integração entre poucos componentes | tela + componentes + caso de uso | porta de saída falsa |

## Componente — o que verificar

- Renderiza os textos do Gherkin em cada estado (vazio, carregando, erro, sucesso).
- Emite o evento certo com o payload certo.
- Acessibilidade básica: rótulo ligado ao campo, `role`, `aria-invalid` no erro, botão é `button`.
- Busque por papel/texto/rótulo visível, não por classe CSS.

```ts
import { mount } from '@vue/test-utils'
import { describe, it, expect } from 'vitest'
import FormEntrar from '@/core/commons/usuario/autenticacao/components/FormEntrar.vue'

describe('RF-134 Entrar no hub', () => {
  it('mostra "Informe sua senha" quando envia sem senha', async () => {
    const w = mount(FormEntrar)
    await w.find('form').trigger('submit')
    expect(w.text()).toContain('Informe sua senha')
  })
})
```

(O caminho e o texto são ilustrativos: use os do código e do Gherkin.)

## TDD

Vermelho pelo motivo certo → mínimo para verde → refatora. Um comportamento por teste, nome com o ID. Sem `setTimeout` real: `vi.useFakeTimers()`. Sem snapshot como única verificação.
