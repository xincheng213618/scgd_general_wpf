import { ArrowDownOutlined, ArrowUpOutlined, DashboardOutlined, MinusOutlined, ReloadOutlined } from '@ant-design/icons'
import { useEffect, useState } from 'react'
import { useLocation } from 'react-router-dom'
import { getPublicBenchmarkComparison } from '../services/site'
import type { PublicBenchmarkComparison, PublicBenchmarkScore } from '../types/site'
import './PublicBenchmarksPage.css'

const refreshIntervalMs = 15_000
const runIdPattern = /^(?:[0-9a-f]{32}|[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12})$/i
const scoreFormat = new Intl.NumberFormat('zh-CN', { maximumFractionDigits: 1, minimumFractionDigits: 1 })
const memoryFormat = new Intl.NumberFormat('zh-CN', { maximumFractionDigits: 1 })
const clockFormat = new Intl.DateTimeFormat('zh-CN', { hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23' })

type ComparisonState = {
  data: PublicBenchmarkComparison | null
  error: string
  loading: boolean
  updatedAt: Date | null
}

function sharedRunId(fragment: string) {
  const value = new URLSearchParams(fragment.replace(/^#/, '')).get('run')
  return value && runIdPattern.test(value) && /[1-9a-f]/i.test(value) ? value : undefined
}

function validScore(value: number | null | undefined): value is number {
  return typeof value === 'number' && Number.isFinite(value) && value > 0
}

function shownScore(value: number | null | undefined) {
  return validScore(value) ? scoreFormat.format(value) : '—'
}

function scorePercent(value: number, scale: number) {
  return `${100 * Math.max(0, Math.min(1, value / scale))}%`
}

function ScoreChange({ current, previous }: { current: number; previous?: number | null }) {
  if (!validScore(current) || !validScore(previous)) {
    return <span className="pub-bench-change pub-bench-change--neutral">暂无上次成绩</span>
  }
  const change = current - previous
  if (Math.abs(change) < 0.05) {
    return <span className="pub-bench-change pub-bench-change--neutral"><MinusOutlined aria-hidden /> 与上次持平</span>
  }
  return (
    <span className={`pub-bench-change pub-bench-change--${change > 0 ? 'up' : 'down'}`}>
      {change > 0 ? <ArrowUpOutlined aria-hidden /> : <ArrowDownOutlined aria-hidden />}
      <span>较上次 {change > 0 ? '+' : '−'}{scoreFormat.format(Math.abs(change))} 分</span>
    </span>
  )
}

function ScoreMeter({ score, reference, scale }: { score: number; reference: number; scale: number }) {
  return (
    <div className="pub-bench-meter" aria-hidden="true">
      <span className="pub-bench-meter-fill" style={{ width: scorePercent(validScore(score) ? score : 0, scale) }} />
      <span className="pub-bench-meter-reference" style={{ left: scorePercent(reference, scale) }} />
    </div>
  )
}

function ScoreRow({ item, reference, scale, mode }: {
  item: PublicBenchmarkScore
  reference: number
  scale: number
  mode: 'public' | 'selected' | 'example'
}) {
  const example = mode === 'example'
  const score = mode === 'public' && validScore(item.latestScore) ? item.latestScore : item.overallScore
  const gpu = item.gpuModels?.filter(Boolean).join(' / ')
  const memory = typeof item.totalMemoryMiB === 'number' && item.totalMemoryMiB > 0
    ? `${memoryFormat.format(item.totalMemoryMiB / 1024)} GiB 内存`
    : null

  return (
    <article className={`pub-bench-score-row pub-bench-score-row--${mode}`}>
      <div className="pub-bench-hardware">
        <span className="pub-bench-hardware-name">
          {example ? item.label : item.cpuModel || item.label || '共享设备'}
        </span>
        {example && item.cpuModel && <span className="pub-bench-hardware-detail">{item.cpuModel}</span>}
        {(gpu || memory) && (
          <span className="pub-bench-hardware-detail">{[gpu, memory].filter(Boolean).join(' · ')}</span>
        )}
      </div>
      <div className="pub-bench-score-visual">
        <div className="pub-bench-score-line">
          <span className="pub-bench-score-value">{shownScore(score)} <small>分</small></span>
          {mode === 'public' && validScore(item.overallScore) && (
            <span className="pub-bench-best" title="该设备分享过的最高分">最高 {shownScore(item.overallScore)}</span>
          )}
        </div>
        <ScoreMeter score={score} reference={reference} scale={scale} />
      </div>
      {mode === 'public' && (
        <div className="pub-bench-score-trend">
          <ScoreChange current={score} previous={item.previousScore} />
          {validScore(item.previousScore) && <span className="pub-bench-hardware-detail">上次 {shownScore(item.previousScore)} 分</span>}
        </div>
      )}
    </article>
  )
}

function BenchmarkComparison({ runId }: { runId?: string }) {
  const [state, setState] = useState<ComparisonState>({ data: null, error: '', loading: true, updatedAt: null })
  const [refreshKey, setRefreshKey] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    let pending = false
    const load = () => {
      if (pending) return
      pending = true
      void getPublicBenchmarkComparison(runId, controller.signal).then((data) => {
        if (!controller.signal.aborted) setState({ data, error: '', loading: false, updatedAt: new Date() })
      }).catch(() => {
        if (!controller.signal.aborted) setState((previous) => ({ ...previous, error: '暂时无法读取公开成绩。', loading: false }))
      }).finally(() => { pending = false })
    }
    const refreshWhenVisible = () => {
      if (document.visibilityState === 'visible') load()
    }

    load()
    const timer = window.setInterval(refreshWhenVisible, refreshIntervalMs)
    document.addEventListener('visibilitychange', refreshWhenVisible)
    return () => {
      controller.abort()
      window.clearInterval(timer)
      document.removeEventListener('visibilitychange', refreshWhenVisible)
    }
  }, [runId, refreshKey])

  const { data, loading, error, updatedAt } = state
  const references = data?.references ?? []
  const examples = references.length < 3 ? data?.exampleReferences ?? [] : []
  const selected = data?.selectedRun
  const reference = validScore(data?.referenceScore) ? data.referenceScore : 100
  const comparedScores = [...references, ...examples, ...(selected ? [selected] : [])]
    .flatMap((item) => [item.overallScore, item.latestScore]).filter(validScore)
  const scale = Math.max(reference * 1.3, ...comparedScores) * 1.05

  function refresh() {
    setState((previous) => ({ ...previous, loading: true, error: '' }))
    setRefreshKey((key) => key + 1)
  }

  return (
    <div className="pub-bench">
      <header className="pub-bench-hero">
        <div className="pub-bench-hero-main">
          <h1>基准测试对比</h1>
          <p>比较不同设备处理相同全幅图像任务时的 CPU 与内存性能。</p>
          <div className="pub-bench-actions">
            <button type="button" className="pub-bench-refresh" disabled={loading} onClick={refresh}>
              <ReloadOutlined spin={loading} aria-hidden /> {loading ? '正在刷新' : '刷新成绩'}
            </button>
            {updatedAt && <span>更新于 {clockFormat.format(updatedAt)}</span>}
          </div>
        </div>
        <div className="pub-bench-anchor">
          <DashboardOutlined className="pub-bench-anchor-icon" aria-hidden />
          <span>参考基准</span>
          <strong>{data ? shownScore(data.referenceScore) : '—'}</strong>
          {data && <span>{data.referenceName}</span>}
        </div>
      </header>

      {error && <p className="pub-bench-notice" role="status">{error}{data ? ' 当前显示上次取得的记录。' : ' 可以手动刷新。'}</p>}
      {loading && !data && !error && <p className="pub-bench-notice" role="status">正在读取公开成绩…</p>}
      {selected && (
        <section className="pub-bench-section pub-bench-section--selected" aria-labelledby="pub-bench-selected-title">
          <h2 id="pub-bench-selected-title">刚分享的成绩</h2>
          <ScoreRow item={selected} reference={reference} scale={scale} mode="selected" />
        </section>
      )}
      {runId && data && !selected && <p className="pub-bench-notice" role="status">该成绩暂时未出现在公开对比中。</p>}

      {data && (
        <section className="pub-bench-section" aria-labelledby="pub-bench-public-title">
          <div className="pub-bench-section-heading">
            <div>
              <h2 id="pub-bench-public-title">公开成绩</h2>
              <p>按最高分排列，并显示最近两次成绩的变化。</p>
            </div>
            {references.length > 0 && (
              <span className="pub-bench-legend"><i aria-hidden /> 参考基准 · {shownScore(reference)} 分</span>
            )}
          </div>
          {references.length === 0 ? <p className="pub-bench-empty">还没有设备公开分享成绩。</p> : (
            <div className="pub-bench-score-list">
              {references.map((item, index) => <ScoreRow key={index} item={item} reference={reference} scale={scale} mode="public" />)}
            </div>
          )}
        </section>
      )}

      {examples.length > 0 && (
        <section className="pub-bench-section pub-bench-section--examples" aria-labelledby="pub-bench-example-title">
          <div className="pub-bench-section-heading">
            <div>
              <h2 id="pub-bench-example-title">示例设备</h2>
              <p>演示数据。GPU 型号用于识别设备配置。</p>
            </div>
          </div>
          <div className="pub-bench-score-list">
            {examples.map((item, index) => <ScoreRow key={index} item={item} reference={reference} scale={scale} mode="example" />)}
          </div>
        </section>
      )}
    </div>
  )
}

export function PublicBenchmarksPage() {
  const { hash } = useLocation()
  const runId = sharedRunId(hash)
  return <BenchmarkComparison key={runId ?? ''} runId={runId} />
}
