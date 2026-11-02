import { ref, watch } from 'vue'

const prefersDark =
  typeof window !== 'undefined' &&
  typeof window.matchMedia === 'function' &&
  window.matchMedia('(prefers-color-scheme: dark)').matches

const dark = ref(prefersDark)

let lastClickX = 0
let lastClickY = 0
let isInitialized = false

if (typeof window !== 'undefined') {
  window.addEventListener(
    'pointerdown',
    (event: PointerEvent) => {
      lastClickX = event.clientX
      lastClickY = event.clientY
    },
    { passive: true },
  )
}

function applyThemeAttribute(isDark: boolean): void {
  if (typeof document === 'undefined') return
  document.documentElement.dataset.theme = isDark ? 'dark' : 'light'
}

watch(
  dark,
  (value) => {
    if (typeof document === 'undefined' || typeof window === 'undefined') return

    const prefersReducedMotion =
      typeof window.matchMedia === 'function' &&
      window.matchMedia('(prefers-reduced-motion: reduce)').matches

    const docWithTransition = document as Document & {
      startViewTransition?: (callback: () => void) => { ready: Promise<void> }
    }

    if (!isInitialized || prefersReducedMotion || !docWithTransition.startViewTransition) {
      applyThemeAttribute(value)
      isInitialized = true
      return
    }

    const x = lastClickX || 100
    const y = lastClickY || window.innerHeight - 80
    const endRadius = Math.hypot(
      Math.max(x, window.innerWidth - x),
      Math.max(y, window.innerHeight - y),
    )

    const transition = docWithTransition.startViewTransition(() => {
      applyThemeAttribute(value)
    })

    transition.ready
      .then(() => {
        document.documentElement.animate(
          {
            clipPath: [
              `circle(0px at ${x}px ${y}px)`,
              `circle(${endRadius}px at ${x}px ${y}px)`,
            ],
          },
          {
            duration: 540,
            easing: 'cubic-bezier(0.22, 1, 0.36, 1)',
            pseudoElement: '::view-transition-new(root)',
          },
        )
      })
      .catch(() => {
        // Si la transición es interrumpida, el atributo data-theme ya quedó aplicado
      })
  },
  { immediate: true },
)

export function useTheme() {
  return { dark }
}
