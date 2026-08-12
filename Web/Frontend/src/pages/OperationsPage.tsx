import {
  DesktopOutlined,
  MessageOutlined,
  ReloadOutlined,
  SafetyCertificateOutlined,
  WarningOutlined,
} from '@ant-design/icons'
import type { ColumnsType } from 'antd/es/table'
import {
  Alert,
  App,
  Button,
  Card,
  Col,
  Descriptions,
  Row,
  Space,
  Statistic,
  Table,
  Tag,
  Typography,
} from 'antd'
import { useCallback, useEffect, useRef, useState } from 'react'
import { getOperationsOverview } from '../services/admin'
import type {
  OperationsHost,
  OperationsOverview,
  OperationsSupportSession,
  OperationsTask,
} from '../types/admin'
import { shortDate } from '../utils/format'
import {
  formatOperationsUptime,
  operationsCapabilityLabel,
  operationsHostStatus,
  operationsSupportStatus,
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
]

const taskColumns: ColumnsType<OperationsTask> = [
  { title: '创建时间', dataIndex: 'createdAt', width: 170, render: (value) => shortDate(value) },
  { title: '终端', dataIndex: 'hostName', width: 160 },
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
  const { message } = App.useApp()
  const [data, setData] = useState<OperationsOverview | null>(null)
  const [loading, setLoading] = useState(true)
  const [refreshing, setRefreshing] = useState(false)
  const controllerRef = useRef<AbortController | null>(null)

  const load = useCallback(async (initial = false) => {
    controllerRef.current?.abort()
    const controller = new AbortController()
    controllerRef.current = controller
    if (initial) setLoading(true)
    else setRefreshing(true)
    try {
      setData(await getOperationsOverview(controller.signal))
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') return
      message.error(error instanceof Error ? error.message : '加载终端运维状态失败')
    } finally {
      if (controllerRef.current === controller) {
        controllerRef.current = null
        setLoading(false)
        setRefreshing(false)
      }
    }
  }, [message])

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
  return (
    <Space direction="vertical" size={16} className="page-stack">
      <Alert
        type="info"
        showIcon
        icon={<SafetyCertificateOutlined />}
        message="只读终端运维总览"
        description="本页只展示经过固定字段裁剪的安全快照、任务状态和会话计数，不返回任务 payload、回执 evidence 或支持消息正文，也不会创建任务或发送消息。"
      />

      {data && data.summary.totalHosts === 0 && (
        <Alert
          type="warning"
          showIcon
          message="尚无终端连接 Web Relay"
          description="桌面端配置 COLORVISION_OPERATIONS_RELAY_URL 与 ops:relay API Key 并发出首次心跳后，终端会自动出现在这里。"
          action={<Button size="small" href="/admin/api-keys">管理 API Key</Button>}
        />
      )}

      <Row gutter={[16, 16]}>
        <Col xs={12} xl={6}>
          <Card loading={loading}><Statistic title="已登记终端" value={summary?.totalHosts ?? 0} prefix={<DesktopOutlined />} /></Card>
        </Col>
        <Col xs={12} xl={6}>
          <Card loading={loading}><Statistic title="在线终端" value={summary?.onlineHosts ?? 0} valueStyle={{ color: summary?.onlineHosts ? '#389e0d' : undefined }} /></Card>
        </Col>
        <Col xs={12} xl={6}>
          <Card loading={loading}><Statistic title="待处理任务" value={summary?.pendingTasks ?? 0} prefix={<ReloadOutlined />} /></Card>
        </Col>
        <Col xs={12} xl={6}>
          <Card loading={loading}><Statistic title="活动支持会话" value={summary?.activeSupportSessions ?? 0} prefix={<MessageOutlined />} /></Card>
        </Col>
      </Row>

      {summary && (summary.staleHosts > 0 || summary.failedTasks > 0) && (
        <Alert
          type={summary.failedTasks > 0 ? 'error' : 'warning'}
          showIcon
          icon={<WarningOutlined />}
          message={`需要关注：${summary.staleHosts} 台终端未连接，${summary.failedTasks} 个任务失败或被拒绝`}
        />
      )}

      <Card
        title="Relay 终端"
        loading={loading}
        extra={(
          <Space wrap>
            <Typography.Text type="secondary">
              {data ? `${data.onlineThresholdSeconds} 秒无心跳视为未连接 · 更新于 ${shortDate(data.generatedAt)}` : '加载中'}
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
          expandable={{ expandedRowRender: hostDetails }}
          scroll={{ x: 900 }}
        />
      </Card>

      <Card title="最近任务" loading={loading} extra={<Tag>不显示任务输入与回执详情</Tag>}>
        <Table
          rowKey="taskId"
          size="small"
          columns={taskColumns}
          dataSource={data?.recentTasks ?? []}
          pagination={{ pageSize: 10, hideOnSinglePage: true }}
          locale={{ emptyText: '尚无 Relay 任务' }}
          scroll={{ x: 980 }}
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
