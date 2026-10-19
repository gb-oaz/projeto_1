// =============================================================================
// boaz · arquivos dos fundos da marca (chamado por gerar.mjs)
//
// Devolve { <caminho relativo a 0_Assets/design>: conteúdo } com:
//   identidade/imagens/fundos/<conceito>-<formato>.svg   fundo oficial (Branco e Preto, claro),
//                                                        com logo e mascotes, parado
//   plataformas/web/fundos.mjs                           o núcleo ligado a ./marca.mjs e ./mascotes.mjs
//   plataformas/web/fundos-temas.json                    cores dos fundos em cada tema e modo
// =============================================================================
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { criarMarca, geometria, pecas } from './marca.mjs';
import { medirNome } from './marca-arquivos.mjs';
import * as mascotes from './mascotes.mjs';
import { temasDosMascotes } from './mascotes-arquivos.mjs';
import { criarFundos, coresDosFundos, CONCEITOS, FORMATOS } from './fundos.mjs';

const AQUI = dirname(fileURLToPath(import.meta.url));

// núcleo dos fundos no Node (o tema.svg desenha com ele o fundo dos cards)
export const fundosNode = () => criarFundos({ marca: criarMarca(medirNome()), geometria, pecas, mascotes });

export function arquivosFundos(cores, marca) {
  const temas = coresDosFundos(cores, temasDosMascotes(cores, marca));
  const f = criarFundos({ marca: criarMarca(medirNome()), geometria, pecas, mascotes });
  const oficial = temas['branco-preto'].claro;
  const s = {};
  for (const id of Object.keys(CONCEITOS)) for (const [fmt, [largura, altura]] of Object.entries(FORMATOS)) {
    s[`identidade/imagens/fundos/${id}-${fmt}.svg`] = f.desenhar(id, { largura, altura, cores: oficial, semente: 1, movimentoReduzido: true }) + '\n';
  }
  s['plataformas/web/fundos.mjs'] = "import { marca, geometria, pecas } from './marca.mjs';\nimport * as mascotes from './mascotes.mjs';\n" +
    readFileSync(join(AQUI, 'fundos.mjs'), 'utf8') + '\nexport const fundos = criarFundos({ marca, geometria, pecas, mascotes });\n';
  s['plataformas/web/fundos-temas.json'] = JSON.stringify(temas, null, 2) + '\n';
  return s;
}
