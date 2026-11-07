# 1_Requisitos

Requisitos do **produto**: o que ele faz, para quem e com quais regras.
Aqui não se fala de backend, frontend ou tecnologia.

- Arquivos `<NN>_<nome>.md`, criados e alterados pela skill `/requisito`.
  Leia no GitHub ou no editor; o PDF sai com temas e paleta das vistas pelo `./boaz.sh pdf`.
- Padrão (marca d'água, formato, componentes): `0_Assets/requisitos/tema.md`.
- Cada documento abre com a marca de topo (projeto, documento, versão, data/hora, status)
  e fecha com o histórico. Toda alteração soma 1 na versão.
- `./boaz.sh requisitos` lista os documentos com versão, data e status.
- `./boaz.sh pdf <documento>` gera `pdf/<documento>_v<NN>_<AAAAMMDD-HHMM>.pdf` e apaga os PDFs de versões anteriores do mesmo documento.
- Para ilustrar, referencie um diagrama **com a versão explícita** (exemplo de formato, não é um arquivo real):
  `[![…](../2_Diagramas/<legenda>_v<NN>_<AAAAMMDD-HHMM>.svg)](../2_Diagramas/<legenda>_v<NN>_<AAAAMMDD-HHMM>.svg)`
- Quando o diagrama ganha versão nova, a referência passa para ela e a versão anterior é apagada.
- `node 0_Assets/requisitos/requisito.mjs conferir 1_Requisitos/*.md` confere marca d'água, links e imagens.
