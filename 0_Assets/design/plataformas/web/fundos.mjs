import { marca, geometria, pecas } from './marca.mjs';
import * as mascotes from './mascotes.mjs';
// =============================================================================
// boaz · fundos da marca deboaz: seis conceitos feitos de peças do símbolo e de
// mascotes, em qualquer formato (16:9, 9:16, 1:1 ou outro)
//
//   estampa    símbolo e mascotes em grade, bem leve        páginas, login, verso do cartão do aluno
//   anel       anel gigante cortado pela borda               cabeçalho do hub, capa de sala, banner
//   antenas    antenas em ritmo na borda de baixo            rodapé, faixa de progresso, espera
//   ondas      linhas concêntricas saindo de um anel         formulários e configurações do hub
//   recorte    símbolo enorme saindo pela borda              abertura do app, boas-vindas
//   halo       brilhos suaves com as peças no centro         abertura, carregamento, fim de partida
//
// Em todos: peças (logo, mascotes ou os dois), mascote montado (expressão, forma,
// proporção, movimento) ou sorteado (estampa e antenas, campo vazio = sortido),
// animação do logo ('' | 'abertura' | 'carregamento' | 'fechamento' | 'ciclo') e semente do sorteio.
// Com movimentoReduzido, nada se move.
//
// Este módulo não usa nada do Node nem importa nada: recebe o núcleo da marca e o
// dos mascotes em criarFundos. O gerador (scripts/fundos-arquivos.mjs) grava os
// fundos oficiais em identidade/imagens/fundos/ e entrega plataformas/web/fundos.mjs
// já ligado a ./marca.mjs e ./mascotes.mjs.
//
// Uso na tela (web):
//   import { fundos } from '../design/fundos.mjs';
//   import temas from '../design/fundos-temas.json';
//   el.innerHTML = fundos.desenhar('halo', { largura: 1600, altura: 900, cores: temas[paleta][modo],
//     mascote: { expressao: 'feliz', forma: 'bolha', movimento: 'flutuar' }, logo: 'abertura' });
// Regras de uso: 1_Requisitos/90_identidade-visual.html.
// =============================================================================

/** Os conceitos, na ordem do catálogo. */
export const CONCEITOS = {
  estampa: { nome: 'Estampa', uso: 'páginas, tela de login, verso do cartão do aluno', sortido: true, mascote: { expressao: '', forma: '' } },
  anel: { nome: 'Anel gigante', uso: 'cabeçalho do hub, capa de sala, banner da landing page', mascote: { expressao: 'curioso', forma: 'circulo' } },
  antenas: { nome: 'Antenas em ritmo', uso: 'rodapé de telas, faixa de progresso, tela de espera', sortido: true, quantidade: true, mascote: { expressao: '', forma: '' } },
  ondas: { nome: 'Ondas do vão', uso: 'formulários e telas de configuração do hub', mascote: { expressao: 'pensando', forma: 'circulo' } },
  recorte: { nome: 'Símbolo recortado', uso: 'abertura do app, tela de boas-vindas, apresentações', mascote: { expressao: 'tchau', forma: 'rosto' } },
  halo: { nome: 'Halo', uso: 'abertura, tela de carregamento, fim de partida', mascote: { expressao: 'feliz', forma: 'bolha', movimento: 'flutuar' }, logo: 'abertura' },
};
/** Formatos de referência (o fundo serve em qualquer proporção). */
export const FORMATOS = { horizontal: [1600, 900], vertical: [900, 1600], quadrado: [1000, 1000] };
/** Papéis de cor que os fundos usam (de tokens/cores.json), mais a cor do mascote. */
export const PAPEIS = ['surface', 'surface-container', 'surface-container-high', 'on-surface', 'on-surface-variant', 'outline-variant',
  'primary', 'secondary', 'tertiary', 'primary-container', 'secondary-container', 'tertiary-container'];

const ALEGRES = ['feliz', 'piscadinha', 'acertou', 'rindo', 'curioso', 'estrela', 'orgulhoso', 'surpreso', 'brincalhao', 'apaixonado', 'tchau', 'aliviado'];
const FZ = 'fill="freeze" calcMode="spline" keyTimes="0;1" keySplines="0.3 0 0.2 1"';
const n = (v) => Math.round(v * 100) / 100;
const rnd = (seed) => { let x = (Math.abs(Math.floor(seed)) % 2147483646) + 1; return () => (x = (x * 16807) % 2147483647) / 2147483647; };

/**
 * marca = criarMarca(NOME) do núcleo da marca (com geometria e pecas);
 * mascotes = { estatico, animado, embutir, FORMAS } do núcleo dos mascotes.
 */
export function criarFundos({ marca, geometria, pecas, mascotes }) {
  const { estatico, animado, embutir, FORMAS } = mascotes;
  const SORTEIO = Object.keys(FORMAS).filter((k) => FORMAS[k].tipo !== 'glifo' && k !== 'rosto' && !k.startsWith('cartao'));
  const GEO = { longo: geometria('longo'), curto: geometria('curto') };
  let seq = 0;

  // laço de abrir e fechar das peças soltas: [tempo, valor] em 4,6 s
  const CICLO = 4.6;
  const laco = (attr, a, b) => `<animate attributeName="${attr}" values="${a};${b};${b};${a};${a}" keyTimes="0;0.2;0.62;0.82;1" calcMode="spline" keySplines="0.3 0 0.2 1;0 0 1 1;0.3 0 0.2 1;0 0 1 1" dur="${CICLO}s" repeatCount="indefinite"/>`;
  const encaixa = (svg, x, y, w, h, op) => `<g opacity="${op}">${svg.replace(/^<svg[^>]*viewBox="([^"]+)"[^>]*>/, `<svg x="${n(x)}" y="${n(y)}" width="${n(w)}" height="${n(h)}" viewBox="$1">`)}</g>`;
  const simb = (x, y, alt, cor, v, op, anim) => {
    const g = GEO[v], p = pecas(cor), larg = alt * g.largura / g.altura;
    if (anim === 'abertura') return encaixa(marca.abertura(v, p, { nome: false }), x, y, larg, alt, op);
    if (anim === 'carregamento') return encaixa(marca.carregamento(v, p), x, y, larg, alt, op);
    if (anim === 'fechamento') return encaixa(marca.fechamento(v, p, { nome: false }), x, y, larg, alt, op);
    if (anim === 'ciclo') return encaixa(marca.ciclo(v, p, { nome: false }), x, y, larg, alt, op);
    const k = alt / g.altura;
    return `<g transform="translate(${n(x)} ${n(y)}) scale(${n(k * 1e5) / 1e5}) translate(0 ${-g.y})" opacity="${op}"><path fill="${p.anel}" d="${g.anel}"/><path fill="${p.b}" d="${g.b}"/><path fill="${p.d}" d="${g.d}"/></g>`;
  };
  const anel = (cx, cy, R, cor, op, anim) => {
    const esp = R * 116 / 270, rm = R - esp / 2, volta = 2 * Math.PI * rm;
    const base = `cx="${n(cx)}" cy="${n(cy)}" r="${n(rm)}" fill="none" stroke="${cor}" stroke-width="${n(esp)}"`;
    if (anim === 'abertura') return `<circle ${base} opacity="${op}" stroke-dasharray="${n(volta)}" stroke-dashoffset="${n(volta)}" transform="rotate(90 ${n(cx)} ${n(cy)})"><animate attributeName="stroke-dashoffset" from="${n(volta)}" to="0" dur="0.9s" ${FZ}/></circle>`;
    if (anim === 'fechamento') return `<circle ${base} opacity="${op}" stroke-dasharray="${n(volta)}" stroke-dashoffset="0" transform="rotate(90 ${n(cx)} ${n(cy)})"><animate attributeName="stroke-dashoffset" from="0" to="${n(-volta)}" begin="0.5s" dur="0.9s" ${FZ}/></circle>`;
    if (anim === 'ciclo') return `<circle ${base} opacity="${op}" stroke-dasharray="${n(volta)}" stroke-dashoffset="${n(volta)}" transform="rotate(90 ${n(cx)} ${n(cy)})"><animate attributeName="stroke-dashoffset" values="${n(volta)};0;0;${n(-volta)};${n(-volta)}" keyTimes="0;0.2;0.62;0.82;1" calcMode="spline" keySplines="0.3 0 0.2 1;0 0 1 1;0.3 0 0.2 1;0 0 1 1" dur="${CICLO}s" repeatCount="indefinite"/></circle>`;
    if (anim === 'carregamento') return `<circle ${base} opacity="${n(op * 0.25)}"/><circle ${base} opacity="${op}" stroke-dasharray="${n(volta * 0.3)} ${n(volta * 0.7)}"><animateTransform attributeName="transform" type="rotate" from="90 ${n(cx)} ${n(cy)}" to="450 ${n(cx)} ${n(cy)}" dur="1.6s" repeatCount="indefinite"/></circle>`;
    return `<circle ${base} opacity="${op}"/>`;
  };
  const antena = (x, y, w, h, cor, op, anim, atraso) => {
    const ry = w * 40 / 116, d = `M${n(x)} ${n(y)}V${n(y - h + ry)}A${n(w / 2)} ${n(ry)} 0 0 1 ${n(x + w)} ${n(y - h + ry)}V${n(y)}Z`;
    if (anim === 'abertura') { const id = `fundo-a${++seq}`;
      return `<defs><clipPath id="${id}"><rect x="${n(x - 1)}" y="${n(y)}" width="${n(w + 2)}" height="${n(h + 2)}"><animate attributeName="y" from="${n(y)}" to="${n(y - h - 1)}" begin="${n(atraso)}s" dur="0.6s" ${FZ}/></rect></clipPath></defs><path fill="${cor}" opacity="${op}" d="${d}" clip-path="url(#${id})"/>`; }
    if (anim === 'fechamento' || anim === 'ciclo') { const id = `fundo-a${++seq}`, topo = n(y - h - 1), anima = anim === 'ciclo' ? laco('y', n(y), topo) : `<animate attributeName="y" from="${topo}" to="${n(y)}" begin="${n(atraso)}s" dur="0.5s" ${FZ}/>`;
      return `<defs><clipPath id="${id}"><rect x="${n(x - 1)}" y="${anim === 'ciclo' ? n(y) : topo}" width="${n(w + 2)}" height="${n(h + 2)}">${anima}</rect></clipPath></defs><path fill="${cor}" opacity="${op}" d="${d}" clip-path="url(#${id})"/>`; }
    if (anim === 'carregamento') return `<g><animateTransform attributeName="transform" type="translate" values="0 0;0 ${n(h * 0.35)};0 0" dur="1.6s" begin="${n(-atraso)}s" repeatCount="indefinite" calcMode="spline" keyTimes="0;.5;1" keySplines="0.4 0 0.2 1;0.4 0 0.2 1"/><path fill="${cor}" opacity="${op}" d="${d}"/></g>`;
    return `<path fill="${cor}" opacity="${op}" d="${d}"/>`;
  };
  const base = (w, h, cor) => `<rect width="${w}" height="${h}" fill="${cor}"/>`;

  /**
   * Desenha um fundo e devolve o SVG.
   * op = { largura, altura, cores (PAPEIS + mascote), pecas: 'ambos'|'logo'|'mascotes',
   *        mascote: { expressao, forma, proporcao, movimento }, logo: ''|'abertura'|'carregamento',
   *        quantidade (antenas), semente, movimentoReduzido }
   */
  function desenhar(conceito, op = {}) {
    const def = CONCEITOS[conceito];
    const { largura: w = 1600, altura: h = 900, cores: c, pecas: P = 'ambos', quantidade = 3, semente = 1, movimentoReduzido = false } = op;
    const ms = { proporcao: 'normal', movimento: '', ...def.mascote, ...(op.mascote || {}) };
    const a = movimentoReduzido ? '' : (op.logo ?? def.logo ?? '');
    const r = rnd(semente), b = Math.min(w, h);
    const montar = (formaPadrao) => ({
      expressao: ms.expressao || ALEGRES[Math.floor(r() * ALEGRES.length)],
      forma: ms.forma || formaPadrao || SORTEIO[Math.floor(r() * SORTEIO.length)],
      proporcao: ms.proporcao, movimento: movimentoReduzido ? '' : ms.movimento });
    const masc = (m, cx, cy, t, opac = 1) => {
      const o = { forma: m.forma, proporcao: m.proporcao, cor: c.mascote || undefined };
      const svg = m.movimento ? animado(m.expressao, { ...o, movimento: m.movimento }) : estatico(m.expressao, o);
      return `<g opacity="${opac}">${embutir(svg, `fundo-m${++seq}-`, cx - t / 2, cy - t / 2, t, t)}</g>`;
    };
    const tres = { anel: c.tertiary, b: c.primary, d: c.secondary };
    let s = '';
    if (conceito === 'estampa') {
      const t = b / 7, sx = t * 1.25, sy = t * 1.5; s = base(w, h, c.surface); let i = 0;
      for (let y = -sy * 0.4, lin = 0; y < h + sy; y += sy, lin++) for (let x = (lin % 2 ? sx / 2 : 0) - sx * 0.2; x < w + sx; x += sx, i++) {
        const logo = P === 'logo' || (P === 'ambos' && (i + lin) % 2 === 0);
        s += logo ? simb(x - t * 0.34, y, t, c['on-surface'], 'longo', 0.09, a) : masc(montar(), x, y + t / 2, t * 0.8, 0.32); }
    } else if (conceito === 'anel') {
      const R = Math.max(w, h) * 0.55, cx = w * 0.92, cy = h * 0.98;
      if (P === 'mascotes') s = base(w, h, c['surface-container']) + masc(montar(), cx - R * 0.1, cy - R * 0.1, R * 1.6);
      else s = base(w, h, c['primary-container']) + anel(cx, cy, R, c.primary, 0.9, a) + anel(w * 0.12, h * 0.2, b * 0.07, c.secondary, 0.9, a) +
        (P === 'ambos' ? masc(montar(), cx - R * 0.38, cy - R * 0.38, R * 0.62) : anel(cx, cy, R * 0.42, c.tertiary, 0.7, a));
    } else if (conceito === 'antenas') {
      const k = Math.max(8, Math.round(w / (b / 9))), g = w / k, wd = g * 0.62; s = base(w, h, c.surface);
      const qtd = Math.max(0, Math.min(quantidade, 8, k)), vagas = [...Array(k).keys()].sort(() => r() - 0.5).slice(0, qtd);
      if (P === 'mascotes') { const gg = w / Math.max(qtd, 1); for (let i = 0; i < qtd; i++) { const t = Math.min(gg * (0.75 + r() * 0.3), h * 0.5); s += masc(montar(), i * gg + gg / 2, h - t * 0.45, t); } }
      else { const alt = []; for (let i = 0; i < k; i++) { alt[i] = h * (0.18 + r() * 0.42); s += antena(i * g + (g - wd) / 2, h + 2, wd, alt[i], i % 2 ? c.secondary : c.primary, 0.85, a, i * 0.07); }
        if (P === 'ambos') vagas.forEach((i) => { s += masc(montar('rosto'), i * g + g / 2, h - alt[i] - g * 0.55, g * 1.1); });
        s += anel(w * 0.82, h * 0.22, b * 0.09, c.tertiary, 0.9, a); }
    } else if (conceito === 'ondas') {
      const cx = w * 0.1, cy = h * 0.12; s = base(w, h, c.surface);
      for (let i = 1; i < 26; i++) s += `<circle cx="${n(cx)}" cy="${n(cy)}" r="${n(b * 0.16 + i * b * 0.055)}" fill="none" stroke="${c['outline-variant']}" stroke-width="${n(b * 0.006)}"/>`;
      if (P === 'logo') s += anel(cx, cy, b * 0.16, c.primary, 1, a);
      else if (P === 'ambos') s += anel(cx, cy, b * 0.16, c.primary, 1, a) + masc(montar(), w * 0.86, h * 0.82, b * 0.26);
      else s += masc(montar(), cx, cy, b * 0.4);
    } else if (conceito === 'recorte') {
      const alt = Math.max(w, h) * 0.95, x = w - alt * 0.52, y = h - alt * 0.78; s = base(w, h, c['surface-container']);
      if (P === 'mascotes') s += masc(montar(), w * 0.78, h * 0.78, alt * 0.95);
      else s += simb(x, y, alt, { anel: c['tertiary-container'], b: c['primary-container'], d: c['secondary-container'] }, 'longo', 1, a) +
        (P === 'ambos' ? masc(montar(), x + alt * 0.34, y + alt * 0.66, alt * 0.26) : '');
    } else if (conceito === 'halo') {
      const id = `fundo-h${++seq}`;
      const gr = (k, cor, x, y, raio) => `<radialGradient id="${id}-${k}" cx="${n(x)}" cy="${n(y)}" r="${n(raio)}" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="${cor}" stop-opacity=".55"/><stop offset="1" stop-color="${cor}" stop-opacity="0"/></radialGradient>`;
      s = `<defs>${gr(1, c.primary, w * 0.2, h * 0.25, b * 0.9)}${gr(2, c.secondary, w * 0.85, h * 0.8, b * 0.9)}${gr(3, c.tertiary, w * 0.7, h * 0.15, b * 0.6)}</defs>` + base(w, h, c.surface) +
        [1, 2, 3].map((k) => `<rect width="${w}" height="${h}" fill="url(#${id}-${k})"/>`).join('');
      if (P === 'logo') s += simb(w / 2 - b * 0.09, h / 2 - b * 0.13, b * 0.26, tres, 'longo', 1, a);
      else if (P === 'mascotes') s += masc(montar(), w / 2, h / 2, b * 0.38);
      else s += simb(w / 2 - b * 0.2, h / 2 - b * 0.13, b * 0.26, tres, 'longo', 1, a) + masc(montar(), w / 2 + b * 0.14, h / 2 + b * 0.02, b * 0.26);
    } else throw new Error(`fundo desconhecido: ${conceito}`);
    return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${w} ${h}" width="${w}" height="${h}" role="img" aria-label="${def.nome}">${s}</svg>`;
  }
  return { desenhar };
}

/** Cores dos fundos por tema e modo: os PAPEIS de tokens/cores.json + mascote (cor do mascote no tema, ou vazio). */
export function coresDosFundos(cores, temasMascotes) {
  const saida = {};
  for (const [id, t] of Object.entries(cores.temas)) {
    const modo = (esq) => ({ ...Object.fromEntries(PAPEIS.map((p) => [p, esq[p].$value.toUpperCase()])), mascote: temasMascotes[id]?.cor ?? '' });
    saida[id] = { nome: temasMascotes[id]?.nome ?? id, claro: modo(t.md.sys.color.light), escuro: modo(t.md.sys.color.dark) };
  }
  return saida;
}

export const fundos = criarFundos({ marca, geometria, pecas, mascotes });
