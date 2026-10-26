import { mountGridDistortionDemo } from './grid-distortion-demo.js'

const cleanups = [...document.querySelectorAll('[data-grid-distortion-demo]')].map(mountGridDistortionDemo)
window.addEventListener('pagehide', () => cleanups.forEach(cleanup => cleanup?.()), { once: true })
