import { createApp } from 'vue'
import { createPinia } from 'pinia'

import 'bootstrap/dist/css/bootstrap.min.css'
import 'bootstrap-icons/font/bootstrap-icons.css'
import 'bootstrap/dist/js/bootstrap.bundle.min.js'
import './assets/main.css'

import App from './App.vue'
import { router } from './router'
import { useAuthStore } from './stores/auth'
import { useThemeStore } from './stores/theme'

const app = createApp(App)

// Pinia is the shared client-side state store (Architecture §19.4).
const pinia = createPinia()
app.use(pinia)

// Initialize theme state and synchronize DOM classes/attributes
const themeStore = useThemeStore()
themeStore.initTheme()

// Vue Router 4 client-side navigation (Architecture §19.4).
app.use(router)

// Authentication interceptor hook: clear local session state whenever the
// shared Axios client observes an HTTP 401 (Architecture §19.5).
window.addEventListener('cakra:unauthorized', () => {
  useAuthStore().clear()
})

app.mount('#app')
