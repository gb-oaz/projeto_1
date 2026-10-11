# 1_Requisitos

Requisitos do **produto**: o que ele faz, para quem e com quais regras.
Aqui não se fala de backend, frontend ou tecnologia.

- Arquivos `<NN>_<nome>.html`, autocontidos, criados e alterados pela skill `/requisito`.
  Abra no navegador: temas claro, sistema e escuro, e botão **PDF**.
- Padrão (marca d'água, estilo, formato, componentes): `0_Assets/requisitos/tema.html`.
- Cada documento abre com a marca de topo (projeto, documento, versão, data/hora, status)
  e fecha com o histórico. Toda alteração soma 1 na versão.
- `./boaz.sh requisitos` lista os documentos com versão, data e status.
- `./boaz.sh pdf <documento>` gera `pdf/<documento>_v<NN>_<AAAAMMDD-HHMM>.pdf`.
- Para ilustrar, referencie um diagrama **com a versão explícita**:
  `<figure class="diagrama"><img src="../2_Diagramas/fluxo-cadastro_v02_20261010-1430.svg" alt="…">…</figure>`
