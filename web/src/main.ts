import { createApp } from 'vue';
import PrimeVue from 'primevue/config';
import Aura from '@primeuix/themes/aura';
import 'primeicons/primeicons.css';
import App from './App.vue';
import router from './router';
import './style.css';

createApp(App).use(PrimeVue, { theme: { preset: Aura } }).use(router).mount('#app');
