<!-- Raiz do app quest: as páginas entram por infra/in/ quando houver telas
     (toda tela começa por uma vista aprovada, 06_design-system-telas). Até
     lá, o app mostra só a tela de em construção. -->
<script setup lang="ts">
import EmConstrucao from '@core/commons/components/EmConstrucao.vue';
</script>

<template>
  <div data-app="quest">
    <EmConstrucao frente="quest" />
  </div>
</template>
