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

// ── gerado: o nome deboaz em contorno (Fredoka Medium) ──
export const NOME = {"de":"M837.3 787.7L837.3 787.7Q813.4 787.7 792.0 777.8Q770.7 767.9 754.6 750.5Q738.5 733.1 729.1 709.4Q719.8 685.8 719.8 658.7L719.8 658.7Q719.8 632.7 729.1 609.6Q738.5 586.4 754.9 569.0Q771.2 551.6 792.6 541.7Q813.9 531.8 837.3 531.8L837.3 531.8Q862.2 531.8 882 541.7Q901.8 551.6 915.5 569.0Q929.3 586.4 936.6 609.6Q943.9 632.7 944.4 659.2L944.4 659.2Q944.4 685.8 937.4 708.9Q930.4 732.0 916.6 749.7Q902.8 767.4 882.8 777.5Q862.8 787.7 837.3 787.7ZM845.6 726.8L845.6 726.8Q863.8 726.8 878.1 718.3Q892.4 709.7 901.0 694.6Q909.6 679.5 909.6 660.3L909.6 660.3Q909.6 640.5 901.0 625.4Q892.4 610.4 878.1 601.5Q863.8 592.7 845.6 592.7L845.6 592.7Q827.4 592.7 812.8 601.3Q798.3 609.8 789.7 625.2Q781.1 640.5 781.1 660.3L781.1 660.3Q781.1 679.5 789.7 694.6Q798.3 709.7 813.1 718.3Q827.9 726.8 845.6 726.8ZM941.8 787.7L941.8 787.7Q927.2 787.7 920.5 782.7Q913.7 777.8 911.6 766.9L911.6 766.9L911.6 446.6Q911.6 437.7 912.9 430.7Q914.2 423.7 920.7 419.3Q927.2 414.8 941.3 414.8L941.3 414.8Q956.4 414.8 962.6 419Q968.8 423.2 970.7 430.2Q972.5 437.2 972.5 446.0L972.5 446.0L972.5 756.0Q972.5 764.8 970.9 772.1Q969.4 779.4 962.9 783.5Q956.4 787.7 941.8 787.7ZM1149.8 791.3L1149.8 791.3Q1116 791.3 1090.8 780.4Q1065.6 769.5 1048.7 750.5Q1031.8 731.5 1023.2 707.9Q1014.6 684.2 1014.6 658.7L1014.6 658.7Q1014.6 621.3 1031.8 592.2Q1048.9 563.0 1079.3 546.4Q1109.8 529.8 1149.3 529.8L1149.3 529.8Q1177.4 529.8 1198.7 538.6Q1220 547.4 1234.3 561.7Q1248.6 576.0 1256.1 593.2Q1263.7 610.4 1263.7 626.5L1263.7 626.5Q1263.7 654.0 1250.4 666.5Q1237.2 679 1217.4 679L1217.4 679L1080.1 679Q1080.6 696.7 1091.3 709.7Q1102.0 722.7 1118.3 729.7Q1134.7 736.7 1151.9 736.7L1151.9 736.7Q1164.9 736.7 1175.0 735.2Q1185.2 733.6 1192.4 731.3Q1199.7 728.9 1205.2 726.3Q1210.6 723.7 1215.3 721.6Q1220 719.6 1224.2 719.0L1224.2 719.0Q1229.9 718.5 1235.6 721.6Q1241.3 724.8 1245.0 732.6L1245.0 732.6Q1248.1 737.8 1249.4 742.2Q1250.7 746.6 1250.7 750.8L1250.7 750.8Q1250.7 761.2 1238.2 770.3Q1225.7 779.4 1203.1 785.3Q1180.5 791.3 1149.8 791.3ZM1080.1 642.1L1080.1 642.1L1186.7 642.1Q1195.6 642.1 1200.2 639.2Q1204.9 636.4 1204.9 627L1204.9 627Q1204.9 615.0 1197.6 605.4Q1190.4 595.8 1177.9 590.3Q1165.4 584.9 1148.8 584.9L1148.8 584.9Q1129.5 584.9 1113.9 592.4Q1098.3 600.0 1089.2 613.0Q1080.1 626.0 1080.1 642.1Z","boaz":"M1440.0 787.7L1440.0 787.7Q1414.5 787.7 1394.5 777.5Q1374.4 767.4 1360.7 749.7Q1346.9 732.0 1339.9 708.9Q1332.8 685.8 1332.8 659.2L1332.8 659.2Q1333.4 632.7 1340.6 609.6Q1347.9 586.4 1361.7 569.0Q1375.5 551.6 1395.2 541.7Q1415 531.8 1440.0 531.8L1440.0 531.8Q1463.9 531.8 1484.9 541.7Q1506 551.6 1522.4 569.0Q1538.8 586.4 1548.1 609.6Q1557.5 632.7 1557.5 658.7L1557.5 658.7Q1557.5 685.8 1548.1 709.4Q1538.8 733.1 1522.6 750.5Q1506.5 767.9 1485.2 777.8Q1463.9 787.7 1440.0 787.7ZM1431.6 726.8L1431.6 726.8Q1449.8 726.8 1464.4 718.3Q1479.0 709.7 1487.5 694.6Q1496.1 679.5 1496.1 660.3L1496.1 660.3Q1496.1 640.5 1487.5 625.2Q1479.0 609.8 1464.4 601.3Q1449.8 592.7 1431.6 592.7L1431.6 592.7Q1414.0 592.7 1399.4 601.5Q1384.8 610.4 1376.3 625.4Q1367.7 640.5 1367.7 660.3L1367.7 660.3Q1367.7 679.5 1376.3 694.6Q1384.8 709.7 1399.4 718.3Q1414.0 726.8 1431.6 726.8ZM1335.4 788.2L1335.4 788.2Q1321.4 788.2 1314.6 783.8Q1307.9 779.4 1306.3 772.3Q1304.8 765.3 1304.8 756.5L1304.8 756.5L1304.8 446.6Q1304.8 437.7 1306.3 430.7Q1307.9 423.7 1314.6 419.5Q1321.4 415.4 1335.4 415.4L1335.4 415.4Q1350 415.4 1356.2 419.5Q1362.5 423.7 1364.0 431.0Q1365.6 438.2 1365.6 446.6L1365.6 446.6L1365.6 766.9Q1363.5 778.3 1357.0 783.3Q1350.5 788.2 1335.4 788.2ZM1723.9 791.3L1723.9 791.3Q1698.9 791.3 1675.3 781.7Q1651.6 772.1 1632.6 754.4Q1613.6 736.7 1602.5 712.8Q1591.3 688.9 1591.3 660.3L1591.3 660.3Q1591.3 632.7 1601.7 608.8Q1612.1 584.9 1630.3 566.7Q1648.5 548.5 1672.1 538.3Q1695.8 528.2 1722.8 528.2L1722.8 528.2Q1759.2 528.2 1788.9 545.6Q1818.5 563.0 1836.2 592.7Q1853.9 622.3 1853.9 659.8L1853.9 659.8Q1853.9 689.4 1843.0 713.6Q1832.0 737.8 1813.3 755.2Q1794.6 772.6 1771.5 782.0Q1748.3 791.3 1723.9 791.3ZM1722.8 730.0L1722.8 730.0Q1740 730.0 1755.9 721.4Q1771.7 712.8 1781.9 696.7Q1792 680.6 1792 658.7L1792 658.7Q1792 638.4 1782.9 622.8Q1773.8 607.2 1758.2 598.4Q1742.6 589.6 1722.3 589.6L1722.3 589.6Q1703.1 589.6 1687.5 598.7Q1671.9 607.8 1662.8 623.6Q1653.7 639.5 1653.7 659.2L1653.7 659.2Q1653.7 681.1 1663.6 696.9Q1673.4 712.8 1689.3 721.4Q1705.2 730.0 1722.8 730.0ZM2108.2 787.2L2108.2 787.2Q2093.1 787.2 2085.8 781.4Q2078.5 775.7 2078.5 765.8L2078.5 765.8L2082.2 748.7Q2078 755.4 2068.1 764.5Q2058.2 773.6 2042.1 780.7Q2026 787.7 2003.1 787.7L2003.1 787.7Q1978.7 787.7 1957.1 777.5Q1935.5 767.4 1919.1 749.5Q1902.8 731.5 1893.1 708.4Q1883.5 685.2 1883.5 658.7L1883.5 658.7Q1883.5 633.2 1893.1 610.6Q1902.8 588 1919.4 570.3Q1936.0 552.6 1957.6 542.5Q1979.2 532.4 2003.1 532.4L2003.1 532.4Q2024.4 532.4 2039.3 538.1Q2054.1 543.8 2064.5 552.4Q2074.9 561.0 2081.6 570.3L2081.6 570.3L2078.5 555.2Q2078.5 543.8 2085.8 537.8Q2093.1 531.8 2108.7 531.8L2108.7 531.8Q2123.2 531.8 2129.5 536.3Q2135.7 540.7 2137.3 548.2Q2138.8 555.8 2138.8 565.1L2138.8 565.1L2138.8 755.4Q2138.8 764.3 2137.3 771.6Q2135.7 778.8 2129.2 783Q2122.7 787.2 2108.2 787.2ZM2012.5 726.8L2012.5 726.8Q2030.2 726.8 2044.2 718Q2058.2 709.2 2066.8 693.8Q2075.4 678.5 2075.4 659.8L2075.4 659.8Q2075.4 639.5 2066.6 624.4Q2057.7 609.3 2043.4 600.7Q2029.1 592.2 2011.4 592.2L2011.4 592.2Q1992.2 592.2 1977.1 601.3Q1962.0 610.4 1953.5 625.7Q1944.9 641.0 1944.9 660.3L1944.9 660.3Q1944.9 679 1953.7 694.1Q1962.6 709.2 1977.9 718Q1993.2 726.8 2012.5 726.8ZM2395.7 788.2L2210.1 788.2Q2196.0 788.2 2187.7 779.6Q2179.4 771.0 2179.4 757.5L2179.4 757.5Q2179.4 750.2 2181.7 744.5Q2184.1 738.8 2188.8 734.1L2188.8 734.1L2324.5 594.2L2222.0 594.2Q2213.7 594.2 2206.4 592.4Q2199.2 590.6 2194.7 584.1Q2190.3 577.6 2190.3 563.0L2190.3 563.0Q2190.3 548.5 2194.7 542.0Q2199.2 535.5 2206.4 533.7Q2213.7 531.8 2222.0 531.8L2222.0 531.8L2390 531.8Q2403.5 531.8 2412.1 540.2Q2420.7 548.5 2420.7 562L2420.7 562Q2420.7 568.8 2418.3 574.7Q2416 580.7 2412.4 584.4L2412.4 584.4L2278.2 725.8L2396.2 725.8Q2405.1 725.8 2412.1 727.6Q2419.1 729.4 2423.3 736.2Q2427.4 743.0 2427.4 757.5L2427.4 757.5Q2427.4 771.6 2423.0 778.3Q2418.6 785.1 2411.6 786.6Q2404.6 788.2 2395.7 788.2L2395.7 788.2Z","largura":2428};
export const marca = criarMarca(NOME);
