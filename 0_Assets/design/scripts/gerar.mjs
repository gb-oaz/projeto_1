#!/usr/bin/env node
// =============================================================================
// boaz · gerador do design system (Material Design 3)
//
//   pnpm run gerar      lê identidade/marca.json e tokens/*.json e escreve:
//                         tokens/cores.json            paletas e esquemas de cor
//                         plataformas/web/tokens.css   variáveis CSS (Vue)
//                         plataformas/web/tipografia.css  classes .md-typescale-*
//                         plataformas/web/tokens.ts    tokens tipados e janelas
//                         plataformas/maui/Tokens.xaml ResourceDictionary (.NET MAUI)
//                         plataformas/maui/paletas/    um dicionário de cores por tema
//                         tema.svg                     quadro visual (cores, tipos, componentes, mascotes)
//                         mascotes/                    exemplos dos mascotes (scripts/mascotes-arquivos.mjs)
//                         plataformas/web/mascotes.mjs núcleo dos mascotes para as telas
//                         design-system.html           só o bloco entre os marcadores
//                                                      design:tokens:inicio/fim
//                         + cópias em cada aplicação de 5_DEV/frontend (sincronizar.mjs)
//   pnpm run conferir   não escreve nada; falha se algum arquivo gerado estiver
//                       diferente do que seria gerado agora
//
// Cores: @material/material-color-utilities (a biblioteca oficial do M3, HCT).
// Nada aqui é editado à mão: mude a identidade ou os tokens e rode de novo.
// =============================================================================
import { mkdirSync, readFileSync, writeFileSync, readdirSync, statSync, rmSync, existsSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  argbFromHex, hexFromArgb, Hct, TonalPalette, DynamicScheme, DynamicColor,
  MaterialDynamicColors, customColor,
  SchemeTonalSpot, SchemeFidelity, SchemeContent, SchemeVibrant, SchemeExpressive,
  SchemeNeutral, SchemeMonochrome, SchemeRainbow, SchemeFruitSalad,
} from '@material/material-color-utilities';
import { temaSvg } from './tema-svg.mjs';
import { copiasSincronizadas } from './sincronizar.mjs';
import { arquivosMascotes, temasDosMascotes } from './mascotes-arquivos.mjs';
import { estatico as mascoteEstatico, embutir, EXPRESSOES as M_EXPR, FORMAS as M_FORMAS, PROPORCOES as M_PROP } from './mascotes.mjs';
import { VITRINE as M_VITRINE } from './mascotes-arquivos.mjs';

const RAIZ = join(dirname(fileURLToPath(import.meta.url)), '..');
const CONFERIR = process.argv.includes('--conferir');
const lerJson = (p) => JSON.parse(readFileSync(join(RAIZ, p), 'utf8'));

const marca = lerJson('identidade/marca.json');
const fundamentos = lerJson('tokens/fundamentos.json').md;
const componentes = lerJson('tokens/componentes.json').md.comp;
const AVISO = 'Gerado por 0_Assets/design/scripts/gerar.mjs a partir de identidade/marca.json e tokens/*.json — não edite à mão.';

// ── utilitários ──────────────────────────────────────────────────────────────
const kebab = (s) => s.replace(/([a-z0-9])([A-Z])/g, '$1-$2').toLowerCase();
const pascal = (s) => s.split(/[-.]/).map((p) => p.charAt(0).toUpperCase() + p.slice(1)).join('');
const camel = (s) => { const p = pascal(s); return p.charAt(0).toLowerCase() + p.slice(1); };
const arred = (n, casas = 4) => Number(n.toFixed(casas));
const hex = (argb) => hexFromArgb(argb).toUpperCase();
const folhas = (no, caminho = []) => Object.entries(no)
  .filter(([k]) => !k.startsWith('$'))
  .flatMap(([k, v]) => ('$value' in v ? [[[...caminho, k], v]] : folhas(v, [...caminho, k])));

// ── 1. cores ─────────────────────────────────────────────────────────────────
const VARIANTES = {
  'tonal-spot': SchemeTonalSpot, fidelity: SchemeFidelity, content: SchemeContent,
  vibrant: SchemeVibrant, expressive: SchemeExpressive, neutral: SchemeNeutral,
  monochrome: SchemeMonochrome, rainbow: SchemeRainbow, 'fruit-salad': SchemeFruitSalad,
};
const cfg = marca.cores;
const kebabOk = (n) => /^[a-z][a-z0-9-]*$/.test(n);
const PAPEIS = Object.getOwnPropertyNames(MaterialDynamicColors)
  .filter((k) => MaterialDynamicColors[k] instanceof DynamicColor && !k.endsWith('PaletteKeyColor'));
const TONS = [0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 95, 99, 100];
const TONS_NEUTROS = [0, 4, 6, 10, 12, 17, 20, 22, 24, 30, 40, 50, 60, 70, 80, 87, 90, 92, 94, 95, 96, 98, 99, 100];
const paletaDe = (h) => (h ? TonalPalette.fromInt(argbFromHex(h)) : null);

// Contraste WCAG entre duas cores #RRGGBB
const luminancia = (h) => {
  const [r, g, b] = [1, 3, 5].map((i) => parseInt(h.slice(i, i + 2), 16) / 255).map((v) => (v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
};
const contraste = (a, b) => { const [x, y] = [luminancia(a), luminancia(b)].sort((p, q) => q - p); return (x + 0.05) / (y + 0.05); };

// Um tema de cor: esquemas claro/escuro/alto contraste e paletas tonais
function gerarTema(id, t) {
  if (!kebabOk(id)) throw new Error(`cores.temas: nome "${id}" deve ser kebab-case`);
  const Variante = VARIANTES[t.variante];
  if (!Variante) throw new Error(`cores.temas.${id}.variante "${t.variante}" inválida: use ${Object.keys(VARIANTES).join(', ')}`);
  const semente = argbFromHex(t.semente);
  const esquema = (escuro, contraste) => {
    const base = new Variante(Hct.fromInt(semente), escuro, contraste);
    const s = new DynamicScheme({
      sourceColorArgb: semente, variant: base.variant, contrastLevel: contraste, isDark: escuro,
      primaryPalette: base.primaryPalette,
      secondaryPalette: paletaDe(t.secundaria) ?? base.secondaryPalette,
      tertiaryPalette: paletaDe(t.terciaria) ?? base.tertiaryPalette,
      neutralPalette: paletaDe(t.neutra) ?? base.neutralPalette,
      neutralVariantPalette: base.neutralVariantPalette,
    });
    if (t.erro) s.errorPalette = paletaDe(t.erro);
    return s;
  };
  // extras (sucesso, aviso…) harmonizados com a semente deste tema
  const extras = Object.entries(cfg.extras ?? {}).filter(([k]) => !k.startsWith('$'))
    .map(([nome, e]) => [nome, customColor(semente, { name: nome, value: argbFromHex(e.cor), blend: !!e.harmonizar })]);
  const papeis = (escuro, nivel) => {
    const s = esquema(escuro, nivel);
    const r = Object.fromEntries(PAPEIS.map((k) => [kebab(k), hex(MaterialDynamicColors[k].getArgb(s))]));
    for (const [nome, cc] of extras) {
      const g = escuro ? cc.dark : cc.light;
      Object.assign(r, {
        [nome]: hex(g.color), [`on-${nome}`]: hex(g.onColor),
        [`${nome}-container`]: hex(g.colorContainer), [`on-${nome}-container`]: hex(g.onColorContainer),
      });
    }
    // ajustes: papéis fixados à mão neste modo (ex.: superfície branca pura);
    // valem também no alto contraste e precisam manter o contraste dos pares
    const aj = Object.entries(t.ajustes?.[escuro ? 'dark' : 'light'] ?? {});
    for (const [k, v] of aj) {
      if (!(k in r)) throw new Error(`cores.temas.${id}.ajustes: papel "${k}" não existe`);
      if (!/^#[0-9a-fA-F]{6}$/.test(v)) throw new Error(`cores.temas.${id}.ajustes.${k}: use #RRGGBB`);
      r[k] = v.toUpperCase();
    }
    if (aj.length) {
      const minimo = nivel > 0 ? 7 : 4.5, mudou = new Set(aj.map(([k]) => k));
      const pares = Object.keys(r).filter((k) => k.startsWith('on-') && !k.endsWith('-variant') && r[k.slice(3)]).map((k) => [k, k.slice(3)]);
      for (const sup of Object.keys(r).filter((k) => k === 'background' || k.startsWith('surface') && !k.endsWith('-variant') && k !== 'surface-tint')) pares.push(['on-surface', sup]);
      for (const [a, b] of pares) {
        if (!mudou.has(a) && !mudou.has(b)) continue;
        const q = contraste(r[a], r[b]);
        if (q < minimo) throw new Error(`cores.temas.${id}.ajustes (${escuro ? 'dark' : 'light'}${nivel > 0 ? ', alto contraste' : ''}): ${a} sobre ${b} tem contraste ${q.toFixed(2)}:1, mínimo ${minimo}:1`);
      }
    }
    return r;
  };
  const ref = esquema(false, 0);
  const fontes = {
    primary: [ref.primaryPalette, TONS], secondary: [ref.secondaryPalette, TONS],
    tertiary: [ref.tertiaryPalette, TONS], neutral: [ref.neutralPalette, TONS_NEUTROS],
    'neutral-variant': [ref.neutralVariantPalette, TONS], error: [ref.errorPalette, TONS],
    ...Object.fromEntries(extras.map(([nome, cc]) => [nome, [TonalPalette.fromInt(cc.value), TONS]])),
  };
  return {
    id, nome: t.nome ?? id, descricao: t.descricao ?? '', harmonia: t.harmonia ?? '',
    semente: t.semente.toUpperCase(), terciaria: t.terciaria?.toUpperCase() ?? null, variante: t.variante,
    extras: extras.map(([n]) => n), modo: t.modo_preferido ?? null,
    esquemas: { light: papeis(false, 0), dark: papeis(true, 0), 'light-high-contrast': papeis(false, 1), 'dark-high-contrast': papeis(true, 1) },
    paletas: Object.fromEntries(Object.entries(fontes).map(([n, [p, tons]]) => [n, Object.fromEntries(tons.map((x) => [x, hex(p.tone(x))]))])),
  };
}

let TEMAS = Object.fromEntries(Object.entries(cfg.temas).filter(([k]) => !k.startsWith('$')).map(([id, t]) => [id, gerarTema(id, t)]));
// tema_padrao: o id de um tema, ou { claro, escuro } — um tema em cada modo, combinado
let PADRAO;
if (typeof cfg.tema_padrao === 'string') {
  PADRAO = TEMAS[cfg.tema_padrao];
  if (!PADRAO) throw new Error(`cores.tema_padrao "${cfg.tema_padrao}" não está em cores.temas`);
} else {
  const { claro, escuro } = cfg.tema_padrao ?? {};
  const C = TEMAS[claro], E = TEMAS[escuro];
  if (!C || !E) throw new Error(`cores.tema_padrao: "claro" e "escuro" precisam ser temas de cores.temas`);
  PADRAO = {
    id: `${claro}-${escuro}`, nome: `${C.nome} e ${E.nome}`,
    descricao: `${C.nome} no modo claro e ${E.nome} no escuro, acompanhando o sistema.`,
    harmonia: C.harmonia === E.harmonia ? C.harmonia : `${C.harmonia} / ${E.harmonia}`,
    semente: C.semente, terciaria: C.terciaria, variante: C.variante, extras: C.extras, modo: null,
    esquemas: { light: C.esquemas.light, dark: E.esquemas.dark, 'light-high-contrast': C.esquemas['light-high-contrast'], 'dark-high-contrast': E.esquemas['dark-high-contrast'] },
    paletas: C.paletas,
  };
  TEMAS = { [PADRAO.id]: PADRAO, ...TEMAS };
}
const OUTROS = Object.values(TEMAS).filter((t) => t !== PADRAO);
// o tema padrão alimenta os nomes sem prefixo (--md-sys-color-*, PrimaryLight, colors)
const ESQUEMAS = PADRAO.esquemas;
const paletas = PADRAO.paletas;
const EXTRAS = PADRAO.extras.map((n) => [n]);
const c = { semente: PADRAO.semente, variante: PADRAO.variante };

const dtcg = (o) => Object.fromEntries(Object.entries(o).map(([k, v]) => [k, { $type: 'color', $value: v }]));
const cores = {
  $descricao: `${AVISO} Um grupo por tema de cor; o padrão é "${PADRAO.id}".`,
  padrao: PADRAO.id,
  temas: Object.fromEntries(Object.values(TEMAS).map((t) => [t.id, {
    $descricao: `${t.nome}: ${t.descricao} Harmonia ${t.harmonia}. Semente ${t.semente}, esquema ${t.variante}.`,
    md: {
      ref: { palette: Object.fromEntries(Object.entries(t.paletas).map(([n, p]) => [n, dtcg(p)])) },
      sys: { color: Object.fromEntries(Object.entries(t.esquemas).map(([n, e]) => [n, dtcg(e)])) },
    },
  }])),
};

// ── 2. tokens resolvidos ─────────────────────────────────────────────────────
const tipos = Object.fromEntries(folhas(fundamentos.sys.typescale).map(([[n], v]) => [n, v.$value]));
const fonte = { brand: marca.tipografia.marca, plain: marca.tipografia.texto };
const pilhaFonte = (f) => [f.familia, ...f.alternativas].map((x) => (/\s/.test(x) ? `"${x}"` : x)).join(', ');
const PESOS = { 400: 'Regular', 500: 'Medium', 700: 'Bold' };
const cantos = Object.fromEntries(folhas(fundamentos.sys.shape.corner).map(([[n], v]) => [n, v.$value]));
const elevacoes = Object.fromEntries(folhas(fundamentos.sys.elevation).map(([[n], v]) => [n, v.$value]));
const espacos = folhas(fundamentos.sys.spacing).map(([[n], v]) => [n, v.$value]);
const janelas = Object.fromEntries(folhas(fundamentos.sys.layout.window).map(([[n], v]) => [n, v.$value]));
const layout = Object.fromEntries(folhas(fundamentos.sys.layout).filter(([p]) => p[0] !== 'window').map(([[n], v]) => [n, v.$value]));
const easing = Object.fromEntries(folhas(fundamentos.sys.motion.easing).map(([[n], v]) => [n, v.$value]));
const duracoes = Object.fromEntries(folhas(fundamentos.sys.motion.duration).map(([[n], v]) => [n, v.$value]));
const estados = Object.fromEntries(folhas(fundamentos.sys.state).map(([[n], v]) => [n, v.$value]));
const pesosRef = Object.fromEntries(folhas(fundamentos.ref.typeface).map(([[n], v]) => [n, v.$value]));

// Sombras da web, no padrão do M3 (sombra de contato + sombra difusa)
const SOMBRAS = {
  level0: 'none',
  level1: [[1, 2, 0], [1, 3, 1]], level2: [[1, 2, 0], [2, 6, 2]], level3: [[1, 3, 0], [4, 8, 3]],
  level4: [[2, 3, 0], [6, 10, 4]], level5: [[4, 4, 0], [8, 12, 6]],
};
const sombraCss = (n) => (SOMBRAS[n] === 'none' ? 'none' : SOMBRAS[n].map(([y, b, s], i) =>
  `0 ${y}px ${b}px ${s}px color-mix(in srgb, var(--md-sys-color-shadow) ${i ? 15 : 30}%, transparent)`).join(', '));

// Referência {md.sys.<grupo>.<nome>} de um token de componente
function referencia(valor) {
  const m = typeof valor === 'string' && valor.match(/^\{md\.sys\.([a-z]+)\.(.+)\}$/);
  if (!m) return null;
  const [, grupo, resto] = m;
  const nome = resto.replace(/^corner\./, '');
  const existe = { color: nome in ESQUEMAS.light, typescale: nome in tipos, shape: nome in cantos, elevation: nome in elevacoes }[grupo];
  if (!existe) throw new Error(`referência inexistente: ${valor}`);
  return { grupo, nome };
}
const tokensComp = folhas(componentes).map(([caminho, v]) => ({ nome: caminho.join('-'), valor: v.$value, ref: referencia(v.$value) }));

// ── 3. web: tokens.css ───────────────────────────────────────────────────────
const px = (n) => `${n}px`;
const rem = (n) => `${arred(n / 16)}rem`;
const bloco = (seletor, linhas, recuo = '') => `${recuo}${seletor} {\n${linhas.map((l) => `${recuo}  ${l}`).join('\n')}\n${recuo}}`;
const varsCor = (e) => Object.entries(e).map(([k, v]) => `--md-sys-color-${k}: ${v};`);
const tipoVars = (prefixo, alvo) => ['font', 'size', 'line-height', 'tracking', 'weight']
  .map((p) => `${prefixo}-${p}: var(--md-sys-typescale-${alvo}-${p});`);

const raiz = [
  '/* referência: fontes e paletas tonais */',
  `--md-ref-typeface-brand: ${pilhaFonte(fonte.brand)};`,
  `--md-ref-typeface-plain: ${pilhaFonte(fonte.plain)};`,
  ...Object.entries(pesosRef).map(([k, v]) => `--md-ref-typeface-${k}: ${v};`),
  ...Object.entries(paletas).flatMap(([n, p]) => Object.entries(p).map(([t, v]) => `--md-ref-palette-${n}${t}: ${v};`)),
  '',
  '/* tipografia: tamanhos em rem (acompanham o tamanho de fonte escolhido pela pessoa) */',
  ...Object.entries(tipos).flatMap(([n, t]) => [
    `--md-sys-typescale-${n}-font: var(--md-ref-typeface-${t.font});`,
    `--md-sys-typescale-${n}-size: ${rem(t.size)};`,
    `--md-sys-typescale-${n}-line-height: ${rem(t['line-height'])};`,
    `--md-sys-typescale-${n}-tracking: ${arred(t.tracking / t.size)}em;`,
    `--md-sys-typescale-${n}-weight: ${t.weight};`,
  ]),
  '',
  '/* forma e espaçamento */',
  ...Object.entries(cantos).map(([n, v]) => `--md-sys-shape-corner-${n}: ${px(v)};`),
  ...espacos.map(([n, v]) => `--md-sys-spacing-${n}: ${px(v)};`),
  '',
  '/* layout (os valores por janela estão nas media queries abaixo) */',
  ...Object.entries(layout).map(([n, v]) => `--md-sys-layout-${n}: ${px(v)};`),
  '',
  '/* movimento e estados */',
  ...Object.entries(easing).map(([n, v]) => `--md-sys-motion-easing-${n}: cubic-bezier(${v.join(', ')});`),
  ...Object.entries(duracoes).map(([n, v]) => `--md-sys-motion-duration-${n}: ${v}ms;`),
  ...Object.entries(estados).map(([n, v]) => `--md-sys-state-${n}: ${n.includes('opacity') ? v : px(v)};`),
];

// Elevação e componentes apontam para cores: são declarados também em cada
// esquema, para que um trecho .md-scheme-dark recalcule as referências.
const dependentes = [
  '/* elevação (a cor da sombra vem do esquema) */',
  ...Object.keys(elevacoes).map((n) => `--md-sys-elevation-${n}: ${sombraCss(n)};`),
  '',
  '/* componentes */',
  ...tokensComp.flatMap(({ nome, valor, ref: r }) => {
    const v = `--md-comp-${nome}`;
    if (!r) return [`${v}: ${typeof valor === 'number' ? (/opacity/.test(nome) ? valor : px(valor)) : valor};`];
    if (r.grupo === 'typescale') return tipoVars(v, r.nome);
    const alvo = { color: 'color', shape: 'shape-corner', elevation: 'elevation' }[r.grupo];
    return [`${v}: var(--md-sys-${alvo}-${r.nome});`];
  }),
];

const janelaVars = (j, n) => [
  `--md-sys-layout-window: ${n};`, `--md-sys-layout-margin: ${px(j.margin)};`,
  `--md-sys-layout-columns: ${j.columns};`, `--md-sys-layout-gutter: ${px(j.gutter)};`, `--md-sys-layout-panes: ${j.panes};`,
];
const [primeira, ...demais] = Object.entries(janelas);

// Blocos de cor de um tema. O padrão usa :root; os outros, <html data-paleta="id">.
// Trechos da página: .md-paleta-<id> (claro) e .md-paleta-<id>.md-scheme-dark (escuro);
// .md-scheme-light/.md-scheme-dark sozinhos usam o tema padrão.
const varsRef = (t) => Object.entries(t.paletas).flatMap(([n, p]) => Object.entries(p).map(([x, v]) => `--md-ref-palette-${n}${x}: ${v};`));
function cssCores(t) {
  const padrao = t === PADRAO;
  const raiz = padrao ? ':root' : `:root[data-paleta="${t.id}"]`;
  const claro = padrao ? [raiz, '.md-scheme-light', `.md-paleta-${t.id}`] : [raiz, `.md-paleta-${t.id}`];
  const escuro = padrao ? ['.md-scheme-dark', `.md-paleta-${t.id}.md-scheme-dark`] : [`.md-paleta-${t.id}.md-scheme-dark`];
  const E = t.esquemas, sel = (a, recuo = '') => a.join(`,\n${recuo}`);
  const media = (q, ...blocos) => `@media ${q} {\n${blocos.join('\n')}\n}`;
  return [
    `/* ── tema de cor: ${t.nome} (${t.id})${padrao ? ' · padrão' : ''} — ${t.harmonia} ── */`,
    bloco(sel(claro), ['color-scheme: light;', ...(padrao ? [] : varsRef(t)), ...varsCor(E.light)]),
    bloco(sel(escuro), ['color-scheme: dark;', ...varsCor(E.dark)]),
    media('(prefers-color-scheme: dark)', bloco(`${raiz}:not([data-theme="light"])`, ['color-scheme: dark;', ...varsCor(E.dark)], '  ')),
    bloco(`${raiz}[data-theme="dark"]`, ['color-scheme: dark;', ...varsCor(E.dark)]),
    media('(prefers-contrast: more)', bloco(sel(claro, '  '), varsCor(E['light-high-contrast']), '  '), bloco(sel(escuro, '  '), varsCor(E['dark-high-contrast']), '  ')),
    media('(prefers-contrast: more) and (prefers-color-scheme: dark)', bloco(`${raiz}:not([data-theme="light"])`, varsCor(E['dark-high-contrast']), '  ')),
    media('(prefers-contrast: more)', bloco(`${raiz}[data-theme="dark"]`, varsCor(E['dark-high-contrast']), '  ')),
  ];
}

const css = [
  `/* boaz · tokens do design system (Material Design 3)\n * ${AVISO}\n *\n * Esquema claro por padrão; escuro pelo sistema ou por <html data-theme="dark">\n * (data-theme="light" força o claro). Alto contraste segue prefers-contrast: more.\n * .md-scheme-light / .md-scheme-dark aplicam um esquema só a um trecho da página.
 *
 * Temas de cor: ${Object.values(TEMAS).map((t) => t.id + (t === PADRAO ? ' (padrão)' : '')).join(', ')}.
 * Troque com <html data-paleta="<id>">; num trecho, class="md-paleta-<id>" (+ md-scheme-dark). */`,
  bloco(':root', [...raiz, '', '/* janela compact (padrão, celular) */', ...janelaVars(primeira[1], primeira[0])]),
  bloco([':root', '.md-scheme-light', '.md-scheme-dark', ...Object.keys(TEMAS).map((id) => `.md-paleta-${id}`)].join(',\n'), dependentes),
  ...demais.map(([n, j]) => `@media (min-width: ${j.min}px) {\n${bloco(':root', janelaVars(j, n), '  ')}\n}`),
  ...Object.values(TEMAS).flatMap(cssCores),
  `@media (prefers-reduced-motion: reduce) {\n${bloco(':root', Object.keys(duracoes).map((n) => `--md-sys-motion-duration-${n}: 0ms;`), '  ')}\n}`,
].join('\n\n') + '\n';

const tipografiaCss = [
  `/* boaz · classes da escala tipográfica (Material Design 3)\n * ${AVISO}\n * Requer tokens.css. Uso: <h1 class="md-typescale-headline-large">. */`,
  ...Object.keys(tipos).map((n) => bloco(`.md-typescale-${n}`, [
    `font-family: var(--md-sys-typescale-${n}-font);`, `font-size: var(--md-sys-typescale-${n}-size);`,
    `line-height: var(--md-sys-typescale-${n}-line-height);`, `letter-spacing: var(--md-sys-typescale-${n}-tracking);`,
    `font-weight: var(--md-sys-typescale-${n}-weight);`,
  ])),
].join('\n\n') + '\n';

// ── 4. web: tokens.ts ────────────────────────────────────────────────────────
const objCamel = (o) => Object.fromEntries(Object.entries(o).map(([k, v]) => [camel(k), v]));
const ts = `// boaz · tokens do design system (Material Design 3)
// ${AVISO}
// Os estilos ficam em tokens.css; aqui estão os valores para lógica em TypeScript
// (ex.: escolher a navegação pela janela) e para bibliotecas que pedem objetos.

export type WindowClass = ${Object.keys(janelas).map((n) => `'${n}'`).join(' | ')};

/** Classes de tamanho de janela do M3 (largura em px/dp). */
export const windows = ${JSON.stringify(objCamel(janelas), null, 2)} as const;

/** Classe de janela para uma largura, ex.: windowClassOf(window.innerWidth). */
export function windowClassOf(width: number): WindowClass {
${Object.entries(janelas).reverse().map(([n, j]) => `  if (width >= ${j.min}) return '${n}';`).join('\n')}
  return 'compact';
}

/** Media query de cada janela, para matchMedia ou useMediaQuery. */
export const windowMedia: Record<WindowClass, string> = ${JSON.stringify(Object.fromEntries(Object.entries(janelas).map(([n, j]) =>
  [n, j.max == null ? `(min-width: ${j.min}px)` : j.min === 0 ? `(max-width: ${j.max}px)` : `(min-width: ${j.min}px) and (max-width: ${j.max}px)`])), null, 2)};

export type PaletteId = ${Object.keys(TEMAS).map((id) => `'${id}'`).join(' | ')};

/** Tema de cor padrão (o que vale sem <html data-paleta>). */
export const defaultPalette: PaletteId = '${PADRAO.id}';

/** Temas de cor: aplique na web com document.documentElement.dataset.paleta = id. */
export const palettes = ${JSON.stringify(Object.fromEntries(Object.values(TEMAS).map((t) => [t.id, {
  name: t.nome, description: t.descricao, harmony: t.harmonia, seed: t.semente, preferredMode: t.modo,
  colors: Object.fromEntries(Object.entries(t.esquemas).map(([n, e]) => [camel(n), objCamel(e)])),
}])), null, 2)} as const;

/** Cores do tema padrão. */
export const colors = ${JSON.stringify(Object.fromEntries(Object.entries(ESQUEMAS).map(([n, e]) => [camel(n), objCamel(e)])), null, 2)} as const;

export const typeface = ${JSON.stringify({ brand: pilhaFonte(fonte.brand), plain: pilhaFonte(fonte.plain) }, null, 2)} as const;

/** Escala tipográfica em px (size, lineHeight, tracking). */
export const typescale = ${JSON.stringify(objCamel(Object.fromEntries(Object.entries(tipos).map(([n, t]) => [n, objCamel(t)]))), null, 2)} as const;

export const shape = ${JSON.stringify(objCamel(cantos), null, 2)} as const;
export const elevation = ${JSON.stringify(elevacoes, null, 2)} as const;
export const spacing = ${JSON.stringify(Object.fromEntries(espacos), null, 2)} as const;
export const layout = ${JSON.stringify(objCamel(layout), null, 2)} as const;
export const motion = ${JSON.stringify({ easing: objCamel(easing), duration: objCamel(duracoes) }, null, 2)} as const;
export const state = ${JSON.stringify(objCamel(estados), null, 2)} as const;
`;

// ── 5. MAUI: Tokens.xaml ─────────────────────────────────────────────────────
const tema = (papel) => `{AppThemeBinding Light={StaticResource ${pascal(papel)}Light}, Dark={StaticResource ${pascal(papel)}Dark}}`;
const aliasFonte = (t) => `${t.font === 'brand' ? 'Marca' : 'Texto'}${PESOS[t.weight] ?? 'Regular'}`;
const setter = (p, v) => `    <Setter Property="${p}" Value="${v}" />`;
const BOTOES = {
  Filled: { fundo: 'primary', texto: 'on-primary' },
  Tonal: { fundo: 'secondary-container', texto: 'on-secondary-container' },
  Elevated: { fundo: 'surface-container-low', texto: 'primary' },
  Outlined: { fundo: null, texto: 'on-surface-variant', borda: 'outline-variant' },
  Text: { fundo: null, texto: 'primary' },
};
const b = Object.fromEntries(tokensComp.filter((t) => t.nome.startsWith('button-') && !t.ref).map((t) => [t.nome.slice(7), t.valor]));
const rotulo = tipos['label-large'];
const xaml = `<?xml version="1.0" encoding="UTF-8" ?>
<!--
  boaz · tokens do design system (Material Design 3) para .NET MAUI
  ${AVISO}

  Uso: em App.xaml, <ResourceDictionary Source="Tokens.xaml" /> dentro de MergedDictionaries.
  Cores: <Papel>Light e <Papel>Dark; use com AppThemeBinding, como nos estilos abaixo.
  As cores abaixo são do tema padrão (${PADRAO.id}); os outros temas estão em paletas/Paleta<Tema>.xaml.
  Fontes: registre em MauiProgram.ConfigureFonts os aliases ${[...new Set(Object.values(tipos).map(aliasFonte))].join(', ')}
  (arquivos em 0_Assets/design/identidade/fontes/, listados em marca.json).
  Medidas em unidades independentes de dispositivo (= dp). Alto contraste e easing ficam só na web.
-->
<ResourceDictionary xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">

  <!-- ── cores ── -->
${Object.keys(ESQUEMAS.light).flatMap((k) => [
  `  <Color x:Key="${pascal(k)}Light">${ESQUEMAS.light[k]}</Color>`,
  `  <Color x:Key="${pascal(k)}Dark">${ESQUEMAS.dark[k]}</Color>`,
]).join('\n')}

  <!-- ── forma, espaçamento, layout, movimento ── -->
${Object.entries(cantos).map(([n, v]) => `  <CornerRadius x:Key="ShapeCorner${pascal(n)}">${v}</CornerRadius>`).join('\n')}
${espacos.map(([n, v]) => `  <x:Double x:Key="Spacing${n}">${v}</x:Double>`).join('\n')}
${Object.entries(layout).map(([n, v]) => `  <x:Double x:Key="Layout${pascal(n)}">${v}</x:Double>`).join('\n')}
${Object.entries(janelas).flatMap(([n, j]) => [
  `  <x:Double x:Key="Window${pascal(n)}Min">${j.min}</x:Double>`,
  `  <x:Double x:Key="Window${pascal(n)}Margin">${j.margin}</x:Double>`,
]).join('\n')}
${Object.entries(duracoes).map(([n, v]) => `  <x:UInt32 x:Key="MotionDuration${pascal(n)}">${v}</x:UInt32>`).join('\n')}
${Object.entries(elevacoes).filter(([n]) => SOMBRAS[n] !== 'none').map(([n]) => {
  const [y, blur] = SOMBRAS[n][1];
  return `  <Shadow x:Key="Elevation${pascal(n)}" Brush="Black" Opacity="0.25" Radius="${blur}" Offset="0,${y}" />`;
}).join('\n')}

  <!-- ── componentes: medidas (as cores seguem o papel indicado em tokens/componentes.json) ── -->
${tokensComp.filter((t) => !t.ref && typeof t.valor === 'number').map((t) => `  <x:Double x:Key="${pascal(t.nome)}">${t.valor}</x:Double>`).join('\n')}

  <!-- ── tipografia: um estilo de Label por papel (x:Key = papel em PascalCase) ── -->
${Object.entries(tipos).map(([n, t]) => [
  `  <Style x:Key="${pascal(n)}" TargetType="Label">`,
  setter('FontFamily', aliasFonte(t)),
  setter('FontSize', t.size),
  setter('LineHeight', arred(t['line-height'] / t.size, 3)),
  setter('CharacterSpacing', t.tracking),
  setter('TextColor', tema('on-surface')),
  '  </Style>',
].join('\n')).join('\n')}

  <!-- ── botões (Material 3): Style="{StaticResource Button<Variante>}" ── -->
${Object.entries(BOTOES).map(([n, v]) => [
  `  <Style x:Key="Button${n}" TargetType="Button">`,
  setter('MinimumHeightRequest', b.height),
  setter('MinimumWidthRequest', b['min-width']),
  setter('CornerRadius', b.height / 2),
  setter('Padding', `${n === 'Text' ? b['text-padding'] : b.padding},0`),
  setter('FontFamily', aliasFonte(rotulo)),
  setter('FontSize', rotulo.size),
  setter('CharacterSpacing', rotulo.tracking),
  setter('BackgroundColor', v.fundo ? tema(v.fundo) : 'Transparent'),
  setter('TextColor', tema(v.texto)),
  ...(v.borda ? [setter('BorderColor', tema(v.borda)), setter('BorderWidth', b['outlined-outline-width'])] : [setter('BorderWidth', 0)]),
  ...(n === 'Elevated' ? [setter('Shadow', '{StaticResource ElevationLevel1}')] : []),
  '  </Style>',
].join('\n')).join('\n')}

</ResourceDictionary>
`;

// MAUI: um dicionário por tema de cor, com as mesmas chaves das cores de Tokens.xaml
const xamlPaleta = (t) => `<?xml version="1.0" encoding="UTF-8" ?>
<!--
  boaz · tema de cor ${t.nome} (${t.id})${t === PADRAO ? ' · padrão' : ''} — ${t.harmonia}
  ${AVISO}

  Mesmas chaves de cor de Tokens.xaml (<Papel>Light / <Papel>Dark). Para usar este tema, inclua
  este arquivo DEPOIS de Tokens.xaml em MergedDictionaries; para trocar em tempo de execução,
  substitua o dicionário e recrie as páginas (os estilos usam StaticResource).
-->
<ResourceDictionary xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
${Object.keys(t.esquemas.light).flatMap((k) => [
  `  <Color x:Key="${pascal(k)}Light">${t.esquemas.light[k]}</Color>`,
  `  <Color x:Key="${pascal(k)}Dark">${t.esquemas.dark[k]}</Color>`,
]).join('\n')}
</ResourceDictionary>
`;

// ── 6. catálogo: bloco de tokens embutido no design-system.html ─────────────
const fontesGoogle = [...new Set([fonte.brand, fonte.plain].filter((f) => f.google_fonts).map((f) => f.familia))];
const ic = marca.icones;
const linkFontes = [
  ...fontesGoogle.map((f) => `<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=${f.replace(/ /g, '+')}:wght@400;500;700&display=swap">`),
  `<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=${ic.familia.replace(/ /g, '+')}:opsz,wght,FILL,GRAD@20..48,100..700,0..1,-50..200&display=block">`,
];
const htmlArq = 'design-system.html';
const html = readFileSync(join(RAIZ, htmlArq), 'utf8');
const INI = '<!-- design:tokens:inicio';
const FIM = '<!-- design:tokens:fim -->';
const i = html.indexOf(INI);
const f = html.indexOf(FIM);
if (i < 0 || f < 0) throw new Error(`${htmlArq}: marcadores ${INI} … ${FIM} não encontrados`);
const iconeCss = `.material-symbols { font-family: "${ic.familia}"; font-weight: normal; font-style: normal; font-size: var(--md-sys-layout-icon-size); line-height: 1; display: inline-block; white-space: nowrap; direction: ltr; -webkit-font-smoothing: antialiased; font-variation-settings: "FILL" ${ic.preenchimento}, "wght" ${ic.peso}, "GRAD" ${ic.grau}, "opsz" ${ic.tamanho_otico}; }`;
const novoBloco = `${html.slice(i, html.indexOf('\n', i) + 1)}${linkFontes.join('\n')}\n<style id="design-tokens">\n${css}\n${tipografiaCss}\n${iconeCss}\n</style>\n<script id="design-dados" type="application/json">${JSON.stringify({ semente: c.semente.toUpperCase(), variante: c.variante, status: marca.status, fontes: { marca: fonte.brand.familia, texto: fonte.plain.familia }, icones: ic.familia, extras: EXTRAS.map(([n]) => n), temas: Object.values(TEMAS).map((t) => ({ id: t.id, nome: t.nome, descricao: t.descricao, harmonia: t.harmonia, semente: t.semente, terciaria: t.terciaria, modo: t.modo, padrao: t === PADRAO })), papeis: Object.keys(ESQUEMAS.light), paletas: Object.fromEntries(Object.entries(paletas).map(([n, p]) => [n, Object.keys(p).map(Number)])), tipos, janelas })}</script>\n`;
const nucleoMascotes = readFileSync(join(RAIZ, 'scripts', 'mascotes.mjs'), 'utf8');
const blocoMascotes = `<script type="module" id="design-mascotes">\n${nucleoMascotes}\nwindow.boazMascotes = { EXPRESSOES, FORMAS, PROPORCOES, MOVIMENTOS, CORES, estatico, animado, sequencia, temas: ${JSON.stringify(temasDosMascotes(cores, marca))} };\nwindow.dispatchEvent(new Event('boaz-mascotes'));\n</script>\n`;
const htmlNovo = html.slice(0, i) + novoBloco + blocoMascotes + html.slice(f);

// ── 7. quadro visual tema.svg (versão e data vêm do catálogo) ──────────────
const meta = (n) => (html.match(new RegExp(`<meta name="design:${n}" content="([^"]*)"`)) || [])[1];
const svg = temaSvg({
  esquemas: { light: ESQUEMAS.light, dark: ESQUEMAS.dark }, tipos, cantos, janelas, tokensComp, fonte,
  versao: meta('versao'), data: meta('data'), status: marca.status, semente: c.semente.toUpperCase(), variante: c.variante,
  extras: EXTRAS.map(([n]) => n),
  temas: Object.values(TEMAS).map((t) => ({ ...t, padrao: t === PADRAO })),
  mascote: (id, op, prefixo, x, y, largura, altura) => embutir(mascoteEstatico(id, op), prefixo, x, y, largura, altura),
  mascotes: {
    expressoes: Object.entries(M_EXPR).map(([k, v]) => [k, v.nome]),
    formas: [['bolha', 'feliz'], ...Object.entries(M_VITRINE.formas)].map(([k, e]) => [k, M_FORMAS[k].nome, e]),
    glifos: Object.entries(M_FORMAS).filter(([, v]) => v.tipo === 'glifo').map(([k, v]) => [k, v.nome.replace(/^(Número|Letra) /, '')]),
    proporcoes: Object.entries(M_PROP).map(([k, v]) => [k, v.nome]),
  },
});

// ── 8. escrever ou conferir ─────────────────────────────────────────────────
const saidas = {
  'tokens/cores.json': JSON.stringify(cores, null, 2) + '\n',
  'plataformas/web/tokens.css': css,
  'plataformas/web/tipografia.css': tipografiaCss,
  'plataformas/web/tokens.ts': ts,
  'plataformas/maui/Tokens.xaml': xaml,
  ...Object.fromEntries(Object.values(TEMAS).map((t) => [`plataformas/maui/paletas/Paleta${pascal(t.id)}.xaml`, xamlPaleta(t)])),
  [htmlArq]: htmlNovo,
  'tema.svg': svg,
  ...arquivosMascotes(cores, marca),
};
Object.assign(saidas, copiasSincronizadas(RAIZ, saidas));
let diferentes = 0;
for (const [arq, conteudo] of Object.entries(saidas)) {
  const caminho = join(RAIZ, arq);
  let atual = null;
  try { atual = readFileSync(caminho, 'utf8'); } catch { /* arquivo novo */ }
  if (atual === conteudo) { console.log(`  = ${arq}`); continue; }
  diferentes++;
  if (CONFERIR) console.log(`  ✖ ${arq} desatualizado`);
  else { mkdirSync(dirname(caminho), { recursive: true }); writeFileSync(caminho, conteudo); console.log(`  ✔ ${arq}`); }
}
// mascotes/: arquivo que o gerador não produz mais é sobra (apagado ao gerar, acusado ao conferir)
const listar = (d) => (existsSync(d) ? readdirSync(d).flatMap((n) => (statSync(join(d, n)).isDirectory() ? listar(join(d, n)) : [join(d, n)])) : []);
for (const arq of listar(join(RAIZ, 'mascotes')).map((p) => relative(RAIZ, p)).filter((p) => !(p in saidas))) {
  diferentes++;
  if (CONFERIR) console.log(`  ✖ ${arq} sobrando`);
  else { rmSync(join(RAIZ, arq)); console.log(`  ✖ ${arq} removido`); }
}
if (CONFERIR && diferentes) {
  console.error(`\n${diferentes} arquivo(s) desatualizado(s): rode \`pnpm run gerar\` em ${relative(process.cwd(), RAIZ) || '.'}`);
  process.exit(1);
}
