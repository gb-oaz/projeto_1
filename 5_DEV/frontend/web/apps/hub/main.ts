// Ponto de composição do app hub (RN-149): liga as portas do core aos
// adaptadores de infra/ e monta o app. Ainda não há features.
import '@core/commons/design/tokens.css';
import '@core/commons/design/tipografia.css';
import { createApp } from 'vue';
import App from './App.vue';

createApp(App).mount('#app');
