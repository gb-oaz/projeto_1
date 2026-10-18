// =============================================================================
// boaz · núcleo da marca deboaz: símbolo, logo e ícone, em qualquer cor
//
// O símbolo é um b e um d: uma barriga redonda com uma antena de cada lado
// (haste de 116, barriga de raio 270, topo da haste em elipse 58 × 40), separadas
// do anel por um vão de 22 que acompanha a curva dele.
//   longo  hastes longas — landing page e hub (público adulto e profissional)
//   curto  hastes curtas — quest (crianças, jovens, estudantes e funcionários)
//
// O oficial é monocromático (preto no claro, branco no escuro). Num tema de cor,
// o anel fica na terciária, a antena do b na primária e a do d na secundária, e
// o logo usa símbolo na terciária, "de" na secundária e "boaz" na primária
// (coresDaMarca). O nome sai em contorno da fonte da marca (Fredoka Medium):
// o gerador mede os glifos e entrega os caminhos em `nome` (criarMarca).
//
// Este módulo não usa nada do Node, então também roda no navegador. O gerador
// (scripts/gerar.mjs) grava os arquivos oficiais em identidade/logos e
// identidade/icones e copia o núcleo, já com o nome, para plataformas/web/marca.mjs.
//
// Uso na tela (web):
//   import { marca, FRENTES } from '../design/marca.mjs';
//   import temas from '../design/marca-temas.json';
//   const c = temas[paleta][modo];            // modo: 'claro' | 'escuro'
//   el.innerHTML = marca.logo(FRENTES.hub, c);
// Regras de uso: 1_Requisitos/90_identidade-visual.html.
// =============================================================================

/** Variação do símbolo: onde a haste começa (0 = longa). */
export const VARIANTES = {
  longo: { nome: 'Hastes longas', topo: 0 },
  curto: { nome: 'Hastes curtas', topo: 180 },
};
/** Variação de cada frente do produto. */
export const FRENTES = { landing: 'longo', hub: 'longo', quest: 'curto' };

const HW = 116, R = 270, r = R - HW, CX = 270, CY = 523, CAP = 40, LARG = 540, BASE = 793;
// vão entre as antenas e o anel: a base de cada antena é um arco concêntrico ao anel, VAO além dele
const VAO = 22, RV = R + VAO;
const n = (v) => Math.round(v * 100) / 100;
const yVao = (x) => CY - Math.sqrt(RV * RV - (x - CX) ** 2);
const haste = (x, topo) => `M${x} ${topo + CAP}A58 ${CAP} 0 0 1 ${x + HW} ${topo + CAP}V${n(yVao(x + HW))}A${RV} ${RV} 0 0 0 ${x} ${n(yVao(x))}Z`;
// sentido: 1 = horário (como as hastes), 0 = anti-horário (abre o miolo com fill-rule nonzero)
const circ = (raio, sentido) => `M${CX + raio} ${CY}A${raio} ${raio} 0 1 ${sentido} ${CX - raio} ${CY}A${raio} ${raio} 0 1 ${sentido} ${CX + raio} ${CY}Z`;
const anelCheio = () => circ(R, 1) + circ(r, 0);
const hastes = (variante) => { const topo = VARIANTES[variante].topo; return haste(0, topo) + haste(LARG - HW, topo); };
/** Peças do símbolo separadas (para o app desenhar cada uma na sua cor): { anel, b, d, y, altura }. */
export const geometria = (variante = 'longo') => {
  const topo = VARIANTES[variante].topo;
  return { anel: anelCheio(), b: haste(0, topo), d: haste(LARG - HW, topo), y: topo, altura: BASE - topo, largura: LARG };
};
/** Caminho do símbolo, no quadro 0..540 × topo..793. */
export const caminhoSimbolo = (variante = 'longo') => hastes(variante) + anelCheio();
const caixa = (variante) => { const topo = VARIANTES[variante].topo; return { y: topo, h: BASE - topo }; };
const esc = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');
const cab = (vb, titulo, extra = '') => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${vb}"${extra} role="img" aria-label="${esc(titulo)}">`;

const lum = (h) => {
  const c = [1, 3, 5].map((i) => parseInt(h.slice(i, i + 2), 16) / 255).map((v) => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4));
  return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2];
};
/** Razão de contraste entre duas cores #RRGGBB. */
export const contraste = (a, b) => { const x = lum(a), y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };

/**
 * Funções de desenho. `nome` = { de, boaz, x, largura }: caminhos do nome em
 * contorno, medidos pelo gerador no mesmo quadro do símbolo (baseline em 783).
 */
/**
 * Cores das peças do símbolo: { anel, b, d } (b = antena da esquerda, d = da direita).
 * Aceita uma cor só (string), { simbolo } (tudo nela) ou as peças separadas.
 */
export const pecas = (cor = 'currentColor') => {
  if (typeof cor === 'string') return { anel: cor, b: cor, d: cor };
  const base = cor.simbolo ?? 'currentColor';
  return { anel: cor.anel ?? base, b: cor.b ?? base, d: cor.d ?? base };
};
const desenho = (variante, cor) => {
  const p = pecas(cor), topo = VARIANTES[variante].topo;
  return `<path fill="${p.anel}" d="${anelCheio()}"/><path fill="${p.b}" d="${haste(0, topo)}"/><path fill="${p.d}" d="${haste(LARG - HW, topo)}"/>`;
};

export function criarMarca(nome) {
  /** Símbolo; cor = uma cor ou { anel, b, d }. */
  const simbolo = (variante = 'longo', cor = 'currentColor', titulo = 'deboaz') => {
    const { y, h } = caixa(variante);
    return `${cab(`0 ${y} ${LARG} ${h}`, titulo)}${desenho(variante, cor)}</svg>`;
  };
  /** Logo principal: símbolo + nome. cores = { anel, b, d, de, boaz } (ou { simbolo, de, boaz }; padrão: currentColor). */
  const logo = (variante = 'longo', cores = {}) => {
    const { y, h } = caixa(variante);
    const c = { simbolo: 'currentColor', de: 'currentColor', boaz: 'currentColor', ...cores };
    return `${cab(`0 ${y} ${n(nome.largura)} ${h}`, 'deboaz')}${desenho(variante, c)}` +
      `<path fill="${c.de}" d="${nome.de}"/><path fill="${c.boaz}" d="${nome.boaz}"/></svg>`;
  };
  /**
   * Ícone quadrado de 1024: fundo liso e o símbolo centralizado.
   * area = altura do símbolo (660 cabe na área segura de 66 %); raio = canto do quadrado.
   * fundo null = só o símbolo, com fundo transparente (frente do ícone adaptativo).
   */
  const icone = (variante = 'longo', { fundo = '#000000', cor = '#FFFFFF', area = 660, raio = 0, titulo = 'deboaz' } = {}) => {
    const { y, h } = caixa(variante), k = area / h, ox = (1024 - LARG * k) / 2, oy = (1024 - h * k) / 2 - y * k;
    return `${cab('0 0 1024 1024', titulo, ' width="1024" height="1024"')}` +
      (fundo ? `<rect width="1024" height="1024"${raio ? ` rx="${raio}"` : ''} fill="${fundo}"/>` : '') +
      `<g transform="translate(${n(ox)} ${n(oy)}) scale(${n(k * 1e4) / 1e4})">${desenho(variante, cor)}</g></svg>`;
  };
  /** Favicon: quadrado de cantos arredondados, símbolo maior para ler em 16 px. */
  const favicon = (variante = 'longo', { fundo = '#000000', cor = '#FFFFFF' } = {}) => icone(variante, { fundo, cor, area: 800, raio: 224 });
  // ── animações (SMIL: tocam também dentro de <img>) ──────────────────────────
  // O anel em traço: círculo de raio 212 com traço de 116 (= barriga de 154 a 270).
  // As antenas são as mesmas do símbolo parado, reveladas ou movidas por recorte.
  const RM = (R + r) / 2, VOLTA = 2 * Math.PI * RM;
  // ids únicos por desenho: vários SVGs embutidos na mesma página não podem dividir recortes
  let seq = 0;
  const antena = (variante, i) => haste(i ? LARG - HW : 0, VARIANTES[variante].topo);
  /**
   * Abertura: o anel se desenha a partir de baixo, as antenas brotam do vão e sobem
   * e, com nome, "de" e "boaz" entram em seguida. Toca uma vez (≈ 2 s) e para no
   * logo. cores = { simbolo, de, boaz }.
   */
  const abertura = (variante = 'longo', cores = {}, { nome: comNome = true } = {}) => {
    const c = { simbolo: 'currentColor', de: 'currentColor', boaz: 'currentColor', ...cores }, p = pecas(c);
    const { y, h } = caixa(variante), largura = comNome ? n(nome.largura) : LARG;
    const fz = 'fill="freeze" calcMode="spline" keyTimes="0;1" keySplines="0.3 0 0.2 1"';
    const anel = `<circle cx="${CX}" cy="${CY}" r="${RM}" fill="none" stroke="${p.anel}" stroke-width="${HW}" stroke-dasharray="${n(VOLTA)}" stroke-dashoffset="${n(VOLTA)}" transform="rotate(90 ${CX} ${CY})">` +
      `<animate attributeName="stroke-dashoffset" from="${n(VOLTA)}" to="0" begin="0s" dur="0.8s" ${fz}/></circle>`;
    const baixo = Math.ceil(yVao(0)), id = `marca-ab-${variante}-${++seq}`;
    const defs = [0, 1].map((i) => `<clipPath id="${id}-${i}"><rect x="0" y="${baixo}" width="${LARG}" height="${BASE}">` +
      `<animate attributeName="y" from="${baixo}" to="${y}" begin="${0.6 + i * 0.15}s" dur="0.55s" ${fz}/></rect></clipPath>`).join('');
    const antenas = [0, 1].map((i) => `<path fill="${i ? p.d : p.b}" d="${antena(variante, i)}" clip-path="url(#${id}-${i})"/>`).join('');
    const entra = (d, cor, t) => `<path fill="${cor}" d="${d}" opacity="0"><animate attributeName="opacity" from="0" to="1" begin="${t}s" dur="0.35s" fill="freeze"/>` +
      `<animateTransform attributeName="transform" type="translate" from="0 60" to="0 0" begin="${t}s" dur="0.45s" ${fz}/></path>`;
    return `${cab(`0 ${y} ${largura} ${h}`, 'deboaz')}<defs>${defs}</defs>${anel}${antenas}` +
      (comNome ? entra(nome.de, c.de, 1.2) + entra(nome.boaz, c.boaz, 1.4) : '') + '</svg>';
  };
  /**
   * Carregamento: um arco corre pelo anel sobre um trilho claro e as antenas descem
   * para dentro do vão e voltam, alternadas. Laço de 1,6 s. cor = uma cor ou { anel, b, d }.
   */
  const carregamento = (variante = 'longo', cor = 'currentColor') => {
    const p = pecas(cor), { y, h } = caixa(variante), arco = n(VOLTA * 0.3), desce = 110, vao = `marca-vao-${variante}-${++seq}`;
    const anel = `<g fill="none" stroke="${p.anel}" stroke-width="${HW}"><circle cx="${CX}" cy="${CY}" r="${RM}" opacity="0.18"/>` +
      `<circle cx="${CX}" cy="${CY}" r="${RM}" stroke-dasharray="${arco} ${n(VOLTA - arco)}">` +
      `<animateTransform attributeName="transform" type="rotate" from="90 ${CX} ${CY}" to="450 ${CX} ${CY}" dur="1.6s" repeatCount="indefinite"/></circle></g>`;
    // a máscara esconde o que entra no vão: as antenas somem ao descer
    const mascara = `<mask id="${vao}"><rect x="0" y="${y}" width="${LARG}" height="${h}" fill="#fff"/><circle cx="${CX}" cy="${CY}" r="${RV}" fill="#000"/></mask>`;
    const antenas = [0, 1].map((i) => `<g><animateTransform attributeName="transform" type="translate" values="0 0;0 ${desce};0 0" keyTimes="0;0.5;1" calcMode="spline" keySplines="0.4 0 0.2 1;0.4 0 0.2 1" begin="${-i * 0.8}s" dur="1.6s" repeatCount="indefinite"/>` +
      `<path fill="${i ? p.d : p.b}" d="${antena(variante, i)}"/></g>`).join('');
    return `${cab(`0 ${y} ${LARG} ${h}`, 'Carregando')}<defs>${mascara}</defs>${anel}<g mask="url(#${vao})">${antenas}</g></svg>`;
  };
  /**
   * Linha do tempo de abrir e/ou fechar o logo. Abrir: o anel se desenha, as antenas
   * brotam do vão e o nome entra. Fechar (o inverso): o nome sai, as antenas
   * recolhem para o vão e o anel se apaga. Devolve os tempos (s) de cada peça.
   */
  const LINHA = (abrir, fechar, laco) => {
    const t = { abrir, fechar, laco };
    const fimAbrir = abrir ? 1.8 : 0, F = fimAbrir + (abrir && fechar ? (laco ? 1.6 : 0.4) : 0.15);
    t.anel = abrir ? [0, 0.8] : null;                 // desenha
    t.antena = (i) => abrir ? [0.6 + i * 0.15, 0.6 + i * 0.15 + 0.55] : null;
    t.nome = (i) => abrir ? [1.2 + i * 0.2, 1.2 + i * 0.2 + 0.45] : null;
    t.nomeSai = (i) => fechar ? [F + (1 - i) * 0.12, F + (1 - i) * 0.12 + 0.35] : null;
    t.antenaSai = (i) => fechar ? [F + 0.35 + (1 - i) * 0.15, F + 0.35 + (1 - i) * 0.15 + 0.5] : null;
    t.anelSai = fechar ? [F + 1.0, F + 1.8] : null;  // apaga, seguindo o mesmo sentido
    t.fim = fechar ? F + 1.8 : fimAbrir;
    t.total = laco ? t.fim + 0.6 : t.fim;
    return t;
  };
  // animação por pontos [[tempo, valor], ...]: em laço repete o ciclo inteiro; senão para no último valor
  const pontos = (attr, pts, t, tipo = '') => {
    const T = t.total, ks = pts.map(([x]) => n(Math.min(1, Math.max(0, x / T))));
    const vs = pts.map(([, v]) => v).join(';');
    const sp = Array(pts.length - 1).fill('0.3 0 0.2 1').join(';');
    const tag = tipo ? `animateTransform attributeName="transform" type="${tipo}"` : `animate attributeName="${attr}"`;
    return `<${tag} values="${vs}" keyTimes="${ks.join(';')}" calcMode="spline" keySplines="${sp}" dur="${n(T)}s" ${t.laco ? 'repeatCount="indefinite"' : 'fill="freeze"'}/>`;
  };
  // monta a sequência de valores de uma peça: oculto → visível (abrir) → oculto (fechar)
  const trilha = (t, ab, fe, oculto, visivel, oculto2 = oculto) => {
    const p = [[0, ab ? oculto : visivel]];
    if (ab) p.push([ab[0], oculto], [ab[1], visivel]);
    if (fe) p.push([fe[0], visivel], [fe[1], oculto2]);
    p.push([t.total, fe ? oculto2 : visivel]);
    return p.filter((x, i, a) => i === 0 || x[0] > a[i - 1][0] || (x[0] === a[i - 1][0] && x[1] !== a[i - 1][1]));
  };
  const sequenciaLogo = (variante, cores, comNome, t) => {
    const c = { simbolo: 'currentColor', de: 'currentColor', boaz: 'currentColor', ...cores }, p = pecas(c);
    const { y, h } = caixa(variante), largura = comNome ? n(nome.largura) : LARG, id = `marca-sq-${variante}-${++seq}`;
    const V = n(VOLTA), baixo = Math.ceil(yVao(0));
    const anelPts = trilha(t, t.anel, t.anelSai, V, 0, -V);
    const anel = `<circle cx="${CX}" cy="${CY}" r="${RM}" fill="none" stroke="${p.anel}" stroke-width="${HW}" stroke-dasharray="${V}" stroke-dashoffset="${anelPts[0][1]}" transform="rotate(90 ${CX} ${CY})">${pontos('stroke-dashoffset', anelPts, t)}</circle>`;
    const defs = [0, 1].map((i) => { const pts = trilha(t, t.antena(i), t.antenaSai(i), baixo, y);
      return `<clipPath id="${id}-${i}"><rect x="0" y="${pts[0][1]}" width="${LARG}" height="${BASE}">${pontos('y', pts, t)}</rect></clipPath>`; }).join('');
    const antenas = [0, 1].map((i) => `<path fill="${i ? p.d : p.b}" d="${antena(variante, i)}" clip-path="url(#${id}-${i})"/>`).join('');
    const texto = (d, cor, i) => { const op = trilha(t, t.nome(i), t.nomeSai(i), 0, 1), tr = trilha(t, t.nome(i), t.nomeSai(i), '0 60', '0 0', '0 60');
      return `<path fill="${cor}" d="${d}" opacity="${op[0][1]}" transform="translate(${tr[0][1]})">${pontos('opacity', op, t)}${pontos('', tr, t, 'translate')}</path>`; };
    return `${cab(`0 ${y} ${largura} ${h}`, 'deboaz')}<defs>${defs}</defs>${anel}${antenas}` +
      (comNome ? texto(nome.de, c.de, 0) + texto(nome.boaz, c.boaz, 1) : '') + '</svg>';
  };
  /** Fechamento: o inverso da abertura — o nome sai, as antenas recolhem para o vão e o anel se apaga. Toca uma vez (≈ 2 s). */
  const fechamento = (variante = 'longo', cores = {}, { nome: comNome = true } = {}) => sequenciaLogo(variante, cores, comNome, LINHA(false, true, false));
  /** Ciclo: abertura, pausa com o logo formado, fechamento e pausa vazia, em laço (≈ 6,6 s). */
  const ciclo = (variante = 'longo', cores = {}, { nome: comNome = true } = {}) => sequenciaLogo(variante, cores, comNome, LINHA(true, true, true));
  return { simbolo, logo, icone, favicon, abertura, carregamento, fechamento, ciclo };
}

/**
 * Cores da marca em cada tema de cor e modo, a partir de tokens/cores.json:
 * { <tema>: { nome, mono, claro: { simbolo, de, boaz, iconeFundo, iconeCor }, escuro: {…} } }.
 * Tema monocromático: tudo em on-surface e o ícone oficial (fundo preto, símbolo branco).
 * Tema de cor: anel tertiary, antena do b e "boaz" primary, antena do d e "de" secondary;
 * ícone em primary com on-primary.
 */
export function coresDaMarca(cores, marca) {
  const defs = marca.cores.temas, padrao = marca.cores.tema_padrao, saida = {};
  for (const [id, t] of Object.entries(cores.temas)) {
    const ids = typeof padrao === 'object' && id === `${padrao.claro}-${padrao.escuro}` ? [padrao.claro, padrao.escuro] : [id];
    const mono = ids.some((i) => defs[i]?.variante === 'monochrome');
    const modo = (esq) => {
      const v = (k) => esq[k].$value.toUpperCase();
      return mono
        ? { anel: v('on-surface'), b: v('on-surface'), d: v('on-surface'), de: v('on-surface'), boaz: v('on-surface'), iconeFundo: '#000000', iconeCor: '#FFFFFF' }
        : { anel: v('tertiary'), b: v('primary'), d: v('secondary'), de: v('secondary'), boaz: v('primary'), iconeFundo: v('primary'), iconeCor: v('on-primary') };
    };
    saida[id] = { nome: ids.map((i) => defs[i]?.nome ?? i).join(' / '), mono, claro: modo(t.md.sys.color.light), escuro: modo(t.md.sys.color.dark) };
  }
  return saida;
}
