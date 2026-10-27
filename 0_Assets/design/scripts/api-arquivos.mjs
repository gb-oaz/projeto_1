// =============================================================================
// boaz · identidade da documentação da API (chamado por gerar.mjs)
//
// A documentação do backend (Swagger UI em /doc/api, 05_cd-implantacao RNF-73)
// usa a identidade do deboaz: devolve { <caminho relativo a 0_Assets/design>:
// conteúdo } com
//   plataformas/api/banner.svg          capa: fundo Halo com o logo inteiro (logo + deboaz),
//   plataformas/api/banner-escuro.svg   tema padrão no claro e no escuro, com o logo em ciclo
//                                       (abre, fica, fecha e recomeça; o swagger.css para o
//                                       laço com prefers-reduced-motion trocando pelo parado)
//   plataformas/api/banner-parado.svg   os mesmos, sem movimento
//   plataformas/api/banner-parado-escuro.svg
//   plataformas/api/logo.svg            logo principal, para a barra do topo,
//   plataformas/api/logo-negativo.svg   e o negativo, para o modo escuro
//   plataformas/api/favicon.svg         favicon da marca
//   plataformas/api/cores.css           papéis de cor do tema padrão em --md-sys-color-*:
//                                       claro em :root, escuro em .dark-mode (a classe
//                                       que o Swagger UI liga no modo escuro)
// O sincronizar.mjs copia a pasta para 5_DEV/backend/ms_mono/wwwroot/doc/api/design/.
// =============================================================================
import { fundosNode } from './fundos-arquivos.mjs';

export function arquivosApi(cores, arqMarca, arqFundos) {
  const padrao = cores.padrao;
  const temasFundos = JSON.parse(arqFundos['plataformas/web/fundos-temas.json']);
  const f = fundosNode();
  const banner = (modo, movimentoReduzido = false) => f.desenhar('halo', {
    largura: 1600, altura: 400, cores: temasFundos[padrao][modo],
    pecas: 'logo', nome: true, raio: 160, logo: 'ciclo', semente: 1, movimentoReduzido,
  }) + '\n';
  const esquema = cores.temas[padrao].md.sys.color;
  const bloco = (seletor, papeis) => `${seletor} {\n` +
    Object.entries(papeis).filter(([k]) => !k.startsWith('$')).map(([k, v]) => `  --md-sys-color-${k}: ${v.$value};`).join('\n') + '\n}\n';
  const css = '/* boaz · cores da documentação da API: tema padrão, claro e escuro\n' +
    ' * Gerado por 0_Assets/design/scripts/gerar.mjs — não edite à mão. */\n' +
    bloco(':root', esquema.light) + '\n' + bloco('.dark-mode', esquema.dark);
  return {
    'plataformas/api/banner.svg': banner('claro'),
    'plataformas/api/banner-escuro.svg': banner('escuro'),
    'plataformas/api/banner-parado.svg': banner('claro', true),
    'plataformas/api/banner-parado-escuro.svg': banner('escuro', true),
    'plataformas/api/logo.svg': arqMarca['identidade/logos/logo-principal.svg'],
    'plataformas/api/logo-negativo.svg': arqMarca['identidade/logos/logo-principal-negativo.svg'],
    'plataformas/api/favicon.svg': arqMarca['identidade/icones/favicon.svg'],
    'plataformas/api/cores.css': css,
  };
}
