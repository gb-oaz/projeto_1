# 4_CI_CD

- [`terraform/`](terraform/README.md): infraestrutura da Azure (homologação e
  produção) e a preparação única de cada assinatura — requisito 05
- [`local/`](local/README.md): CI local — repete na máquina, com Docker
  Compose, as verificações do GitHub Actions e roda o qa contra as aplicações
  em contêiner, sem publicar imagem — requisito 03

Os workflows do GitHub Actions ficam em `.github/workflows/`:
[`ci.yml`](../.github/workflows/ci.yml) (CI e publicação das imagens),
[`cd.yml`](../.github/workflows/cd.yml) e
[`cd-ambiente.yml`](../.github/workflows/cd-ambiente.yml) (implantação).
