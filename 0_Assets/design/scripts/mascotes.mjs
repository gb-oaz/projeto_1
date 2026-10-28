// =============================================================================
// boaz · mascotes: personagens com rosto expressivo da identidade visual
//
// Eixos que se combinam livremente:
//   expressão  EXPRESSOES  o rosto (olhos, sobrancelhas, boca e enfeites)
//   forma      FORMAS      o corpo (bolha, círculo, triângulo, … ou cartão vertical/horizontal)
//   proporção  PROPORCOES  o corpo alto, baixo, gordo ou magro (o rosto se ajusta)
//   cor        CORES, #hex ou a cor do tema ativo (coresDosTemas)
//   movimento  MOVIMENTOS  o corpo inteiro (direções, frente/trás, pular, dançar…)
//
// Tudo que se move é transform/opacity de um grupo: a mesma função gera o SVG
// parado (estatico) ou animado em laço com SMIL (animado). Este módulo não usa
// nada do Node, então também roda no navegador. Os arquivos saem por
// scripts/mascotes-arquivos.mjs (chamado por gerar.mjs) e o núcleo é copiado para
// plataformas/web/mascotes.mjs, de onde vai para as aplicações web.
//
// Uso na tela (web):
//   import { animado, estatico } from '../design/mascotes.mjs';
//   import temas from '../design/mascotes-temas.json';
//   const svg = animado('acertou', { forma: 'quadrado', cor: temas[paleta].cor ?? undefined });
//   img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
// Com movimento reduzido, use estatico(...) no lugar de animado(...).
// Regras de uso: 1_Requisitos/06_design-system-telas.html e o catálogo design-system.html#mascotes.
// =============================================================================
const W = 256;

// ── cor ──────────────────────────────────────────────────────────────────────
const rgb = (h) => [1, 3, 5].map((i) => parseInt(h.slice(i, i + 2), 16));
const hex = (a) => '#' + a.map((v) => Math.round(Math.max(0, Math.min(255, v))).toString(16).padStart(2, '0')).join('').toUpperCase();
const mix = (a, b, p) => { const x = rgb(a), y = rgb(b); return hex(x.map((v, i) => v + (y[i] - v) * p)); };

export const CORES = {
  amarelo: '#FFC51A',
  verde: '#8BD534',
  laranja: '#F7892A',
  vermelho: '#FF5A26',
  rosa: '#FF6FA8',
  roxo: '#9B7BF7',
  azul: '#4AA8F0',
  ciano: '#27D3E0',
};
/**
 * Cor dos rostos em cada tema de cor da identidade, a partir de tokens/cores.json e
 * identidade/marca.json (recebidos já lidos, para rodar também no navegador).
 * Tema colorido: a semente (ou o tom 60 da paleta primária, se ela for escura demais para as sobrancelhas e os traços aparecerem).
 * Tema monocromático (branco, preto, padrão branco-preto): null — cada expressão
 * mantém a própria cor, porque nesses temas a cor vem do conteúdo.
 */
export function coresDosTemas(cores, marca) {
  const lum = (h) => { const [r, g, b] = rgb(h).map((v) => { v /= 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; }); return 0.2126 * r + 0.7152 * g + 0.0722 * b; };
  const defs = marca.cores.temas;
  const padrao = marca.cores.tema_padrao;
  const saida = {};
  for (const [id, t] of Object.entries(cores.temas)) {
    const ids = typeof padrao === 'object' && id === `${padrao.claro}-${padrao.escuro}` ? [padrao.claro, padrao.escuro] : [id];
    const def = defs[ids[0]];
    const mono = ids.some((i) => defs[i]?.variante === 'monochrome');
    const ref = t.md.ref.palette.primary;
    let cor = null;
    if (!mono && def) cor = lum(def.semente) < 0.18 ? ref['60'].$value : def.semente.toUpperCase();
    const luz = t.md.sys.color.light, escuro = t.md.sys.color.dark;
    saida[id] = { nome: def && ids.length === 1 ? def.nome : ids.map((i) => defs[i]?.nome).join(' / '), cor, fundo: luz['surface-container-low'].$value, fundoEscuro: escuro['surface-container-low'].$value, texto: luz['on-surface'].$value };
  }
  return saida;
}

const TINTA = '#3A2A22';      // sobrancelhas e traços
const BOCA = '#4A1616';       // interior da boca
const LINGUA = ['#FF8FAB', '#E5547A'];

// ── movimento ────────────────────────────────────────────────────────────────
// Um "movimento" é uma função t∈[0,1) → { tx, ty, r, sx, sy, o }. O contexto
// decide se ela vira transform fixo (quadro t) ou animateTransform em laço.
let CTX = { modo: 'quadro', t: 0, n: 48, dur: 2 };
let seq = 0;
const uid = (p) => `${p}${++seq}`;
const f = (v) => +v.toFixed(2);

const onda = (t, ciclos = 1, fase = 0) => Math.sin(2 * Math.PI * (t * ciclos + fase));
/** 0→1→0 dentro da janela [a,b] (suave). */
const pulso = (t, a, b) => (t <= a || t >= b ? 0 : Math.sin(Math.PI * (t - a) / (b - a)));
/** 0→1 entre a e b, depois mantém 1. */
const rampa = (t, a, b) => (t <= a ? 0 : t >= b ? 1 : 0.5 - 0.5 * Math.cos(Math.PI * (t - a) / (b - a)));
const piscar = (t, a = 0.82, d = 0.08) => pulso(t, a, a + d);

const PADRAO = { tx: 0, ty: 0, r: 0, sx: 1, sy: 1, o: 1 };

/**
 * Grupo com movimento `mov` em torno do pivô [px, py]. Numa sequência de movimentos
 * o laço dura CTX.rep ciclos da expressão: o que é da expressão (piscar, lágrima…)
 * repete a cada ciclo; o que é do corpo (`bruto`) usa o tempo do laço inteiro.
 */
function P(conteudo, mov, [px, py] = [W / 2, W / 2], bruto = false) {
  if (!mov) return conteudo;
  const rep = bruto ? 1 : CTX.rep || 1;
  const em = (t) => ({ ...PADRAO, ...mov(rep === 1 ? t : (t * rep) % 1) });
  if (CTX.modo === 'quadro') {
    const m = em(CTX.t);
    const tr = [];
    if (m.tx || m.ty) tr.push(`translate(${f(m.tx)} ${f(m.ty)})`);
    if (m.r) tr.push(`rotate(${f(m.r)} ${px} ${py})`);
    if (m.sx !== 1 || m.sy !== 1) tr.push(`translate(${px} ${py}) scale(${f(m.sx)} ${f(m.sy)}) translate(${-px} ${-py})`);
    const op = m.o !== 1 ? ` opacity="${f(Math.max(0, m.o))}"` : '';
    return `<g${tr.length ? ` transform="${tr.join(' ')}"` : ''}${op}>${conteudo}</g>`;
  }
  // modo animado: amostra o movimento em n+1 pontos e gera um laço SMIL
  const pts = Array.from({ length: CTX.n + 1 }, (_, k) => em(k / CTX.n));
  const fixo = (fn) => pts.every((p) => fn(p) === fn(pts[0]));
  const anim = (tipo, fn) => `<animateTransform attributeName="transform" type="${tipo}" dur="${CTX.dur}s" repeatCount="indefinite" values="${pts.map(fn).join(';')}"/>`;
  let g = conteudo;
  if (!fixo((p) => `${f(p.sx)} ${f(p.sy)}`)) {
    g = `<g transform="translate(${px} ${py})"><g>${anim('scale', (p) => `${f(p.sx)} ${f(p.sy)}`)}<g transform="translate(${-px} ${-py})">${g}</g></g></g>`;
  }
  if (!fixo((p) => f(p.r))) g = `<g>${anim('rotate', (p) => `${f(p.r)} ${px} ${py}`)}${g}</g>`;
  if (!fixo((p) => `${f(p.tx)} ${f(p.ty)}`)) g = `<g>${anim('translate', (p) => `${f(p.tx)} ${f(p.ty)}`)}${g}</g>`;
  if (!fixo((p) => f(p.o))) g = `<g><animate attributeName="opacity" dur="${CTX.dur}s" repeatCount="indefinite" values="${pts.map((p) => f(Math.max(0, p.o))).join(';')}"/>${g}</g>`;
  return g;
}

// ── peças ────────────────────────────────────────────────────────────────────
function defsBase(c) {
  return `<defs>
<radialGradient id="g-corpo" gradientUnits="userSpaceOnUse" cx="${f(CTX.forma.caixa.x + 0.3 * CTX.forma.caixa.w)}" cy="${f(CTX.forma.caixa.y + 0.22 * CTX.forma.caixa.h)}" r="${f(1.05 * Math.max(CTX.forma.caixa.w, CTX.forma.caixa.h))}">
<stop offset="0" stop-color="${mix(c, '#FFFFFF', 0.38)}"/><stop offset="0.45" stop-color="${c}"/><stop offset="1" stop-color="${mix(c, '#000000', 0.22)}"/></radialGradient>
${CTX.forma.rostoT ? `<radialGradient id="g-corpo-rosto" gradientTransform="${rostoTransform(CTX.forma, true)}" gradientUnits="userSpaceOnUse" cx="${f(CTX.forma.caixa.x + 0.3 * CTX.forma.caixa.w)}" cy="${f(CTX.forma.caixa.y + 0.22 * CTX.forma.caixa.h)}" r="${f(1.05 * Math.max(CTX.forma.caixa.w, CTX.forma.caixa.h))}">
<stop offset="0" stop-color="${mix(c, '#FFFFFF', 0.38)}"/><stop offset="0.45" stop-color="${c}"/><stop offset="1" stop-color="${mix(c, '#000000', 0.22)}"/></radialGradient>` : ''}
<radialGradient id="g-olho" cx="0.4" cy="0.35" r="0.75"><stop offset="0.55" stop-color="#FFFFFF"/><stop offset="1" stop-color="#D9DEE6"/></radialGradient>
<radialGradient id="g-iris" cx="0.45" cy="0.6" r="0.6"><stop offset="0" stop-color="${mix(c, '#FFFFFF', 0.25)}"/><stop offset="0.7" stop-color="${mix(c, '#000000', 0.25)}"/><stop offset="1" stop-color="${mix(c, '#000000', 0.55)}"/></radialGradient>
<linearGradient id="g-lingua" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${LINGUA[0]}"/><stop offset="1" stop-color="${LINGUA[1]}"/></linearGradient>
<linearGradient id="g-gota" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#E8F7FF"/><stop offset="1" stop-color="#7CC8F2"/></linearGradient>
<linearGradient id="g-estrela" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFF176"/><stop offset="1" stop-color="#FFB300"/></linearGradient>
<radialGradient id="g-esfera" cx="0.36" cy="0.3" r="0.78"><stop offset="0.5" stop-color="#000000" stop-opacity="0"/><stop offset="1" stop-color="#000000" stop-opacity="0.38"/></radialGradient>
<filter id="f-adesivo" x="-20%" y="-20%" width="140%" height="140%"><feMorphology in="SourceAlpha" operator="dilate" radius="3.5" result="d"/><feFlood flood-color="#FFFFFF" flood-opacity="0.92"/><feComposite in2="d" operator="in" result="borda"/><feMerge><feMergeNode in="borda"/><feMergeNode in="SourceGraphic"/></feMerge></filter>
<filter id="f-suave" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="4"/></filter>
<filter id="f-sombra" x="-30%" y="-30%" width="160%" height="160%"><feGaussianBlur stdDeviation="2.5"/></filter>
</defs>`;
}

// ── formas do corpo ──────────────────────────────────────────────────────────
// O rosto fica sempre no mesmo lugar (caixa de 256); cada forma é escalada e
// deslocada até conter os pontos do rosto com folga, e o viewBox a acompanha.
const PONTOS_ROSTO = [[62, 50], [194, 50], [56, 98], [200, 98], [60, 126], [196, 126], [92, 146], [164, 146],
  [80, 160], [176, 160], [98, 202], [158, 202], [128, 212], [128, 44]];
const FOLGA = 14;

/** Meia-lua: arco externo fora do círculo interno + arco interno de volta. */
function crescente(ri = 0.8, cx = 0.72, cy = -0.12) {
  const fora = Array.from({ length: 120 }, (_, i) => { const a = (2 * Math.PI * i) / 120; return [Math.cos(a), Math.sin(a)]; }).filter(([x, y]) => Math.hypot(x - cx, y - cy) > ri);
  // começa logo depois da abertura (onde o arco interno entra)
  const k = fora.findIndex(([x, y], i) => { const [px, py] = fora[(i + fora.length - 1) % fora.length]; return Math.hypot(x - px, y - py) > 0.2; });
  const ext = [...fora.slice(k), ...fora.slice(0, k)];
  const dentro = Array.from({ length: 120 }, (_, i) => { const a = (2 * Math.PI * i) / 120; return [cx + ri * Math.cos(a), cy + ri * Math.sin(a)]; }).filter(([x, y]) => Math.hypot(x, y) < 1);
  const j = dentro.findIndex(([x, y], i) => { const [px, py] = dentro[(i + dentro.length - 1) % dentro.length]; return Math.hypot(x - px, y - py) > 0.2; });
  const int = [...dentro.slice(j), ...dentro.slice(0, j)];
  // o arco interno volta no sentido contrário, ligando as pontas
  const d1 = Math.hypot(ext.at(-1)[0] - int[0][0], ext.at(-1)[1] - int[0][1]), d2 = Math.hypot(ext.at(-1)[0] - int.at(-1)[0], ext.at(-1)[1] - int.at(-1)[1]);
  return [...ext, ...(d1 < d2 ? int : int.reverse())];
}
const SETA = [[-1.1, -0.42], [0.15, -0.42], [0.15, -0.95], [1.15, 0], [0.15, 0.95], [0.15, 0.42], [-1.1, 0.42]]; // apontando para a direita
const regular = (n, raio = 1, giro = -Math.PI / 2) => Array.from({ length: n }, (_, i) => [raio * Math.cos(giro + (2 * Math.PI * i) / n), raio * Math.sin(giro + (2 * Math.PI * i) / n)]);
const elipse = (rx, ry) => Array.from({ length: 48 }, (_, i) => [rx * Math.cos((2 * Math.PI * i) / 48), ry * Math.sin((2 * Math.PI * i) / 48)]);

export const FORMAS = {
  bolha: { nome: 'Bolha', tipo: 'fixa', rect: [20, 20, 216, 216, 92] },
  circulo: { nome: 'Círculo', tipo: 'circulo', unid: elipse(1, 1) },
  oval: { nome: 'Oval', tipo: 'elipse', unid: elipse(0.84, 1.04) },
  quadrado: { nome: 'Quadrado', unid: [[-1, -1], [1, -1], [1, 1], [-1, 1]], canto: 0.3 },
  retangulo: { nome: 'Retângulo', unid: [[-0.78, -1.12], [0.78, -1.12], [0.78, 1.12], [-0.78, 1.12]], canto: 0.26 },
  triangulo: { nome: 'Triângulo', unid: regular(3), canto: 0.3 },
  losango: { nome: 'Losango', unid: [[0, -1.3], [1, 0], [0, 1.3], [-1, 0]], canto: 0.24 },
  pentagono: { nome: 'Pentágono', unid: regular(5), canto: 0.22 },
  hexagono: { nome: 'Hexágono', unid: regular(6, 1, 0), canto: 0.2 },
  octogono: { nome: 'Octógono', unid: regular(8, 1, -Math.PI / 2 + Math.PI / 8), canto: 0.14 },
  estrela: { nome: 'Estrela', unid: Array.from({ length: 10 }, (_, i) => { const a = -Math.PI / 2 + (i * Math.PI) / 5, r = i % 2 ? 0.6 : 1; return [r * Math.cos(a), r * Math.sin(a)]; }), canto: 0.1 },
  trapezio: { nome: 'Trapézio', unid: [[-0.62, -0.86], [0.62, -0.86], [1.12, 0.86], [-1.12, 0.86]], canto: 0.2 },
  paralelogramo: { nome: 'Paralelogramo', unid: [[-0.58, -0.92], [1.16, -0.92], [0.58, 0.92], [-1.16, 0.92]], canto: 0.2 },
  // lote 2: mais formas planas
  semicirculo: { nome: 'Semicírculo', unid: Array.from({ length: 25 }, (_, i) => { const a = Math.PI + (Math.PI * i) / 24; return [1.1 * Math.cos(a), 1.1 * Math.sin(a) + 0.5]; }), canto: 0.1 },
  coracao: { nome: 'Coração', unid: Array.from({ length: 72 }, (_, i) => { const t = (2 * Math.PI * i) / 72; return [Math.sin(t) ** 3, -(13 * Math.cos(t) - 5 * Math.cos(2 * t) - 2 * Math.cos(3 * t) - Math.cos(4 * t)) / 16]; }), canto: 0.02 },
  gota: { nome: 'Gota', unid: [...Array.from({ length: 40 }, (_, i) => { const a = -Math.PI / 2 + 0.62 + ((2 * Math.PI - 1.24) * i) / 39; return [Math.cos(a), 0.25 + Math.sin(a)]; }), [0, -1.55]], canto: 0.06 },
  trevo: { nome: 'Trevo', unid: Array.from({ length: 96 }, (_, i) => { const a = (2 * Math.PI * i) / 96, r = 1 + 0.24 * Math.cos(4 * a); return [r * Math.cos(a), r * Math.sin(a)]; }), canto: 0.02 },
  nuvem: { nome: 'Nuvem', unid: Array.from({ length: 112 }, (_, i) => { const a = (2 * Math.PI * i) / 112, r = 1 + 0.12 * Math.abs(Math.cos(3.5 * a)); return [1.35 * r * Math.cos(a), 0.92 * r * Math.sin(a)]; }), canto: 0.02 },
  meialua: { nome: 'Meia-lua', unid: crescente(), canto: 0.08, livreX: true },
  heptagono: { nome: 'Heptágono', unid: regular(7), canto: 0.16 },
  decagono: { nome: 'Decágono', unid: regular(10, 1, -Math.PI / 2 + Math.PI / 10), canto: 0.12 },
  'seta-direita': { nome: 'Seta para a direita', unid: SETA, canto: 0.14, livreX: true },
  'seta-esquerda': { nome: 'Seta para a esquerda', unid: SETA.map(([x, y]) => [-x, y]).reverse(), canto: 0.14, livreX: true },
  'seta-cima': { nome: 'Seta para cima', unid: SETA.map(([x, y]) => [y, -x]), canto: 0.14, livreY: true },
  'seta-baixo': { nome: 'Seta para baixo', unid: SETA.map(([x, y]) => [-y, x]), canto: 0.14, livreY: true },
  cruz: { nome: 'Cruz', unid: [[-0.36, -1], [0.36, -1], [0.36, -0.36], [1, -0.36], [1, 0.36], [0.36, 0.36], [0.36, 1], [-0.36, 1], [-0.36, 0.36], [-1, 0.36], [-1, -0.36], [-0.36, -0.36]], canto: 0.12 },
  // lote 2: formas "3D" desenhadas (faces com luz e sombra)
  cubo: { nome: 'Cubo', grupo: '3d', unid: [[-1, -1], [1, -1], [1, 1], [-1, 1]], canto: 0.1, faces: (p) => { const [[x0, y0], [x1], [, y1]] = p, d = 0.32 * (x1 - x0); return [{ pts: [[x0, y0], [x1, y0], [x1 + d, y0 - d], [x0 + d, y0 - d]], tom: 'claro' }, { pts: [[x1, y0], [x1 + d, y0 - d], [x1 + d, y1 - d], [x1, y1]], tom: 'escuro' }]; } },
  cilindro: { nome: 'Cilindro', grupo: '3d', unid: [[-1, -1], [1, -1], ...Array.from({ length: 25 }, (_, i) => { const a = (Math.PI * i) / 24; return [Math.cos(a), 1 + 0.3 * Math.sin(a)]; })], ajuste: [[-1, -0.6], [1, -0.6], [1, 1], [-1, 1]], canto: 0.04, faces: (p, cx) => { const ys = p.map((q) => q[1]), y0 = Math.min(...ys), xs = p.map((q) => q[0]), rx = (Math.max(...xs) - Math.min(...xs)) / 2; return [{ pts: elipse(rx, rx * 0.3).map(([x, y]) => [cx + x, y0 + y]), tom: 'claro', sobre: true }]; } },
  cone: { nome: 'Cone', grupo: '3d', unid: [[0, -1.45], ...Array.from({ length: 25 }, (_, i) => { const a = (Math.PI * i) / 24; return [Math.cos(a), 1 + 0.28 * Math.sin(a)]; })], canto: 0.05, faces: (p) => [{ pts: [p[0], ...p.slice(1, 13)], tom: 'sombra', sobre: true }] },
  piramide: { nome: 'Pirâmide', grupo: '3d', unid: [[0, -1.3], [1, 1], [-1, 1]], canto: 0.08, faces: (p) => { const [a, b] = p, w = b[0] - p[2][0]; return [{ pts: [a, [b[0] + 0.32 * w, b[1] - 0.26 * w], b], tom: 'escuro' }]; } },
  esfera: { nome: 'Esfera', grupo: '3d', tipo: 'circulo', unid: elipse(1, 1), faces: (p) => [{ pts: p, tom: 'esfera', sobre: true }] },
  // sem corpo: só olhos, sobrancelhas, boca e enfeites, para usar a carinha em qualquer lugar
  rosto: { nome: 'Só o rosto', tipo: 'nenhuma' },
  cartao: { nome: 'Cartão vertical', tipo: 'cartao', rect: [-32, -84, 320, 426.67, 28] },
  cartaoHorizontal: { nome: 'Cartão horizontal', tipo: 'cartao', rect: [-85.33, -36, 426.67, 320, 28] },
};

function dentro([x, y], pol) {
  let em = false;
  for (let i = 0, j = pol.length - 1; i < pol.length; j = i++) {
    const [xi, yi] = pol[i], [xj, yj] = pol[j];
    if ((yi > y) !== (yj > y) && x < ((xj - xi) * (y - yi)) / (yj - yi) + xi) em = !em;
  }
  return em;
}
function distSeg([x, y], [x1, y1], [x2, y2]) {
  const dx = x2 - x1, dy = y2 - y1;
  const u = Math.max(0, Math.min(1, ((x - x1) * dx + (y - y1) * dy) / (dx * dx + dy * dy)));
  return Math.hypot(x - x1 - u * dx, y - y1 - u * dy);
}
const cabe = (pol) => PONTOS_ROSTO.every((p) => dentro(p, pol) && pol.every((a, i) => distSeg(p, a, pol[(i + 1) % pol.length]) >= FOLGA));

/** Polígono com cantos arredondados (raio absoluto r). */
function poligonoSuave(pol, r) {
  const m = pol.length;
  const corte = pol.map((v, i) => {
    const ant = pol[(i + m - 1) % m], prox = pol[(i + 1) % m];
    const k = (o) => Math.min(r / Math.hypot(o[0] - v[0], o[1] - v[1]), 0.5);
    return [[v[0] + (ant[0] - v[0]) * k(ant), v[1] + (ant[1] - v[1]) * k(ant)], v, [v[0] + (prox[0] - v[0]) * k(prox), v[1] + (prox[1] - v[1]) * k(prox)]];
  });
  return corte.map(([a, v, b], i) => `${i ? 'L' : 'M'}${f(a[0])} ${f(a[1])} Q${f(v[0])} ${f(v[1])} ${f(b[0])} ${f(b[1])}`).join(' ') + ' Z';
}

// Proporções do corpo: estica (kx, ky) em volta do centro do rosto. O rosto encolhe
// junto quando o corpo fica mais estreito ou mais baixo e, no alto, sobe para o topo.
export const PROPORCOES = {
  normal: { nome: 'Normal', kx: 1, ky: 1 },
  alto: { nome: 'Alto', kx: 0.95, ky: 1.45, subir: 0.3 },
  baixo: { nome: 'Baixo', kx: 1.12, ky: 0.74 },
  gordo: { nome: 'Gordo', kx: 1.42, ky: 1.04 },
  magro: { nome: 'Magro', kx: 0.7, ky: 1.12 },
};
const transformaPontos = (pts, { kx, ky }) => pts.map(([x, y]) => [128 + (x - 128) * kx, 124 + (y - 124) * ky]);

// Glifos desenhados como linha central numa caixa 100×140; o corpo é o traço grosso
// (estilo balão) e o rosto fica sobre o ponto `a` do traço, um pouco menor.
const GLIFOS = {
  0: ['M50 10 C80 10 86 40 86 70 C86 100 80 130 50 130 C20 130 14 100 14 70 C14 40 20 10 50 10 Z', [50, 14]],
  1: ['M28 32 L56 12 L56 130', [56, 72]],
  2: ['M18 40 C18 10 82 8 82 42 C82 70 40 90 18 128 L84 128', [50, 20]],
  3: ['M18 22 C40 6 84 10 82 38 C80 62 56 66 44 68 C66 68 86 80 84 104 C82 132 34 136 16 116', [54, 16]],
  4: ['M66 130 L66 12 L14 92 L88 92', [64, 92]],
  5: ['M80 14 L26 14 L22 64 C40 52 84 54 84 92 C84 128 36 136 16 116', [56, 126]],
  6: ['M74 18 C48 4 16 30 16 82 C16 118 36 130 52 130 C76 130 86 112 86 92 C86 70 70 58 52 58 C34 58 20 70 16 84', [50, 128]],
  7: ['M16 16 L84 16 L40 130', [50, 16]],
  8: ['M50 66 C24 66 18 52 18 40 C18 22 32 10 50 10 C68 10 82 22 82 40 C82 52 76 66 50 66 C22 66 14 84 14 98 C14 118 30 130 50 130 C70 130 86 118 86 98 C86 84 78 66 50 66 Z', [50, 128]],
  9: ['M84 56 C80 70 66 82 48 82 C30 82 14 70 14 48 C14 28 30 10 50 10 C72 10 86 28 86 58 C86 100 66 132 26 124', [50, 12]],
  A: ['M12 130 L50 12 L88 130 M28 88 L72 88', [50, 88]],
  B: ['M20 130 L20 10 L56 10 C76 10 84 22 84 38 C84 54 72 66 54 66 L20 66 M54 66 C78 66 88 80 88 98 C88 118 76 130 56 130 L20 130', [22, 70]],
  C: ['M84 30 C72 14 60 10 50 10 C26 10 14 38 14 70 C14 102 26 130 50 130 C62 130 74 124 84 110', [16, 70]],
  D: ['M20 10 L20 130 L46 130 C74 130 86 104 86 70 C86 36 74 10 46 10 Z', [22, 70]],
  E: ['M82 12 L20 12 L20 128 L82 128 M20 70 L70 70', [22, 70]],
  F: ['M82 12 L20 12 L20 130 M20 70 L70 70', [22, 70]],
  G: ['M84 30 C72 14 60 10 50 10 C26 10 14 38 14 70 C14 102 26 130 50 130 C72 130 86 112 86 80 L56 80', [16, 70]],
  H: ['M18 10 L18 130 M82 10 L82 130 M18 70 L82 70', [50, 70]],
  I: ['M50 12 L50 128 M26 12 L74 12 M26 128 L74 128', [50, 70]],
  J: ['M78 12 L78 96 C78 120 64 130 48 130 C30 130 20 120 18 104', [78, 60]],
  K: ['M20 10 L20 130 M82 12 L20 76 M42 56 L84 128', [22, 72]],
  L: ['M22 10 L22 128 L84 128', [22, 88]],
  M: ['M14 130 L14 12 L50 80 L86 12 L86 130', [14, 76]],
  N: ['M18 130 L18 12 L82 128 L82 10', [50, 70]],
  O: ['M50 12 C78 12 88 40 88 70 C88 100 78 128 50 128 C22 128 12 100 12 70 C12 40 22 12 50 12 Z', [50, 14]],
  P: ['M20 130 L20 12 L54 12 C76 12 86 26 86 44 C86 62 76 76 54 76 L20 76', [22, 100]],
  Q: ['M50 12 C78 12 88 40 88 70 C88 100 78 128 50 128 C22 128 12 100 12 70 C12 40 22 12 50 12 Z M60 98 L90 132', [50, 14]],
  R: ['M20 130 L20 12 L54 12 C76 12 86 26 86 44 C86 62 76 76 54 76 L20 76 M50 76 L84 130', [22, 100]],
  S: ['M82 28 C72 14 62 10 48 10 C28 10 16 22 16 38 C16 58 32 64 50 70 C70 76 86 84 86 102 C86 120 72 130 50 130 C32 130 20 124 14 110', [50, 70]],
  T: ['M12 14 L88 14 M50 14 L50 130', [50, 76]],
  U: ['M16 10 L16 90 C16 116 30 130 50 130 C70 130 84 116 84 90 L84 10', [50, 126]],
  V: ['M12 12 L50 130 L88 12', [48, 112]],
  W: ['M8 12 L28 130 L50 60 L72 130 L92 12', [50, 64]],
  X: ['M14 12 L86 130 M86 12 L14 130', [50, 71]],
  Y: ['M12 12 L50 70 L88 12 M50 70 L50 130', [50, 82]],
  Z: ['M14 14 L86 14 L14 128 L86 128', [50, 71]],
  mais: ['M50 20 L50 120 M2 70 L98 70', [50, 70], 'Mais'],
  menos: ['M8 70 L92 70', [50, 70], 'Menos'],
  vezes: ['M16 36 L84 104 M84 36 L16 104', [50, 70], 'Vezes'],
  dividir: ['M8 70 L92 70 M50 20 L50 20.1 M50 120 L50 120.1', [50, 70], 'Dividir'],
  igual: ['M10 46 L90 46 M10 96 L90 96', [50, 46], 'Igual'],
};
const GLIFO_ESCALA = 2.6, GLIFO_TRACO = 40, GLIFO_ROSTO = 0.54;
for (const [k, [d, a, nome]] of Object.entries(GLIFOS)) {
  const id = /^\d$/.test(k) ? `numero-${k}` : /^[A-Z]$/.test(k) ? `letra-${k.toLowerCase()}` : k;
  FORMAS[id] = { nome: nome || (/^\d$/.test(k) ? `Número ${k}` : `Letra ${k}`), grupo: /^\d$/.test(k) ? 'numeros' : /^[A-Z]$/.test(k) ? 'letras' : 'sinais', tipo: 'glifo', traco: d, ancora: a };
}
function glifoBase(F, id) {
  const k = GLIFO_ESCALA, [ax, ay] = F.ancora;
  const d = F.traco.replace(/(-?\d+(?:\.\d+)?)[ ,](-?\d+(?:\.\d+)?)/g, (_, x, y) => `${f(128 + (x - ax) * k)} ${f(124 + (y - ay) * k)}`);
  const nums = [...d.matchAll(/(-?\d+(?:\.\d+)?) (-?\d+(?:\.\d+)?)/g)].map((m) => [+m[1], +m[2]]);
  const sw = GLIFO_TRACO * k, m = sw / 2;
  const xs = nums.map((p) => p[0]), ys = nums.map((p) => p[1]);
  const caixa = { x: Math.min(...xs) - m, y: Math.min(...ys) - m, w: Math.max(...xs) - Math.min(...xs) + sw, h: Math.max(...ys) - Math.min(...ys) + sw };
  return { ...F, id, d, largura: sw, caixa, pol: retanguloPol(caixa.x, caixa.y, caixa.w, caixa.h, Math.min(caixa.w, caixa.h) / 4), centro: [caixa.x + caixa.w / 2, caixa.y + caixa.h / 2], rostoT: { sf: GLIFO_ROSTO, dy: 0 } };
}
/** Caixa da forma incluindo as faces 3D. */
function caixaCom(pol, faces) {
  const pts = [...pol, ...(faces || []).flatMap((q) => q.pts)];
  const xs = pts.map((p) => p[0]), ys = pts.map((p) => p[1]);
  return { x: Math.min(...xs), y: Math.min(...ys), w: Math.max(...xs) - Math.min(...xs), h: Math.max(...ys) - Math.min(...ys) };
}

const resolvidas = {};
/** Forma encaixada no rosto, na proporção pedida. */
export function forma(id, prop = 'normal') {
  const chave = `${id}:${prop}`;
  if (resolvidas[chave]) return resolvidas[chave];
  const base = formaBase(id);
  const P0 = PROPORCOES[prop] || PROPORCOES.normal;
  if (prop === 'normal' || ['cartao', 'glifo', 'nenhuma'].includes(base.tipo)) return (resolvidas[chave] = base);
  const { kx, ky } = P0;
  const pol = transformaPontos(base.pol, P0);
  let d;
  if (base.rect) {
    const [x, y, w, h, rx] = base.rect;
    const [[x0, y0]] = transformaPontos([[x, y]], P0);
    d = retanguloD(x0, y0, w * kx, h * ky, Math.min(rx * Math.min(kx, ky), (w * kx) / 2, (h * ky) / 2));
  } else if (base.tipo === 'circulo' || base.tipo === 'elipse') {
    const c = base.centro, cx = 128 + (c[0] - 128) * kx, cy = 124 + (c[1] - 124) * ky, rx = ((base.caixa.ew ?? base.caixa.w) / 2) * kx, ry = ((base.caixa.eh ?? base.caixa.h) / 2) * ky;
    d = `M${f(cx - rx)} ${f(cy)} A${f(rx)} ${f(ry)} 0 1 0 ${f(cx + rx)} ${f(cy)} A${f(rx)} ${f(ry)} 0 1 0 ${f(cx - rx)} ${f(cy)} Z`;
  } else d = poligonoSuave(transformaPontos(base.polBruto, P0), base.raio * Math.min(kx, ky));
  const polB = base.polBruto ? transformaPontos(base.polBruto, P0) : pol;
  const faces = FORMAS[id].faces?.(polB, 128 + (base.centro[0] - 128) * kx);
  const caixa = caixaCom(pol, faces);
  // rosto: escala uniforme que ainda cabe e, no alto, sobe enquanto continuar cabendo
  const sf = Math.min(1, kx, ky);
  let dy = P0.subir ? -(ky - 1) * base.caixa.h * P0.subir : 0;
  const ponto = (dyy) => ([x, y]) => [128 + (x - 128) * sf, 124 + dyy + (y - 124) * sf];
  while (dy < -1 && !PONTOS_ROSTO.map(ponto(dy)).every((p) => dentro(p, pol) && pol.every((a, i) => distSeg(p, a, pol[(i + 1) % pol.length]) >= FOLGA * 0.8))) dy *= 0.8;
  if (dy > -1) dy = 0;
  const r = { ...base, id, prop, d, pol, caixa, faces, rostoT: sf === 1 && dy === 0 ? null : { sf, dy } };
  r.ancoras = ancoras(r);
  return (resolvidas[chave] = r);
}
const retanguloD = (x, y, w, h, rx) => `M${f(x + rx)} ${f(y)} H${f(x + w - rx)} A${f(rx)} ${f(rx)} 0 0 1 ${f(x + w)} ${f(y + rx)} V${f(y + h - rx)} A${f(rx)} ${f(rx)} 0 0 1 ${f(x + w - rx)} ${f(y + h)} H${f(x + rx)} A${f(rx)} ${f(rx)} 0 0 1 ${f(x)} ${f(y + h - rx)} V${f(y + rx)} A${f(rx)} ${f(rx)} 0 0 1 ${f(x + rx)} ${f(y)} Z`;

/** Encaixa a forma no rosto: menor escala (com deslocamento vertical) que contém os pontos. */
function formaBase(id) {
  if (resolvidas[id]) return resolvidas[id];
  const F = FORMAS[id];
  let r;
  if (F.tipo === 'glifo') r = glifoBase(F, id);
  else if (F.tipo === 'nenhuma') {
    const caixa = { x: 44, y: 38, w: 168, h: 176 };
    r = { ...F, id, d: '', caixa, pol: retanguloPol(caixa.x, caixa.y, caixa.w, caixa.h, 60), centro: [128, 124] };
  }
  else if (F.rect) {
    const [x, y, w, h, rx] = F.rect;
    r = { ...F, id, d: `M${x + rx} ${y} H${f(x + w - rx)} A${rx} ${rx} 0 0 1 ${f(x + w)} ${y + rx} V${f(y + h - rx)} A${rx} ${rx} 0 0 1 ${f(x + w - rx)} ${f(y + h)} H${x + rx} A${rx} ${rx} 0 0 1 ${x} ${f(y + h - rx)} V${y + rx} A${rx} ${rx} 0 0 1 ${x + rx} ${y} Z`, caixa: { x, y, w, h } };
    r.pol = retanguloPol(x, y, w, h, rx);
  } else {
    let melhor;
    const ajuste = F.ajuste || F.unid;
    // livreX / livreY: o rosto também pode ir para os lados ou para cima/baixo
    // (deslocamento proporcional ao tamanho); nas outras, só um ajuste vertical em pixels
    const fracoes = [-0.6, -0.5, -0.4, -0.3, -0.2, -0.1, 0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6];
    const verticais = F.livreY ? fracoes.map((fy) => [fy, 0]) : Array.from({ length: 23 }, (_, i) => [0, -70 + 5 * i]);
    for (const fx of F.livreX ? fracoes : [0]) {
      for (const [fy, dyPx] of verticais) {
        const pol = (s) => ajuste.map(([x, y]) => [128 + (x - fx) * s, 124 + dyPx + (y - fy) * s]);
        let lo = 40, hi = 600;
        if (!cabe(pol(hi))) continue;
        for (let k = 0; k < 30; k++) { const m = (lo + hi) / 2; if (cabe(pol(m))) hi = m; else lo = m; }
        if (!melhor || hi < melhor.s) melhor = { s: hi, fx, fy, dyPx };
      }
    }
    // folga extra para o arredondamento dos cantos não encostar no rosto
    const s = melhor.s * (1 + (F.canto || 0) * 0.35);
    const dx = -melhor.fx * s, dy = melhor.dyPx - melhor.fy * s;
    const pol = F.unid.map(([x, y]) => [128 + dx + x * s, 124 + dy + y * s]);
    const cx = 128 + dx, cy = 124 + dy;
    const faces = F.faces?.(pol, cx);
    const caixa = caixaCom(pol, faces);
    if (F.tipo === 'circulo' || F.tipo === 'elipse') { const c0 = caixaCom(pol); Object.assign(caixa, { ...caixaCom(pol, faces) }); caixa.ew = c0.w; caixa.eh = c0.h; }
    const ew = (caixa.ew ?? caixa.w) / 2, eh = (caixa.eh ?? caixa.h) / 2;
    const d = F.tipo === 'circulo' || F.tipo === 'elipse'
      ? `M${f(cx - ew)} ${f(cy)} A${f(ew)} ${f(eh)} 0 1 0 ${f(cx + ew)} ${f(cy)} A${f(ew)} ${f(eh)} 0 1 0 ${f(cx - ew)} ${f(cy)} Z`
      : poligonoSuave(pol, F.canto * s);
    r = { ...F, id, d, caixa, pol, polBruto: pol, raio: F.canto * s, centro: [cx, cy], faces };
  }
  r.ancoras = ancoras(r);
  return (resolvidas[id] = r);
}

function retanguloPol(x, y, w, h, rx) {
  const arco = (cx, cy, a0) => Array.from({ length: 9 }, (_, i) => { const a = a0 + (i * Math.PI) / 16; return [cx + rx * Math.cos(a), cy + rx * Math.sin(a)]; });
  return [...arco(x + w - rx, y + rx, -Math.PI / 2), ...arco(x + w - rx, y + h - rx, 0), ...arco(x + rx, y + h - rx, Math.PI / 2), ...arco(x + rx, y + rx, Math.PI)];
}
/** Bordas esquerda e direita da forma na altura y. */
function bordas(pol, y) {
  const xs = [];
  pol.forEach((a, i) => { const b = pol[(i + 1) % pol.length]; if ((a[1] - y) * (b[1] - y) <= 0 && a[1] !== b[1]) xs.push(a[0] + ((y - a[1]) * (b[0] - a[0])) / (b[1] - a[1])); });
  return xs.length ? [Math.min(...xs), Math.max(...xs)] : null;
}
/** Base e centro da forma: pivôs dos movimentos (a sombra fica na base). */
function ancoras(F) {
  return { base: F.tipo === 'cartao' ? 260 : F.caixa.y + F.caixa.h, centro: [F.caixa.x + F.caixa.w / 2, F.caixa.y + F.caixa.h / 2] };
}

/** Transform do rosto (proporções) e o inverso, para o corpo continuar no lugar. */
function rostoTransform(F, inverso = false) {
  if (!F.rostoT) return '';
  const { sf, dy } = F.rostoT;
  return inverso ? `translate(128 124) scale(${f(1 / sf)}) translate(-128 ${f(-124 - dy)})` : `translate(128 ${f(124 + dy)}) scale(${f(sf)}) translate(-128 -124)`;
}

/** Glifo: traço grosso arredondado com sombra e brilho (máscara do próprio traço). */
function corpoGlifo(F) {
  const { x, y, w, h } = F.caixa, sw = F.largura;
  const traco = (extra) => `<path d="${F.d}" fill="none" stroke-width="${f(sw)}" stroke-linecap="round" stroke-linejoin="round" ${extra}/>`;
  const mask = uid('mask-glifo-');
  const corpo = `${traco('stroke="#000000" opacity="0.14" filter="url(#f-suave)" transform="translate(0 6)"')}${traco('stroke="url(#g-corpo)"')}`
    + `<mask id="${mask}" maskUnits="userSpaceOnUse" x="${f(x - 20)}" y="${f(y - 20)}" width="${f(w + 40)}" height="${f(h + 40)}">${traco('stroke="#FFFFFF"')}</mask>`
    + `<g mask="url(#${mask})"><ellipse cx="${f(x + 0.3 * w)}" cy="${f(y + 0.16 * h)}" rx="${f(0.3 * w)}" ry="${f(0.08 * h)}" fill="#FFFFFF" opacity="0.3" filter="url(#f-suave)" transform="rotate(-20 ${f(x + 0.3 * w)} ${f(y + 0.16 * h)})"/></g>`;
  return `<g transform="${rostoTransform(F, true)}">${corpo}</g>`;
}

function corpoForma(c) {
  const F = CTX.forma, { x, y, w, h } = F.caixa;
  if (F.tipo === 'nenhuma') return '';
  if (F.tipo === 'glifo') return corpoGlifo(F);
  const tons = { claro: `fill="${mix(c, '#FFFFFF', 0.35)}"`, escuro: `fill="${mix(c, '#000000', 0.25)}"`, sombra: 'fill="#000000" opacity="0.14"', esfera: 'fill="url(#g-esfera)"' };
  const face = (q) => (q.tom === 'esfera' ? `<path d="${F.d}" ${tons.esfera}/><ellipse cx="${f(x + 0.33 * w)}" cy="${f(y + 0.27 * h)}" rx="${f(0.09 * w)}" ry="${f(0.06 * h)}" fill="#FFFFFF" opacity="0.85" transform="rotate(-30 ${f(x + 0.33 * w)} ${f(y + 0.27 * h)})"/>` : `<path d="${poligonoSuave(q.pts, 6)}" ${tons[q.tom]} stroke="${mix(c, '#000000', 0.12)}" stroke-width="1.5" stroke-linejoin="round"/>`);
  const atras = (F.faces || []).filter((q) => !q.sobre).map(face).join('');
  const sobre = (F.faces || []).filter((q) => q.sobre).map(face).join('');
  const clip = uid('clip-forma-');
  const brilho = `<clipPath id="${clip}"><path d="${F.d}"/></clipPath><g clip-path="url(#${clip})"><ellipse cx="${f(x + 0.32 * w)}" cy="${f(y + 0.19 * h)}" rx="${f(0.22 * w)}" ry="${f(Math.min(0.1 * h, 0.11 * w))}" fill="#FFFFFF" opacity="${F.tipo === 'cartao' ? 0.18 : 0.28}" filter="url(#f-suave)" transform="rotate(-24 ${f(x + 0.32 * w)} ${f(y + 0.19 * h)})"/></g>`;
  const sombra = F.tipo === 'cartao' ? '' : `<path d="${F.d}" fill="#000000" opacity="0.14" filter="url(#f-suave)" transform="translate(0 6)"/>`;
  const corpo = `${sombra}${atras}<path d="${F.d}" fill="url(#g-corpo)"/>${sobre}${brilho}`;
  return F.rostoT ? `<g transform="${rostoTransform(F, true)}">${corpo}</g>` : corpo;
}

/** Corpo + rosto. No cartão o fundo fica parado e só o rosto se move. */
function rosto(c, conteudo, mov, pivo) {
  if (CTX.movimento) {
    // um movimento do catálogo comanda o corpo todo (no cartão, só o rosto)
    if (CTX.forma.tipo === 'cartao') return corpoForma(c) + comMovimento(conteudo, CTX.movimento, 236);
    mov = null;
  }
  return CTX.forma.tipo === 'cartao' ? corpoForma(c) + P(conteudo, mov, pivo) : P(corpoForma(c) + conteudo, mov, pivo);
}

/**
 * Olho: branco com volume, íris (ou pupila preta grande) que pode olhar em volta,
 * pálpebra de cima (inclinada, com piscada) e de baixo (sorriso dos olhos).
 */
function olho(c, { cx, cy, rx = 26, ry = 28, estilo = 'iris', pupila = 1, olhar, palpebra = 0, angulo = 0, caida, piscada, fechado = 'normal', baixo = 0 }) {
  const clip = uid('clip-olho-');
  const ir = (estilo === 'iris' ? 15 : 17) * pupila;
  const iris = estilo === 'iris'
    ? `<circle cx="${cx}" cy="${cy + 2}" r="${ir}" fill="url(#g-iris)"/><circle cx="${cx}" cy="${cy + 2}" r="${f(ir * 0.55)}" fill="#111111"/>`
    : `<circle cx="${cx}" cy="${cy + 2}" r="${ir}" fill="#141414"/>`;
  const brilho = `<circle cx="${f(cx - ir * 0.38)}" cy="${f(cy + 2 - ir * 0.42)}" r="${f(Math.max(3, ir * 0.24))}" fill="#FFFFFF"/><circle cx="${f(cx + ir * 0.3)}" cy="${f(cy + 2 + ir * 0.35)}" r="${f(Math.max(1.5, ir * 0.1))}" fill="#FFFFFF" opacity="0.8"/>`;
  // pálpebra: retângulo da cor do corpo descendo até `borda`; `caida` a leva até fechar
  const topo = cy - ry;
  const borda = palpebra > 0 ? topo + 2 * ry * palpebra : topo - 6;
  const linha = mix(c, '#000000', 0.5);
  const tampa = `<g transform="rotate(${angulo} ${cx} ${f(borda)})"><rect x="${cx - rx - 30}" y="${f(borda - 2 * ry - 40)}" width="${2 * rx + 60}" height="${2 * ry + 40}" fill="url(#${CTX.forma.rostoT ? 'g-corpo-rosto' : 'g-corpo'})"/><line x1="${cx - rx - 30}" y1="${f(borda)}" x2="${cx + rx + 30}" y2="${f(borda)}" stroke="${linha}" stroke-width="4"/></g>`;
  const fechar = 2 * ry + 6 - (borda - topo);
  const tampaMov = caida ? P(tampa, (t) => ({ ty: caida(t) * fechar })) : tampa;
  const baixa = baixo > 0
    ? `<g><ellipse cx="${cx}" cy="${f(cy + ry + 2 * ry * (1 - baixo))}" rx="${rx + 14}" ry="${ry + 4}" fill="url(#${CTX.forma.rostoT ? 'g-corpo-rosto' : 'g-corpo'})"/></g>`
    : '';
  const olhoSvg = `<g>
<ellipse cx="${cx}" cy="${cy + 3}" rx="${rx + 5}" ry="${ry + 5}" fill="${mix(c, '#000000', 0.35)}" opacity="0.45" filter="url(#f-sombra)"/>
<clipPath id="${clip}"><ellipse cx="${cx}" cy="${cy}" rx="${rx}" ry="${ry}"/></clipPath>
<ellipse cx="${cx}" cy="${cy}" rx="${rx}" ry="${ry}" fill="url(#g-olho)"/>
<g clip-path="url(#${clip})">${P(iris + brilho, olhar)}${baixa}${tampaMov}</g>
<ellipse cx="${cx}" cy="${cy}" rx="${rx}" ry="${ry}" fill="none" stroke="${linha}" stroke-width="2" opacity="0.55"/>
</g>`;
  if (!piscada) return olhoSvg;
  // piscada: o olho achata e um traço de olho fechado aparece no lugar
  const fechadoSvg = fechado === 'feliz' ? olhoFeliz(cx, cy, rx - 4) : traco(`M${cx - rx + 2} ${cy + 4} Q${cx} ${cy + 16} ${cx + rx - 2} ${cy + 4}`, 7);
  return P(olhoSvg, (t) => { const k = piscada(t); return { sy: 1 - 0.95 * k, o: 1 - rampa(k, 0.55, 0.85) }; }, [cx, cy + 6])
    + P(fechadoSvg, (t) => ({ o: rampa(piscada(t), 0.55, 0.85) }), [cx, cy]);
}

/** Olho fechado de alegria: arco ^ */
const olhoFeliz = (cx, cy, l = 22) => `<path d="M${cx - l} ${cy + 8} Q${cx} ${cy - 18} ${cx + l} ${cy + 8}" fill="none" stroke="${TINTA}" stroke-width="9" stroke-linecap="round"/>`;

function estrela(cx, cy, r) {
  const pts = Array.from({ length: 10 }, (_, i) => {
    const a = -Math.PI / 2 + (i * Math.PI) / 5;
    const rr = i % 2 ? r * 0.48 : r;
    return `${f(cx + rr * Math.cos(a))},${f(cy + rr * Math.sin(a))}`;
  }).join(' ');
  return `<polygon points="${pts}" fill="url(#g-estrela)" stroke="#9A5B00" stroke-width="3.5" stroke-linejoin="round"/><circle cx="${cx - r * 0.25}" cy="${cy - r * 0.25}" r="${r * 0.12}" fill="#FFFFFF"/>`;
}

const sobrancelha = (d, l = 11) => `<path d="${d}" fill="none" stroke="${TINTA}" stroke-width="${l}" stroke-linecap="round"/>`;

/** Boca aberta: dentes em cima, língua no fundo; `fora` põe a língua para fora. */
function bocaAberta(c, { cx = 128, y = 156, l = 84, prof = 42, curva = 8, dentes = true, lingua = true, fora, foraMov }) {
  const clip = uid('clip-boca-');
  const x1 = cx - l / 2, x2 = cx + l / 2;
  const d = `M${x1} ${y} Q${cx} ${y + curva} ${x2} ${y} C${x2} ${y + prof * 1.3} ${x1} ${y + prof * 1.3} ${x1} ${y} Z`;
  const rim = mix(c, '#000000', 0.35);
  let dentro = `<rect x="${x1 - 4}" y="${y - 10}" width="${l + 8}" height="${prof + 30}" fill="${BOCA}"/>`;
  if (lingua) dentro += `<ellipse cx="${cx + 6}" cy="${f(y + prof * 0.95)}" rx="${f(l * 0.3)}" ry="${f(prof * 0.38)}" fill="url(#g-lingua)"/>`;
  if (dentes) {
    const n = Math.max(3, Math.round(l / 20));
    const dl = l / n;
    dentro += Array.from({ length: n }, (_, i) => `<rect x="${f(x1 + i * dl + 1)}" y="${y - 6}" width="${f(dl - 2)}" height="${f(Math.min(16, prof * 0.38) + 6)}" rx="4" fill="#FFFFFF"/>`).join('');
  }
  let foraSvg = '';
  if (fora) {
    const lx = cx + (fora.dx || 0), top = y + prof * 0.55, w = fora.l || 34, comp = fora.comp || 40;
    const tl = `<path d="M${lx - w / 2} ${f(top)} V${f(top + comp - w / 2)} A${w / 2} ${w / 2} 0 0 0 ${lx + w / 2} ${f(top + comp - w / 2)} V${f(top)} Z" fill="url(#g-lingua)" stroke="${LINGUA[1]}" stroke-width="2"/><path d="M${lx} ${f(top + 6)} V${f(top + comp - 14)}" stroke="${LINGUA[1]}" stroke-width="3" stroke-linecap="round" opacity="0.7"/>`;
    foraSvg = P(tl, foraMov, [lx, top]);
  }
  return `<path d="${d}" fill="${rim}" opacity="0.35" transform="translate(0 4)" filter="url(#f-sombra)"/>
<clipPath id="${clip}"><path d="${d}"/></clipPath>
<g clip-path="url(#${clip})">${dentro}</g>
<path d="${d}" fill="none" stroke="${rim}" stroke-width="3" stroke-linejoin="round"/>${foraSvg}`;
}

const traco = (d, l = 7, cor = TINTA) => `<path d="${d}" fill="none" stroke="${cor}" stroke-width="${l}" stroke-linecap="round" stroke-linejoin="round"/>`;
const bochechas = (o = 0.45, mov) => P(`<ellipse cx="62" cy="150" rx="18" ry="11" fill="#FF5C8A" opacity="${o}" filter="url(#f-sombra)"/><ellipse cx="194" cy="150" rx="18" ry="11" fill="#FF5C8A" opacity="${o}" filter="url(#f-sombra)"/>`, mov);
const gota = (x, y, s = 1) => `<path d="M${x} ${y} C${x + 9 * s} ${y + 13 * s} ${x + 11 * s} ${y + 20 * s} ${x} ${y + 25 * s} C${x - 11 * s} ${y + 20 * s} ${x - 9 * s} ${y + 13 * s} ${x} ${y} Z" fill="url(#g-gota)" stroke="#4B9CD3" stroke-width="2"/><ellipse cx="${x - 3 * s}" cy="${y + 17 * s}" rx="${2.5 * s}" ry="${4 * s}" fill="#FFFFFF" opacity="0.9"/>`;
const brilhinho = (x, y, r) => `<path d="M${x} ${y - r} Q${x} ${y} ${x + r} ${y} Q${x} ${y} ${x} ${y + r} Q${x} ${y} ${x - r} ${y} Q${x} ${y} ${x} ${y - r} Z" fill="#FFFFFF" stroke="#FFD54A" stroke-width="2" stroke-linejoin="round"/>`;
const coracao = (cx, cy, k, cor = '#E5484D') => `<path d="M${f(cx)} ${f(cy + k * 0.75)} C${f(cx - k * 1.3)} ${f(cy - k * 0.1)} ${f(cx - k * 0.8)} ${f(cy - k * 1.05)} ${f(cx)} ${f(cy - k * 0.45)} C${f(cx + k * 0.8)} ${f(cy - k * 1.05)} ${f(cx + k * 1.3)} ${f(cy - k * 0.1)} ${f(cx)} ${f(cy + k * 0.75)} Z" fill="${cor}" stroke="#B3202A" stroke-width="2.5" stroke-linejoin="round"/><ellipse cx="${f(cx - k * 0.42)}" cy="${f(cy - k * 0.42)}" rx="${f(k * 0.18)}" ry="${f(k * 0.11)}" fill="#FFFFFF" opacity="0.8" transform="rotate(-35 ${f(cx - k * 0.42)} ${f(cy - k * 0.42)})"/>`;
/** Espiral de olho tonto (3 voltas). */
const espiral = (cx, cy, r) => `<path d="${Array.from({ length: 61 }, (_, i) => { const a = (i / 60) * 6 * Math.PI, rr = (r * i) / 60; return `${i ? 'L' : 'M'}${f(cx + rr * Math.cos(a))} ${f(cy + rr * Math.sin(a))}`; }).join(' ')}" fill="none" stroke="${TINTA}" stroke-width="4" stroke-linecap="round"/>`;

function interrogacao(x, y, s = 1) {
  const d = `M${x - 14 * s} ${y - 8 * s} C${x - 14 * s} ${y - 30 * s} ${x + 16 * s} ${y - 30 * s} ${x + 16 * s} ${y - 10 * s} C${x + 16 * s} ${y + 2 * s} ${x} ${y + 2 * s} ${x} ${y + 14 * s}`;
  return `<path d="${d}" fill="none" stroke="${TINTA}" stroke-width="${15 * s}" stroke-linecap="round"/><path d="${d}" fill="none" stroke="#FFFFFF" stroke-width="${8 * s}" stroke-linecap="round"/><circle cx="${x}" cy="${y + 30 * s}" r="${7.5 * s}" fill="${TINTA}"/><circle cx="${x}" cy="${y + 30 * s}" r="${4 * s}" fill="#FFFFFF"/>`;
}
function letraZ(x, y, s) {
  const d = `M${x - 8 * s} ${y - 8 * s} H${x + 8 * s} L${x - 8 * s} ${y + 8 * s} H${x + 8 * s}`;
  return traco(d, 9 * s, TINTA) + traco(d, 4 * s, '#FFFFFF');
}

/** Capacete de obra no alto da cabeça (aba em y ≈ 50). */
function capacete() {
  const casco = 'M70 50 C70 14 98 2 128 2 C158 2 186 14 186 50 Z';
  return `<path d="${casco}" fill="#000000" opacity="0.14" filter="url(#f-suave)" transform="translate(0 5)"/>
<path d="${casco}" fill="#FFC51A" stroke="#9A5B00" stroke-width="3.5" stroke-linejoin="round"/>
<path d="M114 6 C116 22 116 38 114 50 H142 C140 38 140 22 142 6 C134 4 122 4 114 6 Z" fill="#FFD54A" stroke="#9A5B00" stroke-width="3" stroke-linejoin="round"/>
<ellipse cx="96" cy="22" rx="12" ry="6" fill="#FFFFFF" opacity="0.55" transform="rotate(-28 96 22)"/>
<rect x="52" y="44" width="152" height="14" rx="7" fill="#F2A900" stroke="#9A5B00" stroke-width="3.5"/>`;
}
/** Chave inglesa em pé, com a boca aberta para cima; (x, y) é o centro da cabeça. */
function chaveInglesa(x, y) {
  const mask = uid('mask-chave-');
  return `<mask id="${mask}" maskUnits="userSpaceOnUse" x="${x - 30}" y="${y - 30}" width="60" height="100"><rect x="${x - 30}" y="${y - 30}" width="60" height="100" fill="#FFFFFF"/><rect x="${x - 7}" y="${y - 32}" width="14" height="26" rx="3" fill="#000000"/></mask>
<g mask="url(#${mask})" stroke="${TINTA}" stroke-width="3.5" stroke-linejoin="round"><circle cx="${x}" cy="${y}" r="20" fill="#C7CED6"/><rect x="${x - 8}" y="${y + 12}" width="16" height="46" rx="8" fill="#AEB7C1"/></g>
<circle cx="${x}" cy="${y + 48}" r="3.5" fill="${TINTA}"/><path d="M${x - 12} ${y - 4} A13 13 0 0 1 ${x - 6} ${y - 14}" fill="none" stroke="#FFFFFF" stroke-width="3" stroke-linecap="round" opacity="0.8"/>`;
}

// ── expressões ───────────────────────────────────────────────────────────────
// cada uma: nome, quando usar, cor, duração do laço, quadro da pose estática, desenho(c) e, se os enfeites
// passarem da caixa do rosto, `alem` ([x0, y0, x1, y1]) para o enquadramento de "só o rosto"
const centro = [128, 236]; // pivô da cabeça (base)

export const EXPRESSOES = {
  feliz: {
    nome: 'Feliz', uso: 'boas-vindas, início de atividade', cor: 'amarelo', dur: 2,
    desenho: (c) => rosto(c, `
${sobrancelha('M68 72 Q90 60 112 70')}${sobrancelha('M144 70 Q166 60 188 72')}
${olho(c, { cx: 92, cy: 108, olhar: (t) => ({ tx: 2, ty: 2 }), piscada: (t) => piscar(t, 0.72) })}
${olho(c, { cx: 164, cy: 108, olhar: (t) => ({ tx: -2, ty: 2 }), piscada: (t) => piscar(t, 0.72) })}
${bochechas(0.35)}
${bocaAberta(c, { y: 156, l: 88, prof: 38 })}`, (t) => ({ ty: -6 * Math.abs(onda(t, 1)), sy: 1 - 0.02 * Math.abs(onda(t, 1, 0.25)) }), centro),
  },
  acertou: {
    nome: 'Acertou!', uso: 'resposta certa, exercício concluído', cor: 'verde', dur: 1.6,
    desenho: (c) => rosto(c, `
${sobrancelha('M70 62 Q90 50 110 56', 8)}${sobrancelha('M146 56 Q166 50 186 62', 8)}
${olhoFeliz(92, 108)}${olhoFeliz(164, 108)}
${bochechas(0.5)}
${bocaAberta(c, { y: 146, l: 108, prof: 50, curva: 10 })}`, (t) => {
      const pulo = t < 0.6 ? Math.sin(Math.PI * t / 0.6) : 0;
      const aterrissa = pulso(t, 0.58, 0.8);
      return { ty: -16 * pulo, sx: 1 + 0.05 * aterrissa, sy: 1 - 0.07 * aterrissa + 0.03 * pulo };
    }, centro)
      + P(brilhinho(40, 52, 14), (t) => ({ sx: 0.4 + 0.8 * pulso(t, 0, 0.5), sy: 0.4 + 0.8 * pulso(t, 0, 0.5), r: 90 * t, o: pulso(t, 0, 0.5) }), [40, 52])
      + P(brilhinho(218, 64, 11), (t) => ({ sx: 0.4 + 0.8 * pulso(t, 0.2, 0.7), sy: 0.4 + 0.8 * pulso(t, 0.2, 0.7), o: pulso(t, 0.2, 0.7) }), [218, 64])
      + P(brilhinho(214, 200, 9), (t) => ({ sx: 0.4 + 0.8 * pulso(t, 0.45, 0.95), sy: 0.4 + 0.8 * pulso(t, 0.45, 0.95), o: pulso(t, 0.45, 0.95) }), [214, 200]),
    pose: 0.25,
  },
  estrela: {
    nome: 'Arrasou', uso: 'sequência de acertos, conquista, medalha', cor: 'rosa', dur: 1.4,
    desenho: (c) => rosto(c, `
${sobrancelha('M64 64 Q88 50 112 60')}${sobrancelha('M144 60 Q168 50 192 64')}
${P(estrela(92, 108, 30), (t) => ({ sx: 1 + 0.14 * pulso(t % 0.5 * 2, 0, 1), sy: 1 + 0.14 * pulso(t % 0.5 * 2, 0, 1), r: 10 * onda(t) }), [92, 108])}
${P(estrela(164, 108, 30), (t) => ({ sx: 1 + 0.14 * pulso(t % 0.5 * 2, 0, 1), sy: 1 + 0.14 * pulso(t % 0.5 * 2, 0, 1), r: -10 * onda(t) }), [164, 108])}
${bochechas(0.45)}
${bocaAberta(c, { y: 150, l: 100, prof: 46, curva: 10 })}`, (t) => ({ ty: -5 * Math.abs(onda(t, 2)), r: 3 * onda(t) }), centro),
  },
  errou: {
    nome: 'Errou', uso: 'resposta errada (tom acolhedor: tente de novo)', cor: 'azul', dur: 2.4,
    desenho: (c) => rosto(c, `
${sobrancelha('M70 80 Q92 76 112 62')}${sobrancelha('M144 62 Q164 76 186 80')}
${olho(c, { cx: 92, cy: 112, palpebra: 0.3, angulo: -14, olhar: () => ({ tx: 3, ty: 6 }), piscada: (t) => piscar(t, 0.88, 0.1) })}
${olho(c, { cx: 164, cy: 112, palpebra: 0.3, angulo: 14, olhar: () => ({ tx: -3, ty: 6 }), piscada: (t) => piscar(t, 0.88, 0.1) })}
${traco('M98 184 Q128 160 158 184', 8)}`, (t) => ({ ty: 3 * rampa(t, 0, 0.3) - 3 * rampa(t, 0.8, 1) }), centro)
      + P(gota(182, 136, 1), (t) => ({ sx: rampa(t, 0, 0.25), sy: rampa(t, 0, 0.25), ty: 70 * rampa(t, 0.3, 0.8), o: 1 - rampa(t, 0.62, 0.8) }), [182, 136]),
    pose: 0.28,
  },
  ops: {
    nome: 'Ops', uso: 'quase acertou, deslize, atenção', cor: 'amarelo', dur: 1.6,
    desenho: (c) => rosto(c, `
${sobrancelha('M70 70 Q92 60 112 58')}${sobrancelha('M144 58 Q164 60 186 70')}
${olho(c, { cx: 92, cy: 108, pupila: 0.75, olhar: (t) => ({ tx: 4 * onda(t, 2) }) })}
${olho(c, { cx: 164, cy: 108, pupila: 0.75, olhar: (t) => ({ tx: 4 * onda(t, 2) }) })}
<rect x="86" y="154" width="84" height="34" rx="14" fill="${BOCA}"/><rect x="90" y="158" width="76" height="26" rx="10" fill="#FFFFFF"/>
<path d="M90 171 H166 M109 158 V184 M128 158 V184 M147 158 V184" stroke="#C9CED6" stroke-width="2.5"/>
<rect x="86" y="154" width="84" height="34" rx="14" fill="none" stroke="${mix(c, '#000000', 0.35)}" stroke-width="3"/>`, (t) => ({ tx: 2.5 * onda(t, 6) * pulso(t, 0, 0.4) }), centro)
      + P(gota(196, 44, 0.9), (t) => ({ ty: 26 * rampa(t, 0.1, 0.9), o: 1 - rampa(t, 0.75, 0.95) }), [196, 44]),
  },
  duvida: {
    nome: 'Dúvida', uso: 'pergunta, dica, “não entendi”', cor: 'roxo', dur: 2.4,
    desenho: (c) => rosto(c, `
${sobrancelha('M68 80 Q90 78 112 80')}
${P(sobrancelha('M144 66 Q166 46 188 62'), (t) => ({ ty: -5 * pulso(t, 0.1, 0.5) }), [166, 60])}
${olho(c, { cx: 92, cy: 112, palpebra: 0.18, olhar: (t) => ({ tx: -6 + 12 * rampa(t, 0.45, 0.6) - 12 * rampa(t, 0.9, 1), ty: -6 }) })}
${olho(c, { cx: 164, cy: 110, olhar: (t) => ({ tx: -6 + 12 * rampa(t, 0.45, 0.6) - 12 * rampa(t, 0.9, 1), ty: -6 }), piscada: (t) => piscar(t, 0.3) })}
${traco('M104 176 Q114 166 124 174 T144 172 Q152 168 158 164', 7)}`, (t) => ({ r: -4 * rampa(t, 0, 0.2) + 4 * rampa(t, 0.85, 1) }), centro)
      + P(interrogacao(214, 52, 1), (t) => ({ ty: -6 * onda(t, 2), r: 12 * onda(t, 1) }), [214, 80]),
  },
  pensando: {
    nome: 'Pensando', uso: 'carregando, tempo de resposta, raciocínio', cor: 'ciano', dur: 2.4,
    desenho: (c) => rosto(c, `
${sobrancelha('M70 78 Q92 72 112 76')}${sobrancelha('M144 66 Q166 56 188 64')}
${olho(c, { cx: 92, cy: 112, palpebra: 0.12, olhar: (t) => ({ tx: 7 + 3 * onda(t, 1), ty: -9 + 2 * onda(t, 1, 0.25) }), piscada: (t) => piscar(t, 0.6) })}
${olho(c, { cx: 164, cy: 112, palpebra: 0.12, olhar: (t) => ({ tx: 7 + 3 * onda(t, 1), ty: -9 + 2 * onda(t, 1, 0.25) }), piscada: (t) => piscar(t, 0.6) })}
${traco('M128 174 Q140 170 152 172', 8)}<circle cx="160" cy="171" r="5" fill="${TINTA}"/>`, (t) => ({ r: 3 * onda(t) }), centro)
      + [[176, 40, 6, 0], [196, 28, 9, 0.2], [222, 22, 12, 0.4]].map(([x, y, r, a]) =>
        P(`<circle cx="${x}" cy="${y}" r="${r}" fill="#FFFFFF" stroke="${TINTA}" stroke-width="3"/>`, (t) => ({ o: rampa(t, a, a + 0.12) * (1 - rampa(t, 0.85, 0.97)), sx: 0.6 + 0.4 * rampa(t, a, a + 0.12), sy: 0.6 + 0.4 * rampa(t, a, a + 0.12) }), [x, y])).join(''),
    pose: 0.7,
  },
  surpreso: {
    nome: 'Uau!', uso: 'descoberta, resultado surpreendente', cor: 'laranja', dur: 1.8,
    desenho: (c) => rosto(c, `
${P(sobrancelha('M66 62 Q90 44 112 56') + sobrancelha('M144 56 Q166 44 190 62'), (t) => ({ ty: -8 * pulso(t, 0, 0.35) }), [128, 56])}
${olho(c, { cx: 92, cy: 110, rx: 30, ry: 33, pupila: 0.6, piscada: (t) => piscar(t, 0.8) })}
${olho(c, { cx: 164, cy: 110, rx: 30, ry: 33, pupila: 0.6, piscada: (t) => piscar(t, 0.8) })}
${P(`<ellipse cx="128" cy="180" rx="20" ry="24" fill="${BOCA}" stroke="${mix(c, '#000000', 0.35)}" stroke-width="3"/><ellipse cx="128" cy="194" rx="12" ry="7" fill="url(#g-lingua)"/>`, (t) => ({ sx: 1 + 0.15 * pulso(t, 0, 0.35), sy: 1 + 0.2 * pulso(t, 0, 0.35) }), [128, 180])}`,
      (t) => ({ sx: 1 + 0.05 * pulso(t, 0, 0.3), sy: 1 + 0.06 * pulso(t, 0, 0.3) }), centro),
    pose: 0.15,
  },
  bravo: {
    nome: 'Bravo', uso: 'frustração, muitas tentativas erradas', cor: 'vermelho', dur: 1.6,
    desenho: (c) => rosto(c, `
${olho(c, { cx: 92, cy: 114, palpebra: 0.38, angulo: 18, pupila: 0.8, olhar: () => ({ ty: 6 }) })}
${olho(c, { cx: 164, cy: 114, palpebra: 0.38, angulo: -18, pupila: 0.8, olhar: () => ({ ty: 6 }) })}
${P(`<path d="M60 68 L118 92 L114 102 L56 82 Z" fill="${TINTA}" stroke="${TINTA}" stroke-width="6" stroke-linejoin="round"/><path d="M196 68 L138 92 L142 102 L200 82 Z" fill="${TINTA}" stroke="${TINTA}" stroke-width="6" stroke-linejoin="round"/>`, (t) => ({ ty: 4 * pulso(t, 0, 0.5) }), [128, 85])}
${traco('M100 180 Q128 168 156 180', 8)}`, (t) => ({ tx: 3 * onda(t, 8) * pulso(t, 0, 0.5) }), centro)
      + P(`<path d="M206 40 l10 -6 M214 52 l12 0 M206 64 l10 6" stroke="${TINTA}" stroke-width="6" stroke-linecap="round"/>`, (t) => ({ o: pulso(t, 0.05, 0.5), sx: 1 + 0.3 * pulso(t, 0.05, 0.5), sy: 1 + 0.3 * pulso(t, 0.05, 0.5) }), [200, 52]),
    pose: 0.25,
  },
  brincalhao: {
    nome: 'Brincalhão', uso: 'diversão, pausa, jogo', cor: 'laranja', dur: 1.6,
    desenho: (c) => rosto(c, `
${sobrancelha('M66 70 Q90 54 114 66')}${sobrancelha('M142 66 Q166 54 190 70')}
${olho(c, { cx: 92, cy: 106, olhar: (t) => ({ tx: 4, ty: -2 }), piscada: (t) => piscar(t, 0.5) })}
${olho(c, { cx: 164, cy: 106, olhar: (t) => ({ tx: -4, ty: 2 }), piscada: (t) => piscar(t, 0.5) })}
${bochechas(0.45)}
${bocaAberta(c, { y: 146, l: 100, prof: 40, lingua: false, fora: { dx: 14, l: 34, comp: 46 }, foraMov: (t) => ({ r: 12 * onda(t, 2) }) })}`, (t) => ({ r: 5 * onda(t) }), centro),
  },
  sonolento: {
    nome: 'Sonolento', uso: 'inatividade, pausa longa, fim do dia', cor: 'roxo', dur: 3,
    desenho: (c) => rosto(c, `
${traco('M74 70 Q84 64 94 66', 7)}${traco('M162 66 Q172 64 182 70', 7)}
${olho(c, { cx: 92, cy: 116, estilo: 'preto', palpebra: 0.45, olhar: () => ({ ty: 9 }), caida: (t) => 0.3 * pulso(t, 0.2, 0.75) })}
${olho(c, { cx: 164, cy: 116, estilo: 'preto', palpebra: 0.45, olhar: () => ({ ty: 9 }), caida: (t) => 0.3 * pulso(t, 0.2, 0.75) })}
${P(`<ellipse cx="128" cy="180" rx="10" ry="12" fill="${BOCA}" stroke="${mix(c, '#000000', 0.35)}" stroke-width="3"/>`, (t) => ({ sy: 0.5 + 0.6 * pulso(t, 0.3, 0.7), sx: 0.8 + 0.3 * pulso(t, 0.3, 0.7) }), [128, 180])}`,
      (t) => ({ ty: 4 * pulso(t, 0.2, 0.75), r: -3 * pulso(t, 0.2, 0.75) }), centro)
      + [[192, 64, 0.8, 0], [210, 44, 1, 0.3], [228, 24, 1.2, 0.6]].map(([x, y, s, a]) =>
        P(letraZ(x, y, s), (t) => { const u = (t - a + 1) % 1; return { o: pulso(u, 0, 0.9), ty: -10 * u, tx: 6 * u }; }, [x, y])).join(''),
    pose: 0,
  },
  desconfiado: {
    nome: 'Desconfiado', uso: '“tem certeza?”, revisar a resposta', cor: 'ciano', dur: 2.8,
    desenho: (c) => rosto(c, `
${traco('M70 70 Q82 64 94 68', 7)}${P(traco('M160 82 Q172 72 184 74', 7), (t) => ({ ty: -6 * pulso(t, 0.35, 0.65) }), [172, 76])}
${olho(c, { cx: 92, cy: 116, estilo: 'preto', palpebra: 0.42, angulo: 8, olhar: (t) => ({ tx: 9 - 18 * rampa(t, 0.1, 0.25) + 18 * rampa(t, 0.7, 0.85), ty: 6 }) })}
${olho(c, { cx: 164, cy: 116, estilo: 'preto', palpebra: 0.5, angulo: -6, olhar: (t) => ({ tx: 9 - 18 * rampa(t, 0.1, 0.25) + 18 * rampa(t, 0.7, 0.85), ty: 6 }) })}
${traco('M114 176 Q132 182 148 168', 7)}`, null, centro),
  },
  piscadinha: {
    nome: 'Piscadinha', uso: 'incentivo, “você consegue”, dica secreta', cor: 'verde', dur: 2,
    desenho: (c) => rosto(c, `
${sobrancelha('M66 68 Q90 54 114 64')}${P(sobrancelha('M144 74 Q166 70 188 76'), (t) => ({ ty: 4 * pulso(t, 0.2, 0.7) }), [166, 74])}
${olho(c, { cx: 92, cy: 106, olhar: () => ({ tx: 3, ty: 1 }) })}
${olho(c, { cx: 164, cy: 108, olhar: () => ({ tx: -3, ty: 1 }), piscada: (t) => rampa(t, 0.15, 0.25) * (1 - rampa(t, 0.65, 0.75)), fechado: 'feliz' })}
${bochechas(0.5)}
${traco('M92 162 Q124 194 166 160', 9)}${traco('M160 154 Q168 160 166 168', 7)}`, (t) => ({ r: 5 * pulso(t, 0.15, 0.75) }), centro)
      + P(brilhinho(214, 90, 12), (t) => ({ o: pulso(t, 0.3, 0.7), sx: 0.5 + 0.7 * pulso(t, 0.3, 0.7), sy: 0.5 + 0.7 * pulso(t, 0.3, 0.7) }), [214, 90]),
    pose: 0.45,
  },
  envergonhado: {
    nome: 'Envergonhado', uso: 'errou algo simples, elogio recebido', cor: 'rosa', dur: 2.4,
    desenho: (c) => rosto(c, `
${sobrancelha('M70 76 Q92 68 112 64', 9)}${sobrancelha('M144 64 Q164 68 186 76', 9)}
${olho(c, { cx: 92, cy: 112, palpebra: 0.22, baixo: 0.12, olhar: (t) => ({ tx: -8 + 3 * onda(t), ty: 8 }), piscada: (t) => piscar(t, 0.55) })}
${olho(c, { cx: 164, cy: 112, palpebra: 0.22, baixo: 0.12, olhar: (t) => ({ tx: -8 + 3 * onda(t), ty: 8 }), piscada: (t) => piscar(t, 0.55) })}
${bochechas(0.7, (t) => ({ o: 0.75 + 0.25 * onda(t, 2) }))}
${traco('M106 172 Q114 166 122 172 T138 172 T152 170', 6)}`, (t) => ({ r: 3 * onda(t), tx: 2 * onda(t) }), centro),
  },
  // ── lote 2: progresso, dificuldade, atenção e social ─────────────────────
  orgulhoso: {
    nome: 'Orgulhoso', uso: 'meta batida, nível novo, sequência longa', cor: 'amarelo', dur: 2.4,
    desenho: (c) => rosto(c, `
${sobrancelha('M66 68 Q90 54 112 64')}${sobrancelha('M144 64 Q166 54 190 68')}
${olho(c, { cx: 92, cy: 112, palpebra: 0.46, olhar: () => ({ ty: -3 }), piscada: (t) => piscar(t, 0.8) })}
${olho(c, { cx: 164, cy: 112, palpebra: 0.46, olhar: () => ({ ty: -3 }), piscada: (t) => piscar(t, 0.8) })}
${bochechas(0.4)}
${traco('M94 166 Q126 188 162 162', 9)}${traco('M156 156 Q166 160 164 170', 6)}`, (t) => ({ r: -4 - 2 * onda(t), ty: -2 }), centro)
      + P(brilhinho(206, 58, 13), (t) => ({ o: pulso(t, 0.1, 0.6), sx: 0.5 + 0.7 * pulso(t, 0.1, 0.6), sy: 0.5 + 0.7 * pulso(t, 0.1, 0.6), r: 90 * t }), [206, 58]),
  },
  rindo: {
    nome: 'Rindo muito', uso: 'piada, conquista divertida, resposta engraçada', cor: 'laranja', dur: 1.2,
    desenho: (c) => rosto(c, `
${sobrancelha('M66 64 Q90 48 112 58')}${sobrancelha('M144 58 Q166 48 190 64')}
${traco('M72 98 L104 110 L72 122', 9)}${traco('M184 98 L152 110 L184 122', 9)}
${bochechas(0.5)}
${bocaAberta(c, { y: 146, l: 114, prof: 54, curva: 10 })}`, (t) => ({ ty: -5 * Math.abs(onda(t, 3)), sy: 1 + 0.03 * Math.abs(onda(t, 3)) }), centro)
      + [[60, 124, -1], [196, 124, 1]].map(([x, y, l]) => P(gota(x, y, 0.9), (t) => ({ tx: l * 14 * t, ty: 40 * t * t, o: 1 - rampa(t, 0.6, 1) }), [x, y])).join(''),
  },
  apaixonado: {
    nome: 'Apaixonado', uso: 'adorei, favoritar, conteúdo preferido', cor: 'rosa', dur: 1.6,
    desenho: (c) => rosto(c, `
${sobrancelha('M68 66 Q90 54 112 62')}${sobrancelha('M144 62 Q166 54 188 66')}
${[92, 164].map((x) => P(coracao(x, 110, 30), (t) => ({ sx: 1 + 0.16 * pulso(t * 2 % 1, 0, 0.35), sy: 1 + 0.16 * pulso(t * 2 % 1, 0, 0.35) }), [x, 110])).join('')}
${bochechas(0.55)}
${traco('M96 164 Q128 192 160 164', 9)}`, (t) => ({ r: 4 * onda(t), ty: -3 * Math.abs(onda(t)) }), centro)
      + [[196, 70, 0], [218, 110, 0.5]].map(([x, y, a]) => P(coracao(x, y, 12), (t) => { const u = (t + a) % 1; return { ty: -40 * u, o: pulso(u, 0, 1), sx: 0.6 + 0.4 * u, sy: 0.6 + 0.4 * u }; }, [x, y])).join(''),
  },
  aliviado: {
    nome: 'Aliviado', uso: 'passou de fase difícil, prova entregue, fim do tempo', cor: 'verde', dur: 3,
    desenho: (c) => rosto(c, `
${sobrancelha('M70 74 Q92 66 112 70', 9)}${sobrancelha('M144 70 Q164 66 186 74', 9)}
${traco('M70 108 Q92 124 114 108', 8)}${traco('M142 108 Q164 124 186 108', 8)}
${bochechas(0.35)}
${P(`<ellipse cx="128" cy="174" rx="12" ry="10" fill="${BOCA}" stroke="${mix(c, '#000000', 0.35)}" stroke-width="3"/>`, (t) => ({ sx: 0.7 + 0.5 * pulso(t, 0.1, 0.6), sy: 0.6 + 0.6 * pulso(t, 0.1, 0.6) }), [128, 174])}`, (t) => ({ sy: 1 + 0.04 * pulso(t, 0, 0.45) - 0.03 * pulso(t, 0.45, 0.9) }), centro)
      + P(`<path d="M150 172 q10 -12 22 -4 q10 -14 24 -2 q14 4 6 16 q-8 10 -22 4 q-10 10 -22 0 q-14 2 -8 -14 Z" fill="#FFFFFF" opacity="0.85" stroke="#C7CED6" stroke-width="2"/>`, (t) => ({ tx: 30 * rampa(t, 0.15, 0.7), o: pulso(t, 0.15, 0.75), sx: 0.5 + 0.6 * rampa(t, 0.15, 0.7), sy: 0.5 + 0.6 * rampa(t, 0.15, 0.7) }), [150, 172]),
  },
  chorando: {
    nome: 'Chorando', uso: 'erro grave, perdeu a sequência, desistir', cor: 'azul', dur: 1.6,
    desenho: (c) => rosto(c, `
${sobrancelha('M70 84 Q92 78 112 64')}${sobrancelha('M144 64 Q164 78 186 84')}
${traco('M70 116 Q92 98 114 116', 9)}${traco('M142 116 Q164 98 186 116', 9)}
${[[92, -1], [164, 1]].map(([x]) => `<path d="M${x - 12} 120 C${x - 16} 160 ${x - 10} 200 ${x - 14} 232 L${x + 6} 232 C${x + 8} 200 ${x + 12} 160 ${x + 10} 120 Z" fill="#7CC8F2" opacity="0.85"/>`).join('')}
${P(`<path d="M96 190 Q128 146 160 190 Q128 178 96 190 Z" fill="${BOCA}" stroke="${mix(c, '#000000', 0.35)}" stroke-width="3" stroke-linejoin="round"/>`, (t) => ({ sy: 1 + 0.15 * Math.abs(onda(t, 4)) }), [128, 184])}`, (t) => ({ ty: 2 * onda(t, 6), sx: 1 + 0.01 * onda(t, 6) }), centro)
      + [80, 170].map((x, i) => [0, 1, 2].map((k) => P(gota(x + 2, 124, 0.7), (t) => { const u = (t * 2 + k / 3 + i * 0.15) % 1; return { ty: 100 * u, o: 1 - rampa(u, 0.75, 1) }; }, [x, 124])).join('')).join(''),
  },
  frustrado: {
    nome: 'Frustrado', uso: 'várias tentativas sem acerto, travou', cor: 'vermelho', dur: 1.8,
    desenho: (c) => rosto(c, `
${sobrancelha('M66 82 L112 92', 12)}${sobrancelha('M190 82 L144 92', 12)}
${olho(c, { cx: 92, cy: 116, palpebra: 0.42, angulo: 10, pupila: 0.8 })}
${olho(c, { cx: 164, cy: 116, palpebra: 0.42, angulo: -10, pupila: 0.8 })}
${P(`<ellipse cx="62" cy="160" rx="24" ry="18" fill="${mix(c, '#FFFFFF', 0.2)}" opacity="0.7"/><ellipse cx="194" cy="160" rx="24" ry="18" fill="${mix(c, '#FFFFFF', 0.2)}" opacity="0.7"/>`, (t) => ({ sx: 1 + 0.15 * pulso(t, 0, 0.5), sy: 1 + 0.15 * pulso(t, 0, 0.5) }), [128, 160])}
${traco('M112 176 Q120 170 128 176 T144 176', 7)}`, (t) => ({ sx: 1 + 0.04 * pulso(t, 0, 0.5), sy: 1 - 0.02 * pulso(t, 0, 0.5) }), centro)
      + [[52, 44], [204, 44]].map(([x, y], i) => [0, 1].map((k) => P(`<circle cx="${x}" cy="${y}" r="${9 - k * 2}" fill="#D9DEE6" stroke="#B7C0CC" stroke-width="2"/>`, (t) => { const u = (t * 2 + k / 2 + i * 0.25) % 1; return { ty: -30 * u, tx: (i ? 1 : -1) * 8 * u, o: pulso(u, 0, 1), sx: 0.6 + 0.8 * u, sy: 0.6 + 0.8 * u }; }, [x, y])).join('')).join(''),
  },
  tonto: {
    nome: 'Tonto', uso: 'muita informação, confuso, girou demais', cor: 'roxo', dur: 2,
    desenho: (c) => rosto(c, `
${sobrancelha('M68 70 Q90 62 112 72', 9)}${sobrancelha('M144 66 Q166 60 188 66', 9)}
${[92, 164].map((x, i) => `<circle cx="${x}" cy="110" r="27" fill="url(#g-olho)" stroke="${mix(c, '#000000', 0.5)}" stroke-width="2"/>${P(espiral(x, 110, 22), (t) => ({ r: (i ? -1 : 1) * 360 * t }), [x, 110])}`).join('')}
${traco('M98 176 Q108 166 118 176 T138 176 T158 176', 7)}`, (t) => ({ r: 6 * onda(t), tx: 3 * onda(t) }), centro)
      + [0, 1, 2].map((k) => P(estrela(0, 0, 9), (t) => { const a = 2 * Math.PI * (t + k / 3); return { tx: 128 + 86 * Math.cos(a), ty: 34 + 14 * Math.sin(a), o: 0.5 + 0.5 * Math.sin(a) }; }, [0, 0])).join(''),
  },
  preocupado: {
    nome: 'Preocupado', uso: 'pouco tempo, resposta incerta, aviso', cor: 'ciano', dur: 2.4,
    desenho: (c) => rosto(c, `
${sobrancelha('M70 78 Q92 74 112 62')}${sobrancelha('M144 62 Q164 74 186 78')}
${olho(c, { cx: 92, cy: 112, pupila: 0.85, olhar: (t) => ({ tx: -7 + 14 * rampa(t, 0.2, 0.3) - 14 * rampa(t, 0.6, 0.7), ty: 2 }), piscada: (t) => piscar(t, 0.85) })}
${olho(c, { cx: 164, cy: 112, pupila: 0.85, olhar: (t) => ({ tx: -7 + 14 * rampa(t, 0.2, 0.3) - 14 * rampa(t, 0.6, 0.7), ty: 2 }), piscada: (t) => piscar(t, 0.85) })}
${traco('M98 178 L108 170 L118 178 L128 170 L138 178 L148 170 L158 178', 6)}`, (t) => ({ tx: 1.5 * onda(t, 8) }), centro)
      + P(gota(198, 50, 0.9), (t) => ({ ty: 24 * rampa(t, 0.1, 0.8), o: 1 - rampa(t, 0.7, 0.95) }), [198, 50]),
  },
  concentrado: {
    nome: 'Concentrado', uso: 'resolvendo, modo foco, exercício longo', cor: 'azul', dur: 2.4,
    desenho: (c) => rosto(c, `
${sobrancelha('M68 80 Q92 76 114 82')}${sobrancelha('M142 84 Q164 78 188 82')}
${olho(c, { cx: 92, cy: 114, palpebra: 0.3, olhar: () => ({ tx: 5, ty: 7 }), piscada: (t) => piscar(t, 0.7) })}
${olho(c, { cx: 164, cy: 116, palpebra: 0.45, baixo: 0.18, olhar: () => ({ tx: -3, ty: 7 }) })}
${traco('M104 176 L150 172', 7)}${P(`<ellipse cx="152" cy="180" rx="8" ry="10" fill="url(#g-lingua)" stroke="${LINGUA[1]}" stroke-width="2"/>`, (t) => ({ tx: 3 * onda(t, 2), ty: 2 * Math.abs(onda(t, 2)) }), [150, 174])}`, (t) => ({ r: 3 + 1.5 * onda(t) }), centro),
  },
  curioso: {
    nome: 'Curioso', uso: 'antes de abrir uma lição, novidade, explorar', cor: 'verde', dur: 2.4,
    desenho: (c) => rosto(c, `
${P(sobrancelha('M62 60 Q90 40 116 54'), (t) => ({ ty: -4 * pulso(t, 0.2, 0.6) }), [90, 50])}${sobrancelha('M146 78 Q166 72 186 78', 9)}
${olho(c, { cx: 90, cy: 108, rx: 32, ry: 35, olhar: (t) => ({ tx: 6 + 4 * onda(t), ty: -6 }), piscada: (t) => piscar(t, 0.85) })}
${olho(c, { cx: 166, cy: 114, rx: 21, ry: 22, pupila: 0.8, olhar: (t) => ({ tx: 4 + 3 * onda(t), ty: -4 }), piscada: (t) => piscar(t, 0.85) })}
<ellipse cx="136" cy="178" rx="9" ry="10" fill="${BOCA}" stroke="${mix(c, '#000000', 0.35)}" stroke-width="3"/>`, (t) => ({ r: -7 + 3 * onda(t) }), centro),
  },
  esperando: {
    nome: 'Esperando', uso: 'carregando, aguardando a vez, fila', cor: 'amarelo', dur: 3,
    desenho: (c) => rosto(c, `
${sobrancelha('M70 74 Q92 70 112 74', 9)}${sobrancelha('M144 70 Q166 64 186 70', 9)}
${olho(c, { cx: 92, cy: 112, palpebra: 0.32, olhar: () => ({ tx: -6, ty: -6 }), piscada: (t) => piscar(t, 0.3, 0.1) })}
${olho(c, { cx: 164, cy: 112, palpebra: 0.32, olhar: () => ({ tx: -6, ty: -6 }), piscada: (t) => piscar(t, 0.3, 0.1) })}
${P(`<ellipse cx="128" cy="178" rx="14" ry="5" fill="${BOCA}" stroke="${mix(c, '#000000', 0.35)}" stroke-width="3"/>`, (t) => ({ sy: 1 + 2.6 * pulso(t, 0.55, 0.85), sx: 1 - 0.2 * pulso(t, 0.55, 0.85) }), [128, 176])}`, (t) => ({ sy: 1 - 0.02 * Math.abs(onda(t, 3)), r: -2 * pulso(t, 0.55, 0.85) }), centro)
      + `<circle cx="212" cy="54" r="20" fill="#FFFFFF" stroke="${TINTA}" stroke-width="4"/><path d="M212 54 V42" stroke="${TINTA}" stroke-width="3.5" stroke-linecap="round"/>${P(`<path d="M212 54 L224 54" stroke="#E5484D" stroke-width="3.5" stroke-linecap="round"/>`, (t) => ({ r: 360 * t }), [212, 54])}`,
  },
  pressa: {
    nome: 'Com pressa', uso: 'tempo acabando, contagem regressiva', cor: 'laranja', dur: 1,
    desenho: (c) => rosto(c, `
${sobrancelha('M66 62 Q90 46 112 58')}${sobrancelha('M144 58 Q166 46 190 62')}
${olho(c, { cx: 92, cy: 108, rx: 29, ry: 32, pupila: 0.55, olhar: () => ({ tx: 6 }) })}
${olho(c, { cx: 164, cy: 108, rx: 29, ry: 32, pupila: 0.55, olhar: () => ({ tx: 6 }) })}
<rect x="94" y="156" width="68" height="30" rx="12" fill="${BOCA}"/><rect x="98" y="160" width="60" height="22" rx="8" fill="#FFFFFF"/><path d="M98 171 H158 M113 160 V182 M128 160 V182 M143 160 V182" stroke="#C9CED6" stroke-width="2.5"/>`, (t) => ({ tx: 2.5 * onda(t, 6), r: 5 }), centro)
      + [[54, 70, -1], [204, 60, 1]].map(([x, y, l]) => P(gota(x, y, 0.8), (t) => ({ tx: l * 26 * t, ty: -10 * t + 30 * t * t, o: 1 - rampa(t, 0.6, 1) }), [x, y])).join('')
      + P(`<path d="M-4 110 H22 M-10 140 H18 M-4 170 H22" stroke="${TINTA}" stroke-width="5" stroke-linecap="round" opacity="0.5"/>`, (t) => ({ tx: -10 * t, o: 1 - t }), [10, 140]),
  },
  tchau: {
    nome: 'Tchau', uso: 'despedida, fim de sessão, até amanhã', cor: 'amarelo', dur: 1.6,
    desenho: (c) => rosto(c, `
${sobrancelha('M66 68 Q90 54 114 64')}${sobrancelha('M142 64 Q166 54 190 68')}
${olhoFeliz(92, 108)}${olho(c, { cx: 164, cy: 108, olhar: () => ({ tx: -2, ty: 1 }) })}
${bochechas(0.45)}
${bocaAberta(c, { y: 150, l: 96, prof: 42 })}`, (t) => ({ r: 10 * onda(t, 2) }), centro)
      + P(`<path d="M206 56 q10 8 0 16 M218 48 q16 16 0 32 M230 40 q22 24 0 48" fill="none" stroke="${TINTA}" stroke-width="4" stroke-linecap="round"/>`, (t) => ({ o: 0.3 + 0.7 * Math.abs(onda(t, 2)) }), [218, 64]),
  },
  beijo: {
    nome: 'Beijo', uso: 'elogio, agradecimento, carinho', cor: 'rosa', dur: 2,
    desenho: (c) => rosto(c, `
${sobrancelha('M68 70 Q90 58 112 66')}${sobrancelha('M144 66 Q166 58 188 70')}
${olhoFeliz(92, 110)}${olho(c, { cx: 164, cy: 110, piscada: (t) => (t < 0.5 ? 1 : 0), fechado: 'feliz' })}
${bochechas(0.6)}
${P(`<path d="M128 160 C146 156 150 172 136 176 C152 180 146 198 128 192 C122 190 122 186 128 184 C120 180 120 174 128 172 C122 168 122 162 128 160 Z" fill="#E5547A" stroke="#B83A5E" stroke-width="2.5"/>`, (t) => ({ sx: 1 + 0.15 * pulso(t, 0.1, 0.4), sy: 1 + 0.15 * pulso(t, 0.1, 0.4) }), [134, 176])}`, (t) => ({ r: 4 * pulso(t, 0.05, 0.5), ty: -3 * pulso(t, 0.05, 0.5) }), centro)
      + P(coracao(160, 176, 14), (t) => ({ tx: 50 * rampa(t, 0.3, 0.9), ty: -60 * rampa(t, 0.3, 0.9), o: pulso(t, 0.3, 0.95), sx: 0.5 + 0.7 * rampa(t, 0.3, 0.9), sy: 0.5 + 0.7 * rampa(t, 0.3, 0.9) }), [160, 176]),
  },
  silencio: {
    nome: 'Silêncio', uso: 'modo foco, não perturbe, segredo', cor: 'roxo', dur: 2.4,
    desenho: (c) => rosto(c, `
${sobrancelha('M70 74 Q92 66 112 70', 9)}${sobrancelha('M144 70 Q164 66 186 74', 9)}
${olho(c, { cx: 92, cy: 112, palpebra: 0.22, olhar: (t) => ({ tx: 7 * onda(t), ty: 2 }), piscada: (t) => piscar(t, 0.6) })}
${olho(c, { cx: 164, cy: 112, palpebra: 0.22, olhar: (t) => ({ tx: 7 * onda(t), ty: 2 }), piscada: (t) => piscar(t, 0.6) })}
<path d="M92 172 H164" stroke="${TINTA}" stroke-width="9" stroke-linecap="round"/>
${Array.from({ length: 9 }, (_, i) => `<path d="M${98 + i * 7} 164 V180" stroke="#C7CED6" stroke-width="3"/>`).join('')}
${P(`<rect x="160" y="164" width="14" height="22" rx="4" fill="#C7CED6" stroke="${TINTA}" stroke-width="3"/>`, (t) => ({ r: 14 * onda(t, 2) }), [164, 168])}`, null, centro),
  },
  doente: {
    nome: 'Doente', uso: 'pausa, descanso, cuidar da saúde', cor: 'verde', dur: 3,
    desenho: (c) => rosto(c, `
${sobrancelha('M70 82 Q92 78 112 68', 9)}${sobrancelha('M144 68 Q164 78 186 82', 9)}
${olho(c, { cx: 92, cy: 116, palpebra: 0.52, olhar: () => ({ ty: 8 }), caida: (t) => 0.2 * pulso(t, 0.3, 0.8) })}
${olho(c, { cx: 164, cy: 116, palpebra: 0.52, olhar: () => ({ ty: 8 }), caida: (t) => 0.2 * pulso(t, 0.3, 0.8) })}
${bochechas(0.3)}
${traco('M104 180 Q120 172 136 178', 7)}
<g transform="rotate(-28 140 176)"><rect x="132" y="170" width="74" height="13" rx="6.5" fill="#FFFFFF" stroke="${TINTA}" stroke-width="3"/><rect x="138" y="174" width="44" height="5" rx="2.5" fill="#E5484D"/><circle cx="136" cy="176.5" r="8" fill="#E5484D" stroke="${TINTA}" stroke-width="3"/></g>`, (t) => ({ r: 3 * onda(t), ty: 2 * Math.abs(onda(t)) }), centro)
      + P(gota(56, 64, 0.8), (t) => ({ ty: 20 * rampa(t, 0.2, 0.9), o: 1 - rampa(t, 0.8, 1) }), [56, 64]),
  },
  consertando: {
    nome: 'Consertando', uso: 'manutenção, reparo em andamento, “já volta”', cor: 'laranja', dur: 1.6, alem: [40, 0, 252, 256],
    desenho: (c) => rosto(c, `
${capacete()}
${sobrancelha('M70 74 Q92 70 112 76', 9)}${sobrancelha('M144 76 Q164 70 186 74', 9)}
${olho(c, { cx: 92, cy: 112, palpebra: 0.3, olhar: (t) => ({ tx: 8 + 2 * onda(t, 2), ty: 6 }), piscada: (t) => piscar(t, 0.86, 0.06) })}
${olho(c, { cx: 164, cy: 112, palpebra: 0.3, olhar: (t) => ({ tx: 8 + 2 * onda(t, 2), ty: 6 }), piscada: (t) => piscar(t, 0.86, 0.06) })}
${traco('M100 170 Q124 182 150 168', 7)}
${P(`<path d="M138 175 Q140 192 150 190 Q158 188 154 172 Z" fill="url(#g-lingua)" stroke="${LINGUA[1]}" stroke-width="2.5" stroke-linejoin="round"/>`, (t) => ({ tx: 2 * onda(t, 2) }), [146, 174])}`, (t) => ({ r: 3 * onda(t, 2), ty: 2 * Math.abs(onda(t, 2)) }), centro)
      + P(gota(54, 70, 0.75), (t) => ({ ty: 18 * rampa(t, 0.1, 0.8), o: 1 - rampa(t, 0.7, 1) }), [54, 70])
      + P(chaveInglesa(212, 196), (t) => ({ r: 22 * onda(t, 2) }), [212, 222])
      + [[236, 150, 0], [244, 172, 0.5]].map(([x, y, fase]) => P(brilhinho(x, y, 8), (t) => ({ o: pulso((t * 2 + fase) % 1, 0.1, 0.45), sx: 0.4 + 0.8 * pulso((t * 2 + fase) % 1, 0.1, 0.45), sy: 0.4 + 0.8 * pulso((t * 2 + fase) % 1, 0.1, 0.45) }), [x, y])).join(''),
  },
};

// ── movimentos do corpo (valem para qualquer expressão e forma) ─────────────
const ida = (t) => Math.sin(Math.PI * t) ** 2; // 0 → 1 → 0
/** Vai até (dx, dy) — e/ou se aproxima (ds > 0) ou se afasta (ds < 0) — e volta, inclinando na direção. */
const desloca = (nome, dx, dy, ds = 0) => ({
  nome, desloc: [dx, dy, ds], pivo: ds ? 'base' : 'centro',
  corpo: (t) => { const k = ida(t); return { tx: dx * k, ty: dy * k - (ds < 0 ? 30 * k : 0), sx: 1 + ds * k, sy: 1 + ds * k, r: Math.sign(dx) * 7 * onda(t) }; },
});
export const MOVIMENTOS = {
  cima: desloca('Para cima', 0, -50),
  baixo: desloca('Para baixo', 0, 40),
  esquerda: desloca('Para a esquerda', -60, 0),
  direita: desloca('Para a direita', 60, 0),
  'cima-esquerda': desloca('Diagonal ↖', -50, -40),
  'cima-direita': desloca('Diagonal ↗', 50, -40),
  'baixo-esquerda': desloca('Diagonal ↙', -50, 36),
  'baixo-direita': desloca('Diagonal ↘', 50, 36),
  frente: desloca('Para a frente', 0, 0, 0.28),
  tras: desloca('Para trás', 0, 0, -0.35),
  flutuar: { nome: 'Flutuar', corpo: (t) => ({ ty: -7 * onda(t) }) },
  pular: { nome: 'Pular', corpo: (t) => { const p = t < 0.55 ? Math.sin(Math.PI * t / 0.55) : 0, a = pulso(t, 0.53, 0.75); return { ty: -22 * p, sx: 1 + 0.06 * a, sy: 1 - 0.08 * a + 0.03 * p }; }, pivo: 'base' },
  balancar: { nome: 'Balançar', corpo: (t) => ({ r: 7 * onda(t) }), pivo: 'base' },
  dancar: { nome: 'Dançar', corpo: (t) => ({ tx: 10 * onda(t), ty: -6 * Math.abs(onda(t, 2)), r: 6 * onda(t, 2) }), pivo: 'base' },
  tremer: { nome: 'Tremer', corpo: (t) => ({ tx: 3 * onda(t, 12) }) },
  'girar-esquerda': { nome: 'Girar para a esquerda', corpo: (t) => ({ r: -360 * rampa(t, 0.2, 0.8) }), pivo: 'centro' },
  'girar-direita': { nome: 'Girar para a direita', corpo: (t) => ({ r: 360 * rampa(t, 0.2, 0.8) }), pivo: 'centro' },
  comemorar: { nome: 'Comemorar', corpo: (t) => ({ ty: -16 * Math.abs(onda(t, 2)), r: 8 * onda(t, 2), sy: 1 + 0.04 * Math.abs(onda(t, 2)) }), pivo: 'base' },
};

// ── saída ────────────────────────────────────────────────────────────────────
/**
 * Compõe os eixos: expressão (rosto) × forma (corpo) × proporção × movimento.
 */
/**
 * Movimento único ou sequência: nome, lista de nomes ou "pular,girar-direita".
 * Cada movimento ocupa um ciclo da expressão; base e centro são pivôs separados.
 */
export function sequencia(movimento) {
  if (!movimento) return null;
  const nomes = (Array.isArray(movimento) ? movimento : String(movimento).split(',')).map((n) => n.trim()).filter((n) => MOVIMENTOS[n]);
  if (!nomes.length) return null;
  const k = nomes.length;
  const seg = (t) => { const i = Math.min(k - 1, Math.floor(t * k)); return { M: MOVIMENTOS[nomes[i]], u: Math.min(1, t * k - i) }; };
  const parte = (base) => (t) => { const { M, u } = seg(t); return (M.pivo === 'base') === base ? M.corpo(u) : {}; };
  return { nomes, k, nome: nomes.map((n) => MOVIMENTOS[n].nome).join(' → '), seg, base: parte(true), centro: parte(false) };
}
/** Aplica a sequência ao conteúdo (dois grupos: pivô na base e no centro). */
const comMovimento = (conteudo, S, base, centro = [128, 124]) => P(P(conteudo, S.base, [128, base], true), S.centro, centro, true);
/** Ponto [x, y] depois do movimento m em torno do pivô (mesma ordem do transform de P). */
function moverPonto([x, y], m, [px, py]) {
  let a = px + (x - px) * m.sx, b = py + (y - py) * m.sy;
  const r = (m.r * Math.PI) / 180, c = Math.cos(r), s = Math.sin(r);
  [a, b] = [px + (a - px) * c - (b - py) * s, py + (a - px) * s + (b - py) * c];
  return [a + m.tx, b + m.ty];
}

function svg(id, e, { modo, t = 0, cor, forma: idForma = 'bolha', proporcao = 'normal', movimento } = {}) {
  seq = 0;
  const c = /^#[0-9a-f]{6}$/i.test(cor || '') ? cor.toUpperCase() : CORES[cor || e.cor] || CORES[e.cor];
  const F = forma(idForma, proporcao);
  const S = sequencia(movimento);
  const rep = S ? S.k : 1;
  CTX = { modo, t, n: Math.round(e.dur * rep * 24), dur: e.dur * rep, rep, forma: F, movimento: S };
  const A = F.ancoras;
  const rostoSvg = e.desenho(c);
  const cartao = F.tipo === 'cartao';
  let tudo = F.rostoT ? `<g transform="${rostoTransform(F)}">${rostoSvg}</g>` : rostoSvg;
  // só o rosto: contorno branco de adesivo, para ler em qualquer fundo (claro ou escuro)
  if (F.tipo === 'nenhuma') tudo = `<g filter="url(#f-adesivo)">${tudo}</g>`;
  if (cartao) { const clip = uid('clip-cartao-'); tudo = `<clipPath id="${clip}"><path d="${F.d}"/></clipPath><g clip-path="url(#${clip})">${tudo}</g>`; }
  if (S && !cartao) {
    // sombra no chão: encolhe quando o corpo sobe ou se afasta, cresce quando se aproxima
    const sombra = P(`<ellipse cx="128" cy="${f(A.base + 22)}" rx="${f(F.caixa.w * 0.38)}" ry="9" fill="#000000" opacity="0.16" filter="url(#f-suave)"/>`, (t) => {
      const { M, u } = S.seg(t);
      const m = { ...PADRAO, ...M.corpo(u) }, ds = M.desloc?.[2] || 0;
      const alto = Math.min(0, m.ty + (ds < 0 ? 30 * ida(u) : 0));
      const s = Math.max(0.3, m.sx * (1 + alto / 140));
      return { tx: m.tx, ty: (ds ? (m.sx - 1) * 30 : 0) - (ds < 0 ? 30 * ida(u) : 0) + Math.max(0, m.ty), sx: s, sy: s, o: Math.max(0.3, 1 + alto / 120) };
    }, [128, A.base + 22], true);
    tudo = sombra + comMovimento(tudo, S, A.base, A.centro);
  }

  // viewBox: forma + folga para enfeites e para o movimento
  let vista;
  if (cartao) vista = F.rect.slice(0, 4);
  else {
    const { x, y, w, h } = F.caixa;
    const justo = F.tipo === 'nenhuma';
    let x0 = justo ? x : Math.min(x, 4), y0 = justo ? y : Math.min(y, 4), x1 = justo ? x + w : Math.max(x + w, 252), y1 = justo ? y + h : Math.max(y + h, 252);
    // enfeites que passam da caixa do rosto (capacete, ferramenta) entram no enquadramento justo
    if (justo && e.alem) { x0 = Math.min(x0, e.alem[0]); y0 = Math.min(y0, e.alem[1]); x1 = Math.max(x1, e.alem[2]); y1 = Math.max(y1, e.alem[3]); }
    if (S) {
      // percorre o trajeto e junta por onde a forma passa
      // contorno real da forma + a área dos enfeites (brilhos, lágrima, "?")
      const cantos = [...F.pol.filter((_, i) => i % 2 === 0), [24, 24], [232, 24], [24, 232], [232, 232]];
      const amostras = 48 * S.k;
      for (let i = 0; i <= amostras; i++) {
        const { M, u } = S.seg(i / amostras);
        const m = { ...PADRAO, ...M.corpo(u) };
        for (const p of cantos) {
          const [a, b] = moverPonto(p, m, M.pivo === 'base' ? [128, A.base] : A.centro);
          x0 = Math.min(x0, a); x1 = Math.max(x1, a); y0 = Math.min(y0, b); y1 = Math.max(y1, b);
        }
      }
      y1 += 30; // sombra no chão
    }
    const m = 0.05 * Math.max(x1 - x0, y1 - y0) + (S ? 12 : 12);
    x0 -= m; y0 -= m; x1 += m; y1 += m + 6;
    const lado = Math.max(x1 - x0, y1 - y0);
    vista = [(x0 + x1 - lado) / 2, (y0 + y1 - lado) / 2, lado, lado];
  }
  const [vx, vy, vw, vh] = vista;
  const titulo = [e.nome, proporcao !== 'normal' && PROPORCOES[proporcao]?.nome, S?.nome].filter(Boolean).join(' · ');
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${f(vx)} ${f(vy)} ${f(vw)} ${f(vh)}" width="${W}" height="${Math.round((W * vh) / vw)}" role="img" aria-label="${titulo}">
<title>${titulo}</title>
${defsBase(c)}
${tudo}
</svg>
`;
}

/**
 * Opções: { cor, forma, proporcao, movimento }. `cor`: nome de CORES, `#RRGGBB` (por exemplo a cor
 * do tema ativo, de coresDosTemas) ou vazio para a cor da expressão. `movimento`: um nome ou uma sequência.
 */
export function estatico(id, op = {}) { const e = EXPRESSOES[id]; return svg(id, e, { ...op, modo: 'quadro', t: op.t ?? e.pose ?? 0 }); }
export function animado(id, op = {}) { return svg(id, EXPRESSOES[id], { ...op, modo: 'animado' }); }
export function quadros(id, op = {}, fps = 12) {
  const e = EXPRESSOES[id];
  const n = Math.round(e.dur * fps);
  return Array.from({ length: n }, (_, k) => svg(id, e, { ...op, modo: 'quadro', t: k / n }));
}

/**
 * Embute um mascote dentro de outro SVG, centralizado na caixa (x, y, largura, altura);
 * os ids ganham `prefixo` para não colidirem com os de outros mascotes.
 */
export function embutir(svgTexto, prefixo, x, y, largura, altura = largura) {
  const [vx, vy, vw, vh] = svgTexto.match(/viewBox="([^"]+)"/)[1].split(' ').map(Number);
  const k = Math.min(largura / vw, altura / vh), w = vw * k, h = vh * k;
  const corpo = svgTexto.replace(/^[\s\S]*?<\/title>\n?/, '').replace(/<\/svg>\s*$/, '')
    .replace(/id="([^"]+)"/g, `id="${prefixo}$1"`).replace(/url\(#([^)]+)\)/g, `url(#${prefixo}$1)`);
  return `<svg x="${f(x + (largura - w) / 2)}" y="${f(y + (altura - h) / 2)}" width="${f(w)}" height="${f(h)}" viewBox="${vx} ${vy} ${vw} ${vh}">${corpo}</svg>`;
}
