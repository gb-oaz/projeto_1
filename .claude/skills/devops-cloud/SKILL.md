---
name: devops-cloud
description: Referência de DevOps e nuvem do boaz, consultada sob demanda — Git e GitHub, GitHub Actions (ci, cd, cd-ambiente, qa-carga), CI local de 4_CI_CD/local (ci.sh, compose), Docker e Docker Compose, publicação de imagens, Terraform em 4_CI_CD/terraform, Microsoft Azure (Container Apps, Cosmos DB for MongoDB, Azure Managed Redis, Key Vault, Application Gateway/WAF, Log Analytics, Application Insights, orçamentos), segurança de infraestrutura, observabilidade e custo. Use ao criar ou revisar pipeline, Dockerfile, compose, módulo Terraform ou ambiente, ao investigar falha de CI/CD, ou quando uma decisão técnica depender de infraestrutura.
---

# DevOps e nuvem → `4_CI_CD/` e `.github/workflows/`

Dono: **`deboaz-arc`**. Back, front e qa consultam esta skill para o próprio `Dockerfile` e para entender o CI, mas não alteram pipeline nem Terraform. Base de comportamento: requisitos `02_metodologia-git`, `03_ci-qualidade`, `04_publicacao-imagens`, `05_cd-implantacao` (leia o cenário pelo ID com a skill `gherkin-bdd`).

## Mapa

| Peça | Onde | Requisito |
|---|---|---|
| Branches, worktrees, PR | git / GitHub | `02_metodologia-git` |
| CI (build, testes, cobertura, lint, qa) | `.github/workflows/ci.yml` | `03_ci-qualidade` |
| CI local (prévia do CI, mesmo veredito) | `4_CI_CD/local/ci.sh`, `compose.yml` | `03` RNF-160 a RNF-166 |
| Publicação de imagens | workflow de CD | `04_publicacao-imagens` |
| CD com aprovação e Terraform | `.github/workflows/cd.yml`, `cd-ambiente.yml` | `05_cd-implantacao` |
| Carga em homologação | `.github/workflows/qa-carga.yml` | `03` RNF-195 |
| Infraestrutura como código | `4_CI_CD/terraform/{infra,ambientes,scripts}` | `05` RN-23, RNF-55 a RNF-90 |

## Regras que não se negociam

- **Toda infraestrutura só pelo Terraform** (RN-23); nada criado à mão no portal, salvo o que o requisito exclui (RNF-90).
- Segredos no Key Vault por RBAC (RN-24, RNF-68); no GitHub, autenticação na Azure por **OIDC** (RNF-52), sem senha de longa duração.
- CI local **não publica** (RN-140) e dá o mesmo veredito do GitHub (RNF-161, RNF-165); nunca altera a cópia de trabalho (RNF-164).
- Versões fixadas de ações, imagens e providers (RNF-19, RNF-57).
- Uma implantação por vez por ambiente (RNF-53); produção só depois de homologação (RN-25), com aprovação por ambiente (RN-20).
- `3_Arquitetura/` é **neutro de provedor** (skill `diagrama-arquitetura`); nomes de produto da Azure ficam em `4_CI_CD/`, nos workflows e nos requisitos `05`.

## Referências (leia só o que a tarefa pedir)

| Assunto | Arquivo |
|---|---|
| Git, GitHub e GitHub Actions (jobs, cache, matriz, permissões, OIDC) | `referencias/github-actions.md` |
| Docker, Dockerfile multi-stage, Docker Compose, saúde e portas | `referencias/docker.md` |
| Terraform e Azure (Container Apps, rede, dados, segredos, monitoramento, custo) | `referencias/terraform-azure.md` |

## Fluxo de uma mudança de pipeline ou infra

1. Ache o cenário que pede a mudança (ID); sem cenário, a regra entra antes no requisito (`deboaz-arc`).
2. Mude o mínimo; mantenha nomes de recursos (RNF-88, RNF-89) e tags (RNF-85).
3. Valide localmente: `4_CI_CD/local/ci.sh` (pipeline), `terraform fmt -check` e `terraform validate` (infra), `docker compose config` (compose). Diga na resposta o que rodou e o que não pôde rodar.
4. Plano do Terraform só no executor do CD (RNF-48); nunca `apply` local em ambiente compartilhado.
5. Atualize o diagrama de implantação (`diagrama-arquitetura`) se a topologia mudou.

## Critérios de qualidade

- [ ] Mudança ligada a um ID de requisito.
- [ ] Sem segredo em código, log ou artefato.
- [ ] Versões fixadas; permissões mínimas no workflow (`permissions:`).
- [ ] Custo estimado quando adiciona recurso fixo.
- [ ] Validação local executada e reportada.

## Skills relacionadas

`arquitetura-software` (decisões e qualidade operacional) · `diagrama-arquitetura` (implantação, cloud) · `desenvolvimento` (CI na entrega da tarefa).
