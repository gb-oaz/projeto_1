// Configuração do Vite comum aos apps (14_estrutura-projetos RNF-180): cada
// app em apps/<app>/ tem o próprio vite.config.ts, que chama configDoApp, e
// gera o pacote estático em dist/<app>/ (RNF-181).
import { fileURLToPath } from 'node:url';
import vue from '@vitejs/plugin-vue';
import { defineConfig, type UserConfig } from 'vite';

export const raiz = fileURLToPath(new URL('.', import.meta.url));

// o core é importado pelo alias @core, nunca por caminho relativo
export const alias = { '@core': fileURLToPath(new URL('./core', import.meta.url)) };

export function configDoApp(app: string): UserConfig {
  return defineConfig({
    root: fileURLToPath(new URL(`./apps/${app}`, import.meta.url)),
    plugins: [vue()],
    resolve: { alias },
    build: {
      outDir: fileURLToPath(new URL(`./dist/${app}`, import.meta.url)),
      emptyOutDir: true,
    },
  });
}
