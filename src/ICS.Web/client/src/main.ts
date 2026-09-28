import { createApp } from 'vue'
import { createPinia } from 'pinia'

// Bootstrap 5 and Bootstrap Icons Styles
import 'bootstrap/dist/css/bootstrap.min.css'
import 'bootstrap-icons/font/bootstrap-icons.css'

// Bootstrap 5 JavaScript Bundle (includes Popper)
import 'bootstrap/dist/js/bootstrap.bundle.min.js'

import App from './App.vue'
import router from './router'

const app = createApp(App)

app.use(createPinia())
app.use(router)

app.mount('#app')
