# 1_Requisitos

Requisitos do **produto**: o que ele faz, para quem e com quais regras.
Aqui não se fala de backend, frontend ou tecnologia.

- Arquivos `<NN>_<nome>.md`, criados e alterados pela skill `/requisito`.
  Leia no GitHub ou no editor; o PDF sai com temas e paleta das vistas pelo `./boaz.sh pdf`.
- Padrão (marca d'água, formato, componentes): `0_Assets/requisitos/tema.md`.
- Cada documento abre com a marca de topo (projeto, documento, versão, data/hora, status)
  e fecha com o histórico. Toda alteração soma 1 na versão.
- `./boaz.sh requisitos` lista os documentos com versão, data e status.
- `./boaz.sh pdf <documento>` gera `pdf/<documento>_v<NN>_<AAAAMMDD-HHMM>.pdf`.
- Para ilustrar, referencie um diagrama **com a versão explícita**:
  `[![…](../2_Diagramas/fluxo-cadastro_v02_20261010-1430.svg)](../2_Diagramas/fluxo-cadastro_v02_20261010-1430.svg)`
- `node 0_Assets/requisitos/requisito.mjs conferir 1_Requisitos/*.md` confere marca d'água, links e imagens.
