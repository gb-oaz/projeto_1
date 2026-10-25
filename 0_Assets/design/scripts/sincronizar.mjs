// =============================================================================
// boaz · cópias sincronizadas do design system nas frentes (requisitos 06 e 14)
//
// Cada frente é um projeto só (14_estrutura-projetos), então os arquivos
// gerados ganham uma cópia por frente, em core/commons/, versionada no git:
//
//   5_DEV/frontend/web/core/commons/design/                    ← plataformas/web/*
//   5_DEV/frontend/mobile/core/commons/Resources/Styles/Design/ ← plataformas/maui/Tokens.xaml e paletas/*.xaml
//
// Os componentes comuns da web ficam direto em web/core/commons/components/ e
// os apps os importam pelo alias @core, sem cópia.
//
// Chamado por gerar.mjs: `pnpm run gerar` escreve as cópias e `pnpm run
// conferir` falha se alguma estiver diferente da origem. Nunca edite as cópias.
// =============================================================================
import { existsSync } from 'node:fs';
import { join, relative } from 'node:path';

/**
 * Cópias que devem existir, como { <caminho relativo a 0_Assets/design>: conteúdo }.
 * `gerados` são as saídas desta execução (o conteúdo novo de plataformas/*).
 */
export function copiasSincronizadas(raizDesign, gerados) {
  const frontend = join(raizDesign, '..', '..', '5_DEV', 'frontend');
  const rel = (p) => relative(raizDesign, p);
  const copias = {};
  // frente que ainda não existe não recebe cópia
  const destinos = [
    ['plataformas/web/', join(frontend, 'web'), ['core', 'commons', 'design']],
    ['plataformas/maui/', join(frontend, 'mobile'), ['core', 'commons', 'Resources', 'Styles', 'Design']],
  ];
  for (const [origem, frente, pasta] of destinos) {
    if (!existsSync(frente)) continue;
    for (const [k, v] of Object.entries(gerados)) {
      if (k.startsWith(origem)) copias[rel(join(frente, ...pasta, k.slice(origem.length)))] = v;
    }
  }
  return copias;
}
