# Rastreabilidade requisito ↔ diagrama ↔ teste ↔ código

O **ID** (`RF-134`, `RN-271`, `RNF-25`; `-api` para a ponta da API) é a única chave. Toda peça que nasce de um cenário cita o ID.

| Peça | Onde | Como cita o ID |
|---|---|---|
| Requisito | `1_Requisitos/<doc>.md` | `<a id="RF-134"></a>` + `### \`RF-134\`` |
| Vista / diagrama | `2_Diagramas/*.svg` | texto ou nota com o ID; legenda no requisito |
| Teste de integração (API) | `5_DEV/qa/backend/integracao/**/*.feature` | tag `@RF-134` no cenário copiado |
| Funcional web / mobile | `5_DEV/qa/frontend/**` | tag ou nome do teste com o ID |
| Carga | `5_DEV/qa/backend/carga/**` | ID do `RNF` com o número |
| Unitário (back/front) | `5_DEV/backend/tests`, `5_DEV/frontend/**/tests` | ID no nome ou comentário do teste |
| Commit | git | ID na mensagem (`… (RN-271)`) |

## Comandos

```bash
# IDs de um documento
grep -oE '<a id="(RF|RN|RNF)-[0-9]+(-api)?"' 1_Requisitos/<doc>.md | grep -oE '(RF|RN|RNF)-[0-9]+(-api)?' | sort -u

# Onde um ID aparece fora do requisito
grep -rn --exclude-dir=node_modules --exclude-dir=bin --exclude-dir=obj 'RF-134\b' 2_Diagramas 5_DEV | cut -c1-160

# IDs do documento sem nenhum teste no qa
for id in $(grep -oE '<a id="(RF|RN|RNF)-[0-9]+' 1_Requisitos/<doc>.md | grep -oE '(RF|RN|RNF)-[0-9]+' | sort -u); do
  grep -rqw "$id" 5_DEV/qa || echo "sem teste: $id"; done

# Testes que citam um ID que não existe mais nos requisitos
grep -rhoE '@?(RF|RN|RNF)-[0-9]+' 5_DEV/qa | tr -d '@' | sort -u | while read id; do
  grep -rq "<a id=\"$id" 1_Requisitos || echo "órfão: $id"; done

# Versão do requisito copiada no .feature (deve bater com o front matter)
grep -n 'v[0-9][0-9]' 5_DEV/qa/backend/integracao/**/<feature>.feature | head -3
```

## Matriz (formato da entrega)

| ID | Cenário | Vista/diagrama | Teste qa | Unitário | Estado |
|---|---|---|---|---|---|
| RF-134 | staff entra | `vista-hub-entrar-web_v03` | `autenticacao.feature @RF-134` | — | verde |
| RF-135 | mãe entra com o código | `vista-quest-entrar-mobile_v02` | — | — | pendente (sem teste) |

Estado: `pendente` · `vermelho` (teste existe, falha pelo motivo certo) · `verde` · `divergente` (código ≠ requisito, reportado ao dono).
