<!-- Raiz do app landing_page: as páginas entram por infra/in/ quando houver telas
     (toda tela começa por uma vista aprovada, 06_design-system-telas). Até
     lá, o app mostra só a tela de em construção. -->
<script setup lang="ts">
import EmConstrucao from '@core/commons/components/EmConstrucao.vue';
</script>

<template>
  <div data-app="landing_page">
    <EmConstrucao frente="landing" />
  </div>
</template>
