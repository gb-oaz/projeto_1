// =============================================================================
// boaz · arquivos da marca deboaz (chamado por gerar.mjs)
//
// Devolve { <caminho relativo a 0_Assets/design>: conteúdo } com:
//   identidade/logos/simbolo[-curto][-negativo].svg       símbolo oficial (preto; negativo = branco)
//   identidade/logos/logo-principal[-curto][-negativo].svg símbolo + nome deboaz
//   identidade/icones/app-icone[-curto].svg               ícone do app (1024, fundo preto, símbolo branco)
//   identidade/icones/app-icone[-curto]-frente.svg        frente do ícone adaptativo (Android), transparente
//   identidade/icones/app-icone-fundo.svg                 fundo do ícone adaptativo
//   identidade/icones/favicon[-curto].svg                 favicon
//   identidade/animacoes/abertura[-curto][-negativo].svg  abertura: o símbolo se desenha e o nome entra (toca uma vez)
//   identidade/animacoes/carregamento[-curto][-negativo].svg carregamento em laço: arco no anel e antenas
//   identidade/animacoes/fechamento[-curto][-negativo].svg o inverso da abertura (toca uma vez)
//   identidade/animacoes/ciclo[-curto][-negativo].svg     abertura + fechamento em laço
//   plataformas/web/marca.mjs                             o núcleo com o nome já medido
//   plataformas/web/marca-temas.json                      cores da marca em cada tema e modo
//   plataformas/maui/Marca.xaml                           as peças do símbolo e do nome como PathGeometry (app)
// Sem sufixo = hastes longas (landing page e hub); -curto = hastes curtas (quest).
// O nome é medido na fonte da marca (identidade/fontes/fredoka-medium.ttf).
// =============================================================================
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import opentype from 'opentype.js';
import { criarMarca, coresDaMarca, geometria } from './marca.mjs';

const AQUI = dirname(fileURLToPath(import.meta.url));
const RAIZ = join(AQUI, '..');

/** Mede "de" e "boaz" em Fredoka Medium, no quadro do símbolo (baseline 783, corpo 520). */
export function medirNome() {
  const buf = readFileSync(join(RAIZ, 'identidade', 'fontes', 'fredoka-medium.ttf'));
  const fonte = opentype.parse(buf.buffer.slice(buf.byteOffset, buf.byteOffset + buf.byteLength));
  const corpo = 520, base = 783, x0 = 700, op = { kerning: true };
  const de = fonte.getPath('de', x0, base, corpo, op);
  const xBoaz = x0 + fonte.getAdvanceWidth('de', corpo, op) +
    fonte.getKerningValue(fonte.charToGlyph('e'), fonte.charToGlyph('b')) * corpo / fonte.unitsPerEm;
  const boaz = fonte.getPath('boaz', xBoaz, base, corpo, op);
  const fim = boaz.getBoundingBox().x2;
  return { de: de.toPathData(1), boaz: boaz.toPathData(1), largura: Math.ceil(fim) };
}

export function arquivosMarca(cores, marca) {
  const nome = medirNome();
  const m = criarMarca(nome);
  const s = {};
  const L = 'identidade/logos/', I = 'identidade/icones/';
  for (const [v, suf] of [['longo', ''], ['curto', '-curto']]) {
    s[`${L}simbolo${suf}.svg`] = m.simbolo(v, '#000000') + '\n';
    s[`${L}simbolo${suf}-negativo.svg`] = m.simbolo(v, '#FFFFFF') + '\n';
    s[`${L}logo-principal${suf}.svg`] = m.logo(v, { simbolo: '#000000', de: '#000000', boaz: '#000000' }) + '\n';
    s[`${L}logo-principal${suf}-negativo.svg`] = m.logo(v, { simbolo: '#FFFFFF', de: '#FFFFFF', boaz: '#FFFFFF' }) + '\n';
    s[`${I}app-icone${suf}.svg`] = m.icone(v) + '\n';
    s[`${I}app-icone${suf}-frente.svg`] = m.icone(v, { fundo: null }) + '\n';
    s[`${I}favicon${suf}.svg`] = m.favicon(v) + '\n';
    for (const [cor, neg] of [['#000000', ''], ['#FFFFFF', '-negativo']]) {
      s[`identidade/animacoes/abertura${suf}${neg}.svg`] = m.abertura(v, { simbolo: cor, de: cor, boaz: cor }) + '\n';
      s[`identidade/animacoes/carregamento${suf}${neg}.svg`] = m.carregamento(v, cor) + '\n';
      s[`identidade/animacoes/fechamento${suf}${neg}.svg`] = m.fechamento(v, { simbolo: cor, de: cor, boaz: cor }) + '\n';
      s[`identidade/animacoes/ciclo${suf}${neg}.svg`] = m.ciclo(v, { simbolo: cor, de: cor, boaz: cor }) + '\n';
    }
  }
  s[`${I}app-icone-fundo.svg`] = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1024 1024" width="1024" height="1024"><rect width="1024" height="1024" fill="#000000"/></svg>\n';
  s['plataformas/web/marca.mjs'] = readFileSync(join(AQUI, 'marca.mjs'), 'utf8') +
    `\n// ── gerado: o nome deboaz em contorno (Fredoka Medium) ──\nexport const NOME = ${JSON.stringify(nome)};\nexport const marca = criarMarca(NOME);\n`;
  s['plataformas/maui/Marca.xaml'] = xamlMarca(nome);
  s['plataformas/web/marca-temas.json'] = JSON.stringify(coresDaMarca(cores, marca), null, 2) + '\n';
  return s;
}

/** As peças do logo como PathGeometry, para o app desenhar em vetor com as cores do tema. */
function xamlMarca(nome) {
  const g = { longo: geometria('longo'), curto: geometria('curto') };
  const geo = (k, d) => `  <PathGeometry x:Key="${k}" FillRule="EvenOdd" Figures="${d}" />`;
  return `<?xml version="1.0" encoding="UTF-8" ?>
<!--
  boaz · marca deboaz para .NET MAUI · gerado por 0_Assets/design/scripts/marca-arquivos.mjs (não edite)

  As peças do símbolo e do nome no quadro do símbolo: largura 540 e altura 793
  (hastes longas: y de 0 a 793) ou 613 (hastes curtas: y de 180 a 793). O nome
  vai de x = 700 a x = ${nome.largura}.
  Uso: ponha as peças num Grid de ${'540'} × 793 com Path Aspect="None" e escale o
  Grid (Scale) para o tamanho desejado; cada Path recebe a cor da peça:
    anel → Tertiary, antena do b → Primary, antena do d → Secondary,
    "de" → Secondary, "boaz" → Primary (com AppThemeBinding Light/Dark).
  No tema monocromático, todas as peças em OnSurface. Regras: 1_Requisitos/90_identidade-visual.html.
-->
<ResourceDictionary xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
${geo('MarcaAnel', g.longo.anel)}
${geo('MarcaAntenaB', g.longo.b)}
${geo('MarcaAntenaD', g.longo.d)}
${geo('MarcaAntenaBCurta', g.curto.b)}
${geo('MarcaAntenaDCurta', g.curto.d)}
${geo('MarcaNomeDe', nome.de)}
${geo('MarcaNomeBoaz', nome.boaz)}
</ResourceDictionary>
`;
}
