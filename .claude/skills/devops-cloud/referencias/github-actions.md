# Git, GitHub e GitHub Actions

## Git no boaz (`02_metodologia-git`)

- Branch por tarefa: `tarefa_<NN>-<descricao>` / `fix_<NN>-<descricao>`, num worktree em `.claude/worktrees/`; a cópia principal não troca de branch.
- Commits pequenos, mensagem em português citando os IDs (`… (RN-271)`).
- Entrega por PR para `master`; o CI precisa estar verde.

## Workflows

| Arquivo | Disparo | Faz |
|---|---|---|
| `ci.yml` | push / PR | compila e testa só a frente que mudou (RNF-34), jobs de web, mobile e qa, job agregador (RNF-36) |
| `cd.yml` | merge / tag (RN-18, RN-19) | publica imagens e implanta com aprovação |
| `cd-ambiente.yml` | manual (RNF-91) | liga/desliga homologação (RN-26) |
| `qa-carga.yml` | manual | carga contra homologação (RNF-195) |

Leia o workflow antes de mudar; os requisitos `03` e `05` descrevem cada job pelo ID.

## Boas práticas

- `permissions:` mínimas por workflow/job; `id-token: write` só onde há OIDC.
- Ações fixadas por versão (idealmente SHA); Dependabot cuida das atualizações (RNF-21).
- `concurrency` para uma implantação por ambiente (RNF-53).
- `environment:` com aprovação para homologação e produção (RN-20).
- Filtro por caminho (`paths`) ou detecção de mudança para rodar só a frente afetada.
- Cache de dependências (pnpm store, NuGet) com chave pelo lockfile.
- Resultados de teste em JUnit XML publicados como artefato; código de saída decide o job.
- Segredos só via `secrets`/Key Vault; nunca `echo` de segredo; `::add-mask::` para valor derivado.
- Executores: `03` RNF-13 (iOS em macOS, RNF-24/RNF-174).

## Diagnóstico de falha

1. `gh run list --branch <branch>` → `gh run view <id> --log-failed`.
2. Reproduza com `4_CI_CD/local/ci.sh` (mesmas verificações, RNF-161).
3. Falha de teste é do dono da frente/qa; falha de pipeline é do arquiteto.
