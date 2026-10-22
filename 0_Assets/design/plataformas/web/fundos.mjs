import { marca, geometria, pecas } from './marca.mjs';
import * as mascotes from './mascotes.mjs';
// =============================================================================
// boaz · fundos da marca deboaz: seis conceitos feitos de peças do símbolo e de
// mascotes, em qualquer formato (16:9, 9:16, 1:1 ou outro)
//
//   estampa    rabiscos espalhados: mascotes, números, sinais e o símbolo, pequenos e girados, num tom discreto   fixos do quest, páginas, login, cartão do aluno
//   anel       anel gigante cortado pela borda, na forma do mascote   cabeçalho do hub, capa de sala, banner
//   antenas    antenas em ritmo; os mascotes sobem e descem com elas   rodapé, faixa de progresso, espera
//   ondas      linhas concêntricas na forma do mascote       formulários e configurações do hub
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
  estampa: { nome: 'Estampa', uso: 'tela Fixos do quest, páginas, tela de login, verso do cartão do aluno', sortido: true, mascote: { expressao: '', forma: '' } },
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
  const { estatico, animado, embutir, FORMAS, forma } = mascotes;
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
  // logo inteiro (símbolo + nome) centrado em cx, cy, com a altura do símbolo = alt; anima a abertura (toca uma vez)
  const logoInteiro = (cx, cy, alt, cor, anim, v = 'longo') => {
    const svg = anim === 'abertura' ? marca.abertura(v, cor) : marca.logo(v, cor);
    const [, , vw, vh] = svg.match(/viewBox="([^"]+)"/)[1].split(' ').map(Number), larg = alt * vw / vh;
    return encaixa(svg, cx - larg / 2, cy - alt / 2, larg, alt, 1);
  };
  // brilho que passa pelas cores dos temas em laço: fica `fica` s em cada tema e troca em `troca` s, suave
  const corCiclo = (lista, fica, troca) => {
    if (lista.length < 2) return '';
    const passo = fica + troca, dur = passo * lista.length, kt = [0], vs = [lista[0]];
    lista.forEach((cor, i) => { kt.push((i * passo + fica) / dur, ((i + 1) * passo) / dur); vs.push(cor, lista[(i + 1) % lista.length]); });
    const sp = lista.map(() => '0 0 1 1;0.4 0 0.2 1').join(';');
    return `<animate attributeName="stop-color" values="${vs.join(';')}" keyTimes="${kt.map((k) => n(k * 1e4) / 1e4).join(';')}" calcMode="spline" keySplines="${sp}" dur="${n(dur)}s" repeatCount="indefinite"/>`;
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
  // contorno na forma do mascote (círculo = o anel do símbolo, igual ao de sempre); o traço tem a espessura do anel
  const contornavel = (id) => id && id !== 'circulo' && FORMAS[id] && forma && !['glifo', 'cartao', 'nenhuma'].includes(FORMAS[id].tipo) && id !== 'rosto';
  const geoForma = (id, cx, cy, R) => {
    const fm = forma(id), c = fm.caixa, k = (2 * R) / Math.max(c.w, c.h);
    return { d: fm.d, k, t: `translate(${n(cx)} ${n(cy)}) scale(${n(k * 1e4) / 1e4}) translate(${n(-(c.x + c.w / 2))} ${n(-(c.y + c.h / 2))})` };
  };
  const anelForma = (id, cx, cy, R, cor, op, anim) => {
    if (!contornavel(id)) return anel(cx, cy, R, cor, op, anim);
    const esp = R * 116 / 270, g = geoForma(id, cx, cy, R - esp / 2);
    const base = `d="${g.d}" fill="none" stroke="${cor}" stroke-width="${n(esp / g.k)}" stroke-linejoin="round" pathLength="100"`;
    const forma0 = (extra, filho = '') => `<g transform="${g.t}" opacity="${op}"><path ${base}${extra}>${filho}</path></g>`;
    if (anim === 'abertura') return forma0(' stroke-dasharray="100" stroke-dashoffset="100"', `<animate attributeName="stroke-dashoffset" from="100" to="0" dur="0.9s" ${FZ}/>`);
    if (anim === 'fechamento') return forma0(' stroke-dasharray="100" stroke-dashoffset="0"', `<animate attributeName="stroke-dashoffset" from="0" to="-100" begin="0.5s" dur="0.9s" ${FZ}/>`);
    if (anim === 'ciclo') return forma0(' stroke-dasharray="100" stroke-dashoffset="100"', `<animate attributeName="stroke-dashoffset" values="100;0;0;-100;-100" keyTimes="0;0.2;0.62;0.82;1" calcMode="spline" keySplines="0.3 0 0.2 1;0 0 1 1;0.3 0 0.2 1;0 0 1 1" dur="${CICLO}s" repeatCount="indefinite"/>`);
    if (anim === 'carregamento') return `<g transform="${g.t}"><path ${base} opacity="${n(op * 0.25)}"/><path ${base} opacity="${op}" stroke-dasharray="30 70"><animate attributeName="stroke-dashoffset" from="0" to="-100" dur="1.6s" repeatCount="indefinite"/></path></g>`;
    return forma0('');
  };
  // linha fina na forma do mascote (ondas)
  const ondaForma = (id, cx, cy, r, cor, larg) => {
    if (!contornavel(id)) return `<circle cx="${n(cx)}" cy="${n(cy)}" r="${n(r)}" fill="none" stroke="${cor}" stroke-width="${n(larg)}"/>`;
    const g = geoForma(id, cx, cy, r);
    return `<g transform="${g.t}"><path d="${g.d}" fill="none" stroke="${cor}" stroke-width="${n(larg / g.k)}" stroke-linejoin="round"/></g>`;
  };
  // o mascote acompanha a antena em que está: mesmo tempo e mesmo deslocamento da barra
  const acompanha = (anim, h, atraso, abaixo) => {
    const T = (de, para, inicio, dur) => `<animateTransform attributeName="transform" type="translate" from="0 ${n(de)}" to="0 ${n(para)}" begin="${n(inicio)}s" dur="${dur}s" ${FZ}/>`;
    if (anim === 'abertura') return { ini: `translate(0 ${n(abaixo)})`, anim: T(abaixo, 0, atraso, 0.6) };
    if (anim === 'fechamento') return { ini: '', anim: T(0, abaixo, atraso, 0.5) };
    if (anim === 'ciclo') return { ini: `translate(0 ${n(abaixo)})`, anim: `<animateTransform attributeName="transform" type="translate" values="0 ${n(abaixo)};0 0;0 0;0 ${n(abaixo)};0 ${n(abaixo)}" keyTimes="0;0.2;0.62;0.82;1" calcMode="spline" keySplines="0.3 0 0.2 1;0 0 1 1;0.3 0 0.2 1;0 0 1 1" dur="${CICLO}s" repeatCount="indefinite"/>` };
    if (anim === 'carregamento') return { ini: '', anim: `<animateTransform attributeName="transform" type="translate" values="0 0;0 ${n(h * 0.35)};0 0" dur="1.6s" begin="${n(-atraso)}s" repeatCount="indefinite" calcMode="spline" keyTimes="0;.5;1" keySplines="0.4 0 0.2 1;0.4 0 0.2 1"/>` };
    return { ini: '', anim: '' };
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
   * op = { largura, altura, cores (PAPEIS + mascote), pecas: 'ambos'|'logo'|'mascotes'|'nenhuma' (só o desenho, para fundo de card),
   *        origem: { x, y } (0 a 1; centro do anel e das ondas), celula (Estampa: tamanho da célula em px; no fundo de tela, fixo em ~48dp), raio (do anel gigante, em px; padrão 0,55 do lado maior),
   *        mascote: { expressao, forma, proporcao, movimento }, logo: ''|'abertura'|'carregamento',
   *        nome (Halo com pecas 'logo': o logo inteiro, símbolo + nome, centrado na origem e com altura = raio; com logo 'abertura', o nome entra depois do símbolo),
   *        variante ('longo' | 'curto': hastes do logo inteiro do Halo; curto no quest),
   *        ciclo: [cores, …] (Halo: os brilhos passam pelas cores desses temas em laço, suave, começando no primeiro; parado, ficam no primeiro; a superfície fica a de `cores`),
   *        cicloTempo: { fica, troca } (segundos em cada tema e na troca; padrão 6 e 2),
   *        quantidade (antenas), semente, movimentoReduzido }
   */
  function desenhar(conceito, op = {}) {
    const def = CONCEITOS[conceito];
    const { largura: w = 1600, altura: h = 900, cores: c, pecas: P = 'ambos', quantidade = 3, semente = 1, movimentoReduzido = false } = op;
    const ms = { proporcao: 'normal', movimento: '', ...def.mascote, ...(op.mascote || {}) };
    const a = movimentoReduzido ? '' : (op.logo ?? def.logo ?? '');
    const r = rnd(semente), b = Math.min(w, h), og = op.origem || {};
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
      // rabiscos espalhados (como um papel de parede de conversa): uma grade com sorteio de posição, tamanho e giro para as peças
      // maiores (mascotes em contorno com rosto, números e sinais, o símbolo) e uma grade fina de miudezas nos vãos; tudo num tom só
      const cel = op.celula ?? Math.max(40, b / 8), tom = c['outline-variant'], sw = n(Math.max(1.2, cel * 0.026)), grandes = [];
      s = base(w, h, c.surface);
      const comMasc = P !== 'logo' && P !== 'nenhuma', comLogo = P !== 'mascotes';
      const GLIFOS_E = ['numero-1', 'numero-2', 'numero-3', 'numero-4', 'numero-5', 'numero-7', 'numero-8', 'mais', 'menos', 'vezes', 'dividir', 'igual', 'letra-a', 'letra-b', 'letra-d'];
      const caminho = (id, cx, cy, t, giro, rosto) => {
        const fm = forma(id); if (!fm || !fm.d) return '';
        const cx0 = fm.caixa.x + fm.caixa.w / 2, cy0 = fm.caixa.y + fm.caixa.h / 2, k = t / Math.max(fm.caixa.w, fm.caixa.h), lw = n(sw / k);
        let g = `<path d="${fm.d}" fill="none" stroke="${tom}" stroke-width="${lw}" stroke-linecap="round" stroke-linejoin="round"/>`;
        if (rosto) {
          const [fx, fy] = fm.centro || [cx0, cy0], v = Math.floor(r() * 4), e = 9, d = 24;
          g += v === 2 ? `<path d="M${fx - d - e} ${fy - 6}h${2 * e}" stroke="${tom}" stroke-width="${lw}" stroke-linecap="round"/>` : `<circle cx="${fx - d}" cy="${fy - 6}" r="${e}" fill="${tom}"/>`;
          g += `<circle cx="${fx + d}" cy="${fy - 6}" r="${e}" fill="${tom}"/>`;
          g += v === 1 ? `<circle cx="${fx}" cy="${fy + 26}" r="12" fill="none" stroke="${tom}" stroke-width="${lw}"/>`
            : `<path d="M${fx - 24} ${fy + 18}Q${fx} ${fy + 44} ${fx + 24} ${fy + 18}" fill="none" stroke="${tom}" stroke-width="${lw}" stroke-linecap="round"/>` + (v === 3 ? `<path d="M${fx - 6} ${fy + 30}q6 14 12 0" fill="${tom}"/>` : '');
        }
        return `<g transform="translate(${n(cx)} ${n(cy)}) rotate(${n(giro)}) scale(${n(k * 1e4) / 1e4}) translate(${n(-cx0)} ${n(-cy0)})">${g}</g>`;
      };
      for (let y = cel * 0.4; y < h + cel; y += cel) for (let x = cel * 0.4; x < w + cel; x += cel) {
        if (r() < 0.12) continue;
        const t = cel * (0.48 + r() * 0.36), cx = x + (r() - 0.5) * cel * 0.5, cy = y + (r() - 0.5) * cel * 0.5, giro = (r() - 0.5) * 70, q = r();
        if (comLogo && (!comMasc || q < 0.18)) s += `<g transform="rotate(${n(giro)} ${n(cx)} ${n(cy)})">${simb(cx - t * 0.3, cy - t / 2, t, tom, r() < 0.5 ? 'longo' : 'curto', 0.55, a)}</g>`;
        else if (comMasc && q < 0.4) s += caminho(GLIFOS_E[Math.floor(r() * GLIFOS_E.length)], cx, cy, t * 0.85, giro, false);
        else if (comMasc) s += caminho(ms.forma || SORTEIO[Math.floor(r() * SORTEIO.length)], cx, cy, t, giro, true);
        grandes.push([cx, cy, t / 2]);
      }
      // miudezas nos vãos: ponto, anel, estrela, brilho, xis, mais, coração e triângulo
      const passo = cel / 2.6, mi = (ix, cx, cy, u, giro) => {
        const T = `transform="rotate(${n(giro)} ${n(cx)} ${n(cy)})"`, L = `fill="none" stroke="${tom}" stroke-width="${sw}" stroke-linecap="round" stroke-linejoin="round"`;
        if (ix === 0) return `<circle cx="${n(cx)}" cy="${n(cy)}" r="${n(u * 0.28)}" fill="${tom}"/>`;
        if (ix === 1) return `<circle cx="${n(cx)}" cy="${n(cy)}" r="${n(u * 0.5)}" ${L}/>`;
        if (ix === 2) { const p = [...Array(10).keys()].map((k) => { const ang = -Math.PI / 2 + k * Math.PI / 5, rr = k % 2 ? u * 0.32 : u * 0.75; return `${n(cx + rr * Math.cos(ang))} ${n(cy + rr * Math.sin(ang))}`; }); return `<path d="M${p.join('L')}Z" ${L} ${T}/>`; }
        if (ix === 3) return `<path d="M${n(cx)} ${n(cy - u)}Q${n(cx)} ${n(cy)} ${n(cx + u)} ${n(cy)}Q${n(cx)} ${n(cy)} ${n(cx)} ${n(cy + u)}Q${n(cx)} ${n(cy)} ${n(cx - u)} ${n(cy)}Q${n(cx)} ${n(cy)} ${n(cx)} ${n(cy - u)}Z" fill="${tom}" ${T}/>`;
        if (ix === 4) return `<path d="M${n(cx - u * 0.5)} ${n(cy - u * 0.5)}L${n(cx + u * 0.5)} ${n(cy + u * 0.5)}M${n(cx + u * 0.5)} ${n(cy - u * 0.5)}L${n(cx - u * 0.5)} ${n(cy + u * 0.5)}" ${L} ${T}/>`;
        if (ix === 5) return `<path d="M${n(cx)} ${n(cy - u * 0.6)}V${n(cy + u * 0.6)}M${n(cx - u * 0.6)} ${n(cy)}H${n(cx + u * 0.6)}" ${L} ${T}/>`;
        if (ix === 6) return `<path d="M${n(cx)} ${n(cy + u * 0.55)}C${n(cx - u * 1.1)} ${n(cy - u * 0.1)} ${n(cx - u * 0.4)} ${n(cy - u * 0.8)} ${n(cx)} ${n(cy - u * 0.25)}C${n(cx + u * 0.4)} ${n(cy - u * 0.8)} ${n(cx + u * 1.1)} ${n(cy - u * 0.1)} ${n(cx)} ${n(cy + u * 0.55)}Z" ${L} ${T}/>`;
        return `<path d="M${n(cx)} ${n(cy - u * 0.6)}L${n(cx + u * 0.6)} ${n(cy + u * 0.45)}L${n(cx - u * 0.6)} ${n(cy + u * 0.45)}Z" ${L} ${T}/>`;
      };
      for (let y = passo / 2; y < h; y += passo) for (let x = passo / 2; x < w; x += passo) {
        const cx = x + (r() - 0.5) * passo * 0.8, cy = y + (r() - 0.5) * passo * 0.8, u = cel * (0.06 + r() * 0.07);
        if (grandes.some(([gx, gy, gr]) => Math.hypot(gx - cx, gy - cy) < gr + u + 3) || r() < 0.35) continue;
        s += mi(Math.floor(r() * 8), cx, cy, u, (r() - 0.5) * 60);
      }
    } else if (conceito === 'anel') {
      const R = op.raio ?? Math.max(w, h) * 0.55, cx = w * (og.x ?? 0.92), cy = h * (og.y ?? 0.98);
      if (P === 'nenhuma') s = base(w, h, c['primary-container']) + anelForma(ms.forma, cx, cy, R, c.primary, 0.9, a) + anelForma(ms.forma, cx, cy, R * 0.42, c.tertiary, 0.7, a);
      else if (P === 'mascotes') s = base(w, h, c['surface-container']) + masc(montar(), cx - R * 0.1, cy - R * 0.1, R * 1.6);
      else s = base(w, h, c['primary-container']) + anelForma(ms.forma, cx, cy, R, c.primary, 0.9, a) + anelForma(ms.forma, w * 0.12, h * 0.2, b * 0.07, c.secondary, 0.9, a) +
        (P === 'ambos' ? masc(montar(), cx - R * 0.38, cy - R * 0.38, R * 0.62) : anelForma(ms.forma, cx, cy, R * 0.42, c.tertiary, 0.7, a));
    } else if (conceito === 'antenas') {
      const k = Math.max(8, Math.round(w / (b / 9))), g = w / k, wd = g * 0.62; s = base(w, h, c.surface);
      const qtd = Math.max(0, Math.min(quantidade, 8, k)), vagas = [...Array(k).keys()].sort(() => r() - 0.5).slice(0, qtd);
      if (P === 'mascotes') { const gg = w / Math.max(qtd, 1); for (let i = 0; i < qtd; i++) { const t = Math.min(gg * (0.75 + r() * 0.3), h * 0.5); s += masc(montar(), i * gg + gg / 2, h - t * 0.45, t); } }
      else { const alt = []; for (let i = 0; i < k; i++) { alt[i] = h * (0.18 + r() * 0.42); s += antena(i * g + (g - wd) / 2, h + 2, wd, alt[i], i % 2 ? c.secondary : c.primary, 0.85, a, i * 0.07); }
        if (P === 'ambos') vagas.forEach((i) => {
          const ac = acompanha(a, alt[i], i * 0.07, alt[i] + g * 1.2);
          const m = masc(montar('rosto'), i * g + g / 2, h - alt[i] - g * 0.55, g * 1.1);
          s += ac.anim ? `<g${ac.ini ? ` transform="${ac.ini}"` : ''}>${ac.anim}${m}</g>` : m;
        });
        if (P !== 'nenhuma') s += anel(w * 0.82, h * 0.22, b * 0.09, c.tertiary, 0.9, a); }
    } else if (conceito === 'ondas') {
      const cx = w * (og.x ?? 0.1), cy = h * (og.y ?? 0.12); s = base(w, h, c.surface);
      for (let i = 1; i < 26; i++) s += ondaForma(ms.forma, cx, cy, b * 0.16 + i * b * 0.055, c['outline-variant'], b * 0.006);
      if (P === 'logo') s += anelForma(ms.forma, cx, cy, b * 0.16, c.primary, 1, a);
      else if (P === 'ambos') s += anelForma(ms.forma, cx, cy, b * 0.16, c.primary, 1, a) + masc(montar(), w * 0.86, h * 0.82, b * 0.26);
      else if (P === 'mascotes') s += masc(montar(), cx, cy, b * 0.4);
    } else if (conceito === 'recorte') {
      const alt = Math.max(w, h) * 0.95, x = w - alt * 0.52, y = h - alt * 0.78; s = base(w, h, c['surface-container']);
      if (P === 'mascotes') s += masc(montar(), w * 0.78, h * 0.78, alt * 0.95);
      else s += simb(x, y, alt, { anel: c['tertiary-container'], b: c['primary-container'], d: c['secondary-container'] }, 'longo', 1, a) +
        (P === 'ambos' ? masc(montar(), x + alt * 0.34, y + alt * 0.66, alt * 0.26) : '');
    } else if (conceito === 'halo') {
      const id = `fundo-h${++seq}`;
      // com ciclo, cada brilho passa pela mesma cor (primary, secondary, tertiary) de cada tema da lista, a partir do primeiro
      // (parado, fica no primeiro tema da lista); a superfície de baixo é sempre a de `cores`
      const lista = op.ciclo?.length ? op.ciclo : null, cb = lista ? lista[0] : c;
      const { fica = 6, troca = 2 } = op.cicloTempo || {};
      const gr = (k, papel, x, y, raio) => { const an = lista && !movimentoReduzido ? corCiclo(lista.map((t) => t[papel]), fica, troca) : '';
        return `<radialGradient id="${id}-${k}" cx="${n(x)}" cy="${n(y)}" r="${n(raio)}" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="${cb[papel]}" stop-opacity=".55">${an}</stop><stop offset="1" stop-color="${cb[papel]}" stop-opacity="0">${an}</stop></radialGradient>`; };
      s = `<defs>${gr(1, 'primary', w * 0.2, h * 0.25, b * 0.9)}${gr(2, 'secondary', w * 0.85, h * 0.8, b * 0.9)}${gr(3, 'tertiary', w * 0.7, h * 0.15, b * 0.6)}</defs>` + base(w, h, c.surface) +
        [1, 2, 3].map((k) => `<rect width="${w}" height="${h}" fill="url(#${id}-${k})"/>`).join('');
      if (P === 'nenhuma') s += '';
      // logo inteiro nas cores da marca do tema (num tema monocromático, sem cor de mascote, tudo em on-surface)
      else if (P === 'logo' && op.nome) s += logoInteiro(w * (og.x ?? 0.5), h * (og.y ?? 0.5), op.raio ?? b * 0.26, c.mascote ? { ...tres, de: c.secondary, boaz: c.primary } : { simbolo: c['on-surface'], de: c['on-surface'], boaz: c['on-surface'] }, a, op.variante);
      else if (P === 'logo') s += simb(w / 2 - b * 0.09, h / 2 - b * 0.13, b * 0.26, tres, 'longo', 1, a);
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
