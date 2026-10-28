// =============================================================================
// boaz · status do servidor: ilustrações da saúde do sistema, para páginas de
// status, avisos de instabilidade e telas de manutenção ou reparo
//
// O mascote é o servidor (cubo = aplicação, cilindro = banco, esfera = rede) e o
// rosto diz como ele está. Em volta, peças da marca contam o resto:
//   medidor   o anel do símbolo vira um medidor de saúde (0 a 100%, tracejado = programado)
//   pulso     uma linha de batimento atravessa a tela por trás do anel
//   sinal     quatro antenas do símbolo como barras de sinal (0 a 4 acesas)
//   ondas     linhas concêntricas do vão e um brilho na cor do estado
//
//   operacional  feliz, tudo verde        lento        esperando, medidor pela metade
//   instavel     um servidor tonto        fora         doente, pulso reto
//   manutencao   sonolento, tracejado     reparo       consertando, medidor carregando
//   voltou       aliviado comemorando
//
// A cor de cada estado é um papel do tema (sucesso, aviso, error, tertiary), então
// as imagens seguem o tema e o modo; o mascote mantém a cor do estado em qualquer tema.
// A imagem não tem texto: o título e a mensagem (ESTADOS[*].titulo / .mensagem)
// vêm escritos ao lado, num painel de superfície.
//
// Este módulo não usa nada do Node nem importa nada: recebe o núcleo dos mascotes
// em criarStatus. O gerador (scripts/status-arquivos.mjs) grava as imagens
// oficiais em identidade/imagens/status/.
//
//   const st = criarStatus({ mascotes });
//   st.desenhar('reparo', { largura: 1600, altura: 900, cores, movimentoReduzido });
//   st.selo('reparo', { tamanho: 96, cores });           // selo pequeno: medidor + rosto
// Regras de uso: catálogo design-system.html#status.
// =============================================================================

/** Os estados, do melhor ao pior e depois manutenção, reparo e volta. */
export const ESTADOS = {
  operacional: {
    nome: 'Operacional', uso: 'tudo funcionando: página de status, rodapé de saúde', papel: 'sucesso',
    medidor: 1, sinal: 4, pulso: 'normal', mascote: { expressao: 'feliz', forma: 'cubo', cor: 'verde', movimento: 'flutuar' },
    titulo: 'Tudo de boa por aqui', mensagem: 'Todos os serviços estão funcionando normalmente.',
  },
  lento: {
    nome: 'Lento', uso: 'tempo de resposta alto, fila cheia', papel: 'aviso',
    medidor: 0.6, sinal: 2, pulso: 'fraco', mascote: { expressao: 'esperando', forma: 'cubo', cor: 'amarelo' },
    titulo: 'Um pouco mais devagar', mensagem: 'O servidor está lento agora. Funciona, mas pode demorar um pouquinho.',
  },
  instavel: {
    nome: 'Instabilidade parcial', uso: 'um serviço falhando, o resto de pé', papel: 'aviso',
    medidor: 0.4, sinal: 2, pulso: 'irregular',
    rack: [{ expressao: 'feliz', forma: 'cubo', cor: 'verde' }, { expressao: 'tonto', forma: 'cilindro', cor: 'laranja', movimento: 'tremer' }, { expressao: 'feliz', forma: 'esfera', cor: 'verde' }],
    mascote: { expressao: 'tonto', forma: 'cilindro', cor: 'laranja' },
    titulo: 'Parte do sistema instável', mensagem: 'Alguns recursos podem falhar. Já estamos olhando.',
  },
  fora: {
    nome: 'Fora do ar', uso: 'servidor sem resposta (tom acolhedor, nunca alarmista)', papel: 'error',
    medidor: 0.06, sinal: 0, pulso: 'reto', mascote: { expressao: 'doente', forma: 'cilindro', cor: 'azul' },
    titulo: 'Fora do ar', mensagem: 'O servidor não está respondendo. A equipe já foi avisada e está cuidando disso.',
  },
  manutencao: {
    nome: 'Manutenção programada', uso: 'pausa planejada, com hora para voltar', papel: 'tertiary',
    medidor: 1, tracejado: true, sinal: 1, pulso: 'calmo', mascote: { expressao: 'sonolento', forma: 'cubo', cor: 'roxo' },
    titulo: 'Pausa para manutenção', mensagem: 'Estamos cuidando do sistema. Voltamos às {hora}.',
  },
  reparo: {
    nome: 'Reparo em andamento', uso: 'conserto acontecendo agora, tela de “já volta”', papel: 'aviso',
    medidor: 'carregando', sinal: 'carregando', pulso: 'intermitente', mascote: { expressao: 'consertando', forma: 'cubo', cor: 'azul' },
    titulo: 'Consertando…', mensagem: 'Encontramos um problema e já estamos arrumando. Volta já, de boa.',
  },
  voltou: {
    nome: 'Voltou!', uso: 'serviço restabelecido, primeira abertura depois do reparo', papel: 'sucesso',
    medidor: 1, sinal: 4, pulso: 'forte', brilhos: true, mascote: { expressao: 'aliviado', forma: 'cubo', cor: 'verde', movimento: 'comemorar' },
    titulo: 'Voltamos!', mensagem: 'Tudo funcionando de novo. Obrigado pela paciência.',
  },
};
/** Formatos de referência (a imagem serve em qualquer proporção) e o do selo. */
export const FORMATOS = { horizontal: [1600, 900], vertical: [900, 1600], quadrado: [1000, 1000] };
/** Papéis de cor que os status usam (de tokens/cores.json). */
export const PAPEIS = ['surface', 'surface-container', 'surface-container-high', 'on-surface', 'outline-variant', 'sucesso', 'aviso', 'error', 'tertiary'];

const n = (v) => Math.round(v * 100) / 100;
const FZ = 'fill="freeze" calcMode="spline" keyTimes="0;1" keySplines="0.3 0 0.2 1"';

// batimento: pontos [u, v] de um período (u de 0 a 1; v negativo sobe)
const BATIDA = [[0, 0], [0.3, 0], [0.35, -0.12], [0.41, 0], [0.47, 0], [0.5, 0.16], [0.54, -1], [0.59, 0.4], [0.63, 0], [0.72, 0], [0.79, -0.22], [0.87, 0], [1, 0]];
/** Cada pulso: lista de batidas [largura relativa, amplitude] que se repete, segundos por período e traço. */
const PULSOS = {
  normal: { batidas: [[1, 1]], dur: 1.2 },
  fraco: { batidas: [[1.6, 0.4]], dur: 2.4 },
  irregular: { batidas: [[1, 1], [0.7, 0.35], [1.3, 0.8], [0.8, 0.15]], dur: 1.1 },
  reto: { batidas: [[1, 0]], dur: 0 },
  calmo: { batidas: [[1.4, 'onda']], dur: 3.2 },
  intermitente: { batidas: [[1, 0.7], [1, 0.7]], dur: 1.4, tracejado: true },
  forte: { batidas: [[1, 1.25]], dur: 0.9 },
};

export function criarStatus({ mascotes }) {
  const { estatico, animado, embutir } = mascotes;
  let seq = 0;

  const masc = (m, cx, cy, t, parado, forma = m.forma) => {
    const o = { forma, cor: m.cor };
    const svg = parado ? estatico(m.expressao, o) : animado(m.expressao, { ...o, movimento: m.movimento || undefined });
    return embutir(svg, `status-m${++seq}-`, cx - t / 2, cy - t / 2, t, t);
  };

  // anel do símbolo como medidor: trilho em outline-variant e o arco na cor do estado, a partir do topo
  const medidor = (cx, cy, R, esp, cor, trilho, valor, tracejado, parado, encher) => {
    const rm = R - esp / 2, volta = 2 * Math.PI * rm, gira = `transform="rotate(-90 ${n(cx)} ${n(cy)})"`;
    const base = `cx="${n(cx)}" cy="${n(cy)}" r="${n(rm)}" fill="none" stroke-width="${n(esp)}"`;
    let s = `<circle ${base} stroke="${trilho}"/>`;
    if (valor === 'carregando') {
      const arco = `<circle ${base} stroke="${cor}" stroke-linecap="round" stroke-dasharray="${n(volta * 0.3)} ${n(volta * 0.7)}" ${gira}`;
      s += parado ? `${arco}/>` : `${arco}><animateTransform attributeName="transform" type="rotate" from="-90 ${n(cx)} ${n(cy)}" to="270 ${n(cx)} ${n(cy)}" dur="1.6s" repeatCount="indefinite"/></circle>`;
    } else if (tracejado) {
      const p = volta / 24;
      s += `<circle ${base} stroke="${cor}" stroke-dasharray="${n(p * 0.55)} ${n(p * 0.45)}" ${gira}/>`;
    } else {
      // voltou: o arco se enche uma vez e fica cheio
      const cheio = n(volta * valor);
      s += `<circle ${base} stroke="${cor}" stroke-linecap="round" stroke-dasharray="${encher && !parado ? `0 ${n(volta)}` : `${cheio} ${n(volta)}`}" ${gira}>${encher && !parado ? `<animate attributeName="stroke-dasharray" from="0 ${n(volta)}" to="${cheio} ${n(volta)}" dur="1.2s" ${FZ}/>` : ''}</circle>`;
    }
    return s;
  };

  // linha de batimento de x0 a x1 na altura y; anima correndo para a esquerda, em laço
  const pulso = (tipo, x0, x1, y, A, P, cor, larg, parado) => {
    const def = PULSOS[tipo], id = `status-p${++seq}`;
    const padrao = def.batidas.reduce((s, [k]) => s + k * P, 0);
    const pts = [];
    for (let x = x0 - padrao, i = 0; x < x1 + padrao; i++) {
      const [k, amp] = def.batidas[i % def.batidas.length], L = k * P;
      if (amp === 'onda') for (let j = 0; j <= 24; j++) pts.push([x + (L * j) / 24, y - A * 0.16 * Math.sin((2 * Math.PI * j) / 24)]);
      else for (const [u, v] of BATIDA) pts.push([x + u * L, y + v * A * amp]);
      x += L;
    }
    const tr = def.tracejado ? ` stroke-dasharray="${n(larg * 3)} ${n(larg * 2.4)}"` : '';
    const linha = `<path d="M${pts.map(([a, b]) => `${n(a)} ${n(b)}`).join('L')}" fill="none" stroke="${cor}" stroke-width="${n(larg)}" stroke-linecap="round" stroke-linejoin="round"${tr}/>`;
    const anda = !parado && def.dur ? `<animateTransform attributeName="transform" type="translate" from="0 0" to="${n(-padrao)} 0" dur="${n(def.dur * padrao / P)}s" repeatCount="indefinite"/>` : '';
    // as pontas somem nas bordas da imagem
    return `<defs><linearGradient id="${id}-g" x1="${n(x0)}" x2="${n(x1)}" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#FFFFFF" stop-opacity="0"/><stop offset="0.12" stop-color="#FFFFFF"/><stop offset="0.88" stop-color="#FFFFFF"/><stop offset="1" stop-color="#FFFFFF" stop-opacity="0"/></linearGradient>`
      + `<mask id="${id}" maskUnits="userSpaceOnUse" x="${n(x0)}" y="${n(y - A * 2)}" width="${n(x1 - x0)}" height="${n(A * 4)}"><rect x="${n(x0)}" y="${n(y - A * 2)}" width="${n(x1 - x0)}" height="${n(A * 4)}" fill="url(#${id}-g)"/></mask></defs>`
      + `<g mask="url(#${id})"><g>${anda}${linha}</g></g>`;
  };

  // antenas do símbolo como barras de sinal; 'carregando' acende uma de cada vez, em laço
  const sinal = (x, yb, wb, hb, acesas, cor, apagada, parado) => {
    let s = '';
    for (let i = 0; i < 4; i++) {
      const h = hb * (i + 1) / 4, bx = x + i * wb * 1.55, ry = wb / 2;
      const d = `M${n(bx)} ${n(yb)}V${n(yb - h + ry)}A${n(wb / 2)} ${n(ry)} 0 0 1 ${n(bx + wb)} ${n(yb - h + ry)}V${n(yb)}Z`;
      if (acesas === 'carregando') {
        s += `<path d="${d}" fill="${apagada}"/>`;
        const vals = [0, 1, 2, 3, 4].map((k) => (k > i ? 1 : 0)).join(';');
        s += parado ? (i < 2 ? `<path d="${d}" fill="${cor}"/>` : '') : `<path d="${d}" fill="${cor}" opacity="0"><animate attributeName="opacity" values="${vals}" keyTimes="0;0.2;0.4;0.6;0.8" calcMode="discrete" dur="2s" repeatCount="indefinite"/></path>`;
      } else s += `<path d="${d}" fill="${i < acesas ? cor : apagada}"/>`;
    }
    return s;
  };

  // brilhos em volta do anel (voltou)
  const brilhos = (cx, cy, R, cor, parado) => [0, 1, 2, 3, 4, 5].map((i) => {
    const a = -Math.PI / 2 + i * Math.PI / 3 - 0.25, x = cx + Math.cos(a) * R * 1.32, y = cy + Math.sin(a) * R * 1.32, u = R * (i % 2 ? 0.07 : 0.1);
    const d = `M${n(x)} ${n(y - u)}Q${n(x)} ${n(y)} ${n(x + u)} ${n(y)}Q${n(x)} ${n(y)} ${n(x)} ${n(y + u)}Q${n(x)} ${n(y)} ${n(x - u)} ${n(y)}Q${n(x)} ${n(y)} ${n(x)} ${n(y - u)}Z`;
    const pisca = parado ? '' : `<animate attributeName="opacity" values="0;1;0" dur="1.6s" begin="${n(i * 0.27)}s" repeatCount="indefinite"/>`;
    return `<path d="${d}" fill="${cor}">${pisca}</path>`;
  }).join('');

  /**
   * Desenha um estado e devolve o SVG.
   * op = { largura, altura, cores (PAPEIS), movimentoReduzido }
   */
  function desenhar(estado, op = {}) {
    const E = ESTADOS[estado];
    if (!E) throw new Error(`status desconhecido: ${estado}`);
    const { largura: w = 1600, altura: h = 900, cores: c, movimentoReduzido: parado = false } = op;
    const b = Math.min(w, h), R = b * 0.28, esp = R * 0.2, cx = w / 2, cy = h / 2, ri = R - esp, cor = c[E.papel], id = `status-${++seq}`;
    let s = `<defs><radialGradient id="${id}-brilho" cx="${n(cx)}" cy="${n(cy)}" r="${n(R * 2.8)}" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="${cor}" stop-opacity=".24"/><stop offset="1" stop-color="${cor}" stop-opacity="0"/></radialGradient></defs>`;
    s += `<rect width="${w}" height="${h}" fill="${c.surface}"/><rect width="${w}" height="${h}" fill="url(#${id}-brilho)"/>`;
    for (let i = 1; i <= 7; i++) s += `<circle cx="${n(cx)}" cy="${n(cy)}" r="${n(R + i * b * 0.05)}" fill="none" stroke="${c['outline-variant']}" stroke-width="${n(b * 0.003)}" opacity="${n(0.9 - i * 0.11)}"/>`;
    s += pulso(E.pulso, 0, w, cy, R * 0.5, b * 0.24, cor, b * 0.008, parado);
    s += `<circle cx="${n(cx)}" cy="${n(cy)}" r="${n(R)}" fill="${c['surface-container']}"/>`;
    s += medidor(cx, cy, R, esp, cor, c['outline-variant'], E.medidor, E.tracejado, parado, E.brilhos);
    if (E.rack) [0, 2, 1].map((i) => [E.rack[i], i]).forEach(([m, i]) => { s += masc(m, cx + (i - 1) * ri * 0.64, cy + ri * (i === 1 ? 0.02 : 0.14), ri * (i === 1 ? 1.12 : 0.74), parado); });
    else s += masc(E.mascote, cx, cy + ri * 0.04, ri * 1.7, parado);
    s += sinal(cx + R * 0.8, cy + R * 1.04, R * 0.11, R * 0.44, E.sinal, cor, c['outline-variant'], parado);
    if (E.brilhos) s += brilhos(cx, cy, R, cor, parado);
    return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${w} ${h}" width="${w}" height="${h}" role="img" aria-label="Status: ${E.nome}">${s}</svg>`;
  }

  /**
   * Selo: só o medidor com o rosto dentro, para chips, listas de serviços e a barra do topo (mínimo 24dp).
   * op = { tamanho, cores, movimentoReduzido }
   */
  function selo(estado, op = {}) {
    const E = ESTADOS[estado];
    if (!E) throw new Error(`status desconhecido: ${estado}`);
    const { tamanho: t = 256, cores: c, movimentoReduzido: parado = false } = op;
    const R = t / 2, esp = R * 0.16, ri = R - esp;
    let s = `<circle cx="${n(R)}" cy="${n(R)}" r="${n(R)}" fill="${c['surface-container']}"/>`;
    s += medidor(R, R, R, esp, c[E.papel], c['outline-variant'], E.medidor, E.tracejado, parado, false);
    s += masc({ ...E.mascote, movimento: '' }, R, R + ri * 0.04, ri * 1.75, parado, 'rosto');
    return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${n(t)} ${n(t)}" width="${n(t)}" height="${n(t)}" role="img" aria-label="Status: ${E.nome}">${s}</svg>`;
  }

  return { desenhar, selo };
}

/** Cores dos status por tema e modo: os PAPEIS de tokens/cores.json. */
export function coresDoStatus(cores) {
  const saida = {};
  for (const [id, t] of Object.entries(cores.temas)) {
    const modo = (esq) => Object.fromEntries(PAPEIS.map((p) => [p, esq[p].$value.toUpperCase()]));
    saida[id] = { claro: modo(t.md.sys.color.light), escuro: modo(t.md.sys.color.dark) };
  }
  return saida;
}
