import {
  CheckCircleOutlined,
  ClockCircleOutlined,
  DownloadOutlined,
  InboxOutlined,
  ReloadOutlined,
  WarningOutlined,
} from '@ant-design/icons'
import { ProTable, type ActionType, type ProColumns } from '@ant-design/pro-components'
import {
  Alert,
  App,
  Button,
  Card,
  Col,
  Descriptions,
  Drawer,
  List,
  Row,
  Segmented,
  Space,
  Spin,
  Statistic,
  Tag,
  Typography,
} from 'antd'
import { useEffect, useRef, useState } from 'react'
import {
  downloadFeedbackAttachment,
  getFeedbackDetail,
  getFeedbackInbox,
  updateFeedbackStatus,
  updateFeedbackStatuses,
  type FeedbackInboxParams,
} from '../services/admin'
import type {
  FeedbackDetail,
  FeedbackInboxFilter,
  FeedbackInboxResponse,
  FeedbackItem,
  FeedbackStatus,
} from '../types/admin'
import type { AuthSession } from '../types/site'
import {
  feedbackAgeInfo,
  feedbackBeijingTime,
  feedbackDateRangeUtc,
  feedbackStatusColors,
  feedbackStatusLabels,
  applyFeedbackStatusUpdate,
} from '../utils/feedback'
import { humanSize, shortDate } from '../utils/format'
import { Navigate } from 'react-router-dom'

const columnsBase: ProColumns<FeedbackItem>[] = [
  {
    title: '状态',
    dataIndex: 'status',
    width: 110,
    search: false,
    render: (_, record) => (
      <Tag color={feedbackStatusColors[record.status]}>{feedbackStatusLabels[record.status]}</Tag>
    ),
  },
  {
    title: '接收时间与等待',
    dataIndex: 'created_at',
    width: 180,
    search: false,
    render: (_, record) => {
      const age = feedbackAgeInfo(record.status, record.created_at)
      return (
        <Space direction="vertical" size={2}>
          <Typography.Text>{feedbackBeijingTime(record.created_at)}</Typography.Text>
          <Tag color={age.color}>{age.label}</Tag>
        </Space>
      )
    },
  },
  {
    title: '提交者',
    dataIndex: 'user_name',
    width: 150,
    search: false,
    render: (_, record) => (
      <Space direction="vertical" size={2}>
        <Typography.Text>{record.owner_username || record.user_name || '未提供'}</Typography.Text>
        {record.ownership === 'legacy_unbound' && <Tag color="default">历史未绑定</Tag>}
      </Space>
    ),
  },
  {
    title: '机器',
    dataIndex: 'machine',
    width: 170,
    fieldProps: { placeholder: '机器名' },
    render: (_, record) => record.machine_name || '未知机器',
  },
  {
    title: '版本',
    dataIndex: 'app_version',
    width: 130,
    fieldProps: { placeholder: '版本号' },
    render: (_, record) => record.app_version || '-',
  },
  {
    title: '接收日期（北京时间）',
    dataIndex: 'receivedRange',
    valueType: 'dateRange',
    hideInTable: true,
  },
  {
    title: '内容',
    dataIndex: 'query',
    ellipsis: true,
    fieldProps: { placeholder: '编号、提交者、版本或问题描述' },
    render: (_, record) => (
      <Space direction="vertical" size={2}>
        <Typography.Text>{record.message_preview || '仅包含诊断附件'}</Typography.Text>
        <Typography.Text type="secondary" code>{record.feedback_id}</Typography.Text>
      </Space>
    ),
  },
  {
    title: '附件',
    search: false,
    width: 130,
    render: (_, record) => (
      <Typography.Text>{record.attachment_count} 个 · {humanSize(record.attachment_bytes)}</Typography.Text>
    ),
  },
  {
    title: '完整性',
    search: false,
    width: 100,
    render: (_, record) => record.metadata_valid && record.state_valid
      ? <Tag color="green">正常</Tag>
      : <Tag color="orange" icon={<WarningOutlined />}>需检查</Tag>,
  },
]

export function FeedbackPage({ session }: { session: AuthSession | null }) {
  const { message, modal } = App.useApp()
  const actionRef = useRef<ActionType>(null)
  const detailRequestRef = useRef<AbortController | null>(null)
  const listRequestRef = useRef<AbortController | null>(null)
  const activeQueryRef = useRef<FeedbackInboxParams>({})
  const mutationRef = useRef(false)
  const [summary, setSummary] = useState<FeedbackInboxResponse['summary']>({
    records: 0,
    status_counts: { new: 0, in_progress: 0, resolved: 0 },
    attachment_count: 0,
    attachment_bytes: 0,
    invalid_metadata: 0,
    invalid_state: 0,
    oldest_open_at: null,
  })
  const [statusFilter, setStatusFilter] = useState<FeedbackInboxFilter>('open')
  const [detail, setDetail] = useState<FeedbackDetail | null>(null)
  const [detailError, setDetailError] = useState('')
  const [detailLoading, setDetailLoading] = useState(false)
  const [updating, setUpdating] = useState('')
  const [listLoading, setListLoading] = useState(false)
  const [listError, setListError] = useState('')
  const [selectedIds, setSelectedIds] = useState<string[]>([])
  const [bulkPreparing, setBulkPreparing] = useState(false)
  const [canManage, setCanManage] = useState(false)
  const [downloading, setDownloading] = useState('')
  const [accessScope, setAccessScope] = useState<'own' | 'all'>('own')

  const openDetail = async (feedbackId: string) => {
    detailRequestRef.current?.abort()
    const controller = new AbortController()
    detailRequestRef.current = controller
    setDetail(null)
    setDetailError('')
    setDetailLoading(true)
    try {
      const result = await getFeedbackDetail(feedbackId, AbortSignal.any([controller.signal, AbortSignal.timeout(15000)]))
      if (!controller.signal.aborted) setDetail(result)
    } catch (error) {
      if (controller.signal.aborted) return
      setDetailError(error instanceof Error ? error.message : '加载反馈详情失败')
    } finally {
      if (!controller.signal.aborted) setDetailLoading(false)
    }
  }

  useEffect(() => () => {
    detailRequestRef.current?.abort()
    listRequestRef.current?.abort()
  }, [])

  const refresh = async () => {
    await actionRef.current?.reload()
  }

  const changeStatus = async (feedbackId: string, status: FeedbackStatus) => {
    if (mutationRef.current) return
    mutationRef.current = true
    setUpdating(feedbackId)
    try {
      const result = await updateFeedbackStatus(feedbackId, status)
      setDetail((current) => applyFeedbackStatusUpdate(current, result))
      message.success(`反馈已标记为${feedbackStatusLabels[status]}`)
      setSelectedIds((current) => current.filter((id) => id !== feedbackId))
      await refresh()
    } catch (error) {
      message.error(error instanceof Error ? error.message : '更新反馈状态失败')
    } finally {
      mutationRef.current = false
      setUpdating('')
    }
  }

  const resolveSelection = async (identifiers: string[]) => {
    if (mutationRef.current) return
    mutationRef.current = true
    setUpdating('bulk')
    try {
      const result = await updateFeedbackStatuses(identifiers, 'resolved')
      for (const item of result.results) {
        if (!('error' in item)) setDetail((current) => applyFeedbackStatusUpdate(current, item))
      }
      const failedIds = result.results.filter((item) => 'error' in item).map((item) => item.feedback_id)
      setSelectedIds(failedIds)
      if (result.failed) {
        message.warning(`已解决 ${result.changed + result.unchanged} 条，失败 ${result.failed} 条；失败项仍保留勾选，可重试`)
      } else {
        message.success(`已将 ${result.changed + result.unchanged} 条反馈标记为已解决`)
      }
      await refresh()
    } catch (error) {
      message.error(error instanceof Error ? error.message : '批量更新失败，请刷新核对后重试')
    } finally {
      mutationRef.current = false
      setUpdating('')
    }
  }

  const confirmResolution = (identifiers: string[]) => {
    modal.confirm({
      title: `将这 ${identifiers.length} 条反馈标记为已解决？`,
      content: '仅修改处理状态，原始反馈和附件继续保留。之后可以重新打开；新收到的反馈不会包含在本次操作中。',
      okText: '全部标记已解决',
      cancelText: '取消',
      onOk: () => resolveSelection(identifiers),
    })
  }

  const prepareResolveAll = async () => {
    setBulkPreparing(true)
    try {
      const params = { ...activeQueryRef.current, status: 'open' as const, current: 1, pageSize: 100 }
      const signal = AbortSignal.timeout(15000)
      const first = await getFeedbackInbox(params, signal)
      if (!first.total) { message.info('当前筛选下没有未解决反馈'); return }
      if (first.total > 500) { message.info('请先按机器、版本或日期筛选到 500 条以内'); return }
      const identifiers = first.items.map((item) => item.feedback_id)
      for (let current = 2; identifiers.length < first.total; current++) {
        const page = await getFeedbackInbox({ ...params, current }, signal)
        if (page.total !== first.total || !page.items.length) throw new Error('反馈列表已变化，请刷新后重试')
        identifiers.push(...page.items.map((item) => item.feedback_id))
      }
      if (new Set(identifiers).size !== first.total) throw new Error('反馈列表已变化，请刷新后重试')
      confirmResolution(identifiers)
    } catch (error) {
      message.error(error instanceof Error ? error.message : '获取待处理反馈失败')
    } finally {
      setBulkPreparing(false)
    }
  }

  if (session === null) return <Spin tip="正在验证登录状态…" />
  if (!session.authenticated) return <Navigate to="/login?next=/feedback" replace />

  const columns: ProColumns<FeedbackItem>[] = [
    ...columnsBase,
    {
      title: '操作',
      search: false,
      width: canManage ? 190 : 90,
      fixed: 'right',
      render: (_, record) => (
        <Space size={0}>
          <Button type="link" onClick={() => void openDetail(record.feedback_id)}>详情</Button>
          {canManage && (
            <Button type="link" disabled={Boolean(updating) || bulkPreparing} loading={updating === record.feedback_id}
              onClick={() => void changeStatus(record.feedback_id, record.status === 'resolved' ? 'in_progress' : 'resolved')}>
              {record.status === 'resolved' ? '重新打开' : '标记已解决'}
            </Button>
          )}
        </Space>
      ),
    },
  ]
  const openCount = summary.status_counts.new + summary.status_counts.in_progress
  const oldestOpenAge = summary.oldest_open_at
    ? feedbackAgeInfo('new', summary.oldest_open_at)
    : null

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <Row gutter={[16, 16]}>
        <Col xs={12} lg={6}><Card><Statistic title="未解决" value={openCount} prefix={<InboxOutlined />} valueStyle={{ color: openCount ? '#cf1322' : undefined }} /></Card></Col>
        <Col xs={12} lg={6}><Card><Statistic title="新反馈" value={summary.status_counts.new} valueStyle={{ color: summary.status_counts.new ? '#cf1322' : undefined }} /></Card></Col>
        <Col xs={12} lg={6}><Card><Statistic title="处理中" value={summary.status_counts.in_progress} prefix={<ClockCircleOutlined />} /></Card></Col>
        <Col xs={12} lg={6}><Card><Statistic title="已解决" value={summary.status_counts.resolved} prefix={<CheckCircleOutlined />} /></Card></Col>
      </Row>
      {openCount > 0 && oldestOpenAge && (
        <Alert
          type={oldestOpenAge.color === 'red' || oldestOpenAge.color === 'orange' ? 'warning' : 'info'}
          showIcon
          message={`当前有 ${openCount} 条未解决反馈，最久一条已${oldestOpenAge.label}。可直接标记已解决，也可勾选批量处理。`}
        />
      )}
      {(summary.invalid_metadata > 0 || summary.invalid_state > 0) && (
        <Alert
          type="warning"
          showIcon
          message={`历史记录完整性：元数据异常 ${summary.invalid_metadata} 条，处理状态异常 ${summary.invalid_state} 条；附件仍保留，处理状态可正常更新。`}
        />
      )}
      {listError && <Alert type="error" showIcon message="刷新反馈失败" description={listError}
        action={<Button size="small" onClick={() => void refresh()}>重试</Button>} />}
      <Segmented<FeedbackInboxFilter>
        value={statusFilter}
        onChange={(value) => { setSelectedIds([]); setStatusFilter(value) }}
        options={[
          { label: `未解决 (${openCount})`, value: 'open' },
          { label: `新反馈 (${summary.status_counts.new})`, value: 'new' },
          { label: `处理中 (${summary.status_counts.in_progress})`, value: 'in_progress' },
          { label: `已解决 (${summary.status_counts.resolved})`, value: 'resolved' },
          { label: `全部 (${summary.records})`, value: 'all' },
        ]}
      />
      <ProTable<FeedbackItem>
        actionRef={actionRef}
        rowKey="feedback_id"
        columns={columns}
        params={{ inboxStatus: statusFilter }}
        request={async (params) => {
          listRequestRef.current?.abort()
          const controller = new AbortController()
          listRequestRef.current = controller
          const query: FeedbackInboxParams = {
            current: params.current,
            pageSize: params.pageSize,
            status: params.inboxStatus as FeedbackInboxFilter,
            query: params.query as string | undefined,
            machine: params.machine as string | undefined,
            appVersion: params.app_version as string | undefined,
            ...feedbackDateRangeUtc(params.receivedRange),
          }
          const result = await getFeedbackInbox(query, AbortSignal.any([controller.signal, AbortSignal.timeout(15000)]))
          if (!controller.signal.aborted) {
            activeQueryRef.current = query
            setSummary(result.summary)
            setAccessScope(result.access.scope)
            setCanManage(result.access.can_manage)
            setListError('')
          }
          return { data: result.items, success: true, total: result.total }
        }}
        onLoadingChange={(loading) => setListLoading(Boolean(loading))}
        onSubmit={() => setSelectedIds([])}
        onReset={() => setSelectedIds([])}
        onRequestError={(error) => {
          if (error.name !== 'AbortError') setListError(error.name === 'TimeoutError' ? '请求超时，请重试' : error.message)
        }}
        rowSelection={canManage ? {
          selectedRowKeys: selectedIds,
          preserveSelectedRowKeys: true,
          onChange: (keys) => setSelectedIds(keys.map(String)),
          getCheckboxProps: () => ({ disabled: Boolean(updating) || bulkPreparing }),
        } : false}
        tableAlertOptionRender={() => <Button type="link" onClick={() => setSelectedIds([])}>清空选择</Button>}
        pagination={{ defaultPageSize: 20, showSizeChanger: true, showTotal: (total) => `共 ${total} 条` }}
        locale={{ emptyText: statusFilter === 'open' ? '暂无未解决反馈，可切换到“已解决”查看历史记录' : '没有符合条件的反馈' }}
        options={{ density: true, fullScreen: true, reload: false, setting: true }}
        cardBordered
        headerTitle={accessScope === 'own' ? '我的反馈' : '反馈收件箱'}
        toolBarRender={() => [
          <Button key="refresh" icon={<ReloadOutlined />} disabled={Boolean(updating)}
            loading={listLoading} onClick={() => void refresh()}>刷新</Button>,
          canManage && selectedIds.length > 0 && <Button key="selected" disabled={selectedIds.length > 500 || Boolean(updating) || bulkPreparing}
            onClick={() => confirmResolution([...selectedIds])}>选中项标记已解决 ({selectedIds.length})</Button>,
          canManage && <Button key="resolve-all" type="primary" icon={<CheckCircleOutlined />}
            loading={bulkPreparing || updating === 'bulk'} disabled={listLoading || Boolean(updating) || !openCount}
            onClick={() => void prepareResolveAll()}>一键解决当前筛选</Button>,
        ]}
        scroll={{ x: 1150 }}
      />
      <Drawer
        title={detail ? `反馈 ${detail.feedback_id}` : '反馈详情'}
        width={640}
        open={detailLoading || Boolean(detail) || Boolean(detailError)}
        onClose={() => {
          detailRequestRef.current?.abort()
          setDetail(null)
          setDetailError('')
          setDetailLoading(false)
        }}
        loading={detailLoading}
        extra={detail && (
          <Space>
            <Button icon={<ReloadOutlined />} disabled={Boolean(updating)} onClick={() => void openDetail(detail.feedback_id)}>刷新</Button>
            {detail.access.can_manage && <>
              {detail.status !== 'in_progress' && <Button disabled={Boolean(updating)} onClick={() => void changeStatus(detail.feedback_id, 'in_progress')}>
                {detail.status === 'resolved' ? '重新打开' : '开始处理'}
              </Button>}
              {detail.status !== 'resolved' && <Button type="primary" icon={<CheckCircleOutlined />}
                loading={updating === detail.feedback_id} disabled={Boolean(updating)}
                onClick={() => void changeStatus(detail.feedback_id, 'resolved')}>标记已解决</Button>}
            </>}
          </Space>
        )}
      >
        {detailError && <Alert type="error" showIcon message="反馈详情加载失败" description={detailError} />}
        {detail && (
          <Space direction="vertical" size="large" style={{ width: '100%' }}>
            {!detail.metadata_valid && (
              <Alert type="warning" showIcon message="历史记录缺少有效元数据" description="附件仍可下载，提交者、版本和问题描述可能为空。" />
            )}
            <Descriptions bordered size="small" column={1}>
              <Descriptions.Item label="状态"><Tag color={feedbackStatusColors[detail.status]}>{feedbackStatusLabels[detail.status]}</Tag></Descriptions.Item>
              <Descriptions.Item label="服务端接收时间（北京时间）">{feedbackBeijingTime(detail.created_at)}</Descriptions.Item>
              <Descriptions.Item label="提交者">{detail.user_name || '未提供'}</Descriptions.Item>
              <Descriptions.Item label="账号归属">
                {detail.owner_username || (detail.ownership === 'legacy_unbound' ? '历史未绑定' : '未提供')}
              </Descriptions.Item>
              <Descriptions.Item label="机器">{detail.machine_name || '未知机器'}</Descriptions.Item>
              <Descriptions.Item label="应用版本">{detail.app_version || '未提供'}</Descriptions.Item>
              <Descriptions.Item label="机器信息">{detail.machine_info || '未提供'}</Descriptions.Item>
              <Descriptions.Item label="客户端标识">{detail.client_ip || '未提供'}</Descriptions.Item>
            </Descriptions>
            <Card size="small" title="问题描述">
              <Typography.Paragraph style={{ whiteSpace: 'pre-wrap', marginBottom: 0 }}>
                {detail.message || '提交时未填写问题描述。'}
              </Typography.Paragraph>
            </Card>
            <Card size="small" title={`诊断附件（${detail.attachments.length}）`}>
              <List
                dataSource={detail.attachments}
                locale={{ emptyText: '没有诊断附件' }}
                renderItem={(attachment) => (
                  <List.Item
                    actions={[
                      <Button
                        key="download"
                        type="link"
                        icon={<DownloadOutlined />}
                        loading={downloading === attachment.name}
                        onClick={async () => {
                          setDownloading(attachment.name)
                          try {
                            await downloadFeedbackAttachment(detail.feedback_id, attachment.name)
                          } catch (error) {
                            message.error(error instanceof Error ? error.message : '附件下载失败')
                          } finally {
                            setDownloading('')
                          }
                        }}
                      >
                        下载
                      </Button>,
                    ]}
                  >
                    <List.Item.Meta
                      title={attachment.name}
                      description={`${humanSize(attachment.size_bytes)} · ${shortDate(attachment.modified_at)}${attachment.sha256 ? ` · SHA-256 ${attachment.sha256.slice(0, 12)}…` : ''}`}
                    />
                  </List.Item>
                )}
              />
            </Card>
          </Space>
        )}
      </Drawer>
    </Space>
  )
}
