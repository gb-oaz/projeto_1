// =============================================================================
// boaz · imagens de status do servidor (chamado por gerar.mjs)
//
// Devolve { <caminho relativo a 0_Assets/design>: conteúdo } com, no tema oficial
// (Branco e Preto), em claro e escuro:
//   identidade/imagens/status/<estado>-<formato>-<claro|escuro>.svg            parado
//   identidade/imagens/status/animados/<estado>-<formato>-<claro|escuro>.svg   em laço (SMIL)
//   identidade/imagens/status/selos/<estado>-<claro|escuro>.svg                selo: medidor + rosto
//   plataformas/web/status.mjs                    o núcleo, para desenhar qualquer estado na tela
// Em tema de cor, desenhe na hora com o núcleo e coresDoStatus (ou var(--md-sys-color-…) em SVG embutido).
// =============================================================================
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import * as mascotes from './mascotes.mjs';
import { criarStatus, coresDoStatus, ESTADOS, FORMATOS } from './status.mjs';

const AQUI = dirname(fileURLToPath(import.meta.url));

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
  s['plataformas/web/status.mjs'] = readFileSync(join(AQUI, 'status.mjs'), 'utf8');
  return s;
}
