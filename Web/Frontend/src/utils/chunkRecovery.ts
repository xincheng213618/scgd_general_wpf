const chunkReloadKey = 'colorvision-web-chunk-reload-at'
const reloadCooldownMs = 60_000

/** Recover once when an open tab references chunks removed by a new deploy. */
export function installChunkRecovery() {
  window.addEventListener('vite:preloadError', (event) => {
    event.preventDefault()

    const now = Date.now()
    const previous = Number(window.sessionStorage.getItem(chunkReloadKey) || 0)
    if (Number.isFinite(previous) && now - previous < reloadCooldownMs) return

    window.sessionStorage.setItem(chunkReloadKey, String(now))
    window.location.reload()
  })
}
