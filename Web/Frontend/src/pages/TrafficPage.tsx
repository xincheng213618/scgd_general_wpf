import { BarChartOutlined, ClockCircleOutlined, ReloadOutlined, TeamOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Col, Row, Select, Skeleton, Space, Statistic, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { getPerformanceSummary, getTrafficStats } from '../services/admin'
import type {
  JobRun,
  PerformanceSummary,
  SlowRequestSample,
  TrafficClientStats,
  TrafficDayStats,
  TrafficErrorRouteStats,
  TrafficRouteStats,
  TrafficStatsResponse,
  WebPageStats,
  WebVitalStats,
} from '../types/admin'
import { humanSize, shortDate } from '../utils/format'
import { describeHttpError } from '../utils/trafficErrors'

const dayOptions = [
  { label: '最近 7 天', value: 7 },
  { label: '最近 30 天', value: 30 },
  { label: '最近 90 天', value: 90 },
  { label: '最近一年', value: 365 },
]

const clientLabels: Record<TrafficClientStats['client'], string> = {
  desktop: '桌面端',
  mobile: '手机',
  tablet: '平板',
  bot: '机器人',
  other: '其它',
}

function validDays(value: string | null) {
  const days = Number(value || 30)
  return Number.isInteger(days) && days >= 1 && days <= 365 ? days : 30
}

function percent(value: number) {
  return `${Number(value || 0).toFixed(2)}%`
}

function milliseconds(value: number) {
  return `${Math.round(Number(value || 0))} ms`
}

const dailyColumns: ColumnsType<TrafficDayStats> = [
  { title: '日期', dataIndex: 'day', width: 120 },
  { title: '请求', dataIndex: 'visits', width: 100, align: 'right' },
  { title: '当日独立访客', dataIndex: 'uniqueVisitors', width: 130, align: 'right' },
  { title: '平均响应', dataIndex: 'avgResponseMs', width: 120, align: 'right', render: milliseconds },
  { title: '最慢响应', dataIndex: 'maxResponseMs', width: 120, align: 'right', render: milliseconds },
  { title: '响应流量', dataIndex: 'totalResponseBytes', width: 120, align: 'right', render: humanSize },
  {
    title: '请求侧 4xx',
    dataIndex: 'clientErrorResponses',
    width: 120,
    align: 'right',
    render: (value, record) => <Tag color={value > 0 ? 'gold' : 'default'}>{value} · {percent(record.clientErrorRate)}</Tag>,
  },
  {
    title: '服务端 5xx',
    dataIndex: 'serverErrorResponses',
    width: 120,
    align: 'right',
    render: (value, record) => <Tag color={value > 0 ? 'red' : 'green'}>{value} · {percent(record.serverErrorRate)}</Tag>,
  },
  {
    title: '历史未分类',
    dataIndex: 'unclassifiedErrorResponses',
    width: 120,
    align: 'right',
    render: (value, record) => <Tag>{value} · {percent(record.unclassifiedErrorRate)}</Tag>,
  },
]

const routeColumns: ColumnsType<TrafficRouteStats> = [
  { title: '方法', dataIndex: 'method', width: 90, render: (value) => <Tag>{value}</Tag> },
  { title: '路由', dataIndex: 'route', render: (value) => <Typography.Text code>{value}</Typography.Text> },
  { title: '请求', dataIndex: 'visits', width: 100, align: 'right' },
  { title: '平均响应', dataIndex: 'avgResponseMs', width: 120, align: 'right', render: milliseconds },
  { title: '最慢响应', dataIndex: 'maxResponseMs', width: 120, align: 'right', render: milliseconds },
  { title: '响应流量', dataIndex: 'responseBytes', width: 120, align: 'right', render: humanSize },
  {
    title: '4xx',
    dataIndex: 'clientErrorResponses',
    width: 110,
    align: 'right',
    render: (value, record) => <Tag color={value > 0 ? 'gold' : 'default'}>{value} · {percent(record.clientErrorRate)}</Tag>,
  },
  {
    title: '5xx',
    dataIndex: 'serverErrorResponses',
    width: 110,
    align: 'right',
    render: (value, record) => <Tag color={value > 0 ? 'red' : 'green'}>{value} · {percent(record.serverErrorRate)}</Tag>,
  },
  {
    title: '未分类',
    dataIndex: 'unclassifiedErrorResponses',
    width: 110,
    align: 'right',
    render: (value) => <Tag>{value}</Tag>,
  },
]

const clientColumns: ColumnsType<TrafficClientStats> = [
  { title: '客户端', dataIndex: 'client', width: 120, render: (value: TrafficClientStats['client']) => clientLabels[value] },
  { title: '请求', dataIndex: 'visits', width: 100, align: 'right' },
  { title: '访客日', dataIndex: 'uniqueVisitorDays', width: 100, align: 'right' },
  { title: '占比', dataIndex: 'share', width: 100, align: 'right', render: percent },
  { title: '平均响应', dataIndex: 'avgResponseMs', width: 120, align: 'right', render: milliseconds },
  {
    title: '4xx',
    dataIndex: 'clientErrorResponses',
    width: 110,
    align: 'right',
    render: (value) => <Tag color={value > 0 ? 'gold' : 'default'}>{value}</Tag>,
  },
  {
    title: '5xx',
    dataIndex: 'serverErrorResponses',
    width: 110,
    align: 'right',
    render: (value) => <Tag color={value > 0 ? 'red' : 'green'}>{value}</Tag>,
  },
  {
    title: '未分类',
    dataIndex: 'unclassifiedErrorResponses',
    width: 110,
    align: 'right',
    render: (value) => <Tag>{value}</Tag>,
  },
]

const errorRouteColumns: ColumnsType<TrafficErrorRouteStats> = [
  {
    title: '状态',
    dataIndex: 'statusCode',
    width: 90,
    render: (value: number) => <Tag color={value >= 500 ? 'red' : 'gold'}>{value}</Tag>,
  },
  {
    title: '含义',
    dataIndex: 'statusCode',
    width: 180,
    render: (value: number) => describeHttpError(value),
  },
  { title: '方法', dataIndex: 'method', width: 90, render: (value) => <Tag>{value}</Tag> },
  { title: '规范化路由', dataIndex: 'route', render: (value) => <Typography.Text code>{value}</Typography.Text> },
  { title: '响应', dataIndex: 'responses', width: 100, align: 'right' },
  { title: '精确明细占比', dataIndex: 'share', width: 130, align: 'right', render: percent },
]

const webPageColumns: ColumnsType<WebPageStats> = [
  { title: '页面路由', dataIndex: 'route', render: (value) => <Typography.Text code>{value}</Typography.Text> },
  { title: '浏览量', dataIndex: 'pageViews', width: 100, align: 'right' },
  { title: '访客日', dataIndex: 'uniqueVisitorDays', width: 100, align: 'right' },
  { title: '首次加载', dataIndex: 'hardNavigations', width: 100, align: 'right' },
  { title: '站内切换', dataIndex: 'spaNavigations', width: 100, align: 'right' },
]

const vitalDescriptions: Record<WebVitalStats['metric'], string> = {
  LCP: '主要内容呈现',
  CLS: '视觉稳定性',
  INP: '交互响应',
}

function vitalValue(vital: WebVitalStats) {
  if (vital.unit === 'score') return vital.average.toFixed(3)
  return Math.round(vital.average)
}

function vitalColor(vital: WebVitalStats) {
  if (vital.samples === 0) return undefined
  if (vital.poorSamples > 0) return '#cf1322'
  if (vital.needsImprovementSamples > 0) return '#d48806'
  return '#389e0d'
}

function httpStatus(value: number) {
  return <Tag color={value >= 500 ? 'red' : value >= 400 ? 'gold' : 'green'}>{value}</Tag>
}

function jobStatus(value: string) {
  const color = value === 'error' ? 'red' : value === 'success' ? 'green' : value === 'running' ? 'blue' : 'default'
  return <Tag color={color}>{value || 'unknown'}</Tag>
}

const slowRequestColumns: ColumnsType<SlowRequestSample> = [
  { title: '发生时间', dataIndex: 'recorded_at', width: 150, render: shortDate },
  { title: '方法', dataIndex: 'method', width: 84, render: (value) => <Tag>{value}</Tag> },
  { title: '路径', dataIndex: 'path', render: (value) => <Typography.Text code>{value}</Typography.Text> },
  { title: '状态', dataIndex: 'status', width: 80, align: 'right', render: httpStatus },
  { title: '耗时', dataIndex: 'duration_ms', width: 100, align: 'right', render: milliseconds },
]

const slowJobColumns: ColumnsType<JobRun> = [
  { title: '开始时间', dataIndex: 'started_at', width: 150, render: shortDate },
  { title: '任务', dataIndex: 'job_id', render: (value) => <Typography.Text code>{value}</Typography.Text> },
  { title: '状态', dataIndex: 'status', width: 90, render: jobStatus },
  { title: '耗时', dataIndex: 'duration_ms', width: 100, align: 'right', render: milliseconds },
  {
    title: '结果',
    key: 'result',
    render: (_, record) => (
      <Typography.Text type={record.error ? 'danger' : 'secondary'} ellipsis={{ tooltip: record.error || record.summary }}>
        {record.error || record.summary || '-'}
      </Typography.Text>
    ),
  },
]

export function TrafficPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const [data, setData] = useState<TrafficStatsResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [performance, setPerformance] = useState<PerformanceSummary | null>(null)
  const [performanceLoading, setPerformanceLoading] = useState(true)
  const [performanceError, setPerformanceError] = useState('')
  const [reloadKey, setReloadKey] = useState(0)
  const days = validDays(searchParams.get('days'))

  useEffect(() => {
    let mounted = true
    const controller = new AbortController()
    queueMicrotask(() => {
      if (!mounted) return
      setLoading(true)
      setError('')
      setPerformanceLoading(true)
      setPerformanceError('')
    })
    getTrafficStats(days, 10, controller.signal)
      .then((payload) => {
        if (mounted) setData(payload)
      })
      .catch((requestError) => {
        if (mounted) setError(requestError instanceof Error ? requestError.message : '请求统计加载失败')
      })
      .finally(() => {
        if (mounted) setLoading(false)
      })
    getPerformanceSummary(controller.signal)
      .then((payload) => {
        if (mounted) setPerformance(payload)
      })
      .catch((requestError) => {
        if (mounted) setPerformanceError(requestError instanceof Error ? requestError.message : '性能诊断加载失败')
      })
      .finally(() => {
        if (mounted) setPerformanceLoading(false)
      })
    return () => {
      mounted = false
      controller.abort()
    }
  }, [days, reloadKey])

  if (loading && !data) return <Skeleton active paragraph={{ rows: 10 }} />
  if (error && !data) return <Alert type="error" showIcon message={error} action={<Button onClick={() => setReloadKey((key) => key + 1)}>重试</Button>} />
  if (!data) return null

  const recorderProblem = data.recorder.lastError || data.recorder.dropped > 0

  return (
    <Space direction="vertical" size={16} className="page-stack">
      <Card>
        <div className="section-heading compact">
          <div>
            <Space size={4} wrap>
              <Tag icon={<BarChartOutlined />} color="blue">Traffic</Tag>
              <Tag>日界线 {data.summary.timeZone}</Tag>
            </Space>
            <Typography.Title level={2}>访问统计</Typography.Title>
            <Typography.Paragraph type="secondary">
              {data.summary.periodStart} 至 {data.summary.periodEnd} 的页面与 API 请求概况。
            </Typography.Paragraph>
          </div>
          <Space wrap>
            <Tag>响应流量 {humanSize(data.summary.totalResponseBytes)}</Tag>
            <Tag color={data.summary.clientErrorResponses > 0 ? 'gold' : 'default'}>
              4xx {data.summary.clientErrorResponses} · {percent(data.summary.clientErrorRate)}
            </Tag>
            <Tag color={data.summary.serverErrorResponses > 0 ? 'red' : 'green'}>
              5xx {data.summary.serverErrorResponses} · {percent(data.summary.serverErrorRate)}
            </Tag>
            <Select
              aria-label="统计周期"
              value={days}
              options={dayOptions}
              style={{ width: 140 }}
              onChange={(value) => {
                const next = new URLSearchParams(searchParams)
                next.set('days', String(value))
                setSearchParams(next)
              }}
            />
            <Button icon={<ReloadOutlined />} loading={loading || performanceLoading} onClick={() => setReloadKey((key) => key + 1)}>
              刷新
            </Button>
          </Space>
        </div>
      </Card>

      {error && <Alert type="warning" showIcon message="刷新失败，当前展示上一次成功结果" description={error} />}
      {data.summary.hasLegacyCalendarData && (
        <Alert
          type="info"
          showIcon
          message={`部分历史数据仍使用旧版日界线（截至 ${data.summary.legacyCalendarDataThroughDay}）`}
          description={`自 ${data.summary.calendarBoundaryEffectiveAt ? shortDate(data.summary.calendarBoundaryEffectiveAt) : '本次升级'} 起，新请求按 ${data.summary.timeZone} 划分统计日；历史聚合无法可靠跨日重分配，因此保持原值。`}
        />
      )}
      {data.summary.unclassifiedErrorResponses > 0 && (
        <Alert
          type="info"
          showIcon
          message={`有 ${data.summary.unclassifiedErrorResponses} 条历史错误响应尚未分类`}
          description="旧版统计只保留了 HTTP 4xx/5xx 合计，无法可靠回填具体类别；新请求已开始精确区分，请勿把这部分直接视为服务端故障。"
        />
      )}
      {recorderProblem && (
        <Alert
          type="warning"
          showIcon
          message="统计记录器需要关注"
          description={data.recorder.lastError || `已有 ${data.recorder.dropped} 条记录因缓冲区压力被丢弃。`}
        />
      )}

      <Row gutter={[16, 16]}>
        <Col xs={24} md={12} xl={6}>
          <Card loading={loading}><Statistic title={`${data.summary.days} 天请求`} value={data.summary.visits} prefix={<BarChartOutlined />} /></Card>
        </Col>
        <Col xs={24} md={12} xl={6}>
          <Card loading={loading}><Statistic title="独立访客日累计" value={data.summary.uniqueVisitorDays} prefix={<TeamOutlined />} /></Card>
        </Col>
        <Col xs={24} md={12} xl={6}>
          <Card loading={loading}><Statistic title="平均响应" value={Math.round(data.summary.avgResponseMs)} suffix="ms" prefix={<ClockCircleOutlined />} /></Card>
        </Col>
        <Col xs={24} md={12} xl={6}>
          <Card loading={loading}><Statistic title="服务端故障（5xx）" value={data.summary.serverErrorResponses} suffix={`次 · ${percent(data.summary.serverErrorRate)}`} valueStyle={{ color: data.summary.serverErrorResponses > 0 ? '#cf1322' : undefined }} /></Card>
        </Col>
      </Row>

      <Card
        title="页面体验"
        loading={loading}
        extra={(
          <Space wrap>
            <Tag>真实浏览器采样</Tag>
            <Tag color="blue">与 HTTP 请求分开统计</Tag>
          </Space>
        )}
      >
        <Space direction="vertical" size={16} className="page-stack">
          <Typography.Paragraph type="secondary">
            页面浏览量覆盖首次加载和 React 站内切换；LCP、CLS、INP 由浏览器上报并按固定路由聚合，不保存查询参数、来源页、原始 IP 或完整 User-Agent。
          </Typography.Paragraph>
          {data.web.summary.pageViews === 0 && (
            <Alert
              type="info"
              showIcon
              message="尚无页面体验样本"
              description="新版本上线并产生真实页面访问后，这里会开始展示页面浏览量和 Core Web Vitals。"
            />
          )}
          <Row gutter={[16, 16]}>
            <Col xs={24} md={8}>
              <Statistic title="页面浏览量" value={data.web.summary.pageViews} />
            </Col>
            <Col xs={24} md={8}>
              <Statistic title="页面访客日" value={data.web.summary.uniqueVisitorDays} />
            </Col>
            <Col xs={24} md={8}>
              <Statistic title="站内页面切换" value={data.web.summary.spaNavigations} />
            </Col>
          </Row>
          <Row gutter={[16, 16]}>
            {data.web.vitals.map((vital) => (
              <Col xs={24} md={8} key={vital.metric}>
                <Card size="small">
                  <Statistic
                    title={`${vital.metric} · ${vitalDescriptions[vital.metric]}`}
                    value={vitalValue(vital)}
                    suffix={vital.unit === 'ms' ? 'ms' : undefined}
                    valueStyle={{ color: vitalColor(vital) }}
                  />
                  <Space wrap size={4}>
                    <Tag>样本 {vital.samples}</Tag>
                    <Tag color={vital.samples > 0 && vital.goodRate >= 75 ? 'green' : 'default'}>
                      良好 {percent(vital.goodRate)}
                    </Tag>
                    {vital.needsImprovementSamples > 0 && <Tag color="gold">待优化 {vital.needsImprovementSamples}</Tag>}
                    {vital.poorSamples > 0 && <Tag color="red">较差 {vital.poorSamples}</Tag>}
                  </Space>
                </Card>
              </Col>
            ))}
          </Row>
          <Table
            rowKey="route"
            size="small"
            columns={webPageColumns}
            dataSource={data.web.topPages}
            pagination={false}
            locale={{ emptyText: '尚无页面浏览记录' }}
            scroll={{ x: 720 }}
          />
        </Space>
      </Card>

      <Card title="今日" loading={loading} extra={<Tag>{data.today.day}</Tag>}>
        <Space wrap size={24}>
          <Statistic title="请求" value={data.today.visits} />
          <Statistic title="当日独立访客" value={data.today.uniqueVisitors} />
          <Statistic title="平均响应" value={Math.round(data.today.avgResponseMs)} suffix="ms" />
          <Statistic title="最慢响应" value={Math.round(data.today.maxResponseMs)} suffix="ms" />
          <Statistic title="响应流量" value={humanSize(data.today.totalResponseBytes)} />
          <Statistic title="请求侧 4xx" value={data.today.clientErrorResponses} suffix={`次 · ${percent(data.today.clientErrorRate)}`} />
          <Statistic title="服务端 5xx" value={data.today.serverErrorResponses} suffix={`次 · ${percent(data.today.serverErrorRate)}`} valueStyle={{ color: data.today.serverErrorResponses > 0 ? '#cf1322' : undefined }} />
          {data.today.unclassifiedErrorResponses > 0 && <Statistic title="历史未分类" value={data.today.unclassifiedErrorResponses} />}
          <Space wrap>
            <Tag color={data.recorder.pending > 0 ? 'gold' : 'green'}>待写入 {data.recorder.pending}</Tag>
            <Tag color={data.recorder.dropped > 0 ? 'red' : 'default'}>丢弃 {data.recorder.dropped}</Tag>
            {data.recorder.capacity !== undefined && <Tag>缓冲容量 {data.recorder.capacity}</Tag>}
            {data.recorder.lastFlushAt && <Tag>最近落盘 {shortDate(data.recorder.lastFlushAt)}</Tag>}
          </Space>
        </Space>
      </Card>

      <Card
        title="错误路由诊断"
        loading={loading}
        extra={(
          <Space wrap>
            <Tag color={data.errorDiagnostics.partial ? 'gold' : 'green'}>
              精确覆盖 {percent(data.errorDiagnostics.coverageRate)}
            </Tag>
            <Tag>
              已记录 {data.errorDiagnostics.recordedResponses}/{data.errorDiagnostics.totalErrorResponses}
            </Tag>
          </Space>
        )}
      >
        <Space direction="vertical" size={12} className="page-stack">
          <Typography.Paragraph type="secondary">
            按精确 HTTP 状态码、请求方法和规范化路由聚合，不保存原始 URL、查询参数、IP 或请求头。
          </Typography.Paragraph>
          {data.errorDiagnostics.partial && (
            <Alert
              type="info"
              showIcon
              message={`选定区间有 ${data.errorDiagnostics.totalErrorResponses - data.errorDiagnostics.recordedResponses} 条旧错误没有精确状态码`}
              description="精确状态码从本次升级后开始记录，旧版汇总无法可靠回填；下表仅展示已有精确明细。"
            />
          )}
          <Table
            rowKey={(record) => `${record.statusCode}:${record.method}:${record.route}`}
            size="small"
            columns={errorRouteColumns}
            dataSource={data.errorDiagnostics.items}
            pagination={false}
            locale={{ emptyText: data.errorDiagnostics.totalErrorResponses > 0 ? '现有错误均来自升级前，尚无精确状态码明细' : '选定区间没有错误响应' }}
            scroll={{ x: 820 }}
          />
        </Space>
      </Card>

      <Card
        title="实时慢事件"
        loading={performanceLoading && !performance}
        extra={performance && (
          <Space wrap>
            <Tag>慢请求阈值 {milliseconds(performance.threshold_ms)}</Tag>
            <Tag color={performance.request_buffer_count > 0 ? 'gold' : 'green'}>
              进程缓冲 {performance.request_buffer_count}/{performance.request_buffer_capacity}
            </Tag>
            <Tag>采样于 {shortDate(performance.generated_at)}</Tag>
            <Tag>进程启动 {shortDate(performance.process_started_at)}</Tag>
          </Space>
        )}
      >
        <Space direction="vertical" size={12} className="page-stack">
          <Typography.Paragraph type="secondary">
            这里复用当前 Web 进程的有界慢请求缓冲和既有任务历史；请求样本会在服务重启后清空，不会持久化原始 IP、查询参数或请求头。
          </Typography.Paragraph>
          {performanceError && (
            <Alert
              type="warning"
              showIcon
              message={performance ? '性能诊断刷新失败，当前展示上一次成功结果' : '性能诊断暂不可用'}
              description={performanceError}
            />
          )}
          <Row gutter={[16, 16]}>
            <Col xs={24} xxl={14}>
              <Typography.Title level={4}>慢请求</Typography.Title>
              <Table
                rowKey={(record) => `${record.recorded_at}:${record.method}:${record.path}`}
                size="small"
                loading={performanceLoading}
                columns={slowRequestColumns}
                dataSource={performance ? [...performance.slow_requests].reverse() : []}
                pagination={false}
                locale={{ emptyText: '当前服务进程尚未记录慢请求' }}
                scroll={{ x: 760 }}
              />
            </Col>
            <Col xs={24} xxl={10}>
              <Typography.Title level={4}>慢任务或失败任务</Typography.Title>
              <Table
                rowKey="id"
                size="small"
                loading={performanceLoading}
                columns={slowJobColumns}
                dataSource={performance?.slow_jobs || []}
                pagination={false}
                locale={{ emptyText: '最近任务运行正常' }}
                scroll={{ x: 720 }}
              />
            </Col>
          </Row>
        </Space>
      </Card>

      <Card title="每日趋势">
        <Table rowKey="day" loading={loading} columns={dailyColumns} dataSource={data.daily} pagination={false} scroll={{ x: 1240 }} />
      </Card>

      <Row gutter={[16, 16]}>
        <Col xs={24} xl={15}>
          <Card title="热门路由">
            <Table
              rowKey={(record) => `${record.method}:${record.route}`}
              loading={loading}
              columns={routeColumns}
              dataSource={data.topRoutes}
              pagination={false}
              scroll={{ x: 1240 }}
            />
          </Card>
        </Col>
        <Col xs={24} xl={9}>
          <Card title="客户端分布">
            <Table rowKey="client" loading={loading} columns={clientColumns} dataSource={data.clients} pagination={false} scroll={{ x: 900 }} />
          </Card>
        </Col>
      </Row>
    </Space>
  )
}
