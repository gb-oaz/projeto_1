// =============================================================================
// boaz · cópias sincronizadas do design system nas aplicações (requisito 06)
//
// O build de cada imagem só enxerga a pasta da própria aplicação, então os
// arquivos gerados são copiados para dentro dela e versionados no git:
//
//   5_DEV/frontend/web_<app>/src/design/   ← plataformas/web/*
//   5_DEV/frontend/web_<app>/src/comum/    ← 5_DEV/frontend/web_comum/** (componentes comuns da web)
//   5_DEV/frontend/mob_*/Resources/Styles/Design/ ← plataformas/maui/Tokens.xaml e paletas/*.xaml
//
// Chamado por gerar.mjs: `pnpm run gerar` escreve as cópias e `pnpm run
// conferir` falha se alguma estiver diferente da origem. Nunca edite as cópias.
// =============================================================================
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';

const pastas = (dir, filtro) => (existsSync(dir) ? readdirSync(dir).filter((n) => filtro(n) && statSync(join(dir, n)).isDirectory()) : []);
const arquivos = (dir) => (existsSync(dir)
  ? readdirSync(dir).flatMap((n) => (statSync(join(dir, n)).isDirectory() ? arquivos(join(dir, n)).map((f) => join(n, f)) : [n]))
  : []);

/**
 * Cópias que devem existir, como { <caminho relativo a 0_Assets/design>: conteúdo }.
 * `gerados` são as saídas desta execução (o conteúdo novo de plataformas/*).
 */
export function copiasSincronizadas(raizDesign, gerados) {
  const frontend = join(raizDesign, '..', '..', '5_DEV', 'frontend');
  const comum = join(frontend, 'web_comum');
  const rel = (p) => relative(raizDesign, p);
  const copias = {};

  const web = Object.entries(gerados).filter(([k]) => k.startsWith('plataformas/web/'));
  for (const app of pastas(frontend, (n) => n.startsWith('web_') && n !== 'web_comum')) {
    for (const [k, v] of web) copias[rel(join(frontend, app, 'src', 'design', k.slice('plataformas/web/'.length)))] = v;
    for (const f of arquivos(comum)) copias[rel(join(frontend, app, 'src', 'comum', f))] = readFileSync(join(comum, f), 'utf8');
  }

  const maui = Object.entries(gerados).filter(([k]) => k.startsWith('plataformas/maui/'));
  for (const app of pastas(frontend, (n) => n.startsWith('mob_'))) {
    for (const [k, v] of maui) copias[rel(join(frontend, app, 'Resources', 'Styles', 'Design', k.slice('plataformas/maui/'.length)))] = v;
  }
  return copias;
}
