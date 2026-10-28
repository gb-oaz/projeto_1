// =============================================================================
// boaz · splash screens: a tela de abertura do app e das frentes web, em duas camadas
//
//   nativa   o que o sistema mostra antes do app carregar: só o símbolo oficial,
//            parado, sobre uma cor lisa (Android 12+, iOS, PWA); sem animação nem mascote
//   animada  a primeira tela do app: o Halo com o logo inteiro se abrindo (RN-51) e,
//            conforme a frente e o momento, um mascote que entra quando o nome termina
//
// Frentes: hub (hastes longas, só o logo) e quest (hastes curtas, logo + mascote).
// Momentos (trocam o mascote): abertura de sempre, primeiro acesso, de volta depois
// de um tempo, à noite e "voltamos" (primeira abertura depois de um reparo, ligado
// ao status Voltou! de scripts/status.mjs).
//
// Este módulo não usa nada do Node nem importa nada: recebe o núcleo da marca, o
// dos fundos e o dos mascotes em criarSplash. O gerador (scripts/splash-arquivos.mjs)
// grava os oficiais em identidade/imagens/splash/.
//
//   const sp = criarSplash({ marca, fundos, mascotes });
//   sp.animada('quest', { largura: 900, altura: 1600, cores, momento: 'primeiro-acesso', movimentoReduzido });
//   sp.nativa('hub', { largura: 900, altura: 1600, fundo: '#000000', cor: '#FFFFFF' });
//   sp.icone('hub', { cor: '#FFFFFF' });   // ícone da splash do Android 12+ (área segura de 2/3)
// Regras de uso: 1_Requisitos/90_identidade-visual.html e o catálogo design-system.html#splash.
// =============================================================================

/** Frentes com splash: variação do símbolo e mascote da abertura de sempre (null = só o logo). */
export const FRENTES = {
  hub: { nome: 'Hub', variante: 'longo', mascote: null },
  quest: { nome: 'Quest', variante: 'curto', mascote: { expressao: 'feliz', forma: 'bolha', movimento: 'pular' } },
};
/** Momentos da abertura animada; null = o mascote da frente. */
export const MOMENTOS = {
  padrao: { nome: 'Abertura', uso: 'toda abertura do app ou da frente', mascote: null },
  'primeiro-acesso': { nome: 'Primeiro acesso', uso: 'a primeira vez que a pessoa abre a frente', mascote: { expressao: 'curioso', forma: 'bolha', movimento: 'flutuar' } },
  retorno: { nome: 'De volta', uso: 'a pessoa volta depois de um tempo sem entrar', mascote: { expressao: 'tchau', forma: 'bolha' } },
  noite: { nome: 'À noite', uso: 'abertura à noite', mascote: { expressao: 'sonolento', forma: 'meialua' } },
  voltamos: { nome: 'Voltamos', uso: 'primeira abertura depois de um reparo (status Voltou!)', mascote: { expressao: 'aliviado', forma: 'cubo', movimento: 'comemorar' } },
};
/** Formatos de referência: celular em pé, computador e tablet/quadrado. */
export const FORMATOS = { vertical: [900, 1600], horizontal: [1600, 900], quadrado: [1000, 1000] };

const n = (v) => Math.round(v * 100) / 100;
const FZ = 'fill="freeze" calcMode="spline" keyTimes="0;1" keySplines="0.3 0 0.2 1"';
/** O mascote entra quando o nome termina de entrar na abertura do logo (≈ 1,85 s). */
const ENTRA = 1.7;

export function criarSplash({ marca, fundos, mascotes }) {
  const { estatico, animado, embutir } = mascotes;
  let seq = 0;
  // svg embutido na caixa (x, y, w, h), mantendo a proporção
  const encaixa = (svg, x, y, w, h) => svg.replace(/^<svg[^>]*viewBox="([^"]+)"[^>]*>/, `<svg x="${n(x)}" y="${n(y)}" width="${n(w)}" height="${n(h)}" viewBox="$1">`);

  /**
   * Splash animada. op = { largura, altura, cores (de fundos-temas: PAPEIS + mascote), momento,
   *   carregando (símbolo de carregamento embaixo, para quando a abertura demora), ciclo ([cores, …] dos temas, como no Halo),
   *   movimentoReduzido (tudo parado no quadro final) }
   */
  function animada(frente, op = {}) {
    const F = FRENTES[frente];
    if (!F) throw new Error(`frente desconhecida: ${frente}`);
    const { largura: w = 900, altura: h = 1600, cores: c, momento = 'padrao', carregando = false, ciclo, movimentoReduzido: parado = false } = op;
    const m = (MOMENTOS[momento] || MOMENTOS.padrao).mascote || F.mascote;
    const b = Math.min(w, h), alt = b * 0.17, cy = h * (m ? 0.4 : 0.47);
    // tema monocromático (sem cor de mascote): brilhos em tons de superfície, para não sujar o fundo de cinza
    const halo = c.mascote ? c : { ...c, primary: c['surface-container-high'], secondary: c['surface-container-high'], tertiary: c['surface-container'] };
    let s = fundos.desenhar('halo', { largura: w, altura: h, cores: halo, pecas: 'logo', nome: true, logo: parado ? '' : 'abertura', variante: F.variante, origem: { x: 0.5, y: cy / h }, raio: alt, ciclo, movimentoReduzido: parado })
      .replace(/<\/svg>$/, '');
    if (m) {
      const t = b * 0.46, my = cy + alt * 0.5 + t * 0.5;
      const o = { forma: m.forma, cor: c.mascote || undefined };
      const svg = parado ? estatico(m.expressao, o) : animado(m.expressao, { ...o, movimento: m.movimento || undefined });
      const corpo = embutir(svg, `splash-m${++seq}-`, w / 2 - t / 2, my - t / 2, t, t);
      s += parado ? corpo : `<g opacity="0" transform="translate(0 ${n(t * 0.25)})"><animate attributeName="opacity" from="0" to="1" begin="${ENTRA}s" dur="0.35s" fill="freeze"/>`
        + `<animateTransform attributeName="transform" type="translate" from="0 ${n(t * 0.25)}" to="0 0" begin="${ENTRA}s" dur="0.5s" ${FZ}/>${corpo}</g>`;
    }
    if (carregando) {
      const ac = b * 0.07, sv = parado ? marca.simbolo(F.variante, c['on-surface-variant']) : marca.carregamento(F.variante, c['on-surface-variant']);
      const [, , vw, vh] = sv.match(/viewBox="([^"]+)"/)[1].split(' ').map(Number), al = ac * vw / vh;
      s += encaixa(sv, w / 2 - al / 2, h - b * 0.1 - ac, al, ac);
    }
    return s.replace(/aria-label="[^"]*"/, 'aria-label="deboaz"') + '</svg>';
  }

  /** Splash nativa: o símbolo oficial parado no centro de uma cor lisa. op = { largura, altura, fundo, cor } */
  function nativa(frente, op = {}) {
    const F = FRENTES[frente];
    const { largura: w = 900, altura: h = 1600, fundo = '#000000', cor = '#FFFFFF' } = op;
    const ac = Math.min(w, h) * 0.22, sv = marca.simbolo(F.variante, cor);
    const [, , vw, vh] = sv.match(/viewBox="([^"]+)"/)[1].split(' ').map(Number), al = ac * vw / vh;
    return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${w} ${h}" width="${w}" height="${h}" role="img" aria-label="deboaz"><rect width="${w}" height="${h}" fill="${fundo}"/>${encaixa(sv, w / 2 - al / 2, h / 2 - ac / 2, al, ac)}</svg>`;
  }

  /**
   * Ícone da splash nativa do Android 12+ (e do MauiSplashScreen): quadrado de 1024 transparente com o
   * símbolo dentro do círculo central de 2/3 (o sistema recorta o resto). op = { cor }
   */
  const icone = (frente, { cor = '#FFFFFF' } = {}) => marca.icone(FRENTES[frente].variante, { fundo: null, cor, area: 440 });

  return { animada, nativa, icone };
}
