// =============================================================================
// boaz · splash screens oficiais (chamado por gerar.mjs)
//
// Devolve { <caminho relativo a 0_Assets/design>: conteúdo } com, no tema oficial
// (Branco e Preto), em claro e escuro, para as frentes hub e quest:
//   identidade/imagens/splash/<frente>-<formato>-<claro|escuro>.svg            animada: abertura do logo (+ mascote no quest)
//   identidade/imagens/splash/parados/<frente>-<formato>-<claro|escuro>.svg    a mesma, parada no quadro final (movimento reduzido)
//   identidade/imagens/splash/momentos/<momento>-<frente>-<claro|escuro>.svg   momentos, no celular em pé (vertical)
//   identidade/imagens/splash/nativa/<frente>-<formato>-<claro|escuro>.svg     nativa: símbolo parado sobre cor lisa
//   identidade/imagens/splash/nativa/icone-<frente>-<claro|escuro>.svg         ícone da splash do Android 12+ / MauiSplashScreen
// Em tema de cor, desenhe na hora com o núcleo (scripts/splash.mjs) e as cores dos fundos.
// =============================================================================
import { criarMarca } from './marca.mjs';
import { medirNome } from './marca-arquivos.mjs';
import * as mascotes from './mascotes.mjs';
import { temasDosMascotes } from './mascotes-arquivos.mjs';
import { fundosNode } from './fundos-arquivos.mjs';
import { coresDosFundos } from './fundos.mjs';
import { criarSplash, FRENTES, MOMENTOS, FORMATOS } from './splash.mjs';

/** Cores da splash nativa oficial: símbolo preto no claro e branco no escuro (RN-42). */
const NATIVA = { claro: { fundo: '#FFFFFF', cor: '#000000' }, escuro: { fundo: '#000000', cor: '#FFFFFF' } };

export function arquivosSplash(cores, marca) {
  const sp = criarSplash({ marca: criarMarca(medirNome()), fundos: fundosNode(), mascotes });
  const oficial = coresDosFundos(cores, temasDosMascotes(cores, marca))['branco-preto'];
  const s = {}, P = 'identidade/imagens/splash/';
  for (const fr of Object.keys(FRENTES)) for (const modo of ['claro', 'escuro']) {
    const c = oficial[modo];
    for (const [fmt, [largura, altura]] of Object.entries(FORMATOS)) {
      s[`${P}${fr}-${fmt}-${modo}.svg`] = sp.animada(fr, { largura, altura, cores: c }) + '\n';
      s[`${P}parados/${fr}-${fmt}-${modo}.svg`] = sp.animada(fr, { largura, altura, cores: c, movimentoReduzido: true }) + '\n';
      s[`${P}nativa/${fr}-${fmt}-${modo}.svg`] = sp.nativa(fr, { largura, altura, ...NATIVA[modo] }) + '\n';
    }
    for (const mo of Object.keys(MOMENTOS).filter((k) => k !== 'padrao')) s[`${P}momentos/${mo}-${fr}-${modo}.svg`] = sp.animada(fr, { largura: 900, altura: 1600, cores: c, momento: mo }) + '\n';
    s[`${P}nativa/icone-${fr}-${modo}.svg`] = sp.icone(fr, { cor: NATIVA[modo].cor }) + '\n';
  }
  return s;
}
