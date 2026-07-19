import { BarChartOutlined, ClockCircleOutlined, ReloadOutlined, TeamOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Col, Row, Select, Skeleton, Space, Statistic, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { getTrafficStats } from '../services/admin'
import type { TrafficClientStats, TrafficDayStats, TrafficRouteStats, TrafficStatsResponse } from '../types/admin'
import { humanSize, shortDate } from '../utils/format'

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
    title: '错误',
    dataIndex: 'errorResponses',
    width: 120,
    align: 'right',
    render: (value, record) => <Tag color={value > 0 ? 'red' : 'green'}>{value} · {percent(record.errorRate)}</Tag>,
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
    title: '错误率',
    dataIndex: 'errorRate',
    width: 100,
    align: 'right',
    render: (value) => <Tag color={value > 0 ? 'red' : 'green'}>{percent(value)}</Tag>,
  },
]

const clientColumns: ColumnsType<TrafficClientStats> = [
  { title: '客户端', dataIndex: 'client', width: 120, render: (value: TrafficClientStats['client']) => clientLabels[value] },
  { title: '请求', dataIndex: 'visits', width: 100, align: 'right' },
  { title: '访客日', dataIndex: 'uniqueVisitorDays', width: 100, align: 'right' },
  { title: '占比', dataIndex: 'share', width: 100, align: 'right', render: percent },
  { title: '平均响应', dataIndex: 'avgResponseMs', width: 120, align: 'right', render: milliseconds },
  {
    title: '错误',
    dataIndex: 'errorResponses',
    width: 100,
    align: 'right',
    render: (value) => <Tag color={value > 0 ? 'red' : 'green'}>{value}</Tag>,
  },
]

export function TrafficPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const [data, setData] = useState<TrafficStatsResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [reloadKey, setReloadKey] = useState(0)
  const days = validDays(searchParams.get('days'))

  useEffect(() => {
    let mounted = true
    const controller = new AbortController()
    queueMicrotask(() => {
      if (!mounted) return
      setLoading(true)
      setError('')
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
            <Tag icon={<BarChartOutlined />} color="blue">Traffic</Tag>
            <Typography.Title level={2}>访问统计</Typography.Title>
            <Typography.Paragraph type="secondary">
              {data.summary.periodStart} 至 {data.summary.periodEnd} 的页面与 API 请求概况。
            </Typography.Paragraph>
          </div>
          <Space wrap>
            <Tag>响应流量 {humanSize(data.summary.totalResponseBytes)}</Tag>
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
            <Button icon={<ReloadOutlined />} loading={loading} onClick={() => setReloadKey((key) => key + 1)}>
              刷新
            </Button>
          </Space>
        </div>
      </Card>

      {error && <Alert type="warning" showIcon message="刷新失败，当前展示上一次成功结果" description={error} />}
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
          <Card loading={loading}><Statistic title="错误响应" value={data.summary.errorResponses} suffix={percent(data.summary.errorRate)} valueStyle={{ color: data.summary.errorResponses > 0 ? '#cf1322' : undefined }} /></Card>
        </Col>
      </Row>

      <Card title="今日" loading={loading} extra={<Tag>{data.today.day}</Tag>}>
        <Space wrap size={24}>
          <Statistic title="请求" value={data.today.visits} />
          <Statistic title="当日独立访客" value={data.today.uniqueVisitors} />
          <Statistic title="平均响应" value={Math.round(data.today.avgResponseMs)} suffix="ms" />
          <Statistic title="最慢响应" value={Math.round(data.today.maxResponseMs)} suffix="ms" />
          <Statistic title="响应流量" value={humanSize(data.today.totalResponseBytes)} />
          <Statistic title="错误率" value={data.today.errorRate} precision={2} suffix="%" />
          <Space wrap>
            <Tag color={data.recorder.pending > 0 ? 'gold' : 'green'}>待写入 {data.recorder.pending}</Tag>
            <Tag color={data.recorder.dropped > 0 ? 'red' : 'default'}>丢弃 {data.recorder.dropped}</Tag>
            {data.recorder.capacity !== undefined && <Tag>缓冲容量 {data.recorder.capacity}</Tag>}
            {data.recorder.lastFlushAt && <Tag>最近落盘 {shortDate(data.recorder.lastFlushAt)}</Tag>}
          </Space>
        </Space>
      </Card>

      <Card title="每日趋势">
        <Table rowKey="day" loading={loading} columns={dailyColumns} dataSource={data.daily} pagination={false} scroll={{ x: 880 }} />
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
              scroll={{ x: 940 }}
            />
          </Card>
        </Col>
        <Col xs={24} xl={9}>
          <Card title="客户端分布">
            <Table rowKey="client" loading={loading} columns={clientColumns} dataSource={data.clients} pagination={false} scroll={{ x: 620 }} />
          </Card>
        </Col>
      </Row>
    </Space>
  )
}
