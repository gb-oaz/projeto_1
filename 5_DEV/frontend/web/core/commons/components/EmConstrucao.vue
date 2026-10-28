<!-- Tela de "em construção", comum aos três apps enquanto as telas reais não
     chegam: fundo Estampa (90_identidade-visual RN-56), logo da frente e a
     ilustração de status Manutenção programada (RN-157 a RN-159), com o texto
     num painel de superfície (RNF-119, RNF-199). Com movimento reduzido, a
     ilustração fica parada. -->
<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { FRENTES, marca } from '@core/commons/design/marca.mjs';
import * as mascotes from '@core/commons/design/mascotes.mjs';
import { PAPEIS, fundos } from '@core/commons/design/fundos.mjs';
import { ESTADOS, PAPEIS as PAPEIS_STATUS, criarStatus } from '@core/commons/design/status.mjs';

const props = defineProps<{ frente: keyof typeof FRENTES }>();

// cores por papel: o SVG embutido acompanha o tema e o modo pelo CSS
const cor = (papel: string) => `var(--md-sys-color-${papel})`;
const cores = (papeis: string[]) => Object.fromEntries(papeis.map((p) => [p, cor(p)]));
const movimentoReduzido = () => window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;
// janela compact (< 600dp): ilustração quadrada; nas maiores, horizontal
const compact = () => window.matchMedia?.('(max-width: 599px)').matches ?? false;

const logo = computed(() =>
  marca.logo(FRENTES[props.frente], {
    simbolo: cor('on-surface'),
    de: cor('on-surface'),
    boaz: cor('on-surface'),
  }),
);

const status = criarStatus({ mascotes });
const manutencao = ESTADOS.manutencao;

const estampa = ref('');
const ilustracao = ref('');
// Estampa no tamanho da janela, com rabiscos de ~48dp e sorteio novo a cada abertura
const semente = Math.floor(Math.random() * 1e9);
function desenhar() {
  estampa.value = fundos.desenhar('estampa', {
    largura: window.innerWidth,
    altura: window.innerHeight,
    cores: { ...cores(PAPEIS), mascote: '' },
    celula: 48,
    semente,
    movimentoReduzido: true,
  });
  const [largura, altura] = compact() ? [1000, 1000] : [1600, 900];
  ilustracao.value = status.desenhar('manutencao', {
    largura,
    altura,
    cores: cores(PAPEIS_STATUS),
    movimentoReduzido: movimentoReduzido(),
  });
}
onMounted(() => {
  desenhar();
  window.addEventListener('resize', desenhar);
});
onBeforeUnmount(() => window.removeEventListener('resize', desenhar));
</script>

<template>
  <main class="em-construcao">
    <div class="estampa" aria-hidden="true" v-html="estampa"></div>
    <section class="painel" aria-labelledby="em-construcao-titulo">
      <span class="logo" role="img" aria-label="deboaz" v-html="logo"></span>
      <div class="ilustracao" aria-hidden="true" v-html="ilustracao"></div>
      <h1 id="em-construcao-titulo" class="md-typescale-headline-small">{{ manutencao.titulo }}</h1>
      <p class="md-typescale-body-large">
        Estamos construindo o deboaz e cuidando de cada detalhe. Em breve, novidades por aqui.
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
  box-sizing: border-box;
  display: grid;
  justify-items: center;
  gap: var(--md-sys-spacing-4);
  width: 100%;
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
.ilustracao {
  width: 100%;
  overflow: hidden;
  border-radius: var(--md-sys-shape-corner-large);
}
.ilustracao :deep(svg) {
  display: block;
  width: 100%;
  height: auto;
}
</style>
