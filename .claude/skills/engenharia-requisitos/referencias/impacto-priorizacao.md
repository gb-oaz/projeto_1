# Impacto de mudança e priorização

## Análise de impacto (antes de alterar um cenário)

```bash
ID=RF-134
# documentos de requisito que citam o ID (links doc.md#ID)
grep -rln "$ID\b" 1_Requisitos --include=*.md
# vistas e diagramas que citam o ID
grep -rln "$ID\b" 2_Diagramas 3_Arquitetura
# testes do qa e unitários
grep -rln --exclude-dir=node_modules --exclude-dir=bin --exclude-dir=obj "$ID\b" 5_DEV
# âncoras do documento citadas por outros documentos
node 0_Assets/requisitos/requisito.mjs conferir 1_Requisitos/<doc>.md --avisos
```

Classifique a mudança:

| Tipo | Exemplo | Quem age |
|---|---|---|
| Texto exibido | "Sua sessão terminou." mudou | uxui (vista), qa (funcional), front |
| Regra | limite de 8 h vira configurável | arc (classes/documentos), qa, back, front |
| Contrato | novo campo ou código de erro | arc (contrato) **antes** do cenário de API, qa, back, front |
| Remoção | regra sai | todos que citam o ID; ID nunca é reutilizado |
| `RNF` | limite de tempo muda | qa (carga), arc (se afeta infra) |

## Priorização

**Valor × risco** (padrão do projeto, alinhado ao modelo espiral):

| | Risco alto | Risco baixo |
|---|---|---|
| **Valor alto** | 1º — faça já, em fatia pequena, para aprender | 2º — entregue em seguida |
| **Valor baixo** | 3º — só se o risco bloquear outra coisa | 4º — último ou fora de escopo |

Risco = incerteza de regra, integração nova, limite de desempenho, segurança, dependência de outro módulo.

Apoios:
- **MoSCoW** por módulo (Must, Should, Could, Won't) — `Won't` vai para `fora-de-escopo` no documento.
- **Dependência**: módulo que outros leem (ex.: Usuários, Catálogo) vem antes de quem lê.
- **Fatiamento vertical**: uma regra de ponta a ponta (tela + API + teste) vale mais que várias regras só no back.

## Formato da recomendação (na resposta)

```
Prioridade sugerida (valor × risco):
1. <doc> RF-xx — alto valor, alto risco (<qual risco>) → próxima tarefa
2. <doc> RF-yy — alto valor, baixo risco
Bloqueios: Em aberto "<pergunta>" — responde: <quem>
```
