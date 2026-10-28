<!-- Tela de "em construção", comum aos três apps enquanto as telas reais não
     chegam: fundo Estampa (90_identidade-visual RN-56), logo da frente e o
     mascote Consertando, com o texto num painel de superfície (RNF-119).
     Com movimento reduzido, o mascote fica parado (RNF-112). -->
<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { FRENTES, marca } from '@core/commons/design/marca.mjs';
import { animado, estatico } from '@core/commons/design/mascotes.mjs';
import { PAPEIS, fundos } from '@core/commons/design/fundos.mjs';

const props = defineProps<{ frente: keyof typeof FRENTES }>();

// cores por papel: o SVG embutido acompanha o tema e o modo pelo CSS
const cor = (papel: string) => `var(--md-sys-color-${papel})`;
const movimentoReduzido = () => window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;

const logo = computed(() =>
  marca.logo(FRENTES[props.frente], {
    simbolo: cor('on-surface'),
    de: cor('on-surface'),
    boaz: cor('on-surface'),
  }),
);
const mascote = computed(() => {
  const svg = (movimentoReduzido() ? estatico : animado)('consertando');
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
});

// Estampa no tamanho da janela, com rabiscos de ~48dp e sorteio novo a cada abertura
const estampa = ref('');
const semente = Math.floor(Math.random() * 1e9);
function desenharEstampa() {
  estampa.value = fundos.desenhar('estampa', {
    largura: window.innerWidth,
    altura: window.innerHeight,
    cores: { ...Object.fromEntries(PAPEIS.map((p) => [p, cor(p)])), mascote: '' },
    celula: 48,
    semente,
    movimentoReduzido: true,
  });
}
onMounted(() => {
  desenharEstampa();
  window.addEventListener('resize', desenharEstampa);
});
onBeforeUnmount(() => window.removeEventListener('resize', desenharEstampa));
</script>

<template>
  <main class="em-construcao">
    <div class="estampa" aria-hidden="true" v-html="estampa"></div>
    <section class="painel" aria-labelledby="em-construcao-titulo">
      <span class="logo" role="img" aria-label="deboaz" v-html="logo"></span>
      <img class="mascote" :src="mascote" alt="" />
      <h1 id="em-construcao-titulo" class="md-typescale-headline-small">Estamos construindo o deboaz</h1>
      <p class="md-typescale-body-large">
        Esta página ainda está em obra. Em breve, novidades por aqui. Volte logo, de boa.
      </p>
    </section>
  </main>
</template>

<style scoped>
/* a tela ocupa a janela inteira, sem a margem padrão do navegador */
:global(body) {
  margin: 0;
}
.em-construcao {
  position: relative;
  display: grid;
  place-items: center;
  min-height: 100dvh;
  padding: var(--md-sys-layout-margin);
  overflow: hidden;
  background: var(--md-sys-color-surface);
  color: var(--md-sys-color-on-surface);
}
/* Estampa de fundo de tela: 30% mais leve que sozinha (RN-56) */
.estampa {
  position: absolute;
  inset: 0;
  opacity: 0.7;
}
.estampa :deep(svg) {
  display: block;
  width: 100%;
  height: 100%;
}
.painel {
  position: relative;
  display: grid;
  justify-items: center;
  gap: var(--md-sys-spacing-4);
  max-width: calc(var(--md-sys-layout-content-max-width) / 2);
  padding: var(--md-sys-spacing-8) var(--md-sys-spacing-6);
  border-radius: var(--md-sys-shape-corner-extra-large);
  background: var(--md-sys-color-surface-container-low);
  box-shadow: var(--md-sys-elevation-level1);
  text-align: center;
}
.painel h1,
.painel p {
  margin: 0;
}
.painel p {
  color: var(--md-sys-color-on-surface-variant);
}
.logo :deep(svg) {
  display: block;
  height: var(--md-sys-spacing-12);
  width: auto;
}
.mascote {
  width: var(--md-sys-spacing-24);
  height: var(--md-sys-spacing-24);
}
</style>
