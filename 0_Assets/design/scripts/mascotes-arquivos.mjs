// =============================================================================
// boaz · arquivos de exemplo dos mascotes (chamado por gerar.mjs)
//
// Devolve { <caminho relativo a 0_Assets/design>: conteúdo } com:
//   mascotes/estaticos/<expressao>.svg                parado, forma bolha (e rosto-<expressao>.svg, só o rosto)
//   mascotes/animados/<expressao>.svg                 animado, forma bolha
//   mascotes/so-rosto/<expressao>.svg                 só o rosto, com contorno de adesivo
//   mascotes/cartoes/{vertical,horizontal}/<expr>.svg cartão: fundo parado, rosto em movimento
//   mascotes/formas/<forma>-<expressao>.svg           uma vitrine por forma plana ou 3D
//   mascotes/glifos/<numero|letra|sinal>.svg          números, letras e sinais
//   mascotes/proporcoes/<proporcao>-<forma>.svg       alto, baixo, gordo e magro
//   mascotes/movimentos/<movimento>.svg               um exemplo por movimento
//   mascotes/sequencias/<nome>.svg                    sequências de movimentos
//   mascotes/temas.json                               cor do mascote em cada tema de cor
//   plataformas/web/mascotes.mjs                      o núcleo, para gerar qualquer combinação na tela
//   plataformas/web/mascotes-temas.json               a mesma tabela de cores por tema
//
// As combinações que não estão aqui saem do núcleo em tempo de execução
// (animado / estatico com { forma, proporcao, cor, movimento }).
// =============================================================================
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { EXPRESSOES, FORMAS, PROPORCOES, estatico, animado, coresDosTemas } from './mascotes.mjs';

const AQUI = dirname(fileURLToPath(import.meta.url));

/** Combinações de vitrine: cada forma, proporção e movimento com uma expressão que combina. */
export const VITRINE = {
  formas: { circulo: 'feliz', oval: 'pensando', quadrado: 'acertou', retangulo: 'surpreso', triangulo: 'duvida', losango: 'piscadinha', pentagono: 'estrela', hexagono: 'brincalhao', octogono: 'bravo', estrela: 'estrela', trapezio: 'sonolento', paralelogramo: 'ops', semicirculo: 'feliz', coracao: 'apaixonado', gota: 'chorando', trevo: 'piscadinha', nuvem: 'sonolento', meialua: 'esperando', heptagono: 'rindo', decagono: 'pensando', 'seta-direita': 'pressa', 'seta-esquerda': 'curioso', 'seta-cima': 'orgulhoso', 'seta-baixo': 'preocupado', cruz: 'acertou', cubo: 'orgulhoso', cilindro: 'surpreso', cone: 'curioso', piramide: 'concentrado', esfera: 'tonto' },
  proporcoes: { bolha: 'feliz', circulo: 'brincalhao', quadrado: 'piscadinha', triangulo: 'surpreso', retangulo: 'pensando', estrela: 'estrela' },
  sequencias: {
    vitoria: [['pular', 'pular', 'girar-direita', 'comemorar'], 'acertou', 'estrela'],
    danca: [['esquerda', 'direita', 'dancar', 'girar-esquerda'], 'brincalhao', 'circulo'],
    explorar: [['cima-esquerda', 'baixo-direita', 'frente', 'tras'], 'surpreso', 'quadrado'],
    susto: [['tremer', 'tras', 'frente', 'balancar'], 'ops', 'triangulo'],
  },
  movimentos: { cima: ['feliz', 'circulo'], baixo: ['surpreso', 'quadrado'], esquerda: ['brincalhao', 'hexagono'], direita: ['piscadinha', 'pentagono'], 'cima-esquerda': ['acertou', 'triangulo'], 'cima-direita': ['estrela', 'estrela'], 'baixo-esquerda': ['ops', 'oval'], 'baixo-direita': ['desconfiado', 'losango'], frente: ['surpreso', 'bolha'], tras: ['envergonhado', 'retangulo'], flutuar: ['feliz', 'circulo'], pular: ['acertou', 'quadrado'], balancar: ['envergonhado', 'oval'], dancar: ['brincalhao', 'hexagono'], tremer: ['ops', 'triangulo'], 'girar-esquerda': ['estrela', 'estrela'], 'girar-direita': ['feliz', 'hexagono'], comemorar: ['acertou', 'pentagono'] },
};

/** Cor do mascote por tema, para o gerador e para a tela. */
export const temasDosMascotes = (cores, marca) => Object.fromEntries(Object.entries(coresDosTemas(cores, marca)).map(([k, v]) => [k, { nome: v.nome, cor: v.cor }]));

export function arquivosMascotes(cores, marca) {
  const s = {};
  const ids = Object.keys(EXPRESSOES);
  const temas = JSON.stringify(temasDosMascotes(cores, marca), null, 2) + '\n';
  for (const id of ids) {
    s[`mascotes/estaticos/${id}.svg`] = estatico(id);
    s[`mascotes/estaticos/rosto-${id}.svg`] = estatico(id, { forma: 'rosto' });
    s[`mascotes/animados/${id}.svg`] = animado(id);
    s[`mascotes/so-rosto/${id}.svg`] = animado(id, { forma: 'rosto' });
    s[`mascotes/cartoes/vertical/${id}.svg`] = animado(id, { forma: 'cartao' });
    s[`mascotes/cartoes/horizontal/${id}.svg`] = animado(id, { forma: 'cartaoHorizontal' });
  }
  for (const [fm, id] of Object.entries(VITRINE.formas)) s[`mascotes/formas/${fm}-${id}.svg`] = animado(id, { forma: fm });
  const cores8 = ['amarelo', 'verde', 'laranja', 'rosa', 'roxo', 'azul', 'ciano', 'vermelho'];
  const humor = ['feliz', 'piscadinha', 'orgulhoso', 'curioso', 'rindo', 'surpreso', 'acertou', 'pensando'];
  Object.keys(FORMAS).filter((k) => FORMAS[k].tipo === 'glifo').forEach((k, i) => { s[`mascotes/glifos/${k}.svg`] = animado(humor[i % humor.length], { forma: k, cor: cores8[i % cores8.length] }); });
  for (const pr of Object.keys(PROPORCOES).filter((p) => p !== 'normal')) for (const [fm, id] of Object.entries(VITRINE.proporcoes)) s[`mascotes/proporcoes/${pr}-${fm}.svg`] = animado(id, { forma: fm, proporcao: pr });
  for (const [m, [id, fm]] of Object.entries(VITRINE.movimentos)) s[`mascotes/movimentos/${m}.svg`] = animado(id, { forma: fm, movimento: m });
  for (const [nome, [lista, id, fm]] of Object.entries(VITRINE.sequencias)) s[`mascotes/sequencias/${nome}.svg`] = animado(id, { forma: fm, movimento: lista });
  s['mascotes/temas.json'] = temas;
  s['plataformas/web/mascotes.mjs'] = readFileSync(join(AQUI, 'mascotes.mjs'), 'utf8');
  s['plataformas/web/mascotes-temas.json'] = temas;
  return s;
}
