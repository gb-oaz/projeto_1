// =============================================================================
// boaz · tema.svg — quadro visual do design system
//
// Chamado por gerar.mjs. Desenha cores, tipografia, forma, elevação, layout por
// janela e os componentes (claro e escuro lado a lado) com os valores já
// resolvidos (hex e px literais): o SVG abre igual no navegador, no GitHub, no
// Figma e no Inkscape, sem depender de var(--…).
//
// Componente novo: acrescente uma entrada em COMPONENTES (título, descrição,
// altura do conteúdo e a função que desenha num esquema). Use só as funções de
// apoio abaixo e os tokens recebidos — nada de cor ou medida solta.
// =============================================================================

const esc = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

// Ícones Material (Apache 2.0), viewBox 24×24, preenchidos com a cor do <use>
const ICONES = {
  add: 'M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z',
  check: 'M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z',
  close: 'M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z',
  menu: 'M3 18h18v-2H3v2zm0-5h18v-2H3v2zm0-7v2h18V6H3z',
  more: 'M12 8c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm0 2c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0 6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z',
  voltar: 'M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z',
  inicio: 'M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z',
  buscar: 'M15.5 14h-.79l-.28-.27C15.41 12.59 16 11.11 16 9.5 16 5.91 13.09 3 9.5 3S3 5.91 3 9.5 5.91 16 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z',
  favorito: 'M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z',
  salvos: 'M17 3H7c-1.1 0-1.99.9-1.99 2L5 21l7-3 7 3V5c0-1.1-.9-2-2-2z',
  perfil: 'M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z',
  editar: 'M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z',
  avisos: 'M12 22c1.1 0 2-.9 2-2h-4c0 1.1.89 2 2 2zm6-6v-5c0-3.07-1.64-5.64-4.5-6.32V4c0-.83-.67-1.5-1.5-1.5s-1.5.67-1.5 1.5v.68C7.63 5.36 6 7.92 6 11v5l-2 2v1h16v-1l-2-2z',
  apagar: 'M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z',
  evento: 'M17 12h-5v5h5v-5zM16 1v2H8V1H6v2H5c-1.11 0-1.99.9-1.99 2L3 19c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2h-1V1h-2zm3 18H5V8h14v11z',
  anterior: 'M15.41 7.41L14 6l-6 6 6 6 1.41-1.41L10.83 12z',
  proximo: 'M10 6L8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z',
  modulo: 'M3 13h8V3H3v10zm0 8h8v-6H3v6zm10 0h8V11h-8v10zm0-18v6h8V3h-8z',
  escola: 'M5 13.18v4L12 21l7-3.82v-4L12 17l-7-3.82zM12 3L1 9l11 6 9-4.91V17h2V9L12 3z',
  relogio: 'M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm.5-13H11v6l5.25 3.15.75-1.23-4.5-2.67z',
  camera: 'M12 15.2a3.2 3.2 0 100-6.4 3.2 3.2 0 000 6.4zM9 2L7.17 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2h-3.17L15 2H9z',
  paleta: 'M12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9c.83 0 1.5-.67 1.5-1.5 0-.39-.15-.74-.39-1.01-.23-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c2.76 0 5-2.24 5-5 0-4.42-4.03-8-9-8zm-5.5 9c-.83 0-1.5-.67-1.5-1.5S5.67 9 6.5 9 8 9.67 8 10.5 7.33 12 6.5 12zm3-4C8.67 8 8 7.33 8 6.5S8.67 5 9.5 5s1.5.67 1.5 1.5S10.33 8 9.5 8zm5 0c-.83 0-1.5-.67-1.5-1.5S13.67 5 14.5 5s1.5.67 1.5 1.5S15.33 8 14.5 8zm3 4c-.83 0-1.5-.67-1.5-1.5S16.67 9 17.5 9s1.5.67 1.5 1.5-.67 1.5-1.5 1.5z',
  arrastar: 'M11 18c0 1.1-.9 2-2 2s-2-.9-2-2 .9-2 2-2 2 .9 2 2zm-2-8c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0-6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm6 4c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm0 2c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0 6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z',
  cima: 'M4 12l1.41 1.41L11 7.83V20h2V7.83l5.58 5.59L20 12l-8-8-8 8z',
  baixo: 'M20 12l-1.41-1.41L13 16.17V4h-2v12.17l-5.58-5.59L4 12l8 8 8-8z',
  repetir: 'M7 7h10v3l4-4-4-4v3H5v6h2V7zm10 10H7v-3l-4 4 4 4v-3h12v-6h-2v4z',
  chat: 'M20 2H4c-1.1 0-1.99.9-1.99 2L2 22l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zM6 9h12v2H6V9zm8 5H6v-2h8v2zm4-6H6V6h12v2z',
};

// Funções de desenho (componentes, compostos, marca) com os tokens resolvidos; usadas pelo
// tema.svg e pelas vistas de 2_Diagramas, para que as telas saiam iguais ao design system.
export function desenhista({ esquemas, tipos, cantos, janelas, tokensComp, fonte, versao, data, status, semente, variante, extras, temas, mascote, mascotes, logos, prefixo = 'm', coresLogo = null, desenharFundo = null }) {
  let nm = 0; // prefixo único de cada mascote embutido
  const masc = (id, op, x, y, w, h) => mascote(id, op, `${prefixo}${++nm}-`, x, y, w, h);
  const L = 1600, M = 40, GAP = 40, PW = (L - 2 * M - GAP) / 2, PAD = 24;
  const fundo = esquemas.dark; // o quadro é escuro, como o tema dos diagramas
  const comp = Object.fromEntries(tokensComp.map((t) => [t.nome, t]));

  // ── tokens de componente ──
  const num = (n) => { const t = comp[n]; if (!t || typeof t.valor !== 'number') throw new Error(`tema.svg: md.comp.${n} não é medida`); return t.valor; };
  const cor = (e, n) => { const t = comp[n]; if (!t?.ref || t.ref.grupo !== 'color') throw new Error(`tema.svg: md.comp.${n} não é cor`); return e[t.ref.nome]; };
  const canto = (n, h) => { const t = comp[n]; if (!t?.ref || t.ref.grupo !== 'shape') throw new Error(`tema.svg: md.comp.${n} não é forma`); return t.ref.nome === 'full' ? h / 2 : cantos[t.ref.nome]; };
  const tipoDe = (n) => { const t = comp[n]; if (!t?.ref || t.ref.grupo !== 'typescale') throw new Error(`tema.svg: md.comp.${n} não é tipografia`); return t.ref.nome; };
  const nivel = (n) => { const t = comp[n]; return t?.ref?.grupo === 'elevation' ? t.ref.nome : 'level0'; };

  // ── primitivas ──
  // quebra um texto em linhas que caibam em `max` px no papel tipográfico dado
  const quebrar = (texto, max, papel) => texto.split(' ').reduce((ls, p) => {
    const u = ls[ls.length - 1];
    if (u && largura(u + ' ' + p, papel) <= max) ls[ls.length - 1] = u + ' ' + p; else ls.push(p);
    return ls;
  }, []);
  const largura = (s, papel) => String(s).length * tipos[papel].size * (tipos[papel].weight >= 500 ? 0.58 : 0.55);
  const rect = (x, y, w, h, r, fill, extra = '') => `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${Math.min(r, h / 2, w / 2)}"${fill ? ` fill="${fill}"` : ' fill="none"'}${extra ? ' ' + extra : ''}/>`;
  const sombra = (n) => (n && n !== 'level0' ? ` filter="url(#el-${n})"` : '');
  const txt = (x, y, s, papel, fill, anchor = 'start', extra = '') => `<text x="${x}" y="${y}" class="t-${papel}" fill="${fill}"${anchor !== 'start' ? ` text-anchor="${anchor}"` : ''}${extra ? ' ' + extra : ''}>${esc(s)}</text>`;
  const mono = (x, y, s, fill, anchor = 'start') => `<text x="${x}" y="${y}" class="mono" fill="${fill}"${anchor !== 'start' ? ` text-anchor="${anchor}"` : ''}>${esc(s)}</text>`;
  const icone = (nome, x, y, tam, fill) => { if (!ICONES[nome]) throw new Error(`tema.svg: ícone ${nome}`); return `<use href="#i-${nome}" x="${x}" y="${y}" width="${tam}" height="${tam}" fill="${fill}"/>`; };
  // texto centralizado verticalmente numa faixa de altura h (baseline ≈ meio + 0,35·tamanho)
  const meio = (y, h, papel) => y + h / 2 + tipos[papel].size * 0.35;

  // ── componentes ──
  // sobreFundo: botão de texto solto na tela (sobre a Estampa) ganha o fundo da tela; dentro de card e diálogo fica transparente
  function botao(e, x, y, rotulo, variante, { icon, desabilitado, mascote: mx, sobreFundo = false } = {}) {
    const h = num('button-height'), papel = tipoDe('button-label');
    const pad = variante === 'text' ? num('button-text-padding') : num('button-padding');
    const padIc = num('button-padding-with-icon'), tamIc = num('button-icon-size'), gap = num('button-gap');
    if (mx) icon = 'mascote';
    const w = Math.round(largura(rotulo, papel) + (icon ? padIc + tamIc + gap + pad : 2 * pad));
    const r = canto('button-shape', h);
    const V = {
      filled: [cor(e, 'button-filled-container-color'), cor(e, 'button-filled-label-color'), nivel('button-filled-elevation')],
      tonal: [cor(e, 'button-tonal-container-color'), cor(e, 'button-tonal-label-color'), nivel('button-tonal-elevation')],
      elevated: [cor(e, 'button-elevated-container-color'), cor(e, 'button-elevated-label-color'), nivel('button-elevated-elevation')],
      outlined: [cor(e, 'button-outlined-container-color'), cor(e, 'button-outlined-label-color'), null],
      text: [sobreFundo ? cor(e, 'button-text-container-color') : null, cor(e, 'button-text-label-color'), null],
    }[variante];
    let [bg, fg, el] = V;
    let borda = variante === 'outlined' ? `stroke="${cor(e, 'button-outlined-outline-color')}" stroke-width="${num('button-outlined-outline-width')}"` : '';
    let op = '';
    if (desabilitado) {
      fg = e['on-surface']; op = ' opacity="0.38"'; el = null;
      if (bg) bg = e['on-surface'];
      if (borda) borda = `stroke="${e['on-surface']}" stroke-opacity="0.12"`;
    }
    const caixa = bg && desabilitado ? rect(x, y, w, h, r, bg, 'fill-opacity="0.12"') : rect(x, y, w, h, r, bg, borda + sombra(el));
    const tx = icon ? x + padIc + tamIc + gap : x + w / 2;
    return {
      w, svg: `<g>${caixa}<g${op}>${mx ? masc(mx[0], mx[1], x + padIc - 6, y + (h - tamIc) / 2 - 6, tamIc + 12) : icon ? icone(icon, x + padIc, y + (h - tamIc) / 2, tamIc, fg) : ''}${txt(tx, meio(y, h, papel), rotulo, papel, fg, icon ? 'start' : 'middle')}</g></g>`,
    };
  }
  const fila = (itens, x, y, gap = 12) => { let cx = x; return itens.map((f) => { const r = f(cx, y); cx += r.w + gap; return r.svg; }).join(''); };

  function iconButton(e, x, y, ic, tipo) {
    const s = num('icon-button-size'), ti = num('icon-button-icon-size'), r = canto('icon-button-shape', s);
    const T = {
      padrao: [null, cor(e, 'icon-button-icon-color'), ''],
      selecionado: [null, cor(e, 'icon-button-selected-icon-color'), ''],
      filled: [e.primary, e['on-primary'], ''],
      tonal: [e['secondary-container'], e['on-secondary-container'], ''],
      outlined: [null, e['on-surface-variant'], `stroke="${e['outline-variant']}"`],
    }[tipo];
    return { w: s, svg: rect(x, y, s, s, r, T[0], T[2]) + icone(ic, x + (s - ti) / 2, y + (s - ti) / 2, ti, T[1]) };
  }

  function fab(e, x, y, tam, rotulo) {
    const bg = cor(e, 'fab-container-color'), fg = cor(e, 'fab-icon-color'), el = nivel('fab-elevation');
    const T = { small: ['fab-small-size', 'fab-small-shape', 'fab-icon-size'], normal: ['fab-size', 'fab-shape', 'fab-icon-size'], large: ['fab-large-size', 'fab-large-shape', 'fab-large-icon-size'] }[tam];
    const h = num(T[0]), ti = num(T[2]), r = canto(T[1], h);
    if (rotulo) {
      const papel = tipoDe('fab-extended-label'), p = num('fab-extended-padding');
      const w = Math.round(p + ti + 12 + largura(rotulo, papel) + p + 4);
      return { w, svg: rect(x, y, w, h, r, bg, sombra(el)) + icone('editar', x + p, y + (h - ti) / 2, ti, fg) + txt(x + p + ti + 12, meio(y, h, papel), rotulo, papel, fg) };
    }
    return { w: h, svg: rect(x, y, h, h, r, bg, sombra(el)) + icone('add', x + (h - ti) / 2, y + (h - ti) / 2, ti, fg) };
  }

  // chip; com largura, ocupa o espaço dado com o conteúdo centrado (opções que dividem a linha por igual)
  function chip(e, x, y, rotulo, { icon, selecionado, largura: cheio } = {}) {
    const h = num('chip-height'), papel = tipoDe('chip-label'), p = num('chip-padding'), pi = num('chip-padding-with-icon'), ti = num('chip-icon-size');
    const ic = selecionado ? 'check' : icon;
    const nat = Math.round(largura(rotulo, papel) + (ic ? pi + ti + 8 + p : 2 * p)), w = cheio || nat, dx = cheio ? (cheio - nat) / 2 : 0;
    x += dx;
    const r = canto('chip-shape', h);
    const bg = selecionado ? cor(e, 'chip-selected-container-color') : cor(e, 'chip-container-color');
    const fg = selecionado ? cor(e, 'chip-selected-label-color') : cor(e, 'chip-label-color');
    const borda = selecionado ? '' : `stroke="${cor(e, 'chip-outline-color')}"`;
    return { w, svg: rect(x - dx + 0.5, y + 0.5, w - 1, h - 1, r, bg, borda) + (ic ? icone(ic, x + pi, y + (h - ti) / 2, ti, selecionado ? fg : e.primary) : '') + txt(ic ? x + pi + ti + 8 : x + nat / 2, meio(y, h, papel), rotulo, papel, fg, ic ? 'start' : 'middle') };
  }

  function card(e, x, y, w, h, tipo, titulo, linhas) {
    const r = canto('card-shape', h), p = num('card-padding');
    const T = {
      elevated: [cor(e, 'card-elevated-container-color'), sombra(nivel('card-elevated-elevation'))],
      filled: [cor(e, 'card-filled-container-color'), ''],
      outlined: [cor(e, 'card-outlined-container-color'), `stroke="${cor(e, 'card-outlined-outline-color')}"`],
    }[tipo];
    const pt = tipoDe('card-title'), pb = tipoDe('card-body');
    return rect(x, y, w, h, r, T[0], T[1]) + txt(x + p, y + p + tipos[pt].size, titulo, pt, e['on-surface'])
      + linhas.map((l, i) => txt(x + p, y + p + tipos[pt]['line-height'] + 4 + tipos[pb].size + i * tipos[pb]['line-height'], l, pb, e['on-surface-variant'])).join('');
  }

  function campo(e, x, y, w, tipo, rotulo, valor, apoio) {
    const h = num('text-field-height'), p = num('text-field-padding');
    const pl = tipoDe('text-field-label'), pp = tipoDe('text-field-populated-label'), ps = tipoDe('text-field-supporting-text');
    const erro = tipo === 'erro', foco = tipo === 'outlined';
    const destaque = erro ? cor(e, 'text-field-error-color') : foco ? cor(e, 'text-field-focus-color') : cor(e, 'text-field-label-color');
    let s = '';
    if (tipo === 'outlined') {
      const r = canto('text-field-outlined-shape', h);
      // contêiner com o fundo da tela: o valor e o rótulo ficam legíveis sobre a Estampa
      const fc = cor(e, 'text-field-outlined-container-color');
      s += rect(x + 1, y + 1, w - 2, h - 2, r, fc, `stroke="${destaque}" stroke-width="2"`);
      const lw = largura(rotulo, pp) + 8, la = tipos[pp].size;
      s += `<rect x="${x + p - 4}" y="${y - la * 0.7}" width="${lw}" height="${la * 0.7 + 3}" fill="${fc}"/>`;
      s += txt(x + p, y + tipos[pp].size * 0.35, rotulo, pp, destaque);
      s += txt(x + p, meio(y, h, pl), valor, pl, cor(e, 'text-field-input-color'));
    } else {
      const r = canto('text-field-filled-shape', h);
      s += `<path d="M${x} ${y + h}V${y + r}a${r} ${r} 0 0 1 ${r} -${r}H${x + w - r}a${r} ${r} 0 0 1 ${r} ${r}V${y + h}Z" fill="${cor(e, 'text-field-filled-container-color')}"/>`;
      s += `<rect x="${x}" y="${y + h - (erro ? 2 : 1)}" width="${w}" height="${erro ? 2 : 1}" fill="${erro ? destaque : cor(e, 'text-field-filled-indicator-color')}"/>`;
      if (valor) {
        s += txt(x + p, y + 8 + tipos[pp].size, rotulo, pp, destaque);
        s += txt(x + p, y + h - 10, valor, pl, cor(e, 'text-field-input-color'));
      } else s += txt(x + p, meio(y, h, pl), rotulo, pl, cor(e, 'text-field-label-color'));
    }
    return s + txt(x + p, y + h + 4 + tipos[ps].size, apoio, ps, erro ? destaque : cor(e, 'text-field-label-color'));
  }

  function topAppBar(e, x, y, w, titulo) {
    const h = num('top-app-bar-height'), papel = tipoDe('top-app-bar-title'), ic = cor(e, 'top-app-bar-icon-color');
    return rect(x, y, w, h, 0, cor(e, 'top-app-bar-container-color')) + icone('voltar', x + 16, y + (h - 24) / 2, 24, ic)
      + txt(x + 56, meio(y, h, papel), titulo, papel, cor(e, 'top-app-bar-title-color')) + icone('more', x + w - 40, y + (h - 24) / 2, 24, ic);
  }
  const DESTINOS = [['inicio', 'Início'], ['buscar', 'Buscar'], ['salvos', 'Salvos'], ['perfil', 'Perfil']];
  function navBar(e, x, y, w) {
    const h = num('navigation-bar-height'), iw = num('navigation-bar-indicator-width'), ih = num('navigation-bar-indicator-height');
    const papel = tipoDe('navigation-bar-label'), passo = w / DESTINOS.length;
    return rect(x, y, w, h, 0, cor(e, 'navigation-bar-container-color')) + DESTINOS.map(([ic, r], i) => {
      const cx = x + passo * i + passo / 2, ativo = i === 0;
      return (ativo ? rect(cx - iw / 2, y + 12, iw, ih, canto('navigation-bar-indicator-shape', ih), cor(e, 'navigation-bar-indicator-color')) : '')
        + icone(ic, cx - 12, y + 12 + (ih - 24) / 2, 24, ativo ? cor(e, 'navigation-bar-active-icon-color') : cor(e, 'navigation-bar-icon-color'))
        + txt(cx, y + 12 + ih + 4 + tipos[papel].size, r, papel, ativo ? cor(e, 'navigation-bar-active-label-color') : cor(e, 'navigation-bar-label-color'), 'middle');
    }).join('');
  }
  function navRail(e, x, y, h) {
    const w = num('navigation-rail-width'), iw = num('navigation-rail-indicator-width'), ih = num('navigation-rail-indicator-height'), papel = tipoDe('navigation-rail-label');
    let s = rect(x, y, w, h, 0, cor(e, 'navigation-rail-container-color')) + fab(e, x + (w - 56) / 2, y + 12, 'normal').svg.replace(/href="#i-add"/, 'href="#i-editar"');
    DESTINOS.forEach(([ic, r], i) => {
      const cy = y + 92 + i * 64, cx = x + w / 2, ativo = i === 0;
      if (ativo) s += rect(cx - iw / 2, cy, iw, ih, canto('navigation-rail-indicator-shape', ih), cor(e, 'navigation-rail-indicator-color'));
      s += icone(ic, cx - 12, cy + 4, 24, ativo ? e['on-secondary-container'] : e['on-surface-variant']);
      s += txt(cx, cy + ih + 4 + tipos[papel].size, r, papel, ativo ? e['on-surface'] : e['on-surface-variant'], 'middle');
    });
    return s;
  }
  function drawer(e, x, y, w, h) {
    const ih = num('navigation-drawer-item-height'), pl = tipoDe('navigation-drawer-label'), ph = tipoDe('navigation-drawer-headline'), r = cantos.large;
    let s = `<path d="M${x} ${y}H${x + w - r}a${r} ${r} 0 0 1 ${r} ${r}V${y + h - r}a${r} ${r} 0 0 1 -${r} ${r}H${x}Z" fill="${cor(e, 'navigation-drawer-container-color')}"/>`;
    s += txt(x + 28, y + 36, 'boaz', ph, e['on-surface-variant']);
    DESTINOS.forEach(([ic, rot], i) => {
      const iy = y + 56 + i * ih, ativo = i === 0;
      if (ativo) s += rect(x + 12, iy, w - 24, ih, canto('navigation-drawer-item-shape', ih), cor(e, 'navigation-drawer-indicator-color'));
      s += icone(ic, x + 28, iy + (ih - 24) / 2, 24, ativo ? e['on-secondary-container'] : e['on-surface-variant']);
      s += txt(x + 64, meio(iy, ih, pl), rot, pl, ativo ? e['on-secondary-container'] : e['on-surface-variant']);
    });
    return s;
  }
  function lista(e, x, y, w) {
    const pad = num('list-padding'), h2 = num('list-two-line-height'), h1 = num('list-one-line-height');
    const ph = tipoDe('list-headline'), ps = tipoDe('list-supporting-text');
    const itens = [['A', 'Ana Souza', 'Duas linhas: título e texto de apoio'], ['B', 'Bruno Lima', 'Altura mínima de 72dp'], [null, 'Uma linha · 56dp', null]];
    let cy = y, s = '';
    itens.forEach(([av, t, a], i) => {
      const h = a ? h2 : h1;
      if (i) s += `<rect x="${x}" y="${cy}" width="${w}" height="1" fill="${cor(e, 'list-divider-color')}"/>`;
      if (av) s += `<circle cx="${x + pad + 20}" cy="${cy + h / 2}" r="20" fill="${e['primary-container']}"/>` + txt(x + pad + 20, cy + h / 2 + 5.6, av, 'title-medium', e['on-primary-container'], 'middle');
      else s += icone('avisos', x + pad + 8, cy + (h - 24) / 2, 24, e['on-surface-variant']);
      const tx = x + pad + 40 + pad;
      if (a) s += txt(tx, cy + h / 2 - 2, t, ph, cor(e, 'list-headline-color')) + txt(tx, cy + h / 2 + 18, a, ps, cor(e, 'list-supporting-text-color'));
      else s += txt(tx, meio(cy, h, ph), t, ph, cor(e, 'list-headline-color'));
      cy += h;
    });
    return { h: cy - y, svg: s };
  }
  function dialogo(e, x, y, w) {
    const p = num('dialog-padding'), r = canto('dialog-shape', 200), ph = tipoDe('dialog-headline'), pb = tipoDe('dialog-body');
    const linhas = ['O texto que você escreveu será perdido.', 'Esta ação não pode ser desfeita.'];
    const h = p + tipos[ph]['line-height'] + 16 + linhas.length * tipos[pb]['line-height'] + 24 + 40 + p;
    let s = rect(x, y, w, h, r, cor(e, 'dialog-container-color'), sombra(nivel('dialog-elevation')));
    s += txt(x + p, y + p + tipos[ph].size, 'Descartar rascunho?', ph, cor(e, 'dialog-headline-color'));
    linhas.forEach((l, i) => { s += txt(x + p, y + p + tipos[ph]['line-height'] + 16 + tipos[pb].size + i * tipos[pb]['line-height'], l, pb, cor(e, 'dialog-body-color')); });
    const by = y + h - p - 40;
    const b2 = botao(e, 0, 0, 'Descartar', 'text').w, b1 = botao(e, 0, 0, 'Cancelar', 'text').w;
    s += botao(e, x + w - p - b2, by, 'Descartar', 'text').svg + botao(e, x + w - p - b2 - 8 - b1, by, 'Cancelar', 'text').svg;
    return { h, svg: s };
  }
  function snackbar(e, x, y, w) {
    const h = num('snackbar-min-height'), papel = tipoDe('snackbar-label'), r = canto('snackbar-shape', h);
    const acao = 'Desfazer', aw = largura(acao, 'label-large') + 24;
    return rect(x, y, w, h, r, cor(e, 'snackbar-container-color'), sombra(nivel('snackbar-elevation')))
      + txt(x + 16, meio(y, h, papel), 'Rascunho salvo', papel, cor(e, 'snackbar-label-color'))
      + txt(x + w - 48 - aw / 2, meio(y, h, 'label-large'), acao, 'label-large', cor(e, 'snackbar-action-color'), 'middle')
      + icone('close', x + w - 36, y + (h - 24) / 2, 24, cor(e, 'snackbar-label-color'));
  }


  // selo de notificação no canto (cx, cy) de um elemento; n vazio = ponto
  function selo(e, cx, cy, n) {
    const bg = cor(e, 'badge-color'), fg = cor(e, 'badge-label-color');
    if (!n) { const d = num('badge-small-size'); return `<circle cx="${cx}" cy="${cy}" r="${d / 2}" fill="${bg}"/>`; }
    const h = num('badge-large-height'), papel = tipoDe('badge-label');
    const w = Math.max(num('badge-large-min-width'), Math.round(largura(n, papel) + 2 * num('badge-large-padding')));
    return rect(cx - w / 2, cy - h / 2, w, h, canto('badge-shape', h), bg) + txt(cx, meio(cy - h / 2, h, papel), n, papel, fg, 'middle');
  }
  function abas(e, x, y, w, rotulos, ativa = 0) {
    const h = num('tabs-height'), papel = tipoDe('tabs-label'), passo = w / rotulos.length, ih = num('tabs-active-indicator-height');
    let s = rect(x, y, w, h, 0, cor(e, 'tabs-container-color')) + `<rect x="${x}" y="${y + h - 1}" width="${w}" height="1" fill="${cor(e, 'tabs-divider-color')}"/>`;
    rotulos.forEach((r, i) => {
      const cx = x + passo * i + passo / 2, at = i === ativa, lw = largura(r, papel);
      s += txt(cx, meio(y, h, papel), r, papel, at ? cor(e, 'tabs-active-label-color') : cor(e, 'tabs-label-color'), 'middle');
      if (at) s += rect(cx - lw / 2, y + h - ih, lw, ih + 2, ih, cor(e, 'tabs-active-indicator-color'));
    });
    return s;
  }
  // segmentado: sem largura, cada segmento mede o rótulo; com largura, ocupa o contêiner e divide por igual
  function segmentado(e, x, y, rotulos, sel, cheio = 0) {
    const h = num('segmented-button-height'), papel = tipoDe('segmented-button-label'), p = num('segmented-button-padding'), ti = num('segmented-button-icon-size');
    const ws = cheio ? rotulos.map((r, i) => (i < rotulos.length - 1 ? Math.round(cheio / rotulos.length) : cheio - Math.round(cheio / rotulos.length) * (rotulos.length - 1)))
      : rotulos.map((r, i) => Math.round(largura(r, papel) + 2 * p + (i === sel ? ti + 8 : 0)));
    const w = ws.reduce((a, b) => a + b, 0), r = canto('segmented-button-shape', h), linha = cor(e, 'segmented-button-outline-color');
    let s = rect(x, y, w, h, r, cor(e, 'segmented-button-container-color')), cx = x;
    rotulos.forEach((t, i) => {
      const at = i === sel;
      if (at) s += `<path d="${i === 0 ? `M${cx + r} ${y}H${cx + ws[i]}V${y + h}H${cx + r}a${r} ${r} 0 0 1 0 -${h}Z` : i === rotulos.length - 1 ? `M${cx} ${y}H${cx + ws[i] - r}a${r} ${r} 0 0 1 0 ${h}H${cx}Z` : `M${cx} ${y}h${ws[i]}v${h}h-${ws[i]}Z`}" fill="${cor(e, 'segmented-button-selected-container-color')}"/>`;
      const fg = at ? cor(e, 'segmented-button-selected-label-color') : cor(e, 'segmented-button-label-color');
      const ix = cheio ? cx + (ws[i] - largura(t, papel) - ti - 8) / 2 : cx + p;
      if (at) s += icone('check', ix, y + (h - ti) / 2, ti, fg);
      s += txt(at ? ix + ti + 8 : cx + ws[i] / 2, meio(y, h, papel), t, papel, fg, at ? 'start' : 'middle');
      if (i) s += `<rect x="${cx}" y="${y}" width="1" height="${h}" fill="${linha}"/>`;
      cx += ws[i];
    });
    return { w, svg: s + rect(x + 0.5, y + 0.5, w - 1, h - 1, r, null, `stroke="${linha}" stroke-width="${num('segmented-button-outline-width')}"`) };
  }
  // barra de busca: ícone de busca, texto de apoio (ou o que foi digitado) e, com texto, o botão de limpar
  function busca(e, x, y, w, apoio, valor = '') {
    const h = num('search-bar-height'), p = num('search-bar-padding'), g = num('search-bar-gap'), ti = num('search-bar-icon-size'), papel = tipoDe('search-bar-supporting-text');
    let s = rect(x, y, w, h, canto('search-bar-shape', h), cor(e, 'search-bar-container-color'));
    s += icone('buscar', x + p, y + (h - ti) / 2, ti, cor(e, 'search-bar-leading-icon-color'));
    s += txt(x + p + ti + g, meio(y, h, papel), valor || apoio, papel, valor ? cor(e, 'search-bar-input-text-color') : cor(e, 'search-bar-supporting-text-color'));
    if (valor) s += icone('close', x + w - p - ti, y + (h - ti) / 2, ti, cor(e, 'search-bar-trailing-icon-color'));
    return s;
  }
  function menu(e, x, y, w, itens) {
    const ih = num('menu-item-height'), p = num('menu-item-padding'), ti = num('menu-icon-size'), papel = tipoDe('menu-label');
    const h = itens.length * ih + 16;
    let s = rect(x, y, w, h, canto('menu-shape', h), cor(e, 'menu-container-color'), sombra(nivel('menu-elevation')));
    itens.forEach(([ic, t], i) => {
      const iy = y + 8 + i * ih;
      s += icone(ic, x + p, iy + (ih - ti) / 2, ti, cor(e, 'menu-icon-color')) + txt(x + p + ti + p, meio(iy, ih, papel), t, papel, cor(e, 'menu-label-color'));
    });
    return { h, svg: s };
  }
  // controles do carrossel: anterior, pontos, posição em texto, próximo
  function controlesCarrossel(e, x, y, w, total, atual) {
    const d = num('carousel-indicator-dot-size'), aw = num('carousel-indicator-active-width'), g = num('carousel-indicator-gap'), papel = tipoDe('carousel-position-label');
    const pw = aw + (total - 1) * (d + g), pos = `${atual + 1} de ${total}`, tw = largura(pos, papel);
    const bloco = pw + 16 + tw, s0 = x + (w - bloco) / 2, cy = y + 20;
    let s = rect(x, y, w, num('icon-button-size'), num('icon-button-size') / 2, cor(e, 'carousel-controls-container-color'))
      + iconButton(e, x, y, 'anterior', 'padrao').svg + iconButton(e, x + w - num('icon-button-size'), y, 'proximo', 'padrao').svg;
    let cx = s0;
    for (let i = 0; i < total; i++) {
      const ww = i === atual ? aw : d;
      s += rect(cx, cy - d / 2, ww, d, canto('carousel-indicator-shape', d), i === atual ? cor(e, 'carousel-indicator-active-color') : cor(e, 'carousel-indicator-color'));
      cx += ww + g;
    }
    return s + txt(s0 + pw + 16, meio(y, 40, papel), pos, papel, cor(e, 'carousel-position-label-color'));
  }


  // ── componentes compostos (requisitos 10_componentes-compostos-*) ──
  // logo oficial (símbolo + nome), monocromático em on-surface, como nos temas monocromáticos
  const logoW = (h, v = 'longo') => { const [, , w, hh] = logos[v].match(/viewBox="([^"]+)"/)[1].split(' ').map(Number); return Math.round(h * w / hh); };
  const logoAlt = (v) => Number(logos[v].match(/viewBox="([^"]+)"/)[1].split(' ')[3]);
  // logo centralizado na barra: o nome já fica centralizado na altura do símbolo (identidade);
  // mesma escala no hub (hastes longas, 40dp) e no quest (hastes curtas): o nome sai do mesmo tamanho
  const ESCALA_TOPO = 40 / 793;
  const logoNaBarra = (e, x, y, h, v = 'longo') => { const lh = Math.round(logoAlt(v) * ESCALA_TOPO); return logo(e, x, y + (h - lh) / 2, lh, v); };
  const logo = (e, x, y, h = 32, v = 'longo') => {
    const cs = coresLogo ? coresLogo(e) : null; let i = 0;
    return logos[v].replace('<svg ', `<svg x="${x}" y="${y}" width="${logoW(h, v)}" height="${h}" `).replace(/fill="#000000"/g, () => `fill="${cs ? cs[i++] : e['on-surface']}"`).replace(/ role="img" aria-label="[^"]*"/, '');
  };
  const caixaIcone = (e, x, y, ic) => rect(x, y, 40, 40, cantos.medium, e['secondary-container']) + icone(ic, x + 8, y + 8, 24, e['on-secondary-container']);
  // avatar: a foto (a letra no desenho) ou, sem foto, o mascote adotado ([expressão, forma]), sem círculo em volta
  const avatar = (e, cx, cy, r, letra) => Array.isArray(letra) ? masc(letra[0], { forma: letra[1] }, cx - r, cy - r, 2 * r, 2 * r) : `<circle cx="${cx}" cy="${cy}" r="${r}" fill="${e['primary-container']}"/>` + txt(cx, cy + 5, letra, 'title-medium', e['on-primary-container'], 'middle');
  // topo da casca: logo à esquerda (hastes longas no hub, curtas no quest, com o nome do mesmo tamanho);
  // à direita os avisos e a conta (hub) ou o perfil ativo com troca de perfil e a foto (quest)
  // topo da casca, igual no hub e no quest: logo (hastes longas no hub, curtas no quest), avisos, o nome
  // (instituição ou pessoa logada no hub, usuário no quest) e a foto; o toque no nome ou na foto abre as ações
  // conta = false (compact): só o logo e os avisos; a conta fica no destino Conta (Perfil no quest) da barra
  function topoCasca(e, x, y, w, { quest, nome = 'Escola Horizonte', letra = 'E', n = '2', conta = true } = {}) {
    const h = num('top-app-bar-height'), ib = num('icon-button-size'), tx = x + w - 56, v = quest ? 'curto' : 'longo';
    if (!conta) return rect(x, y, w, h, 0, cor(e, 'top-app-bar-container-color')) + logoNaBarra(e, x + 16, y, h, v)
      + iconButton(e, x + w - 4 - ib, y + (h - ib) / 2, 'avisos', 'padrao').svg + selo(e, x + w - 4 - 8, y + (h - ib) / 2 + 8, n);
    // o nome ocupa o espaço entre o logo e os avisos; se não couber, é cortado com reticências
    const livre = tx - (x + 16 + logoW(Math.round(logoAlt(v) * ESCALA_TOPO), v) + 12 + ib + 4);
    while (largura(nome, 'title-small') > livre && nome.length > 4) nome = nome.slice(0, -2).trimEnd() + '…';
    const bx = tx - largura(nome, 'title-small') - 4 - ib;
    return rect(x, y, w, h, 0, cor(e, 'top-app-bar-container-color')) + logoNaBarra(e, x + 16, y, h, v)
      + iconButton(e, bx, y + (h - ib) / 2, 'avisos', 'padrao').svg + selo(e, bx + ib - 8, y + (h - ib) / 2 + 8, n)
      + txt(tx, meio(y, h, 'title-small'), nome, 'title-small', e['on-surface'], 'end') + avatar(e, x + w - 32, y + h / 2, 16, letra);
  }
  // trilho da casca (medium e expanded): os mesmos destinos da barra, em coluna, com selo
  function trilho3(e, x, y, h, destinos, n, ativo = 0) {
    const w = num('navigation-rail-width'), iw = num('navigation-rail-indicator-width'), ih = num('navigation-rail-indicator-height'), papel = tipoDe('navigation-rail-label');
    let s = rect(x, y, w, h, 0, cor(e, 'navigation-rail-container-color'));
    // os destinos de conteúdo ficam no alto e o último (Conta ou Perfil) embaixo
    destinos.forEach(([ic, r], i) => {
      const ult = i === destinos.length - 1 && destinos.length > 2, cy = ult ? y + h - 24 - ih - 4 - tipos[papel]['line-height'] : y + 24 + i * 72, cx = x + w / 2, at = i === ativo;
      if (at) s += rect(cx - iw / 2, cy, iw, ih, canto('navigation-rail-indicator-shape', ih), cor(e, 'navigation-rail-indicator-color'));
      s += icone(ic, cx - 12, cy + 4, 24, at ? e['on-secondary-container'] : e['on-surface-variant']);
      if (i === 1 && n) s += selo(e, cx + 12, cy + 6, n);
      s += txt(cx, cy + ih + 4 + tipos[papel].size, r, papel, at ? e['on-surface'] : e['on-surface-variant'], 'middle');
    });
    return s;
  }
  function nav3(e, x, y, w, destinos, n, ativo = 0) {
    const h = num('navigation-bar-height'), iw = num('navigation-bar-indicator-width'), ih = num('navigation-bar-indicator-height'), papel = tipoDe('navigation-bar-label'), passo = w / destinos.length;
    return rect(x, y, w, h, 0, cor(e, 'navigation-bar-container-color')) + destinos.map(([ic, r], i) => {
      const cx = x + passo * i + passo / 2, at = i === ativo;
      return (at ? rect(cx - iw / 2, y + 12, iw, ih, canto('navigation-bar-indicator-shape', ih), cor(e, 'navigation-bar-indicator-color')) : '')
        + icone(ic, cx - 12, y + 12 + (ih - 24) / 2, 24, at ? cor(e, 'navigation-bar-active-icon-color') : cor(e, 'navigation-bar-icon-color'))
        + (i === 1 && n ? selo(e, cx + 12, y + 16, n) : '')
        + txt(cx, y + 12 + ih + 4 + tipos[papel].size, r, papel, at ? cor(e, 'navigation-bar-active-label-color') : cor(e, 'navigation-bar-label-color'), 'middle');
    }).join('');
  }
  // card de módulo; com metrica, é o painel do módulo para quem cuida dele (instituição, educador): o número principal em
  // primary com a tendência (sucesso quando melhora, erro quando piora), uma série num gráfico ou uma barra de progresso e até
  // três números de apoio com a cor da paleta que diz o que significam (secondary, tertiary, sucesso, erro). Sem mascotes:
  // tom profissional. Sem métrica ainda, pede para configurar
  const COR_MET = (e, c) => ({ secondary: e.secondary, tertiary: e.tertiary, sucesso: e.sucesso, error: e.error, primary: e.primary }[c] || e['on-surface-variant']);
  function cardModulo(e, x, y, w, h, ic, titulo, { apoio, estado, n, ordenando, metrica } = {}) {
    const p = num('card-padding');
    if (metrica !== undefined) {
      let s = rect(x, y, w, h, canto('card-shape', h), cor(e, 'card-filled-container-color')) + caixaIcone(e, x + p, y + p, ic);
      const corta = (t, papel, m) => { while (largura(t, papel) > m && t.length > 4) t = t.slice(0, -2).trimEnd() + '…'; return t; };
      s += txt(x + p + 52, y + p + 26, corta(titulo, 'title-medium', w - 2 * p - 80), 'title-medium', e['on-surface']);
      if (n) s += selo(e, x + w - 20, y + 20, n);
      if (!metrica) {
        quebrar(apoio || 'Configure o módulo para ver os números da sua gestão', w - 2 * p, 'body-medium').slice(0, 2).forEach((l, k) => { s += txt(x + p, y + 84 + k * 20, l, 'body-medium', e['on-surface-variant']); });
        if (estado) { const ew = largura(estado, 'label-medium') + 16; s += rect(x + p, y + h - p - 24, ew, 24, cantos.small, e['tertiary-container']) + txt(x + p + 8, meio(y + h - p - 24, 24, 'label-medium'), estado, 'label-medium', e['on-tertiary-container']); }
        return s;
      }
      const { valor, rotulo, tendencia, melhora, serie, progresso, extras = [] } = metrica, y0 = y + 72, gw = serie ? Math.min(132, w * 0.36) : 0;
      s += txt(x + p, y0 + 28, valor, 'headline-medium', e.primary);
      if (tendencia) {
        // a seta diz para onde o número foi; a cor diz se isso é bom (melhora: padrão subir é bom; false quando cair é bom)
        const sobe = !tendencia.startsWith('-'), bom = melhora ?? sobe, tw = largura(tendencia, 'label-medium') + 30, tx = x + p + largura(valor, 'headline-medium') * 1.08 + 12, bg = bom ? e['sucesso-container'] : e['error-container'], fg = bom ? e['on-sucesso-container'] : e['on-error-container'];
        s += rect(tx, y0 + 8, tw, 24, 12, bg) + icone(sobe ? 'cima' : 'baixo', tx + 6, y0 + 11, 18, fg) + txt(tx + 24, meio(y0 + 8, 24, 'label-medium'), tendencia, 'label-medium', fg);
      }
      s += txt(x + p, y0 + 50, corta(rotulo, 'body-medium', w - 2 * p - gw - 12), 'body-medium', e['on-surface-variant']);
      if (serie) s += graficoBarras(e, x + w - p - gw, y0, gw, 54, serie, { destaque: serie.length - 1, barraMax: 16 });
      let ye = y0 + 66;
      if (progresso !== undefined) { s += rect(x + p, ye, w - 2 * p, 8, 4, e['surface-container-highest']) + rect(x + p, ye, Math.max(8, (w - 2 * p) * progresso), 8, 4, e.primary); ye += 18; }
      if (extras.length) {
        s += `<rect x="${x + p}" y="${ye}" width="${w - 2 * p}" height="1" fill="${e['outline-variant']}"/>`;
        const cw = (w - 2 * p) / extras.length;
        extras.forEach(([v, r, c], k) => {
          const cx = x + p + k * cw, cc = COR_MET(e, c);
          s += `<circle cx="${cx + 5}" cy="${ye + 23}" r="5" fill="${cc}"/>` + txt(cx + 16, ye + 28, v, 'title-medium', cc)
            + quebrar(r, cw - 12, 'body-small').slice(0, 2).map((l, kk) => txt(cx, ye + 46 + kk * 16, l, 'body-small', e['on-surface-variant'])).join('');
        });
      }
      return s;
    }
    let s = rect(x, y, w, h, canto('card-shape', h), cor(e, 'card-filled-container-color'), ordenando ? `stroke="${e.outline}" stroke-dasharray="4 3"` : '');
    s += ordenando ? icone('arrastar', x + p, y + p, 24, e['on-surface-variant']) : caixaIcone(e, x + p, y + p, ic);
    s += txt(x + p, y + p + 64, titulo, 'title-medium', e['on-surface']);
    if (apoio) s += txt(x + p, y + p + 84, apoio, 'body-small', e['on-surface-variant']);
    if (estado) { const ew = largura(estado, 'label-medium') + 16; s += rect(x + p, y + h - p - 24, ew, 24, cantos.small, e['tertiary-container']) + txt(x + p + 8, meio(y + h - p - 24, 24, 'label-medium'), estado, 'label-medium', e['on-tertiary-container']); }
    if (ordenando) s += icone('cima', x + p, y + h - p - 24, 24, e['on-surface-variant']) + icone('baixo', x + p + 40, y + h - p - 24, 24, e['on-surface-variant']);
    if (n) s += selo(e, x + w - 20, y + 20, n);
    return s;
  }
  // card do catálogo: fundo cheio em secondary-container e o mascote padrinho do módulo à direita,
  // com metade dentro do card e metade cortada pela borda
  let ncard = 0;
  function cardCatalogo(e, x, y, w, ic, titulo, frase, dado, acao = 'Saiba mais', padrinho = ['feliz', 'circulo']) {
    const p = num('card-padding'), h = 176, r = canto('carousel-item-shape', h), t = 128, id = `${prefixo}cc${++ncard}`, fg = e['on-secondary-container'];
    const maxTxt = w - p - t / 2 - 12, frs = quebrar(frase, maxTxt, 'body-medium').slice(0, 2);
    // fundo Ondas do vão: só as linhas, na forma do padrinho e saindo dele
    // (desenhado na metade do tamanho e ampliado: linhas mais espaçadas no card pequeno)
    const ondas = desenharFundo ? desenharFundo('ondas', { largura: w / 2, altura: h / 2, pecas: 'nenhuma', origem: { x: 1, y: 0.5 }, mascote: { forma: padrinho[1] },
      cores: { ...e, surface: e['secondary-container'], 'outline-variant': e['on-secondary-container'] } })
      .replace(/ width="[^"]+" height="[^"]+"/, ` x="${x}" y="${y}" width="${w}" height="${h}" opacity="0.14"`) : '';
    return `<clipPath id="${id}">${rect(x, y, w, h, r, '#000000')}</clipPath>` + rect(x, y, w, h, r, e['secondary-container'])
      + `<g clip-path="url(#${id})">${ondas}${masc(padrinho[0], { forma: padrinho[1] }, x + w - t / 2, y + (h - t) / 2, t, t)}</g>`
      + caixaIcone(e, x + p, y + p, ic) + txt(x + p + 52, y + p + 26, titulo, 'title-medium', fg)
      + frs.map((l, i) => txt(x + p, y + p + 64 + i * 18, l, 'body-medium', fg)).join('') + txt(x + p, y + p + 104, dado, 'label-large', fg)
      + botao(e, x + p - 12, y + h - p - 36, acao, 'text').svg;
  }
  // interruptor (switch)
  function interruptor(e, x, y, ligado) {
    const w = num('switch-track-width'), h = num('switch-track-height'), r = h / 2;
    if (ligado) { const d = num('switch-selected-handle-size'); return rect(x, y, w, h, r, cor(e, 'switch-selected-track-color')) + `<circle cx="${x + w - 4 - d / 2}" cy="${y + h / 2}" r="${d / 2}" fill="${cor(e, 'switch-selected-handle-color')}"/>`; }
    const d = num('switch-handle-size'), b = num('switch-track-outline-width');
    return rect(x + b / 2, y + b / 2, w - b, h - b, r, cor(e, 'switch-track-color'), `stroke="${cor(e, 'switch-outline-color')}" stroke-width="${b}"`) + `<circle cx="${x + 8 + d / 2}" cy="${y + h / 2}" r="${d / 2}" fill="${cor(e, 'switch-handle-color')}"/>`;
  }
  function cardAcesso(e, x, y, w, letra, titulo, apoio) {
    const h = num('list-two-line-height');
    return rect(x, y, w, h, canto('card-shape', h), cor(e, 'card-filled-container-color')) + avatar(e, x + 36, y + h / 2, 20, letra)
      + txt(x + 72, y + h / 2 - 2, titulo, 'title-medium', e['on-surface']) + txt(x + 72, y + h / 2 + 18, apoio, 'body-medium', e['on-surface-variant'])
      + iconButton(e, x + w - 48, y + (h - 40) / 2, 'more', 'padrao').svg;
  }
  // aviso: com compacto, a ação vira um botão de ícone (tonal) e sobra espaço para o texto, que quebra em até duas linhas
  // card de tarefa (quest): módulo de origem, título, hora em destaque e prazo em texto. Cor do estado na borda e na hora:
  // primary dentro do prazo, tertiary vencida, neutra sem prazo. Ponto no canto: sucesso = pode fazer, erro = bloqueada.
  // agora: acontecendo neste momento, com o fundo Símbolo recortado e um mascote animado (parado com movimento reduzido)
  // envios: quantas vezes a tarefa pode ser enviada e quantas já foram (Envio único · 0 de 1, Até 3 envios · 1 de 3, Envios livres · 4 feitos)
  function cardTarefa(e, x, y, w, { modulo, titulo, hora, prazo, envios, estado = 'prazo', liberada = true, agora = null }) {
    const h = (agora ? 120 : 96) + (envios ? 22 : 0), cs = estado === 'vencida' ? e.tertiary : estado === 'livre' ? e['on-surface-variant'] : e.primary;
    const borda = estado === 'livre' ? cor(e, 'card-outlined-outline-color') : cs, id = `${prefixo}ct${++ncard}`, r = canto('card-shape', h);
    let s = rect(x, y, w, h, r, cor(e, 'card-outlined-container-color'));
    if (agora && desenharFundo) s += `<clipPath id="${id}">${rect(x, y, w, h, r, '#000000')}</clipPath><g clip-path="url(#${id})">`
      + desenharFundo('recorte', { largura: w, altura: h, pecas: 'ambos', cores: { ...e, mascote: '' }, mascote: { expressao: agora[0], forma: agora[1], movimento: agora[2] || 'pular' }, movimentoReduzido: false })
        .replace(/id="([^"]+)"/g, `id="${id}$1"`).replace(/url\(#([^)]+)\)/g, `url(#${id}$1)`).replace(/href="#([^"]+)"/g, `href="#${id}$1"`)
        .replace('<svg ', `<svg x="${x}" y="${y}" `) + '</g>';
    s += rect(x + (agora ? 1 : 0.5), y + (agora ? 1 : 0.5), w - (agora ? 2 : 1), h - (agora ? 2 : 1), r, null, `stroke="${borda}" stroke-width="${agora ? 2 : 1}"`);
    const tw = w - 32 - (agora ? 72 : 12), corta = (t, p, m = tw) => { while (largura(t, p) > m && t.length > 4) t = t.slice(0, -2).trimEnd() + '…'; return t; };
    let yy = y + 26;
    if (agora) { const aw = largura('Agora', 'label-small') + 16; s += rect(x + 16, y + 12, aw, 20, 10, e.primary) + txt(x + 16 + aw / 2, y + 26, 'Agora', 'label-small', e['on-primary'], 'middle'); yy = y + 50; }
    s += txt(x + 16, yy, corta(modulo, 'label-medium'), 'label-medium', e['on-surface-variant']) + txt(x + 16, yy + 24, corta(titulo, 'title-medium'), 'title-medium', e['on-surface']);
    const hw = largura(hora, 'label-large');
    s += icone('relogio', x + 16, yy + 36, 18, cs) + txt(x + 40, yy + 50, hora, 'label-large', cs) + (prazo && !agora && tw - 24 - hw - 6 > 40 ? txt(x + 40 + hw + 6, yy + 50, corta('· ' + prazo, 'body-small', tw - 24 - hw - 6), 'body-small', e['on-surface-variant']) : '');
    if (envios) s += icone('repetir', x + 16, yy + 58, 18, e['on-surface-variant']) + txt(x + 40, yy + 72, corta(envios, 'body-medium', w - 56 - (agora ? 72 : 0)), 'body-medium', e['on-surface-variant']);
    s += `<circle cx="${x + w - 14}" cy="${y + 14}" r="6" fill="${liberada ? e.sucesso : e.error}" stroke="${cor(e, 'card-outlined-container-color')}" stroke-width="2"/>`;
    return s;
  }
  // vitrine de telas: as telas do módulo num computador (hub) e num celular (quest), com o carrossel das telas;
  // enquanto o módulo não tem as imagens, cada tela é um espaço com o nome dela. Devolve a altura em h
  function vitrineTelas(e, x, y, w, { telas = [['computador', 'Tela do hub'], ['celular', 'Tela do quest']], total = 4, atual = 0 } = {}) {
    const cw = w * 0.8, ch = cw * 0.62, ph = ch * 0.95, pw = ph / 1.9, oy = ch * 0.25;
    let s = '';
    // tela estreita (celular): o nome em duas linhas (frente · tela), sem o apoio
    const espacoTela = (tx, ty, tw, th, rot) => rect(tx, ty, tw, th, cantos.small, e['surface-container-low'], `stroke="${e.outline}" stroke-dasharray="5 4"`)
      + (tw < 140 ? rot.split(' · ').map((l, k, a) => txt(tx + tw / 2, ty + th / 2 + (k - (a.length - 1) / 2) * 16 + 4, l, 'label-small', e['on-surface-variant'], 'middle')).join('')
        : txt(tx + tw / 2, ty + th / 2 - 4, rot, 'label-large', e['on-surface-variant'], 'middle') + txt(tx + tw / 2, ty + th / 2 + 14, tw < 300 ? 'imagem do módulo' : 'imagem quando o módulo estiver pronto', 'label-small', e['on-surface-variant'], 'middle'));
    // computador: janela com a barra de três pontos
    s += rect(x, y, cw, ch, cantos.medium, e['surface-container-highest'], sombra('level1'));
    [0, 1, 2].forEach((i) => { s += `<circle cx="${x + 14 + i * 12}" cy="${y + 12}" r="3.5" fill="${e['outline-variant']}"/>`; });
    s += espacoTela(x + 8, y + 24, cw - 16, ch - 32, telas[0][1]);
    // celular: na frente, à direita e mais abaixo
    const px = x + w - pw, py = y + oy;
    s += rect(px, py, pw, ph, cantos.large, e['inverse-surface'], sombra('level2')) + espacoTela(px + 6, py + 14, pw - 12, ph - 28, telas[1][1]);
    s += rect(px + pw / 2 - 14, py + 5, 28, 4, 2, e['inverse-on-surface']);
    const fim = Math.max(y + ch, py + ph) + 16;
    s += controlesCarrossel(e, x + (w - Math.min(w, 320)) / 2, fim, Math.min(w, 320), total, atual);
    return { svg: s, h: fim + 40 - y };
  }
  // gráfico de barras (base): valores em barras verticais com o topo arredondado; a barra em destaque em primary,
  // as outras em secondary-container, linha de base em outline-variant; rótulos (opcional) embaixo, valores (opcional) em cima
  function graficoBarras(e, x, y, w, h, valores, { destaque = -1, rotulos = null, mostrarValores = false, gap = 4, barraMax = 40 } = {}) {
    const n = valores.length, max = Math.max(...valores, 1), bw = Math.min(barraMax, (w - gap * (n - 1)) / n), base = y + h - (rotulos ? 18 : 0), alto = base - y - (mostrarValores ? 18 : 0);
    let s = `<rect x="${x}" y="${base}" width="${w}" height="1" fill="${e['outline-variant']}"/>`;
    valores.forEach((v, i) => {
      const bh = Math.max(2, alto * v / max), bx = x + i * (bw + gap), r = Math.min(bw / 2, 4), c = i === destaque ? e.primary : e['secondary-container'];
      s += `<path d="M${bx} ${base}V${base - bh + r}a${r} ${r} 0 0 1 ${r} -${r}H${bx + bw - r}a${r} ${r} 0 0 1 ${r} ${r}V${base}Z" fill="${c}"/>`;
      if (mostrarValores) s += txt(bx + bw / 2, base - bh - 6, String(v), 'label-small', i === destaque ? e.primary : e['on-surface-variant'], 'middle');
      if (rotulos) s += txt(bx + bw / 2, base + 14, rotulos[i], 'label-small', e['on-surface-variant'], 'middle');
    });
    return s;
  }
  // card de desafio (quest, tarefa sem prazo): cabeçalho do card de tarefa; pódio com os 3 primeiros como mascotes em forma de
  // número (2º, 1º, 3º) sobre blocos de alturas diferentes; 4º e 5º em linhas com barra de pontos; a posição de quem está vendo em
  // destaque (no pódio, numa linha ou depois de ···); as tentativas num gráfico de barras e Jogar de novo (Envios esgotados quando acabam).
  // ranking: [[nome, pontos]] (5), voce: { pos, pontos }, tentativas: [pontos por envio]
  // pódio em festa: cada colocado com uma expressão feliz e um movimento composto (parados com movimento reduzido)
  const PODIO = [['estrela', ['comemorar', 'pular', 'dancar']], ['rindo', ['dancar', 'balancar']], ['feliz', ['pular', 'balancar']]];
  const fmtPts = (v) => String(v).replace(/\B(?=(\d{3})+(?!\d))/g, '.');
  function cardDesafio(e, x, y, w, { modulo, titulo, envios, liberada = true, ranking, voce, tentativas = [] }) {
    const p = 16, lh = 34, resto = ranking.slice(3), fora = voce.pos > ranking.length, hp = 132;
    const h = 96 + 28 + hp + 8 + resto.length * lh + (fora ? 48 : 0) + 88, r = canto('card-shape', h);
    let s = rect(x + 0.5, y + 0.5, w - 1, h - 1, r, cor(e, 'card-outlined-container-color'), `stroke="${cor(e, 'card-outlined-outline-color')}"`);
    const corta = (t, papel, m) => { while (largura(t, papel) > m && t.length > 4) t = t.slice(0, -2).trimEnd() + '…'; return t; };
    s += txt(x + p, y + 26, corta(modulo, 'label-medium', w - 2 * p - 16), 'label-medium', e['on-surface-variant']) + txt(x + p, y + 50, corta(titulo, 'title-medium', w - 2 * p - 16), 'title-medium', e['on-surface']);
    s += icone('repetir', x + p, y + 62, 18, e['on-surface-variant']) + txt(x + p + 24, y + 76, corta(envios, 'body-medium', w - 2 * p - 24), 'body-medium', e['on-surface-variant']);
    s += `<circle cx="${x + w - 14}" cy="${y + 14}" r="6" fill="${liberada ? e.sucesso : e.error}" stroke="${cor(e, 'card-outlined-container-color')}" stroke-width="2"/>`;
    let yy = y + 96;
    s += txt(x + p, yy + 16, 'Ranking da tarefa', 'label-large', e['on-surface']);
    yy += 28;
    // pódio: 2º à esquerda, 1º no meio (mais alto), 3º à direita
    const cw = (w - 2 * p - 16) / 3, base = yy + hp, alt = [58, 48, 42], mt = [70, 60, 60];
    [1, 0, 2].forEach((i, k) => {
      const cx = x + p + k * (cw + 8), [nome, pts] = ranking[i], eu = voce.pos === i + 1, by = base - alt[i];
      const bg = eu ? e.primary : i === 0 ? e['primary-container'] : e['secondary-container'], fg = eu ? e['on-primary'] : i === 0 ? e['on-primary-container'] : e['on-secondary-container'];
      s += masc(PODIO[i][0], { forma: `numero-${i + 1}`, movimento: PODIO[i][1] }, cx + cw / 2 - mt[i] * 0.5, by - mt[i] + 4, mt[i], mt[i]);
      s += rect(cx, by, cw, alt[i], cantos.small, bg) + txt(cx + cw / 2, by + 18, corta(eu ? 'Você' : nome, 'label-medium', cw - 8), 'label-medium', fg, 'middle')
        + txt(cx + cw / 2, by + 34, fmtPts(pts), 'label-small', fg, 'middle');
    });
    yy = base + 8;
    const max = Math.max(...ranking.map((l) => l[1]), voce.pontos), nx = x + p + 40, bx = x + p + 40 + Math.min(104, (w - 2 * p) * 0.34), ex = x + w - p - 52;
    const linha = (pos, nome, pts, eu) => {
      let t = eu ? rect(x + 8, yy, w - 16, lh - 2, cantos.small, e['secondary-container']) : '';
      const fg = eu ? e['on-secondary-container'] : e['on-surface'];
      t += txt(x + p + 14, yy + 21, `${pos}º`, 'label-large', fg, 'middle') + txt(nx, yy + 21, corta(nome, eu ? 'title-small' : 'body-medium', bx - nx - 8), eu ? 'title-small' : 'body-medium', fg);
      t += rect(bx, yy + 13, ex - bx - 8, 6, 3, e['surface-container-highest']) + rect(bx, yy + 13, Math.max(6, (ex - bx - 8) * pts / max), 6, 3, eu ? e.primary : e.secondary);
      return t + txt(x + w - p, yy + 21, fmtPts(pts), 'label-large', fg, 'end');
    };
    resto.forEach(([nome, pts], i) => { s += linha(i + 4, voce.pos === i + 4 ? 'Você' : nome, pts, voce.pos === i + 4); yy += lh; });
    if (fora) { s += txt(x + w / 2, yy + 8, '···', 'label-large', e['on-surface-variant'], 'middle'); yy += 14; s += linha(voce.pos, 'Você', voce.pontos, true); yy += lh; }
    yy += 10;
    s += `<rect x="${x + p}" y="${yy}" width="${w - 2 * p}" height="1" fill="${e['outline-variant']}"/>`;
    yy += 12;
    const gw = Math.min(132, w - 2 * p - 150), melhor = tentativas.length ? tentativas.indexOf(Math.max(...tentativas)) : -1;
    s += txt(x + p, yy + 14, 'Suas tentativas', 'label-medium', e['on-surface-variant']);
    if (tentativas.length) s += graficoBarras(e, x + p, yy + 22, gw, 34, tentativas, { destaque: melhor, barraMax: 16 });
    const acao = !liberada ? null : tentativas.length ? 'Jogar de novo' : 'Jogar';
    if (acao) { const bb = botao(e, 0, 0, acao, 'tonal'); s += botao(e, x + w - p - bb.w, yy + 16, acao, 'tonal').svg; }
    else s += txt(x + w - p, yy + 42, 'Envios esgotados', 'label-large', e.error, 'end');
    return s;
  }
  // dia da agenda (quest): divisória que separa um dia do outro e, à esquerda, o dia da semana e o número em secondary
  // (hoje com o número num círculo cheio); sem dia, o rótulo da seção (Sem prazo)
  function diaAgenda(e, x, y, w, semana, dia, hoje = false) {
    let s = `<rect x="${x}" y="${y}" width="${w}" height="1" fill="${e['outline-variant']}"/>`;
    if (!dia) return s + txt(x, y + 28, semana, 'label-large', e.secondary);
    s += txt(x + 20, y + 30, semana, 'label-medium', e.secondary, 'middle');
    return s + (hoje ? `<circle cx="${x + 20}" cy="${y + 50}" r="18" fill="${e.secondary}"/>` + txt(x + 20, y + 57, dia, 'title-medium', e['on-secondary'], 'middle') : txt(x + 20, y + 58, dia, 'title-large', e.secondary, 'middle'));
  }
  // quem está acessando (quest): o estudante ou um responsável (marcado), o parentesco num botão segmentado cheio e Entrar; 232 de altura
  function quemAcessa(e, x, y, w, parentes = ['Mãe', 'Pai', 'Outro']) {
    let s = rect(x + 0.5, y + 0.5, w - 1, 55, canto('card-shape', 56), null, `stroke="${e['outline-variant']}"`) + icone('perfil', x + 16, y + 16, 24, e['on-surface']) + txt(x + 52, meio(y, 56, 'body-large'), 'Sou o estudante', 'body-large', e['on-surface']);
    s += rect(x, y + 64, w, 56, canto('card-shape', 56), e['secondary-container']) + icone('perfil', x + 16, y + 80, 24, e['on-secondary-container']) + txt(x + 52, meio(y + 64, 56, 'body-large'), 'Sou responsável', 'body-large', e['on-secondary-container']);
    s += segmentado(e, x, y + 136, parentes, 0, w).svg;
    return s + rect(x, y + 188, w, 40, 20, cor(e, 'button-filled-container-color')) + txt(x + w / 2, meio(y + 188, 40, 'label-large'), 'Entrar', 'label-large', cor(e, 'button-filled-label-color'), 'middle');
  }
  function aviso(e, x, y, w, ic, texto, acao, { compacto = false, iconeAcao = 'proximo' } = {}) {
    const h = 72, ib = num('icon-button-size'), b = acao ? (compacto ? ib : botao(e, 0, 0, acao, 'filled').w) : 0, fg = e['on-secondary-container'];
    const ls = quebrar(texto, w - 56 - (acao ? b + 32 : 16), 'body-medium').slice(0, 2), lh = tipos['body-medium']['line-height'];
    const ty = meio(y, h, 'body-medium') - (ls.length - 1) * lh / 2;
    return rect(x, y, w, h, canto('card-shape', h), e['secondary-container']) + icone(ic, x + 16, y + 24, 24, fg)
      + ls.map((l, i) => txt(x + 56, ty + i * lh, l, 'body-medium', fg)).join('')
      + (acao ? (compacto ? iconButton(e, x + w - 16 - ib, y + (h - ib) / 2, iconeAcao, 'filled').svg : botao(e, x + w - 16 - b, y + 16, acao, 'filled').svg) : '');
  }
  function indicador(e, x, y, w, valor, rotulo) {
    return rect(x, y, w, 88, canto('card-shape', 88), cor(e, 'card-filled-container-color')) + txt(x + 16, y + 44, valor, 'headline-medium', e['on-surface']) + txt(x + 16, y + 68, rotulo, 'label-medium', e['on-surface-variant']);
  }
  const modoDe = (e) => (e === esquemas.dark ? 'dark' : 'light');
  // turma de mascotes: todas as expressões, pequenas e lado a lado, em formas variadas e proporção normal
  const FORMAS_TURMA = ['circulo', 'bolha', 'quadrado', 'hexagono', 'estrela', 'nuvem', 'coracao', 'gota', 'octogono', 'losango', 'pentagono', 'oval', 'esfera', 'trevo'];
  function turma(x, y, w, h, cols) {
    const ex = (mascotes.expressoes || []).map(([id]) => id), n = ex.length;
    // colunas que fecham a grade (divisor de n) mais perto da proporção do espaço
    const ideal = Math.sqrt(n * w / h), divs = [...Array(n).keys()].map((k) => k + 1).filter((k) => n % k === 0 && k >= 3);
    const c = cols || divs.reduce((a, b) => (Math.abs(b - ideal) < Math.abs(a - ideal) ? b : a), divs[0]);
    const linhas = Math.ceil(n / c), cw = w / c, ch = h / linhas, t = Math.min(cw, ch) * 0.9;
    return ex.map((id, i) => {
      const cx = x + (i % c + 0.5) * cw, cy = y + (Math.floor(i / c) + 0.5) * ch;
      return masc(id, { forma: FORMAS_TURMA[(i * 5) % FORMAS_TURMA.length] }, cx - t / 2, cy - t / 2, t, t);
    }).join('');
  }
  // menu da landing page (compact e medium): seções, tema e as três ações
  function menuLanding(e, x, y, w, nomeTema = 'Branco e Preto') {
    const h0 = num('top-app-bar-height'), ih = num('list-one-line-height');
    let s = rect(x, y, w, 584, 0, e.surface) + logoNaBarra(e, x + 16, y, h0, 'longo') + iconButton(e, x + w - 52, y + 12, 'close', 'padrao').svg;
    let yy = y + h0 + 8;
    for (const l of ['Módulos', 'Para quem', 'Como funciona', 'Depoimentos']) { s += txt(x + 16, meio(yy, ih, 'body-large'), l, 'body-large', e['on-surface']); yy += ih; }
    s += `<rect x="${x + 16}" y="${yy + 4}" width="${w - 32}" height="1" fill="${e['outline-variant']}"/>`; yy += 12;
    s += icone('paleta', x + 16, yy + (ih - 24) / 2, 24, e['on-surface-variant']) + txt(x + 56, meio(yy, ih, 'body-large'), 'Tema', 'body-large', e['on-surface'])
      + txt(x + w - 16, meio(yy, ih, 'body-medium'), nomeTema, 'body-medium', e['on-surface-variant'], 'end');
    yy += ih + 16;
    const bw = w - 32;
    s += rect(x + 16, yy, bw, 40, 20, cor(e, 'button-filled-container-color')) + txt(x + w / 2, meio(yy, 40, 'label-large'), 'Começar grátis', 'label-large', cor(e, 'button-filled-label-color'), 'middle');
    yy += 52;
    s += rect(x + 16.5, yy + 0.5, bw - 1, 39, 20, null, `stroke="${cor(e, 'button-outlined-outline-color')}"`) + txt(x + w / 2, meio(yy, 40, 'label-large'), 'Entrar numa sala', 'label-large', cor(e, 'button-outlined-label-color'), 'middle');
    yy += 52;
    s += txt(x + w / 2, meio(yy, 40, 'label-large'), 'Entrar', 'label-large', cor(e, 'button-text-label-color'), 'middle');
    return s;
  }

  return { nm, masc, L, M, GAP, PW, PAD, fundo, comp, num, cor, canto, tipoDe, nivel, quebrar, largura, rect, sombra, txt, mono, icone, meio, botao, fila, iconButton, fab, chip, card, campo, topAppBar, DESTINOS, navBar, navRail, drawer, lista, dialogo, snackbar, selo, abas, segmentado, busca, menu, controlesCarrossel, logoW, logoAlt, ESCALA_TOPO, logoNaBarra, logo, caixaIcone, avatar, topoCasca, nav3, cardModulo, cardCatalogo, cardAcesso, aviso, indicador, modoDe, FORMAS_TURMA, turma, menuLanding, interruptor, trilho3, cardTarefa, quemAcessa, diaAgenda, graficoBarras, cardDesafio, vitrineTelas };
}

export function temaSvg({ esquemas, tipos, cantos, janelas, tokensComp, fonte, versao, data, status, semente, variante, extras, temas, mascote, mascotes, logos, desenharFundo }) {
  const { nm, masc, L, M, GAP, PW, PAD, fundo, comp, num, cor, canto, tipoDe, nivel, quebrar, largura, rect, sombra, txt, mono, icone, meio, botao, fila, iconButton, fab, chip, card, campo, topAppBar, DESTINOS, navBar, navRail, drawer, lista, dialogo, snackbar, selo, abas, segmentado, busca, menu, controlesCarrossel, logoW, logoAlt, ESCALA_TOPO, logoNaBarra, logo, caixaIcone, avatar, topoCasca, nav3, cardModulo, cardCatalogo, cardAcesso, aviso, indicador, modoDe, FORMAS_TURMA, turma, menuLanding, interruptor, trilho3, cardTarefa, quemAcessa, diaAgenda, graficoBarras, cardDesafio, vitrineTelas } = desenhista({ esquemas, tipos, cantos, janelas, tokensComp, fonte, versao, data, status, semente, variante, extras, temas, mascote, mascotes, logos, desenharFundo });

  // ── componentes no quadro: título, descrição, altura do conteúdo, desenho ──
  const COMPONENTES = [
    ['Botões', 'Ênfase decrescente: filled → tonal → elevated → outlined → text. Uma ação filled por tela.', 104, (e, x, y) =>
      fila([(cx, cy) => botao(e, cx, cy, 'Filled', 'filled'), (cx, cy) => botao(e, cx, cy, 'Tonal', 'tonal'), (cx, cy) => botao(e, cx, cy, 'Elevated', 'elevated'),
        (cx, cy) => botao(e, cx, cy, 'Outlined', 'outlined'), (cx, cy) => botao(e, cx, cy, 'Text', 'text')], x, y)
      + fila([(cx, cy) => botao(e, cx, cy, 'Criar', 'filled', { icon: 'add' }), (cx, cy) => botao(e, cx, cy, 'Editar', 'tonal', { icon: 'editar' }),
        (cx, cy) => botao(e, cx, cy, 'Filled', 'filled', { desabilitado: true }), (cx, cy) => botao(e, cx, cy, 'Outlined', 'outlined', { desabilitado: true })], x, y + 64)],
    ['Botões de ícone e FAB', 'Botão de ícone 40dp com alvo de 48dp; FAB pequeno, normal, estendido e grande em primary-container.', 96, (e, x, y) =>
      fila([(cx, cy) => iconButton(e, cx, cy, 'favorito', 'padrao'), (cx, cy) => iconButton(e, cx, cy, 'salvos', 'selecionado'), (cx, cy) => iconButton(e, cx, cy, 'add', 'filled'),
        (cx, cy) => iconButton(e, cx, cy, 'editar', 'tonal'), (cx, cy) => iconButton(e, cx, cy, 'apagar', 'outlined')], x, y + 28, 8)
      + fila([(cx, cy) => fab(e, cx, cy + 28, 'small'), (cx, cy) => fab(e, cx, cy + 20, 'normal'), (cx, cy) => fab(e, cx, cy + 20, 'normal', 'Nova tarefa'), (cx, cy) => fab(e, cx, cy, 'large')], x + 272, y, 16)],
    ['Chips', 'Altura 32dp, canto small. Selecionado ganha secondary-container e marcação.', 32, (e, x, y) =>
      fila([(cx, cy) => chip(e, cx, cy, 'Agendar', { icon: 'evento' }), (cx, cy) => chip(e, cx, cy, 'Filtro ativo', { selecionado: true }),
        (cx, cy) => chip(e, cx, cy, 'Filtro'), (cx, cy) => chip(e, cx, cy, 'Outro filtro')], x, y, 8)],
    ['Cards', 'Elevated destaca do fundo; filled agrupa sem chamar atenção; outlined para listas de itens iguais.', 104, (e, x, y) => {
      const w = (PW - 2 * PAD - 32) / 3;
      return card(e, x, y, w, 104, 'elevated', 'Elevated', ['surface-container-low', 'com sombra de nível 1'])
        + card(e, x + w + 16, y, w, 104, 'filled', 'Filled', ['surface-container-highest', 'sem sombra'])
        + card(e, x + 2 * (w + 16), y, w, 104, 'outlined', 'Outlined', ['surface com contorno', 'outline-variant']);
    }],
    ['Campos de texto', 'Filled e outlined, altura 56dp; rótulo flutua ao preencher; erro com cor, linha e texto de apoio.', 88, (e, x, y) => {
      const w = (PW - 2 * PAD - 32) / 3;
      return campo(e, x, y + 8, w, 'filled', 'Nome', '', 'Texto de apoio') + campo(e, x + w + 16, y + 8, w, 'outlined', 'E-mail', 'ana@exemplo.com', 'Em foco')
        + campo(e, x + 2 * (w + 16), y + 8, w, 'erro', 'Idade', '12', 'Informe de 18 a 120');
    }],
    ['Lista', 'Uma, duas ou três linhas (56, 72 e 88dp), com divisória outline-variant.', 216, (e, x, y) => {
      const w = PW - 2 * PAD, l = lista(e, x, y + 8, w);
      return rect(x, y, w, l.h + 16, cantos.medium, null, `stroke="${e['outline-variant']}"`) + l.svg;
    }],
    ['Barra do topo e barra de navegação', 'Janela compact: barra do topo de 64dp e barra de navegação de 80dp com indicador em secondary-container.', 176, (e, x, y) => {
      const w = PW - 2 * PAD;
      return `<g>${topAppBar(e, x, y, w, 'Início')}</g>` + navBar(e, x, y + 96, w);
    }],
    ['Trilho e gaveta de navegação', 'Trilho (80dp) nas janelas medium e expanded; gaveta (até 360dp) em large e extra-large.', 336, (e, x, y) =>
      navRail(e, x, y, 336) + drawer(e, x + 128, y, 300, 336)],
    ['Selo de notificação', 'Número em 16dp ou ponto de 6dp, em error, no canto de um ícone, destino de navegação ou card.', 48, (e, x, y) => {
      const ib = num('icon-button-size');
      let s = iconButton(e, x, y + 4, 'avisos', 'padrao').svg + selo(e, x + ib - 6, y + 10, '3');
      s += iconButton(e, x + 72, y + 4, 'chat', 'padrao').svg + selo(e, x + 72 + ib - 10, y + 12, '');
      const b = botao(e, x + 144, y + 4, 'Tarefas', 'tonal');
      return s + b.svg + selo(e, x + 144 + b.w - 4, y + 6, '12');
    }],
    ['Abas', 'Abas primárias para vistas do mesmo assunto; indicador de 3dp em primary sob a aba ativa.', 48, (e, x, y) => abas(e, x + (PW - 2 * PAD - 360) / 2, y, 360, ['Configuração', 'Utilização'])],
    ['Botão segmentado', 'De 2 a 5 opções curtas lado a lado; a selecionada ganha secondary-container e marcação. Cheio: ocupa o contêiner e divide por igual (formulários no compact).', 96, (e, x, y) =>
      fila([(cx, cy) => segmentado(e, cx, cy, ['Mensal', 'Anual'], 0), (cx, cy) => segmentado(e, cx, cy, ['Claro', 'Escuro', 'Sistema'], 2)], x, y, 24)
      + segmentado(e, x, y + 56, ['Escola', 'Empresa', 'Autônomo'], 0, 328).svg],
    ['Barra de busca', 'Pesquisa numa lista, em surface-container-high e canto full; o ícone de busca vem na frente e, com texto, o botão de limpar.', 56, (e, x, y) => {
      const w = (PW - 2 * PAD - 24) / 2;
      return busca(e, x, y, w, 'Buscar por nome ou sigla') + busca(e, x + w + 24, y, w, '', 'maria');
    }],
    ['Gráfico de barras', 'Valores lado a lado com o topo arredondado: destaque em primary, os outros em secondary-container; rótulos e valores opcionais. Sempre com o valor também em texto.', 120, (e, x, y) =>
      graficoBarras(e, x, y, 300, 120, [900, 1200, 1350, 1720, 1600, 1500], { destaque: 3, rotulos: ['1ª', '2ª', '3ª', '4ª', '5ª', '6ª'], mostrarValores: true, gap: 8 })
      + graficoBarras(e, x + 340, y + 40, 120, 40, [980, 1240], { destaque: 1 })],
    ['Interruptor', 'Liga ou desliga uma opção na hora; trilho de 52 × 32dp, ligado em primary.', 32, (e, x, y) => {
      const x1 = x + (PW - 2 * PAD - 300) / 2;
      return interruptor(e, x1, y, true) + interruptor(e, x1 + 76, y, false) + txt(x1 + 152, y + 21, 'Mascotes nas cores do tema', tipoDe('switch-label'), cor(e, 'switch-label-color'));
    }],
    ['Menu', 'Lista temporária de ações aberta por um botão (⋮); itens de 48dp em surface-container, sombra de nível 2.', 112, (e, x, y) =>
      ((x1) => iconButton(e, x1, y, 'more', 'padrao').svg + menu(e, x1 + 48, y, 240, [['editar', 'Renomear'], ['apagar', 'Remover deste aparelho']]).svg)(x + (PW - 2 * PAD - 288) / 2)],
    ['Carrossel', 'Um item por vez no compact (2 no medium, 3 no expanded); só avança por ação da pessoa e mostra a posição em texto.', 184, (e, x0, y) => {
      const w = 328, ih = 128, x = x0 + (PW - 2 * PAD - w - num('carousel-gap') - 120) / 2;
      let s = rect(x, y, w, ih, canto('carousel-item-shape', ih), cor(e, 'card-filled-container-color'))
        + txt(x + 24, y + 48, 'Item 1', tipoDe('card-title'), e['on-surface']) + txt(x + 24, y + 72, 'Desliza ou usa os botões', tipoDe('card-body'), e['on-surface-variant']);
      s += rect(x + w + num('carousel-gap'), y, 120, ih, canto('carousel-item-shape', ih), cor(e, 'card-filled-container-color'), 'opacity="0.5"');
      return s + controlesCarrossel(e, x, y + ih + 16, w, 4, 0);
    }],
    ['Diálogo e snackbar', 'Diálogo em surface-container-high, canto extra-large; snackbar em inverse-surface com ação inverse-primary.', 280, (e, x, y) => {
      const w = PW - 2 * PAD;
      let s = rect(x, y, w, 208, cantos.medium, e.scrim, 'fill-opacity="0.32"');
      const d = dialogo(e, x + (w - 360) / 2, y + (208 - 196) / 2, 360);
      s += d.svg;
      return s + snackbar(e, x, y + 232, Math.min(400, w));
    }],
  ];


  // ── componentes compostos no quadro, por frente ──
  const COMPOSTOS = [
    ['Comum · Topo da casca', 'Hub e quest: logo (hastes longas no hub, curtas no quest) e avisos; a partir do medium, também o nome (instituição ou pessoa no hub, usuário no quest) e a foto (sem foto, a inicial no hub e o mascote adotado no quest), que abre a conta ou Trocar perfil.', 288, (e, x, y) => {
      const x1 = x + (PW - 2 * PAD - 360) / 2;
      return topoCasca(e, x1, y, 360, { conta: false }) + topoCasca(e, x1, y + 80, 360) + topoCasca(e, x1, y + 160, 360, { quest: true, nome: 'Lucas Andrade', letra: ['feliz', 'estrela'] })
        + menu(e, x1 + 360 - 240, y + 224, 240, [['voltar', 'Trocar perfil']]).svg;
    }],
    ['Comum · Navegação da casca', 'Barra com três destinos no compact; a partir do medium, trilho (gaveta no large) abaixo do topo, sem Conta ou Perfil, que ficam no nome e na foto do topo.', 80, (e, x, y) =>
      nav3(e, x, y, 336, [['modulo', 'Meus módulos'], ['add', 'Catálogo'], ['perfil', 'Conta']]) + nav3(e, x + 356, y, 336, [['evento', 'Tarefas'], ['salvos', 'Fixos'], ['perfil', 'Perfil']], '5')],
    ['Comum · Card de módulo', 'Meus módulos (hub): painel do módulo com o número principal e a tendência, uma série ou o progresso e números de apoio nas cores da paleta, sem mascotes; Fixos (quest): nome, apoio, selo e estado. Até 2 por linha.', 420, (e, x, y) => {
      const w = (PW - 2 * PAD - 16) / 2;
      return cardModulo(e, x, y, w, 236, 'check', 'Presença', { n: '3', metrica: { valor: '94%', rotulo: 'presença na semana', tendencia: '+2 pts', serie: [88, 91, 90, 92, 94], extras: [['12', 'turmas', 'secondary'], ['3', 'sem chamada hoje', 'tertiary'], ['18', 'faltas seguidas', 'error']] } })
        + cardModulo(e, x + w + 16, y, w, 236, 'perfil', 'Carteirinha', { metrica: { valor: '312 de 340', rotulo: 'carteirinhas prontas', progresso: 0.92, extras: [['21', 'esperando foto', 'tertiary'], ['7', 'sem autorização', 'error'], ['4', 'impressas hoje', 'secondary']] } })
        + cardModulo(e, x, y + 252, w, 160, 'editar', 'Quiz', { n: '1', estado: 'Configurar', metrica: null })
        + cardModulo(e, x + w + 16, y + 252, (w - 12) / 2, 128, 'salvos', 'Flashcards', { apoio: 'Estudo · quest' })
        + cardModulo(e, x + w + 16 + (w - 12) / 2 + 12, y + 252, (w - 12) / 2, 128, 'chat', 'Chat da turma', { n: '5', apoio: '7º A · quest' });
    }],
    ['Comum · Vitrine de telas', 'Página do módulo (landing) e detalhe no catálogo (hub): as telas do módulo num computador e num celular, com o carrossel; até o módulo ficar pronto, cada tela é um espaço com o nome dela.', 340, (e, x, y) =>
      vitrineTelas(e, x + (PW - 2 * PAD - 460) / 2, y, 460, { telas: [['computador', 'Hub · chamada da turma'], ['celular', 'Quest · presença']] }).svg],
    ['Comum · Card do catálogo', 'Vitrine da landing page, catálogo do hub e outros módulos: fundo cheio com as Ondas do vão na forma do padrinho, nome, frase, um dado (quem usa ou preço inicial), ação e o mascote padrinho do módulo, metade à vista.', 176, (e, x, y) =>
      cardCatalogo(e, x, y, 338, 'check', 'Presença', 'Chamada em sala, vista pela família.', '120 escolas usando', 'Saiba mais', ['acertou', 'circulo']) + cardCatalogo(e, x + 354, y, 338, 'editar', 'Quiz', 'Salas ao vivo com ranking.', 'a partir de R$ 49,00/mês', 'Ver detalhes', ['estrela', 'estrela'])],
    ['Comum · Card de acesso', 'Contas acessadas (hub) e perfis acessados (quest): foto (sem foto, a inicial no hub e o mascote adotado no quest), nome, instituição e menu com Remover.', 152, (e, x, y) =>
      cardAcesso(e, x, y, 420, 'A', 'Ana Souza', 'Escola Horizonte · conta master') + cardAcesso(e, x, y + 80, 420, ['feliz', 'hexagono'], 'Lucas Andrade', 'Escola Horizonte · 7º A · 2026')
      + menu(e, x + 436, y + 72, 240, [['apagar', 'Remover deste aparelho']]).svg],
    ['Comum · Aviso para completar', 'Módulo aberto (hub e quest) e tela de acesso: ícone, texto e ação opcional, em secondary-container; no compact a ação é um botão de ícone, para o texto não ficar coberto.', 248, (e, x, y) =>
      aviso(e, x, y, PW - 2 * PAD, 'camera', 'Envie a foto 3x4 para a carteirinha ficar pronta.', 'Enviar foto')
      + aviso(e, x, y + 88, 360, 'avisos', 'Cadastre as turmas para começar a chamada.', 'Configurar', { compacto: true })
      + aviso(e, x, y + 176, PW - 2 * PAD, 'avisos', 'Os códigos de acesso são entregues pela sua instituição.')],
    ['Comum · Topo do módulo', 'Hub e quest: voltar, nome do módulo e avisos dele; no hub, abas Configuração e Utilização por permissão.', 112, (e, x, y) => {
      const w = PW - 2 * PAD, h = num('top-app-bar-height'), ib = num('icon-button-size');
      return rect(x, y, w, h, 0, cor(e, 'top-app-bar-container-color')) + iconButton(e, x + 4, y + (h - ib) / 2, 'voltar', 'padrao').svg
        + txt(x + 56, meio(y, h, tipoDe('top-app-bar-title')), 'Presença', tipoDe('top-app-bar-title'), cor(e, 'top-app-bar-title-color'))
        + iconButton(e, x + w - 52, y + (h - ib) / 2, 'avisos', 'padrao').svg + selo(e, x + w - 52 + ib - 8, y + (h - ib) / 2 + 8, '1')
        + abas(e, x, y + h, w, ['Configuração', 'Utilização']);
    }],
    ['Comum · Boas-vindas com o logo', 'Landing, entrada do hub e acesso do quest: Halo passando pelos temas, logo na abertura e texto num painel; hub e quest: faixa no compact, só o logo do medium em diante.', 504, (e, x, y) => {
      const w = PW - 2 * PAD, h = 264, id = `boas-vindas-${modoDe(e)}`;
      // tema do quadro é monocromático: logo em on-surface (num tema colorido, nas cores da marca);
      // o ciclo começa no primeiro tema colorido, que é o que o desenho parado mostra
      const primeiro = temas.find((t) => t.variante !== 'monochrome')?.esquemas[modoDe(e)];
      const halo = (ww, hh, op) => desenharFundo ? desenharFundo('halo', { largura: ww, altura: hh, pecas: 'logo', nome: true, cores: { ...e, mascote: '' }, ciclo: primeiro ? [primeiro] : undefined, ...op }) : '';
      const pw = 400, px = x + 24, py = y + 28, ph = h - 56, b1 = botao(e, 0, 0, 'Começar grátis', 'filled').w;
      let s = `<clipPath id="${id}">${rect(x, y, w, h, cantos['extra-large'], '#000000')}</clipPath><g clip-path="url(#${id})">${halo(w, h, { origem: { x: 0.8, y: 0.5 }, raio: 64 }).replace('<svg ', `<svg x="${x}" y="${y}" `)}</g>`
        + rect(px, py, pw, ph, cantos['extra-large'], e.surface, sombra('level1')) + txt(px + 24, py + 48, 'Aprender fica deboaz', 'headline-medium', e['on-surface'])
        + quebrar('Quizzes ao vivo, tarefas e o acompanhamento da família numa só plataforma.', pw - 48, 'body-medium').map((l, i) => txt(px + 24, py + 80 + i * 20, l, 'body-medium', e['on-surface-variant'])).join('')
        + botao(e, px + 24, py + ph - 64, 'Começar grátis', 'filled').svg + botao(e, px + 24 + b1 + 8, py + ph - 64, 'Entrar numa sala', 'outlined').svg;
      // compact do quest: faixa do Halo com o logo de hastes curtas, que some antes do título
      const cw = 320, cx0 = x + (w - cw) / 2, fy = y + h + 16, fh = 112, gid = `boas-vindas-faixa-${modoDe(e)}`, cb = botao(e, 0, 0, 'Inserir código', 'filled').w;
      s += halo(cw, fh, { origem: { x: 0.5, y: 0.45 }, raio: 48, variante: 'curto' }).replace('<svg ', `<svg x="${cx0}" y="${fy}" `)
        + `<linearGradient id="${gid}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${e.surface}" stop-opacity="0"/><stop offset="1" stop-color="${e.surface}"/></linearGradient><rect x="${cx0}" y="${fy + fh - 40}" width="${cw}" height="41" fill="url(#${gid})"/>`
        + txt(cx0 + cw / 2, fy + fh + 28, 'Que bom te ver!', 'headline-small', e['on-surface'], 'middle')
        + txt(cx0 + cw / 2, fy + fh + 56, 'Escolha o perfil para entrar', 'body-medium', e['on-surface-variant'], 'middle') + botao(e, cx0 + (cw - cb) / 2, fy + fh + 76, 'Inserir código', 'filled').svg;
      return s;
    }],
    ['Comum · Seletor de tema', 'Landing page, hub e quest: tema de cor, modo (claro, escuro ou sistema) e se os mascotes seguem as cores do tema, guardados no aparelho.', 248, (e, x0, y) => {
      const w = 340, h = 248, x = x0 + (PW - 2 * PAD - w) / 2;
      let s = rect(x, y, w, h, canto('menu-shape', h), cor(e, 'menu-container-color'), sombra(nivel('menu-elevation'))) + txt(x + 16, y + 28, 'Tema de cor', 'label-medium', e['on-surface-variant']);
      temas.slice(0, 6).forEach((t, i) => {
        const te = t.esquemas[modoDe(e)], cx = x + 36 + i * 52, cy = y + 64, c = t.variante === 'monochrome' ? te.surface : te.primary;
        s += `<circle cx="${cx}" cy="${cy}" r="20" fill="${c}" stroke="${e['outline-variant']}"/>` + (t.padrao ? `<circle cx="${cx}" cy="${cy}" r="24" fill="none" stroke="${e.primary}" stroke-width="3"/>` : '');
      });
      return s + txt(x + 16, y + 116, 'Modo', 'label-medium', e['on-surface-variant']) + segmentado(e, x + 16, y + 132, ['Claro', 'Escuro', 'Sistema'], 2).svg
        + `<rect x="${x + 16}" y="${y + 188}" width="${w - 32}" height="1" fill="${e['outline-variant']}"/>`
        + txt(x + 16, y + 224, 'Mascotes nas cores do tema', tipoDe('switch-label'), cor(e, 'switch-label-color')) + interruptor(e, x + w - 16 - num('switch-track-width'), y + 204, true);
    }],
    ['Comum · Indicador', 'Números da landing page e painel do perfil no quest: valor grande e rótulo; só totais, nunca nomes.', 88, (e, x, y) => {
      const w = (PW - 2 * PAD - 24) / 3;
      return indicador(e, x, y, w, '40', 'escolas') + indicador(e, x + w + 12, y, w, '5', 'empresas') + indicador(e, x + 2 * (w + 12), y, w, '120', 'educadores autônomos');
    }],
    ['Landing · Topo', 'Compact: logo, Entrar e menu. A partir do expanded: links das seções, tema e as três ações (desenhado em escala reduzida).', 136, (e, x, y) => {
      const h = num('top-app-bar-height'), bg = cor(e, 'top-app-bar-container-color');
      const cx0 = x + (PW - 2 * PAD - 360) / 2;
      let s = rect(cx0, y, 360, h, 0, bg) + logoNaBarra(e, cx0 + 16, y, h) + botao(e, cx0 + 360 - 48 - 80, y + 12, 'Entrar', 'text').svg + iconButton(e, cx0 + 360 - 48, y + 12, 'menu', 'padrao').svg;
      let g = rect(0, 0, 860, h, 0, bg) + logoNaBarra(e, 16, 0, h);
      let cx = 32 + logoW(40) + 24;
      for (const l of ['Módulos', 'Para quem', 'Como funciona', 'Depoimentos']) { g += txt(cx, meio(0, h, 'label-large'), l, 'label-large', e['on-surface-variant']); cx += largura(l, 'label-large') + 24; }
      g += iconButton(e, cx, 12, 'paleta', 'padrao').svg;
      g += fila([(a, b) => botao(e, a, b, 'Entrar numa sala', 'outlined'), (a, b) => botao(e, a, b, 'Entrar', 'text'), (a, b) => botao(e, a, b, 'Começar grátis', 'filled')], cx + 52, 12, 8);
      const total = cx + 52 + ['Entrar numa sala', 'Entrar', 'Começar grátis'].reduce((t, r, i) => t + botao(e, 0, 0, r, ['outlined', 'text', 'filled'][i]).w + 8, 0) + 8;
      return s + `<g transform="translate(${x} ${y + h + 8}) scale(${((PW - 2 * PAD) / total).toFixed(3)})">${g.replace('width="860"', `width="${total}"`)}</g>`;
    }],
    ['Landing · Menu', 'Compact e medium: logo e fechar, links das seções, o seletor de tema e as três ações.', 584, (e, x, y) =>
      menuLanding(e, x + (PW - 2 * PAD - 360) / 2, y, 360)],
    ['Landing · Vitrine de módulos', 'Carrossel de cards do catálogo, do mais contratado ao menos contratado, com a posição em texto.', 232, (e, x, y) =>
      cardCatalogo(e, x, y, 338, 'check', 'Presença', 'Chamada em sala.', '120 escolas usando', 'Saiba mais', ['acertou', 'circulo']) + cardCatalogo(e, x + 354, y, 338, 'editar', 'Quiz', 'Salas ao vivo.', '95 escolas usando', 'Saiba mais', ['estrela', 'estrela'])
      + controlesCarrossel(e, x + 182, y + 192, 328, 6, 0)],
    ['Landing · Card de perfil', 'Bloco Para quem: um card por perfil de contratação.', 136, (e, x, y) => {
      const w = (PW - 2 * PAD - 24) / 3, el = sombra(nivel('card-elevated-elevation'));
      return [['escola', 'Escola', 'Família perto da sala.'], ['modulo', 'Empresa', 'Treinos com resposta.'], ['perfil', 'Educador', 'Conta só sua.']].map(([ic, t, d], i) => {
        const cx = x + i * (w + 12);
        return rect(cx, y, w, 136, canto('card-shape', 136), cor(e, 'card-elevated-container-color'), el) + caixaIcone(e, cx + 16, y + 16, ic) + txt(cx + 16, y + 88, t, 'title-medium', e['on-surface']) + txt(cx + 16, y + 110, d, 'body-small', e['on-surface-variant']);
      }).join('');
    }],
    ['Landing · Passos', 'Bloco Como funciona: três passos numerados, em pilha no compact e em linha a partir do expanded.', 40, (e, x, y) =>
      (() => { let cx = x; return ['Crie o quiz no hub', 'Abra a sala e passe o código', 'Jogue pelo quest'].map((t, i) => {
        const x0 = cx; cx += 44 + largura(t, 'title-small') + 32;
        return `<circle cx="${x0 + 16}" cy="${y + 20}" r="16" fill="${e.primary}"/>` + txt(x0 + 16, y + 26, String(i + 1), 'title-medium', e['on-primary'], 'middle') + txt(x0 + 44, y + 26, t, 'title-small', e['on-surface']);
      }).join(''); })()],
    ['Landing · Depoimento', 'Bloco Depoimentos e página do módulo: texto autorizado, imagem e nome da instituição ou do educador.', 136, (e, x0, y, x = x0 + (PW - 2 * PAD - 460) / 2) =>
      rect(x, y, 460, 136, canto('carousel-item-shape', 136), e['surface-container-high']) + txt(x + 24, y + 44, 'Os pais acompanham a presença no mesmo dia.', 'body-large', e['on-surface'])
      + avatar(e, x + 44, y + 96, 20, 'E') + txt(x + 76, y + 101, 'Escola Horizonte', 'label-large', e['on-surface-variant'])],
    ['Landing · Barra de ação fixa', 'Compact, depois que a apresentação sai de vista: Começar grátis e Entrar numa sala.', 72, (e, x0, y, x = x0 + (PW - 2 * PAD - 360) / 2) =>
      rect(x, y, 360, 72, 0, e['surface-container'], sombra('level2')) + botao(e, x + 16, y + 16, 'Começar grátis', 'filled').svg + botao(e, x + 180, y + 16, 'Entrar numa sala', 'outlined').svg],
    ['Landing · Rodapé', 'Landing page e página do módulo: marca, lema, seções, frentes e documentos legais.', 128, (e, x, y) => {
      const w = PW - 2 * PAD;
      let s = rect(x, y, w, 128, cantos.medium, e['surface-container-low']) + logo(e, x + 24, y + 20, 40) + txt(x + 24, y + 88, 'Na força do conhecimento,', 'body-small', e['on-surface-variant']) + txt(x + 24, y + 104, 'a sabedoria floresce.', 'body-small', e['on-surface-variant']);
      [['Módulos', 'Como funciona'], ['Hub', 'Quest'], ['Termos de uso', 'Privacidade']].forEach((col, i) => col.forEach((l, k) => { s += txt(x + 260 + i * 140, y + 40 + k * 28, l, 'body-medium', e['on-surface-variant']); }));
      return s;
    }],
    ['Hub · Grade de módulos ordenando', 'Arrastar ou mover para cima e para baixo; os módulos da conta ficam fora da ordem.', 128, (e, x, y) => {
      const w = (PW - 2 * PAD - 36) / 4;
      const x1 = x + (PW - 2 * PAD - 2 * w - 12) / 2;
      return cardModulo(e, x1, y, w, 128, '', 'Presença', { ordenando: true }) + cardModulo(e, x1 + w + 12, y, w, 128, '', 'Quiz', { ordenando: true });
    }],
    ['Hub · Simulador de custo', 'Detalhe do módulo no catálogo: pessoas e prazo recalculam o custo e os prazos de pagamento; só a conta master vê Incluir na conta.', 296, (e, x0, y) => {
      const w = 340, x = x0 + (PW - 2 * PAD - w) / 2;
      let s = rect(x + 0.5, y + 0.5, w - 1, 296 - 1, canto('card-shape', 296), cor(e, 'card-outlined-container-color'), `stroke="${cor(e, 'card-outlined-outline-color')}"`);
      s += campo(e, x + 16, y + 16, w - 32, 'outlined', 'Pessoas que vão usar', '30', '');
      s += segmentado(e, x + 16, y + 92, ['Mensal', 'Anual'], 0).svg;
      s += rect(x + 16, y + 148, w - 32, 80, cantos.medium, e['surface-container-highest']) + txt(x + 32, y + 172, 'Custo', 'label-medium', e['on-surface-variant'])
        + txt(x + 32, y + 202, 'R$ 147,00/mês', 'headline-small', e['on-surface']) + txt(x + 32, y + 220, 'Pagamento todo dia 10', 'body-small', e['on-surface-variant']);
      return s + botao(e, x + 16, y + 240, 'Incluir na conta', 'filled').svg;
    }],
    ['Hub · Diálogo de código', 'Módulos do time deboaz: o módulo entra na conta só com o código certo.', 240, (e, x, y) => {
      const w = PW - 2 * PAD, dw = 340, dx = x + (w - dw) / 2, dy = y + 20, p = num('dialog-padding');
      let s = rect(x, y, w, 240, cantos.medium, e.scrim, 'fill-opacity="0.32"') + rect(dx, dy, dw, 200, canto('dialog-shape', 200), cor(e, 'dialog-container-color'), sombra(nivel('dialog-elevation')));
      s += txt(dx + p, dy + p + tipos[tipoDe('dialog-headline')].size, 'Adicionar módulo', tipoDe('dialog-headline'), cor(e, 'dialog-headline-color'));
      s += campo(e, dx + p, dy + 64, dw - 2 * p, 'outlined', 'Código do módulo', 'A7K2-9QX', 'Entregue pelo time deboaz');
      const b2 = botao(e, 0, 0, 'Adicionar', 'text').w, b1 = botao(e, 0, 0, 'Cancelar', 'text').w;
      return s + botao(e, dx + dw - p - b2, dy + 200 - p - 28, 'Adicionar', 'text').svg + botao(e, dx + dw - p - b2 - 8 - b1, dy + 200 - p - 28, 'Cancelar', 'text').svg;
    }],
    ['Quest · Agenda e card de tarefa', 'Calendário vertical, um dia por divisória (dia em secondary); hora e borda em primary (no prazo) ou tertiary (vencida); ponto sucesso/erro: pode fazer ou bloqueada; envios possíveis e feitos; agora: Símbolo recortado e mascote.', 420, (e, x, y) => {
      const w = 420, cx = x + 56, cw = w - 56, rx = x + w + 24, rw = PW - 2 * PAD - w - 24;
      return diaAgenda(e, x, y, w, 'HOJE', '19', true) + cardTarefa(e, cx, y + 12, cw, { modulo: 'Aula ao vivo · Matemática', titulo: 'Frações na prática', hora: '14:00 – 14:50', envios: 'Participação única', agora: ['feliz', 'estrela', 'pular'] })
        + cardTarefa(e, cx, y + 166, cw, { modulo: 'Exercícios', titulo: 'Frações equivalentes', hora: 'Até 23:59', prazo: 'vence hoje', envios: 'Envio único · 0 de 1' })
        + diaAgenda(e, x, y + 296, w, 'SÁB', '17') + cardTarefa(e, cx, y + 308, cw, { modulo: 'Exercícios', titulo: 'Tabuada do 7', hora: 'Venceu 23:59', prazo: 'entrega aceita', envios: 'Até 3 envios · 1 de 3', estado: 'vencida' })
        + cardTarefa(e, rx, y + 12, rw, { modulo: 'Leitura', titulo: 'Capítulo 3 do livro', hora: 'Abre 08:00', prazo: 'bloqueada até sexta', envios: 'Envio único · 0 de 1', liberada: false })
        + cardTarefa(e, rx, y + 142, rw, { modulo: 'Quiz', titulo: 'Quiz da semana', hora: 'Venceu 18:00', prazo: 'encerrado', envios: 'Envio único · 1 de 1', estado: 'vencida', liberada: false })
        + cardTarefa(e, rx, y + 272, rw, { modulo: 'Pesquisa', titulo: 'Animais do cerrado', hora: 'Sem prazo', envios: 'Envios livres · 2 feitos', estado: 'livre' });
    }],
    ['Quest · Card de desafio', 'Tarefa sem prazo, competitiva: pódio com os 3 primeiros (mascotes em forma de número, felizes e dançando), 4º e 5º em linhas, sua posição em destaque, suas tentativas num gráfico e Jogar de novo; muda a cada envio.', 476, (e, x, y) => {
      const w = 340;
      return cardDesafio(e, x, y, w, { modulo: 'Jogo · Matemática', titulo: 'Soma rápida', envios: 'Envios livres · 6 feitos', ranking: [['Ana S.', 1980], ['Bruno M.', 1870], ['Lucas A.', 1720], ['Carla P.', 1540], ['Davi R.', 1410]], voce: { pos: 3, pontos: 1720 }, tentativas: [900, 1200, 1350, 1720, 1600, 1500] })
        + cardDesafio(e, x + w + 16, y, PW - 2 * PAD - w - 16, { modulo: 'Jogo · Memória', titulo: 'Memória das frações', envios: 'Até 5 envios · 2 de 5', ranking: [['Carla P.', 2100], ['Ana S.', 1950], ['Davi R.', 1720], ['Bruno M.', 1600], ['Eva L.', 1480]], voce: { pos: 8, pontos: 1240 }, tentativas: [980, 1240] });
    }],
    ['Quest · Quem está acessando', 'Depois do código de acesso: o estudante ou um responsável, com o parentesco, guardado na sessão.', 232, (e, x0, y) =>
      quemAcessa(e, x0 + (PW - 2 * PAD - 340) / 2, y, 340)],
  ];

  // ── montagem ──
  const partes = [];
  let y = 0;
  const secao = (titulo, desc) => {
    y += 56;
    partes.push(txt(M, y, titulo, 'headline-small', fundo['on-surface']));
    if (desc) { y += 28; partes.push(txt(M, y, desc, 'body-medium', fundo['on-surface-variant'])); }
    y += 20;
  };
  const painel = (x, w, h, e, rotulo) => {
    partes.push(rect(x, y, w, h, cantos.large, e.surface, `stroke="${e['outline-variant']}"`));
    if (rotulo) partes.push(txt(x + w - PAD, y + 28, rotulo, 'label-medium', e['on-surface-variant'], 'end'));
  };
  // h = altura do conteúdo; o painel soma o rótulo do esquema e as margens
  // id do grupo: comp-<slug>-claro|escuro, para copiar o desenho para uma vista
  const slug = (t) => t.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
  const duplo = (conteudo, desenho, titulo) => {
    const h = PAD + 24 + conteudo + PAD;
    for (const [i, e, r, m] of [[0, esquemas.light, 'CLARO', 'claro'], [1, esquemas.dark, 'ESCURO', 'escuro']]) {
      const x = M + i * (PW + GAP);
      painel(x, PW, h, e, r);
      partes.push(`<g id="comp-${slug(titulo)}-${m}">${desenho(e, x + PAD, y + PAD + 24)}</g>`);
    }
    y += h;
  };

  // marca d'água de topo
  const TOPO = 112;
  partes.push(`<g id="marca-topo">${rect(0, 0, L, TOPO, 0, fundo['surface-container'])}<rect x="0" y="${TOPO - 2}" width="${L}" height="2" fill="url(#acento)"/>`
    + txt(M, 40, 'BOAZ · DESIGN SYSTEM', 'label-medium', fundo.primary, 'start', 'letter-spacing="1.5"')
    + txt(M, 72, 'Tema do design system', 'headline-small', fundo['on-surface'])
    + txt(M, 94, `Material Design 3 · semente ${semente} (${variante}) · identidade ${status === 'provisoria' ? 'provisória' : 'definida'}`, 'body-medium', fundo['on-surface-variant'])
    + txt(L - 192, 44, 'DATA', 'label-small', fundo['on-surface-variant']) + txt(L - 192, 72, data.replace(' ', ' · '), 'title-medium', fundo['on-surface'])
    + `<rect x="${L - 216}" y="32" width="1" height="48" fill="${fundo['outline-variant']}"/>`
    + txt(L - 288, 44, 'VERSÃO', 'label-small', fundo['on-surface-variant']) + rect(L - 288, 56, 48, 22, 11, fundo['primary-container'])
    + txt(L - 264, 71, versao, 'label-medium', fundo['on-primary-container'], 'middle') + '</g>');
  y = TOPO;

  // cores
  secao('Cores', 'Papéis do esquema gerado da semente. Texto on-X sobre fundo X; os pares têm contraste garantido.');
  const familias = [
    ['primary', 'on-primary', 'primary-container', 'on-primary-container'],
    ['secondary', 'on-secondary', 'secondary-container', 'on-secondary-container'],
    ['tertiary', 'on-tertiary', 'tertiary-container', 'on-tertiary-container'],
    ['error', 'on-error', 'error-container', 'on-error-container'],
    ...extras.map((n) => [n, `on-${n}`, `${n}-container`, `on-${n}-container`]),
    ['surface-container-lowest', 'surface-container-low', 'surface-container', 'surface-container-high'],
    ['surface', 'surface-container-highest', 'on-surface', 'on-surface-variant'],
    ['outline', 'outline-variant', 'inverse-surface', 'inverse-primary'],
  ];
  const par = (e, n) => {
    const fixos = { outline: 'surface', 'outline-variant': 'on-surface', 'inverse-surface': 'inverse-on-surface', 'inverse-primary': 'inverse-surface', 'on-surface': 'surface', 'on-surface-variant': 'surface' };
    if (fixos[n]) return e[fixos[n]];
    if (n.startsWith('surface')) return e['on-surface'];
    if (n.startsWith('on-')) return e[n.slice(3)];
    return e[`on-${n}`];
  };
  const SW = (PW - 2 * PAD - 24) / 4, SH = 56;
  duplo(familias.length * (SH + 8) - 8, (e, x, y0) => familias.map((f, li) => f.map((n, ci) => {
    const sx = x + ci * (SW + 8), sy = y0 + li * (SH + 8);
    return rect(sx, sy, SW, SH, cantos.small, e[n], n.startsWith('surface') ? `stroke="${e['outline-variant']}"` : '')
      + txt(sx + 10, sy + 22, n, n.length > 22 ? 'label-small' : 'label-medium', par(e, n)) + mono(sx + 10, sy + 42, e[n], par(e, n));
  }).join('')).join(''), 'Cores');

  // temas de cor
  secao('Temas de cor', `${temas.length} combinações prontas; cada uma gera os mesmos papéis de cor (troque o tema sem mudar componentes). Padrão: ${temas.find((t) => t.padrao).nome}.`);
  const CW = (L - 2 * M - 2 * 24) / 3, CAB = 104, LADO = 104, CH = CAB + 2 * LADO;
  temas.forEach((t, i) => {
    const cx = M + (i % 3) * (CW + 24), cy = y + Math.floor(i / 3) * (CH + 24), clip = `tema-${t.id}`;
    const C = t.esquemas.light, D = t.esquemas.dark;
    let g = `<clipPath id="${clip}">${rect(cx, cy, CW, CH, cantos.large, '#000000')}</clipPath><g clip-path="url(#${clip})">`;
    g += rect(cx, cy, CW, CAB + LADO, 0, C.surface) + rect(cx, cy + CAB + LADO, CW, LADO, 0, D.surface);
    g += txt(cx + 16, cy + 28, t.nome + (t.padrao ? ' · padrão' : ''), 'title-medium', C['on-surface']);
    const modo = t.modo ? ` · melhor no ${t.modo === 'dark' ? 'escuro' : 'claro'}` : '';
    g += mono(cx + CW - 16, cy + 28, (t.variante === 'monochrome' ? 'monocromático' : t.semente + (t.terciaria ? ' + ' + t.terciaria : '')) + modo, C['on-surface-variant'], 'end');
    quebrar(`${t.descricao} Harmonia: ${t.harmonia}.`, CW - 32, 'body-small').slice(0, 3).forEach((l, k) => { g += txt(cx + 16, cy + 50 + k * 16, l, 'body-small', C['on-surface-variant']); });
    for (const [e, oy] of [[C, cy + CAB], [D, cy + CAB + LADO]]) {
      const papeis = ['primary', 'primary-container', 'secondary', 'secondary-container', 'tertiary', 'tertiary-container'];
      const fw = (CW - 32) / papeis.length;
      papeis.forEach((n, k) => { g += `<rect x="${cx + 16 + k * fw}" y="${oy + 8}" width="${fw}" height="28" fill="${e[n]}"/>`; });
      g += fila([(x0, y0) => botao(e, x0, y0, 'Filled', 'filled'), (x0, y0) => botao(e, x0, y0, 'Tonal', 'tonal'),
        (x0, y0) => chip(e, x0, y0 + 4, 'Chip', { selecionado: true }), (x0, y0) => fab(e, x0, y0, 'small')], cx + 16, oy + 48, 10);
    }
    g += '</g>' + rect(cx + 0.5, cy + 0.5, CW - 1, CH - 1, cantos.large, null, `stroke="${fundo['outline-variant']}"`);
    partes.push(g);
  });
  y += Math.ceil(temas.length / 3) * (CH + 24) - 24;

  // tipografia
  secao('Tipografia', `Marca: ${fonte.brand.familia} (display, headline, title-large) · Texto: ${fonte.plain.familia} (o resto). Tamanho / altura de linha · peso · tracking.`);
  const tipoLinhas = Object.entries(tipos);
  const alturaTipo = tipoLinhas.reduce((a, [, t]) => a + Math.max(t['line-height'], 36) + 12, 0);
  painel(M, L - 2 * M, alturaTipo + 2 * PAD, esquemas.light);
  let ty = y + PAD;
  for (const [n, t] of tipoLinhas) {
    const h = Math.max(t['line-height'], 36) + 12;
    partes.push(txt(M + PAD, ty + h / 2 - 2, n, 'label-large', esquemas.light['on-surface'])
      + mono(M + PAD, ty + h / 2 + 14, `${t.size}/${t['line-height']} · ${t.weight} · ${t.tracking}`, esquemas.light['on-surface-variant'])
      + txt(M + 280, ty + h / 2 + t.size * 0.35, n.startsWith('display') ? 'Aa Boaz 123' : 'Tudo pronto para começar', n, esquemas.light['on-surface']));
    ty += h;
  }
  y += alturaTipo + 2 * PAD;

  // forma e elevação
  secao('Forma e elevação', 'Cantos em escala fixa (maior elemento, maior canto). Elevação: cor do contêiner + sombra.');
  const nomesCanto = Object.keys(cantos);
  const FW = (L - 2 * M - 2 * PAD - (nomesCanto.length - 1) * 12) / nomesCanto.length;
  painel(M, L - 2 * M, 2 * PAD + 96 + 24 + 32 + 96 + 24, esquemas.light);
  nomesCanto.forEach((n, i) => {
    const fx = M + PAD + i * (FW + 12);
    partes.push(rect(fx, y + PAD, FW, 96, n === 'full' ? 48 : cantos[n], esquemas.light['primary-container'])
      + txt(fx + FW / 2, y + PAD + 96 + 18, n, 'label-small', esquemas.light['on-surface-variant'], 'middle'));
  });
  ['level0', 'level1', 'level2', 'level3', 'level4', 'level5'].forEach((n, i) => {
    const ew = (L - 2 * M - 2 * PAD - 5 * 24) / 6, ex = M + PAD + i * (ew + 24), ey = y + PAD + 96 + 24 + 32;
    partes.push(rect(ex, ey, ew, 96, cantos.medium, esquemas.light['surface-container-low'], sombra(n))
      + txt(ex + ew / 2, ey + 52, n, 'label-large', esquemas.light['on-surface'], 'middle'));
  });
  y += 2 * PAD + 96 + 24 + 32 + 96 + 24;

  // layout por janela
  secao('Layout por janela', 'Mesma tela em compact, medium e large: margem, colunas e navegação mudam juntos (escala 0,5).');
  const FR = [['compact', 360, 720], ['medium', 720, 720], ['large', 1280, 720]];
  const ESC = 0.5, FH = 720 * ESC + 56;
  painel(M, L - 2 * M, FH + 2 * PAD, esquemas.light);
  let fx = M + PAD;
  for (const [n, w, h] of FR) {
    const e = esquemas.light, j = janelas[n];
    const clip = `janela-${n}`;
    let s = `<clipPath id="${clip}">${rect(0, 0, w, h, 24, '#000000')}</clipPath><g clip-path="url(#${clip})">` + rect(0, 0, w, h, 0, e.surface);
    let cx = 0, cw = w;
    if (j.navigation === 'navigation-rail') { s += navRail(e, 0, 0, h); cx = num('navigation-rail-width'); cw = w - cx; }
    if (j.navigation === 'navigation-drawer') { s += drawer(e, 0, 0, 300, h); cx = 300; cw = w - cx; }
    s += topAppBar(e, cx, 0, cw, 'Início');
    const cols = j.columns, gut = j.gutter, mar = j.margin, colW = (cw - 2 * mar - (cols - 1) * gut) / cols;
    const porCard = cols === 4 ? 4 : cols === 8 ? 4 : 3, nCards = Math.floor(cols / porCard);
    for (let r = 0; r < 3; r++) for (let k = 0; k < nCards; k++) {
      const kx = cx + mar + k * porCard * (colW + gut);
      s += rect(kx, 80 + r * 152, porCard * colW + (porCard - 1) * gut, 136, cantos.medium, e['surface-container-highest']);
    }
    if (j.navigation === 'navigation-bar') s += navBar(e, 0, h - num('navigation-bar-height'), w);
    s += '</g>' + rect(1, 1, w - 2, h - 2, 24, null, `stroke="${e['outline-variant']}" stroke-width="2"`);
    partes.push(`<g transform="translate(${fx} ${y + PAD}) scale(${ESC})">${s}</g>`
      + txt(fx, y + PAD + h * ESC + 24, `${n} · ${j.min}${j.max ? '–' + j.max : '+'}dp`, 'label-large', e['on-surface'])
      + txt(fx, y + PAD + h * ESC + 42, `${cols} colunas · ${j.navigation}`, 'body-small', e['on-surface-variant']));
    fx += w * ESC + 48;
  }
  y += FH + 2 * PAD;

  // componentes
  for (const [titulo, desc, h, desenho] of COMPONENTES) { secao(titulo, desc); duplo(h, desenho, titulo); }

  // ── mascotes ──
  const LW = L - 2 * M;
  const grade = (itens, cols, cw, ch, desenhar) => { itens.forEach((it, i) => desenhar(it, M + PAD + (i % cols) * cw, y + PAD + Math.floor(i / cols) * ch, i)); return PAD * 2 + Math.ceil(itens.length / cols) * ch; };
  const rotuloM = (x, yy, w, s) => partes.push(txt(x + w / 2, yy, s, 'label-medium', fundo['on-surface-variant'], 'middle'));

  secao('Mascotes · expressões', `${mascotes.expressoes.length} expressões para dar retorno: acerto, erro, dúvida, espera, progresso, social. Cor própria de cada expressão nos temas monocromáticos; cor do tema nos coloridos.`);
  {
    const cols = 10, cw = (LW - 2 * PAD) / cols, ch = 132;
    const h = 2 * PAD + Math.ceil(mascotes.expressoes.length / cols) * ch;
    partes.push(`<g id="mascotes-expressoes">` + rect(M, y, LW, h, cantos.large, fundo.surface, `stroke="${fundo['outline-variant']}"`));
    grade(mascotes.expressoes, cols, cw, ch, ([id, nome], x, yy) => { partes.push(masc(id, {}, x + (cw - 92) / 2, yy, 92)); rotuloM(x, yy + 112, cw, nome); });
    partes.push('</g>');
    y += h;
  }

  secao('Mascotes · formas', `Planas, setas e 3D (${mascotes.formas.length}); números, letras e sinais (${mascotes.glifos.length}). O rosto é encaixado em cada forma; qualquer expressão serve em qualquer forma.`);
  {
    const cols = 11, cw = (LW - 2 * PAD) / cols, ch = 128;
    const linhasF = Math.ceil(mascotes.formas.length / cols);
    const gc = 21, gw = (LW - 2 * PAD) / gc, gh = 92;
    const h = 2 * PAD + linhasF * ch + 16 + Math.ceil(mascotes.glifos.length / gc) * gh;
    partes.push(`<g id="mascotes-formas">` + rect(M, y, LW, h, cantos.large, fundo.surface, `stroke="${fundo['outline-variant']}"`));
    grade(mascotes.formas, cols, cw, ch, ([id, nome, e], x, yy) => { partes.push(masc(e, { forma: id }, x + (cw - 96) / 2, yy, 96, 96)); rotuloM(x, yy + 112, cw, nome); });
    const y0 = y + PAD + linhasF * ch + 16;
    const cores8 = ['amarelo', 'verde', 'laranja', 'rosa', 'roxo', 'azul', 'ciano', 'vermelho'];
    mascotes.glifos.forEach(([id], i) => { const x = M + PAD + (i % gc) * gw, yy = y0 + Math.floor(i / gc) * gh; partes.push(masc('feliz', { forma: id, cor: cores8[i % 8] }, x + 2, yy, gw - 4, gh - 8)); });
    partes.push('</g>');
    y += h;
  }

  secao('Mascotes · proporções, só o rosto e cartões', 'Proporção muda o corpo (o rosto se ajusta). Só o rosto tem contorno de adesivo e lê em fundo claro e escuro. No cartão, o fundo fica parado e só o rosto se move.');
  {
    const h = 2 * PAD + 220;
    partes.push(`<g id="mascotes-variacoes">` + rect(M, y, LW, h, cantos.large, fundo.surface, `stroke="${fundo['outline-variant']}"`));
    let x = M + PAD;
    for (const [id, nome] of mascotes.proporcoes) { partes.push(masc('feliz', { forma: 'circulo', proporcao: id }, x, y + PAD, 112, 160)); rotuloM(x, y + PAD + 186, 112, nome); x += 124; }
    x += 24;
    const claro = esquemas.light;
    partes.push(rect(x, y + PAD, 296, 160, cantos.medium, claro.surface) + rect(x + 148, y + PAD, 148, 160, cantos.medium, '#000000'));
    ['acertou', 'duvida', 'errou', 'estrela'].forEach((id, i) => partes.push(masc(id, { forma: 'rosto' }, x + 6 + i * 72 + (i > 1 ? 4 : 0), y + PAD + 44, 64, 72)));
    rotuloM(x, y + PAD + 186, 296, 'Só o rosto · claro e escuro');
    x += 320;
    partes.push(masc('feliz', { forma: 'cartao' }, x, y + PAD, 120, 160));
    partes.push(masc('acertou', { forma: 'cartaoHorizontal' }, x + 136, y + PAD + 20, 160, 120));
    rotuloM(x, y + PAD + 186, 296, 'Cartão vertical e horizontal');
    partes.push('</g>');
    y += h;
  }

  secao('Mascotes nos componentes', 'Botão com o mascote no lugar do ícone (só o rosto), card com cartão, lista com avatar, snackbar de retorno, chip e estado vazio. O mascote nunca é a única mensagem: sempre vem com texto.');
  duplo(372, (e, x, yy) => {
    let s = fila([(cx, cy) => botao(e, cx, cy, 'Acertou!', 'filled', { mascote: ['acertou', { forma: 'rosto' }] }),
      (cx, cy) => botao(e, cx, cy, 'Pedir dica', 'tonal', { mascote: ['duvida', { forma: 'rosto' }] }),
      (cx, cy) => botao(e, cx, cy, 'Tentar de novo', 'outlined', { mascote: ['errou', { forma: 'rosto' }] })], x, yy);
    const y1 = yy + 64;
    // card com cartão horizontal
    const cw = 300;
    s += rect(x, y1, cw, 308, canto('card-shape', 308), cor(e, 'card-elevated-container-color'), sombra(nivel('card-elevated-elevation')));
    s += masc('estrela', { forma: 'cartaoHorizontal' }, x + 12, y1 + 12, cw - 24, (cw - 24) * 0.75);
    s += txt(x + 16, y1 + 252, 'Sequência de 7 dias!', tipoDe('card-title'), e['on-surface']);
    s += txt(x + 16, y1 + 276, 'Você estudou todos os dias da semana.', tipoDe('card-body'), e['on-surface-variant']);
    // lista com avatar
    const lx = x + cw + 24, lw = PW - 2 * PAD - cw - 24;
    s += rect(lx, y1, lw, 144, cantos.medium, null, `stroke="${e['outline-variant']}"`);
    [['orgulhoso', 'Frações', 'Concluído · 10/10', 'amarelo'], ['pensando', 'Geometria', 'Em andamento · 4/12', 'ciano']].forEach(([id, t1, t2, c], i) => {
      const ry = y1 + 8 + i * 64;
      s += masc(id, { cor: c }, lx + 16, ry + 8, 44);
      s += txt(lx + 76, ry + 28, t1, 'body-large', e['on-surface']) + txt(lx + 76, ry + 48, t2, 'body-medium', e['on-surface-variant']);
      if (i) s += `<rect x="${lx}" y="${y1 + 72}" width="${lw}" height="1" fill="${e['outline-variant']}"/>`;
    });
    // snackbar de retorno
    const sy = y1 + 164;
    s += rect(lx, sy, lw, 56, cantos.extraSmall ?? cantos['extra-small'] ?? 4, e['inverse-surface'], sombra('level3'));
    s += masc('rindo', { forma: 'rosto' }, lx + 12, sy + 10, 36);
    s += txt(lx + 56, sy + 34, 'Boa! +10 pontos', 'body-medium', e['inverse-on-surface']) + txt(lx + lw - 16, sy + 34, 'Ver', 'label-large', e['inverse-primary'], 'end');
    // chip e estado vazio
    const cy2 = sy + 76;
    s += rect(lx, cy2, 148, 32, cantos.small, e['secondary-container']) + masc('piscadinha', { forma: 'rosto' }, lx + 8, cy2 + 4, 24) + txt(lx + 38, cy2 + 21, 'Você consegue', 'label-large', e['on-secondary-container']);
    s += masc('sonolento', { forma: 'nuvem' }, lx + 160, cy2 - 8, 56, 48) + txt(lx + 224, cy2 + 14, 'Nada por aqui ainda', 'label-large', e['on-surface']) + txt(lx + 224, cy2 + 32, 'Comece uma trilha', 'body-small', e['on-surface-variant']);
    return s;
  }, 'Mascotes nos componentes');

  // compostos: um painel por bloco, separados por frente no título
  for (const [titulo, desc, h, desenho] of COMPOSTOS) { secao(titulo, desc); duplo(h, desenho, titulo); }

  // marca d'água de rodapé
  y += 48;
  const RODAPE = 72;
  partes.push(`<g id="marca-rodape">${rect(0, y, L, RODAPE, 0, fundo['surface-container'])}<rect x="0" y="${y}" width="${L}" height="1" fill="${fundo['outline-variant']}"/>`
    + txt(M, y + 42, `${versao} · gerado por scripts/gerar.mjs a partir de identidade/marca.json e tokens/*.json — não edite à mão`, 'body-small', fundo['on-surface-variant'])
    + mono(L - M, y + 42, '0_Assets/design/tema.svg', fundo['on-surface-variant'], 'end') + '</g>');
  const H = y + RODAPE;

  // ── documento ──
  const pilha = (f) => [f.familia, ...f.alternativas].map((x) => (/\s/.test(x) ? `'${x}'` : x)).join(', ');
  const estilos = Object.entries(tipos).map(([n, t]) =>
    `.t-${n} { font: ${t.weight} ${t.size}px ${pilha(t.font === 'brand' ? fonte.brand : fonte.plain)}; letter-spacing: ${t.tracking}px; }`).join('\n    ');
  const sombras = { level1: [1, 3, 0.3], level2: [2, 6, 0.3], level3: [4, 8, 0.3], level4: [6, 10, 0.3], level5: [8, 12, 0.3] };
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${L}" height="${H}" viewBox="0 0 ${L} ${H}">
  <title>boaz · tema do design system · ${versao}</title>
  <desc>Quadro visual do design system (Material Design 3): cores, tipografia, forma, elevação, layout por janela e componentes nos esquemas claro e escuro. Gerado por 0_Assets/design/scripts/gerar.mjs — não edite à mão.</desc>
  <style>
    ${estilos}
    .mono { font: 11px ui-monospace, 'SF Mono', Menlo, Consolas, monospace; }
  </style>
  <defs>
    <linearGradient id="acento" x1="0" x2="1"><stop offset="0" stop-color="${fundo.primary}"/><stop offset="1" stop-color="${fundo.tertiary}"/></linearGradient>
${Object.entries(sombras).map(([n, [dy, b, o]]) => `    <filter id="el-${n}" x="-20%" y="-20%" width="140%" height="160%"><feDropShadow dx="0" dy="${dy}" stdDeviation="${b / 2}" flood-color="#000000" flood-opacity="${o}"/></filter>`).join('\n')}
${Object.entries(ICONES).map(([n, d]) => `    <symbol id="i-${n}" viewBox="0 0 24 24"><path d="${d}"/></symbol>`).join('\n')}
  </defs>
  <rect width="${L}" height="${H}" fill="${fundo['surface-container-lowest']}"/>
${partes.map((p) => '  ' + p).join('\n')}
</svg>
`;
}
