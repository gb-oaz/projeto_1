// =============================================================================
// boaz · imagens de status do servidor (chamado por gerar.mjs)
//
// Devolve { <caminho relativo a 0_Assets/design>: conteúdo } com, no tema oficial
// (Branco e Preto), em claro e escuro:
//   identidade/imagens/status/<estado>-<formato>-<claro|escuro>.svg            parado
//   identidade/imagens/status/animados/<estado>-<formato>-<claro|escuro>.svg   em laço (SMIL)
//   identidade/imagens/status/selos/<estado>-<claro|escuro>.svg                selo: medidor + rosto
// Em tema de cor, desenhe na hora com o núcleo (scripts/status.mjs) e coresDoStatus.
// =============================================================================
import * as mascotes from './mascotes.mjs';
import { criarStatus, coresDoStatus, ESTADOS, FORMATOS } from './status.mjs';

export function arquivosStatus(cores) {
  const st = criarStatus({ mascotes });
  const oficial = coresDoStatus(cores)['branco-preto'];
  const s = {};
  for (const id of Object.keys(ESTADOS)) for (const [modo, c] of Object.entries(oficial)) {
    for (const [fmt, [largura, altura]] of Object.entries(FORMATOS)) {
      s[`identidade/imagens/status/${id}-${fmt}-${modo}.svg`] = st.desenhar(id, { largura, altura, cores: c, movimentoReduzido: true }) + '\n';
      s[`identidade/imagens/status/animados/${id}-${fmt}-${modo}.svg`] = st.desenhar(id, { largura, altura, cores: c }) + '\n';
    }
    s[`identidade/imagens/status/selos/${id}-${modo}.svg`] = st.selo(id, { tamanho: 256, cores: c, movimentoReduzido: true }) + '\n';
  }
  return s;
}
