import { useCallback, useEffect, useRef, useState } from 'react'

export function useDashboardResource<T>(enabled: boolean, load: (signal: AbortSignal) => Promise<T>) {
  const [state, setState] = useState<{ data: T | null, loading: boolean, error: string, updatedAt: number | null }>({
    data: null, loading: enabled, error: '', updatedAt: null,
  })
  const active = useRef<AbortController | null>(null)
  const reload = useCallback(() => {
    if (!enabled) return
    active.current?.abort()
    const controller = new AbortController()
    active.current = controller
    const signal = AbortSignal.any([controller.signal, AbortSignal.timeout(15000)])
    setState((previous) => ({ ...previous, loading: true, error: '' }))
    void load(signal).then((data) => {
      if (signal.aborted) throw signal.reason
      if (!controller.signal.aborted) setState({ data, loading: false, error: '', updatedAt: Date.now() })
    }).catch((error: unknown) => {
      if (!controller.signal.aborted) setState((previous) => ({ ...previous, loading: false,
        error: signal.aborted ? '加载超时，请重试' : error instanceof Error ? error.message : '请求失败' }))
    })
  }, [enabled, load])

  useEffect(() => {
    let mounted = true
    queueMicrotask(() => { if (mounted) reload() })
    return () => { mounted = false; active.current?.abort() }
  }, [reload])
  return { ...state, reload }
}
