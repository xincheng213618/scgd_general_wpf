import { ReloadOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Col, Descriptions, Drawer, Empty, Flex, List, Row, Select, Spin, Table, Tag, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { usePhoneLayout } from '../hooks/usePhoneLayout'
import { getHostBenchmarkDeviceHistory, getHostBenchmarkLeaderboard } from '../services/admin'
import type {
  HostBenchmarkDeviceHistory,
  HostBenchmarkDeviceRank,
  HostBenchmarkLeaderboard,
  HostBenchmarkMetric,
  HostBenchmarkRun,
} from '../types/admin'

type RequestState<T> = { status: 'loading' } | { status: 'error'; message: string } | { status: 'ready'; data: T }
type RankedDevice = HostBenchmarkDeviceRank & { rank: number }

const dateFormat = new Intl.DateTimeFormat('zh-CN', {
  year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23',
})

const metricNames: Record<string, string> = {
  'memory-copy': '整帧内存复制',
  'channel-swap': '16 位红蓝通道交换',
  'bayer-rg16': 'BayerRG16 转 BGR16',
  'pixel-compute': '全幅像素计算',
}

function benchmarkDate(value: string) {
  const date = new Date(value)
  return Number.isFinite(date.getTime()) ? dateFormat.format(date) : '—'
}

function formatMeasure(value: number | undefined, digits = 2) {
  return value !== undefined && Number.isFinite(value) ? value.toFixed(digits) : '—'
}

function changeLabel(value: number | null, suffix: string, higherIsBetter: boolean) {
  if (value === null || !Number.isFinite(value)) return '—'
  const rounded = Math.round(value * 100) / 100
  const improved = higherIsBetter ? rounded > 0 : rounded < 0
  return (
    <Typography.Text type={rounded === 0 ? 'secondary' : improved ? 'success' : 'danger'}>
      {rounded > 0 ? '+' : ''}{rounded.toFixed(2)}{suffix}
    </Typography.Text>
  )
}

function timeChange(before: number | undefined, after: number | undefined) {
  return before !== undefined && before > 0 && after !== undefined ? (after / before - 1) * 100 : null
}

function HardwareSummary({ run }: { run: HostBenchmarkRun }) {
  const memory = run.totalMemoryMiB !== undefined && run.totalMemoryMiB > 0
    ? `${formatMeasure(run.totalMemoryMiB / 1024, 1)} GiB`
    : '—'
  return (
    <Flex vertical gap={2} style={{ overflowWrap: 'anywhere' }}>
      <Typography.Text><Typography.Text type="secondary">CPU </Typography.Text>{run.cpuModel || '—'}</Typography.Text>
      <Typography.Text><Typography.Text type="secondary">GPU </Typography.Text>{run.gpuModels?.join(' / ') || '—'}</Typography.Text>
      <Typography.Text><Typography.Text type="secondary">内存 </Typography.Text>{memory}</Typography.Text>
    </Flex>
  )
}

function DeviceNameLink({ device }: { device: HostBenchmarkDeviceRank }) {
  return (
    <Link to={{ search: `?${new URLSearchParams({ device: device.deviceId })}` }}>
      {device.deviceName || device.deviceId}
    </Link>
  )
}

const rankingColumns: TableColumnsType<RankedDevice> = [
  { title: '排名', dataIndex: 'rank', width: 70, align: 'center' },
  {
    title: '设备', key: 'device', width: 180,
    render: (_, device) => <DeviceNameLink device={device} />,
  },
  {
    title: '最高分', key: 'best', width: 145,
    render: (_, device) => (
      <Flex vertical gap={2}>
        <Typography.Text strong>{formatMeasure(device.score)}</Typography.Text>
        <Typography.Text type="secondary">{benchmarkDate(device.best.measuredAtUtc)}</Typography.Text>
      </Flex>
    ),
  },
  {
    title: '最近得分', key: 'latest', width: 145,
    render: (_, device) => (
      <Flex vertical gap={2}>
        <Typography.Text>{formatMeasure(device.latest.overallScore)}</Typography.Text>
        <Typography.Text type="secondary">{benchmarkDate(device.latest.measuredAtUtc)}</Typography.Text>
      </Flex>
    ),
  },
  { title: '最近上报硬件', key: 'hardware', render: (_, device) => <HardwareSummary run={device.latest} /> },
  { title: '记录数', dataIndex: 'runCount', width: 94, align: 'right' },
]

const emptyRanking = <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="尚无设备共享跑分" />

function DeviceRanking({ devices, loading }: { devices: RankedDevice[]; loading: boolean }) {
  const isPhone = usePhoneLayout()
  const pagination = devices.length > 20 ? { pageSize: 20, showSizeChanger: false } : false

  return isPhone ? (
    <List rowKey="deviceId" dataSource={devices} pagination={pagination} loading={loading} locale={{ emptyText: emptyRanking }}
          renderItem={(device) => (
            <List.Item>
              <Flex vertical gap={8} style={{ minWidth: 0, width: '100%', overflowWrap: 'anywhere' }}>
                <Flex justify="space-between" align="center" gap={8}>
                  <Typography.Text strong>{device.rank}. <DeviceNameLink device={device} /></Typography.Text>
                  <Typography.Text type="secondary">{device.runCount} 条</Typography.Text>
                </Flex>
                <Row gutter={12}>
                  <Col span={12}>
                    <Flex vertical gap={2}>
                      <Typography.Text strong>最高 {formatMeasure(device.score)} 分</Typography.Text>
                      <Typography.Text type="secondary">{benchmarkDate(device.best.measuredAtUtc)}</Typography.Text>
                    </Flex>
                  </Col>
                  <Col span={12}>
                    <Flex vertical gap={2}>
                      <Typography.Text>最近 {formatMeasure(device.latest.overallScore)} 分</Typography.Text>
                      <Typography.Text type="secondary">{benchmarkDate(device.latest.measuredAtUtc)}</Typography.Text>
                    </Flex>
                  </Col>
                </Row>
                <HardwareSummary run={device.latest} />
              </Flex>
            </List.Item>
          )} />
  ) : (
    <Table size="small" rowKey="deviceId" columns={rankingColumns} dataSource={devices} loading={loading}
           scroll={{ x: 850 }} pagination={pagination} locale={{ emptyText: emptyRanking }} />
  )
}

const historyColumns: TableColumnsType<HostBenchmarkRun> = [
  { title: '测试时间', dataIndex: 'measuredAtUtc', width: 165, render: benchmarkDate },
  { title: '应用启动时间', dataIndex: 'appStartedAtUtc', width: 165, render: benchmarkDate },
  { title: 'Windows 启动时间（约）', dataIndex: 'approximateSystemStartedAtUtc', width: 190, render: benchmarkDate },
  { title: '应用版本', dataIndex: 'appVersion', width: 110, render: (value: string) => value || '—' },
  { title: '总分', dataIndex: 'overallScore', width: 86, align: 'right', render: (value: number) => formatMeasure(value) },
]

type MetricPair = { id: string; before?: HostBenchmarkMetric; after?: HostBenchmarkMetric }

const comparisonColumns: TableColumnsType<MetricPair> = [
  { title: '分项', key: 'metric', width: 164, render: (_, row) => metricNames[row.id] ?? row.id },
  { title: 'A 中位数', key: 'beforeMedian', width: 101, align: 'right', render: (_, row) => formatMeasure(row.before?.medianMilliseconds) },
  { title: 'B 中位数', key: 'afterMedian', width: 101, align: 'right', render: (_, row) => formatMeasure(row.after?.medianMilliseconds) },
  {
    title: '中位数变化', key: 'medianChange', width: 111, align: 'right',
    render: (_, row) => changeLabel(timeChange(row.before?.medianMilliseconds, row.after?.medianMilliseconds), '%', false),
  },
  { title: 'A P95', key: 'beforeP95', width: 83, align: 'right', render: (_, row) => formatMeasure(row.before?.p95Milliseconds) },
  { title: 'B P95', key: 'afterP95', width: 83, align: 'right', render: (_, row) => formatMeasure(row.after?.p95Milliseconds) },
  {
    title: 'P95 变化', key: 'p95Change', width: 102, align: 'right',
    render: (_, row) => changeLabel(timeChange(row.before?.p95Milliseconds, row.after?.p95Milliseconds), '%', false),
  },
]

function RunSnapshot({ label, run }: { label: string; run: HostBenchmarkRun }) {
  return (
    <Card size="small" title={label} extra={<Typography.Text strong>{formatMeasure(run.overallScore)} 分</Typography.Text>}>
      <Flex vertical gap={6}>
        <HardwareSummary run={run} />
        <Descriptions size="small" column={1}>
          <Descriptions.Item label="应用与运行时">{[run.appVersion, run.runtime].filter(Boolean).join(' / ') || '—'}</Descriptions.Item>
          <Descriptions.Item label="应用启动">{benchmarkDate(run.appStartedAtUtc)}</Descriptions.Item>
          <Descriptions.Item label="Windows 启动（约）">{benchmarkDate(run.approximateSystemStartedAtUtc)}</Descriptions.Item>
          <Descriptions.Item label="测试前可用内存">
            {run.availableMemoryMiBBefore > 0 ? `${formatMeasure(run.availableMemoryMiBBefore / 1024, 1)} GiB` : '—'}
          </Descriptions.Item>
        </Descriptions>
      </Flex>
    </Card>
  )
}

function HistoryComparison({ runs }: { runs: HostBenchmarkRun[] }) {
  const [beforeId, setBeforeId] = useState(runs[1]?.id)
  const [afterId, setAfterId] = useState(runs[0]?.id)
  const before = runs.find((run) => run.id === beforeId)
  const after = runs.find((run) => run.id === afterId)
  const sameVersions = !!before && !!after && before.workloadVersion === after.workloadVersion && before.scoringVersion === after.scoringVersion
  const metricIds = [...new Set([...(before?.metrics ?? []), ...(after?.metrics ?? [])].map((metric) => metric.id))]
  const metrics = metricIds.map((id) => ({ id, before: before?.metrics.find((metric) => metric.id === id), after: after?.metrics.find((metric) => metric.id === id) }))
  const options = runs.map((run) => ({ value: run.id, label: `${benchmarkDate(run.measuredAtUtc)} · ${formatMeasure(run.overallScore)} 分` }))

  if (runs.length < 2) return after ? <RunSnapshot label="最近一次测试" run={after} /> : null

  return (
    <Card title="比较两次测试" size="small">
      <Flex vertical gap={12}>
        <Row gutter={[12, 8]}>
          <Col xs={24} sm={12}>
            <Flex vertical gap={4}>
              <label htmlFor="benchmark-run-a">记录 A</label>
              <Select id="benchmark-run-a" value={beforeId} onChange={setBeforeId}
                      options={options.map((option) => ({ ...option, disabled: option.value === afterId }))} />
            </Flex>
          </Col>
          <Col xs={24} sm={12}>
            <Flex vertical gap={4}>
              <label htmlFor="benchmark-run-b">记录 B</label>
              <Select id="benchmark-run-b" value={afterId} onChange={setAfterId}
                      options={options.map((option) => ({ ...option, disabled: option.value === beforeId }))} />
            </Flex>
          </Col>
        </Row>
        {before && after && (
          <>
            <Row gutter={[12, 12]}>
              <Col xs={24} sm={12}><RunSnapshot label="记录 A" run={before} /></Col>
              <Col xs={24} sm={12}><RunSnapshot label="记录 B" run={after} /></Col>
            </Row>
            {sameVersions ? (
              <>
                <Flex gap={10} align="center" wrap="wrap">
                  <Typography.Text strong>总分变化 {changeLabel(after.overallScore - before.overallScore, ' 分', true)}</Typography.Text>
                  <Typography.Text type="secondary">耗时以毫秒计。变化按 B 相对于 A 计算，负值表示更快。</Typography.Text>
                </Flex>
                <Table size="small" rowKey="id" columns={comparisonColumns} dataSource={metrics} pagination={false} scroll={{ x: 760 }} />
              </>
            ) : <Alert type="info" showIcon message="两条记录的工作负载或计分版本不同，无法计算变化。" />}
          </>
        )}
      </Flex>
    </Card>
  )
}

function DeviceHistory({ deviceId }: { deviceId: string }) {
  const [state, setState] = useState<RequestState<HostBenchmarkDeviceHistory>>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    void getHostBenchmarkDeviceHistory(deviceId, controller.signal).then((data) => {
      if (!controller.signal.aborted) setState({ status: 'ready', data })
    }).catch((error: unknown) => {
      if (!controller.signal.aborted) setState({ status: 'error', message: error instanceof Error ? error.message : '无法读取设备历史' })
    })
    return () => controller.abort()
  }, [deviceId, attempt])

  if (state.status === 'loading') return <Spin tip="正在读取设备历史…"><div style={{ minHeight: 120 }} /></Spin>
  if (state.status === 'error') return (
    <Alert type="error" showIcon message={state.message} action={<Button onClick={() => {
      setState({ status: 'loading' })
      setAttempt((value) => value + 1)
    }}>重试</Button>} />
  )

  const { deviceName, runCount, runs } = state.data
  return (
    <Flex vertical gap={16}>
      <Flex align="center" gap={8} wrap="wrap">
        <Typography.Title level={5} style={{ margin: 0, overflowWrap: 'anywhere' }}>{deviceName || deviceId}</Typography.Title>
        <Tag>{runCount} 条共享记录</Tag>
      </Flex>
      <Typography.Text type="secondary">
        时间按浏览器本地时区显示。应用和 Windows 启动时间可用于比对重启前后的记录。
      </Typography.Text>
      <Table size="small" rowKey="id" columns={historyColumns} dataSource={runs}
             pagination={runs.length > 6 ? { pageSize: 6, showSizeChanger: false } : false} scroll={{ x: 716 }}
             locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="尚无共享跑分记录" /> }} />
      <HistoryComparison runs={runs} />
    </Flex>
  )
}

export function BenchmarksPage() {
  const [state, setState] = useState<RequestState<HostBenchmarkLeaderboard>>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)
  const [searchParams, setSearchParams] = useSearchParams()
  const deviceId = searchParams.get('device')

  useEffect(() => {
    const controller = new AbortController()
    void getHostBenchmarkLeaderboard(controller.signal).then((data) => {
      if (!controller.signal.aborted) setState({ status: 'ready', data })
    }).catch((error: unknown) => {
      if (!controller.signal.aborted) setState({ status: 'error', message: error instanceof Error ? error.message : '无法读取性能排行' })
    })
    return () => controller.abort()
  }, [attempt])

  const data = state.status === 'ready' ? state.data : null
  const devices = (data?.devices ?? []).slice().sort((a, b) => b.score - a.score)
    .map((device, index) => ({ ...device, rank: index + 1 }))

  return (
    <Flex vertical gap={16}>
      <Card title="设备排行" extra={<Button icon={<ReloadOutlined />} loading={state.status === 'loading'} onClick={() => {
        setState({ status: 'loading' })
        setAttempt((value) => value + 1)
      }}>刷新</Button>}>
        <Flex vertical gap={12}>
          <Typography.Text type="secondary">按设备共享记录的最高分排序。测试覆盖 CPU 和内存，GPU 栏显示上报的型号。</Typography.Text>
          {data && (
            <Flex gap={6} wrap="wrap">
              <Tag>参考设备 {data.referenceName} · 100 分</Tag>
              <Tag>工作负载 {data.workloadVersion}</Tag>
              <Tag>计分 {data.scoringVersion}</Tag>
            </Flex>
          )}
          {state.status === 'error' ? <Alert type="error" showIcon message={state.message} /> : (
            <DeviceRanking devices={devices} loading={state.status === 'loading'} />
          )}
        </Flex>
      </Card>

      <Drawer title="设备跑分历史" open={!!deviceId} width="min(1100px, 100vw)" onClose={() => {
        const nextParams = new URLSearchParams(searchParams)
        nextParams.delete('device')
        setSearchParams(nextParams, { replace: true })
      }}>
        {deviceId && <DeviceHistory key={deviceId} deviceId={deviceId} />}
      </Drawer>
    </Flex>
  )
}
