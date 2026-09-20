import {
  DesktopOutlined,
  KeyOutlined,
  MessageOutlined,
  ReloadOutlined,
  SafetyCertificateOutlined,
  WarningOutlined,
} from '@ant-design/icons'
import type { ColumnsType } from 'antd/es/table'
import {
  Alert,
  Button,
  Card,
  Col,
  Descriptions,
  Input,
  Row,
  Space,
  Statistic,
  Table,
  Tag,
  Typography,
} from 'antd'
import { useCallback, useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { getOperationsOverview } from '../services/admin'
import type {
  OperationsHost,
  OperationsOverview,
  OperationsRelayDevice,
  OperationsSupportSession,
  OperationsTask,
} from '../types/admin'
import { shortDate } from '../utils/format'
import {
  formatOperationsUptime,
  operationsCapabilityLabel,
  operationsHostStatus,
  operationsScopeLabel,
  operationsSupportStatus,
  operationsTaskSource,
  operationsTaskStatus,
} from '../utils/operations'

function hostDetails(host: OperationsHost) {
  const snapshot = host.snapshot
  return (
    <Space direction="vertical" size={12} style={{ width: '100%' }}>
      <Descriptions size="small" column={{ xs: 1, md: 2, xl: 4 }}>
        <Descriptions.Item label="应用状态">
          <Tag color={snapshot.isRunning ? 'green' : 'default'}>
            {snapshot.isRunning ? '运行中' : '未运行'}
          </Tag>
        </Descriptions.Item>
        <Descriptions.Item label="连续运行">
          {formatOperationsUptime(snapshot.uptimeSeconds)}
        </Descriptions.Item>
        <Descriptions.Item label="工作集">
          {snapshot.process.memoryMb > 0 ? `${snapshot.process.memoryMb.toFixed(1)} MB` : '-'}
        </Descriptions.Item>
        <Descriptions.Item label="主窗口">
          {snapshot.mainWindow.exists
            ? `${snapshot.mainWindow.state} · ${snapshot.mainWindow.isVisible ? '可见' : '隐藏'}`
            : '未检测到'}
        </Descriptions.Item>
        <Descriptions.Item label="安全运维通道">
          <Tag color={snapshot.secureOperations.isRunning ? 'green' : 'default'}>
            {snapshot.secureOperations.isRunning ? '运行中' : '未运行'}
          </Tag>
        </Descriptions.Item>
        <Descriptions.Item label="签名 Relay 身份">
          <Tag color={host.signedRelayReady ? 'blue' : 'default'}>
            {host.signedRelayReady ? '已建立' : '未建立'}
          </Tag>
        </Descriptions.Item>
        <Descriptions.Item label="已配对设备">
          {snapshot.secureOperations.pairedDeviceCount}
        </Descriptions.Item>
        <Descriptions.Item label="Relay 配置">
          {snapshot.secureOperations.relayConfigured ? '已配置' : '未配置'}
        </Descriptions.Item>
        <Descriptions.Item label="快照时间">
          {shortDate(snapshot.capturedAt)}
        </Descriptions.Item>
      </Descriptions>
      <Space wrap size={[4, 4]}>
        <Typography.Text type="secondary">能力目录：</Typography.Text>
        {host.capabilities.map((capability) => (
          <Tag key={capability}>{operationsCapabilityLabel(capability)}</Tag>
        ))}
        {host.capabilities.length === 0 && <Tag>未上报</Tag>}
      </Space>
    </Space>
  )
}

const hostColumns: ColumnsType<OperationsHost> = [
  {
    title: '终端',
    key: 'host',
    render: (_, host) => (
      <Space direction="vertical" size={0}>
        <Typography.Text strong>{host.displayName}</Typography.Text>
        <Typography.Text type="secondary" copyable={{ text: host.hostId }}>
          {host.hostId}
        </Typography.Text>
      </Space>
    ),
  },
  {
    title: '连接',
    key: 'status',
    width: 110,
    render: (_, host) => {
      const status = operationsHostStatus(host.online, host.reportedStatus)
      return <Tag color={status.color}>{status.label}</Tag>
    },
  },
  { title: '版本', dataIndex: 'appVersion', width: 120, render: (value) => value || '-' },
  {
    title: '安全通道',
    key: 'secure',
    width: 120,
    render: (_, host) => (
      <Tag color={host.snapshot.secureOperations.isRunning ? 'green' : 'default'}>
        {host.snapshot.secureOperations.isRunning ? '运行中' : '未运行'}
      </Tag>
    ),
  },
  { title: '能力', key: 'capabilities', width: 90, align: 'right', render: (_, host) => host.capabilities.length },
  { title: '最后心跳', dataIndex: 'lastSeenAt', width: 170, render: (value) => shortDate(value) },
  { title: '详情', key: 'detail', width: 100, render: (_, host) => <Button type="link" href={`/admin/operations/hosts?host=${encodeURIComponent(host.hostId)}`}>查看终端</Button> },
]

const taskColumns: ColumnsType<OperationsTask> = [
  { title: '创建时间', dataIndex: 'createdAt', width: 170, render: (value) => shortDate(value) },
  { title: '终端', dataIndex: 'hostName', width: 160 },
  {
    title: '来源',
    key: 'source',
    width: 150,
    render: (_, task) => (
      <Space direction="vertical" size={0}>
        <Tag color={task.sourceType === 'device' ? 'purple' : 'default'}>
          {operationsTaskSource(task.sourceType)}
        </Tag>
        {task.deviceName && <Typography.Text type="secondary">{task.deviceName}</Typography.Text>}
      </Space>
    ),
  },
  {
    title: '能力',
    dataIndex: 'capabilityId',
    render: (value) => (
      <Typography.Text title={value}>{operationsCapabilityLabel(value)}</Typography.Text>
    ),
  },
  {
    title: '状态',
    key: 'status',
    width: 120,
    render: (_, task) => {
      const status = operationsTaskStatus(task.status, task.expired)
      return <Tag color={status.color}>{status.label}</Tag>
    },
  },
  {
    title: '回执',
    key: 'receipt',
    width: 150,
    render: (_, task) => task.receiptCount > 0
      ? `${task.receiptCount} 条 · ${task.lastReceiptStatus ? operationsTaskStatus(task.lastReceiptStatus).label : '已记录'}`
      : '尚无回执',
  },
  {
    title: '任务 ID',
    dataIndex: 'taskId',
    width: 150,
    render: (value) => <Typography.Text code copyable={{ text: value }}>{value.slice(0, 10)}</Typography.Text>,
  },
]

const relayDeviceColumns: ColumnsType<OperationsRelayDevice> = [
  {
    title: '配对设备',
    key: 'device',
    width: 160,
    render: (_, device) => (
      <Space direction="vertical" size={0}>
        <Typography.Text strong>{device.displayName}</Typography.Text>
        <Typography.Text type="secondary" copyable={{ text: device.deviceId }}>
          {device.deviceId}
        </Typography.Text>
      </Space>
    ),
  },
  { title: '所属终端', dataIndex: 'hostName', width: 140 },
  {
    title: '状态',
    key: 'status',
    width: 85,
    render: (_, device) => <Tag color={device.active ? 'green' : 'default'}>{device.active ? '有效' : '已撤销'}</Tag>,
  },
  {
    title: '权限范围',
    key: 'scopes',
    width: 235,
    render: (_, device) => (
      <Space wrap size={[4, 4]}>
        {device.scopes.slice(0, 4).map((scope) => <Tag key={scope} title={scope}>{operationsScopeLabel(scope)}</Tag>)}
        {device.scopes.length > 4 && <Tag>+{device.scopes.length - 4}</Tag>}
        {device.scopes.length === 0 && <Tag>无</Tag>}
      </Space>
    ),
  },
  { title: '批准时间', dataIndex: 'approvedAt', width: 140, render: (value) => shortDate(value) },
  { title: '最后同步', dataIndex: 'updatedAt', width: 140, render: (value) => shortDate(value) },
]

const supportColumns: ColumnsType<OperationsSupportSession> = [
  { title: '最后活动', dataIndex: 'lastEventAt', width: 170, render: (value) => shortDate(value) },
  { title: '终端', dataIndex: 'hostName', width: 160 },
  {
    title: '状态',
    dataIndex: 'state',
    width: 140,
    render: (value) => {
      const status = operationsSupportStatus(value)
      return <Tag color={status.color}>{status.label}</Tag>
    },
  },
  { title: '事件', dataIndex: 'eventCount', width: 80, align: 'right' },
  { title: '消息', dataIndex: 'messageCount', width: 80, align: 'right' },
  {
    title: '会话 ID',
    dataIndex: 'sessionId',
    render: (value) => <Typography.Text code copyable={{ text: value }}>{value.slice(0, 12)}</Typography.Text>,
  },
]

export function OperationsPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const hostId = searchParams.get('host') || ''
  const [overview, setData] = useState<OperationsOverview | null>(null)
  const data = overview?.hostId === (hostId || null) ? overview : null
  const [loadError, setLoadError] = useState('')
  const [loading, setLoading] = useState(true)
  const [refreshing, setRefreshing] = useState(false)
  const controllerRef = useRef<AbortController | null>(null)

  const load = useCallback(async (initial = false) => {
    controllerRef.current?.abort()
    const controller = new AbortController()
    controllerRef.current = controller
    if (initial) {
      setLoading(true)
      setLoadError('')
    }
    else setRefreshing(true)
    try {
      const result = await getOperationsOverview(AbortSignal.any([controller.signal, AbortSignal.timeout(15000)]), { hostId })
      if (controller.signal.aborted) return
      setData(result)
      setLoadError('')
    } catch (error) {
      if (controller.signal.aborted) return
      setLoadError(error instanceof Error ? error.message : '加载终端运维状态失败')
    } finally {
      if (controllerRef.current === controller) {
        controllerRef.current = null
        setLoading(false)
        setRefreshing(false)
      }
    }
  }, [hostId])

  useEffect(() => {
    const initialTimer = window.setTimeout(() => void load(true), 0)
    const timer = window.setInterval(() => void load(false), 20_000)
    return () => {
      window.clearTimeout(initialTimer)
      window.clearInterval(timer)
      controllerRef.current?.abort()
    }
  }, [load])

  const summary = data?.summary
  const chooseHost = (value: string) => setSearchParams((previous) => {
    const next = new URLSearchParams(previous)
    if (value.trim()) next.set('host', value.trim())
    else next.delete('host')
    return next
  })
  return (
    <Space direction="vertical" size={16} className="page-stack">
      <Card title={hostId ? data?.hosts[0]?.displayName || '终端详情' : '终端运维'}>
        <Space direction="vertical" size={12} className="wide-space">
          <Input.Search key={hostId} defaultValue={hostId} placeholder="输入完整终端 ID，查看该终端" aria-label="终端 ID" allowClear enterButton="查看" onSearch={chooseHost} style={{ maxWidth: 540 }} />
          {hostId && <Space wrap>
            <Button onClick={() => chooseHost('')}>查看全部终端</Button>
            <Typography.Text copyable={{ text: window.location.href }}>复制终端详情链接</Typography.Text>
            <Typography.Text type="secondary" style={{ overflowWrap: 'anywhere' }}>{hostId}</Typography.Text>
          </Space>}
          <Typography.Text type="secondary">{hostId ? '以下统计、任务、配对设备和支持会话均属于当前终端。' : '统计覆盖全部终端；列表显示最近上报的 100 台终端及最近 100 条活动，可通过完整终端 ID 查找更早的记录。'}</Typography.Text>
        </Space>
      </Card>
      {loadError && <Alert type="error" showIcon message="终端状态更新失败" description={`${loadError}${data ? `；以下为 ${shortDate(data.generatedAt)} 的旧快照，当前连接状态尚未确认。` : '；当前状态未知。'}`} action={<Button onClick={() => void load(false)}>重试</Button>} />}
      <Alert
        type="info"
        showIcon
        icon={<SafetyCertificateOutlined />}
        message="只读终端运维总览"
        description="查看终端最近上报的运行状态和活动记录。心跳未更新可能来自关机或网络中断；任务历史中的失败记录需要结合具体时间判断。"
      />

      {data && data.summary.totalHosts === 0 && (
        <Alert
          type="warning"
          showIcon
          message="尚无终端连接 Web Relay"
          description="支持签名 Relay 的桌面端启动并完成首次同步后，终端和本机已批准的配对设备会自动出现在这里；旧版 API Key Relay 仍兼容。"
        />
      )}

      <Row gutter={[16, 16]}>
        <Col xs={12} xl={6}>
          <Card loading={loading}><Statistic title="已登记终端" value={summary?.totalHosts ?? '—'} prefix={<DesktopOutlined />} /></Card>
        </Col>
        <Col xs={12} xl={6}>
          <Card loading={loading}><Statistic title="在线终端" value={summary?.onlineHosts ?? '—'} valueStyle={{ color: summary?.onlineHosts && !loadError ? '#389e0d' : undefined }} /></Card>
        </Col>
        <Col xs={12} xl={6}>
          <Card loading={loading}><Statistic title="待处理任务" value={summary?.pendingTasks ?? '—'} prefix={<ReloadOutlined />} /></Card>
        </Col>
        <Col xs={12} xl={6}>
          <Card loading={loading}><Statistic title="活动支持会话" value={summary?.activeSupportSessions ?? '—'} prefix={<MessageOutlined />} /></Card>
        </Col>
      </Row>

      <Card
        title="签名设备 Relay"
        loading={loading}
        extra={(
          <Space wrap>
            <Tag icon={<SafetyCertificateOutlined />} color={summary ? 'blue' : 'default'}>{summary?.signedRelayHosts ?? '—'} 台终端已建立身份</Tag>
            <Tag icon={<KeyOutlined />} color={summary ? 'green' : 'default'}>{summary?.activeRelayDevices ?? '—'} 台有效设备</Tag>
            {(summary?.revokedRelayDevices ?? 0) > 0 && <Tag>{summary?.revokedRelayDevices} 台已撤销</Tag>}
          </Space>
        )}
      >
        <Table
          rowKey={(device) => `${device.hostId}:${device.deviceId}`}
          size="small"
          columns={relayDeviceColumns}
          dataSource={data?.relayDevices ?? []}
          pagination={{ pageSize: 10, hideOnSinglePage: true }}
          locale={{ emptyText: '尚无签名 Relay 配对设备' }}
          scroll={{ x: 900 }}
        />
      </Card>

      {summary && summary.staleHosts > 0 && (
        <Alert
          type="warning"
          showIcon
          icon={<WarningOutlined />}
          message={`${summary.staleHosts} 台终端心跳未更新`}
          description="请先核对是否正常关机，再检查网络与桌面端运行状态。"
        />
      )}

      <Card
        title="Relay 终端"
        loading={loading}
        extra={(
          <Space wrap>
            <Typography.Text type="secondary">
              {data ? `${data.onlineThresholdSeconds} 秒无心跳视为未连接 · 更新于 ${shortDate(data.generatedAt)}` : loading ? '加载中' : '状态未知'}
            </Typography.Text>
            <Button icon={<ReloadOutlined />} loading={refreshing} onClick={() => void load(false)}>刷新</Button>
          </Space>
        )}
      >
        <Table
          rowKey="hostId"
          size="small"
          columns={hostColumns}
          dataSource={data?.hosts ?? []}
          pagination={false}
          locale={{ emptyText: '尚无终端心跳' }}
          expandable={{ expandedRowRender: hostDetails, ...(hostId ? { expandedRowKeys: [hostId] } : {}) }}
          scroll={{ x: 900 }}
        />
      </Card>

      <Card
        title="最近任务"
        loading={loading}
        extra={(
          <Space wrap>
            <Tag>历史失败或拒绝 {summary?.failedTasks ?? '—'} 次</Tag>
            <Tag color="purple">{summary?.deviceTasks ?? '—'} 个来自配对设备</Tag>
            <Tag>不显示任务输入与回执详情</Tag>
          </Space>
        )}
      >
        <Table
          rowKey="taskId"
          size="small"
          columns={taskColumns}
          dataSource={data?.recentTasks ?? []}
          pagination={{ pageSize: 10, hideOnSinglePage: true }}
          locale={{ emptyText: '尚无 Relay 任务' }}
          scroll={{ x: 1130 }}
        />
      </Card>

      <Card title="支持会话" loading={loading} extra={<Tag>仅状态与计数</Tag>}>
        <Table
          rowKey={(session) => `${session.hostId}:${session.sessionId}`}
          size="small"
          columns={supportColumns}
          dataSource={data?.supportSessions ?? []}
          pagination={{ pageSize: 10, hideOnSinglePage: true }}
          locale={{ emptyText: '尚无支持会话' }}
          scroll={{ x: 820 }}
        />
      </Card>
    </Space>
  )
}
