import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useThemeStore = defineStore('theme', () => {
  const isDark = ref<boolean>(true)

  function initTheme(): void {
    const stored = localStorage.getItem('cakra_theme')
    if (stored !== null) {
      isDark.value = stored !== 'light'
    } else {
      isDark.value = true // Executive Infographic default
    }
    applyTheme()
  }

  function toggleTheme(): void {
    isDark.value = !isDark.value
    localStorage.setItem('cakra_theme', isDark.value ? 'dark' : 'light')
    applyTheme()
  }

  function applyTheme(): void {
    const root = document.documentElement
    if (isDark.value) {
      root.classList.add('dark')
      root.setAttribute('data-bs-theme', 'dark')
    } else {
      root.classList.remove('dark')
      root.setAttribute('data-bs-theme', 'light')
    }
  }

  return { isDark, initTheme, toggleTheme }
})
