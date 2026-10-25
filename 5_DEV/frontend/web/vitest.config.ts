// Testes do projeto web (14_estrutura-projetos RNF-193): unitários e de
// integração entre poucos componentes, sem navegador (jsdom) e sem backend.
// Os de ponta a ponta ficam em 5_DEV/qa/frontend/web/funcional/.
import vue from '@vitejs/plugin-vue';
import { defineConfig } from 'vitest/config';
import { alias } from './vite.base.ts';

export default defineConfig({
  plugins: [vue()],
  resolve: { alias },
  test: {
    environment: 'jsdom',
    include: ['tests/**/*.test.ts'],
    coverage: {
      provider: 'v8',
      include: ['core/**/*.{ts,vue}', 'apps/**/*.{ts,vue}'],
      // arquivos gerados do design system e configuração do Vite
      exclude: ['core/commons/design/**', 'apps/*/vite.config.ts'],
    },
  },
});
