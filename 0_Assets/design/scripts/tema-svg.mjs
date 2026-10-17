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
};

export function temaSvg({ esquemas, tipos, cantos, janelas, tokensComp, fonte, versao, data, status, semente, variante, extras, temas, mascote, mascotes }) {
  let nm = 0; // prefixo único de cada mascote embutido
  const masc = (id, op, x, y, w, h) => mascote(id, op, `m${++nm}-`, x, y, w, h);
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
  function botao(e, x, y, rotulo, variante, { icon, desabilitado, mascote: mx } = {}) {
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
      outlined: [null, cor(e, 'button-outlined-label-color'), null],
      text: [null, cor(e, 'button-text-label-color'), null],
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

  function chip(e, x, y, rotulo, { icon, selecionado } = {}) {
    const h = num('chip-height'), papel = tipoDe('chip-label'), p = num('chip-padding'), pi = num('chip-padding-with-icon'), ti = num('chip-icon-size');
    const ic = selecionado ? 'check' : icon;
    const w = Math.round(largura(rotulo, papel) + (ic ? pi + ti + 8 + p : 2 * p));
    const r = canto('chip-shape', h);
    const bg = selecionado ? cor(e, 'chip-selected-container-color') : null;
    const fg = selecionado ? cor(e, 'chip-selected-label-color') : cor(e, 'chip-label-color');
    const borda = selecionado ? '' : `stroke="${cor(e, 'chip-outline-color')}"`;
    return { w, svg: rect(x + 0.5, y + 0.5, w - 1, h - 1, r, bg, borda) + (ic ? icone(ic, x + pi, y + (h - ti) / 2, ti, selecionado ? fg : e.primary) : '') + txt(ic ? x + pi + ti + 8 : x + w / 2, meio(y, h, papel), rotulo, papel, fg, ic ? 'start' : 'middle') };
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
      s += rect(x + 1, y + 1, w - 2, h - 2, r, null, `stroke="${destaque}" stroke-width="2"`);
      const lw = largura(rotulo, pp) + 8;
      s += `<rect x="${x + p - 4}" y="${y - 2}" width="${lw}" height="4" fill="${e.surface}"/>`;
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
    ['Diálogo e snackbar', 'Diálogo em surface-container-high, canto extra-large; snackbar em inverse-surface com ação inverse-primary.', 280, (e, x, y) => {
      const w = PW - 2 * PAD;
      let s = rect(x, y, w, 208, cantos.medium, e.scrim, 'fill-opacity="0.32"');
      const d = dialogo(e, x + (w - 360) / 2, y + (208 - 196) / 2, 360);
      s += d.svg;
      return s + snackbar(e, x, y + 232, Math.min(400, w));
    }],
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
