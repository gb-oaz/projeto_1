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
//                         design-system.html           só o bloco entre os marcadores
//                                                      design:tokens:inicio/fim
//   pnpm run conferir   não escreve nada; falha se algum arquivo gerado estiver
//                       diferente do que seria gerado agora
//
// Cores: @material/material-color-utilities (a biblioteca oficial do M3, HCT).
// Nada aqui é editado à mão: mude a identidade ou os tokens e rode de novo.
// =============================================================================
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  argbFromHex, hexFromArgb, Hct, TonalPalette, DynamicScheme, DynamicColor,
  MaterialDynamicColors, customColor,
  SchemeTonalSpot, SchemeFidelity, SchemeContent, SchemeVibrant, SchemeExpressive,
  SchemeNeutral, SchemeMonochrome, SchemeRainbow, SchemeFruitSalad,
} from '@material/material-color-utilities';

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
const c = marca.cores;
const Variante = VARIANTES[c.variante];
if (!Variante) throw new Error(`cores.variante "${c.variante}" inválida: use ${Object.keys(VARIANTES).join(', ')}`);
const semente = argbFromHex(c.semente);
const paletaDe = (h) => (h ? TonalPalette.fromInt(argbFromHex(h)) : null);

function esquema(escuro, contraste) {
  const base = new Variante(Hct.fromInt(semente), escuro, contraste);
  const s = new DynamicScheme({
    sourceColorArgb: semente, variant: base.variant, contrastLevel: contraste, isDark: escuro,
    primaryPalette: base.primaryPalette,
    secondaryPalette: paletaDe(c.secundaria) ?? base.secondaryPalette,
    tertiaryPalette: paletaDe(c.terciaria) ?? base.tertiaryPalette,
    neutralPalette: paletaDe(c.neutra) ?? base.neutralPalette,
    neutralVariantPalette: base.neutralVariantPalette,
  });
  if (c.erro) s.errorPalette = paletaDe(c.erro);
  return s;
}

// Papéis de cor do M3, na ordem da biblioteca (sem as cores-chave das paletas)
const PAPEIS = Object.getOwnPropertyNames(MaterialDynamicColors)
  .filter((k) => MaterialDynamicColors[k] instanceof DynamicColor && !k.endsWith('PaletteKeyColor'));

const EXTRAS = Object.entries(c.extras ?? {}).filter(([k]) => !k.startsWith('$'))
  .map(([nome, e]) => [nome, customColor(semente, { name: nome, value: argbFromHex(e.cor), blend: !!e.harmonizar })]);

function papeis(escuro, contraste) {
  const s = esquema(escuro, contraste);
  const r = Object.fromEntries(PAPEIS.map((k) => [kebab(k), hex(MaterialDynamicColors[k].getArgb(s))]));
  for (const [nome, cc] of EXTRAS) {
    const g = escuro ? cc.dark : cc.light;
    Object.assign(r, {
      [nome]: hex(g.color), [`on-${nome}`]: hex(g.onColor),
      [`${nome}-container`]: hex(g.colorContainer), [`on-${nome}-container`]: hex(g.onColorContainer),
    });
  }
  return r;
}

const ESQUEMAS = {
  light: papeis(false, 0),
  dark: papeis(true, 0),
  'light-high-contrast': papeis(false, 1),
  'dark-high-contrast': papeis(true, 1),
};

const TONS = [0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 95, 99, 100];
const TONS_NEUTROS = [0, 4, 6, 10, 12, 17, 20, 22, 24, 30, 40, 50, 60, 70, 80, 87, 90, 92, 94, 95, 96, 98, 99, 100];
const ref = esquema(false, 0);
const PALETAS = {
  primary: [ref.primaryPalette, TONS], secondary: [ref.secondaryPalette, TONS],
  tertiary: [ref.tertiaryPalette, TONS], neutral: [ref.neutralPalette, TONS_NEUTROS],
  'neutral-variant': [ref.neutralVariantPalette, TONS], error: [ref.errorPalette, TONS],
  ...Object.fromEntries(EXTRAS.map(([nome, cc]) => [nome, [TonalPalette.fromInt(cc.value), TONS]])),
};
const paletas = Object.fromEntries(Object.entries(PALETAS)
  .map(([nome, [p, tons]]) => [nome, Object.fromEntries(tons.map((t) => [t, hex(p.tone(t))]))]));

const cores = {
  $descricao: `${AVISO} Esquema ${c.variante} da semente ${c.semente.toUpperCase()}.`,
  md: {
    ref: { palette: Object.fromEntries(Object.entries(paletas).map(([n, p]) =>
      [n, Object.fromEntries(Object.entries(p).map(([t, v]) => [t, { $type: 'color', $value: v }]))])) },
    sys: { color: Object.fromEntries(Object.entries(ESQUEMAS).map(([n, e]) =>
      [n, Object.fromEntries(Object.entries(e).map(([k, v]) => [k, { $type: 'color', $value: v }]))])) },
  },
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

const css = [
  `/* boaz · tokens do design system (Material Design 3)\n * ${AVISO}\n *\n * Esquema claro por padrão; escuro pelo sistema ou por <html data-theme="dark">\n * (data-theme="light" força o claro). Alto contraste segue prefers-contrast: more.\n * .md-scheme-light / .md-scheme-dark aplicam um esquema só a um trecho da página. */`,
  bloco(':root', [...raiz, '', '/* janela compact (padrão, celular) */', ...janelaVars(primeira[1], primeira[0])]),
  bloco(':root,\n.md-scheme-light,\n.md-scheme-dark', dependentes),
  ...demais.map(([n, j]) => `@media (min-width: ${j.min}px) {\n${bloco(':root', janelaVars(j, n), '  ')}\n}`),
  bloco(':root,\n.md-scheme-light', ['color-scheme: light;', ...varsCor(ESQUEMAS.light)]),
  bloco('.md-scheme-dark', ['color-scheme: dark;', ...varsCor(ESQUEMAS.dark)]),
  `@media (prefers-color-scheme: dark) {\n${bloco(':root:not([data-theme="light"])', ['color-scheme: dark;', ...varsCor(ESQUEMAS.dark)], '  ')}\n}`,
  bloco(':root[data-theme="dark"]', ['color-scheme: dark;', ...varsCor(ESQUEMAS.dark)]),
  `@media (prefers-contrast: more) {\n${bloco(':root,\n  .md-scheme-light', varsCor(ESQUEMAS['light-high-contrast']), '  ')}\n${bloco('.md-scheme-dark', varsCor(ESQUEMAS['dark-high-contrast']), '  ')}\n}`,
  `@media (prefers-contrast: more) and (prefers-color-scheme: dark) {\n${bloco(':root:not([data-theme="light"])', varsCor(ESQUEMAS['dark-high-contrast']), '  ')}\n}`,
  `@media (prefers-contrast: more) {\n${bloco(':root[data-theme="dark"]', varsCor(ESQUEMAS['dark-high-contrast']), '  ')}\n}`,
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
const novoBloco = `${html.slice(i, html.indexOf('\n', i) + 1)}${linkFontes.join('\n')}\n<style id="design-tokens">\n${css}\n${tipografiaCss}\n${iconeCss}\n</style>\n<script id="design-dados" type="application/json">${JSON.stringify({ semente: c.semente.toUpperCase(), variante: c.variante, status: marca.status, fontes: { marca: fonte.brand.familia, texto: fonte.plain.familia }, icones: ic.familia, extras: EXTRAS.map(([n]) => n), papeis: Object.keys(ESQUEMAS.light), paletas: Object.fromEntries(Object.entries(paletas).map(([n, p]) => [n, Object.keys(p).map(Number)])), tipos, janelas })}</script>\n`;
const htmlNovo = html.slice(0, i) + novoBloco + html.slice(f);

// ── 7. escrever ou conferir ─────────────────────────────────────────────────
const saidas = {
  'tokens/cores.json': JSON.stringify(cores, null, 2) + '\n',
  'plataformas/web/tokens.css': css,
  'plataformas/web/tipografia.css': tipografiaCss,
  'plataformas/web/tokens.ts': ts,
  'plataformas/maui/Tokens.xaml': xaml,
  [htmlArq]: htmlNovo,
};
let diferentes = 0;
for (const [arq, conteudo] of Object.entries(saidas)) {
  const caminho = join(RAIZ, arq);
  let atual = null;
  try { atual = readFileSync(caminho, 'utf8'); } catch { /* arquivo novo */ }
  if (atual === conteudo) { console.log(`  = ${arq}`); continue; }
  diferentes++;
  if (CONFERIR) console.log(`  ✖ ${arq} desatualizado`);
  else { writeFileSync(caminho, conteudo); console.log(`  ✔ ${arq}`); }
}
if (CONFERIR && diferentes) {
  console.error(`\n${diferentes} arquivo(s) desatualizado(s): rode \`pnpm run gerar\` em ${relative(process.cwd(), RAIZ) || '.'}`);
  process.exit(1);
}
