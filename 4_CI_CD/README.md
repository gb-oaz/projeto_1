# 4_CI_CD

- [`terraform/`](terraform/README.md): infraestrutura da Azure (homologação e
  produção) e a preparação única de cada assinatura — requisito 05
- CI local (rodar na máquina as mesmas verificações do GitHub Actions): a ser
  construído

Os workflows do GitHub Actions ficam em `.github/workflows/`:
[`ci.yml`](../.github/workflows/ci.yml) (CI e publicação das imagens),
[`cd.yml`](../.github/workflows/cd.yml) e
[`cd-ambiente.yml`](../.github/workflows/cd-ambiente.yml) (implantação).
