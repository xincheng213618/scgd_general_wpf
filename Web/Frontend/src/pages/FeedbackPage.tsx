import {
  CheckCircleOutlined,
  DownloadOutlined,
  InboxOutlined,
  LoadingOutlined,
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
  Space,
  Statistic,
  Tag,
  Typography,
} from 'antd'
import { useEffect, useRef, useState } from 'react'
import {
  feedbackAttachmentUrl,
  getFeedbackDetail,
  getFeedbackInbox,
  updateFeedbackStatus,
} from '../services/admin'
import type {
  FeedbackDetail,
  FeedbackInboxResponse,
  FeedbackItem,
  FeedbackStatus,
} from '../types/admin'
import {
  feedbackStatusAction,
  feedbackStatusColors,
  feedbackStatusLabels,
  nextFeedbackStatus,
} from '../utils/feedback'
import { humanSize, shortDate } from '../utils/format'

const columnsBase: ProColumns<FeedbackItem>[] = [
  {
    title: '状态',
    dataIndex: 'status',
    width: 110,
    valueType: 'select',
    valueEnum: {
      new: { text: feedbackStatusLabels.new },
      in_progress: { text: feedbackStatusLabels.in_progress },
      resolved: { text: feedbackStatusLabels.resolved },
    },
    render: (_, record) => (
      <Tag color={feedbackStatusColors[record.status]}>{feedbackStatusLabels[record.status]}</Tag>
    ),
  },
  {
    title: '提交时间',
    dataIndex: 'created_at',
    width: 170,
    search: false,
    renderText: (value: string) => shortDate(value),
  },
  {
    title: '提交者',
    dataIndex: 'user_name',
    width: 150,
    search: false,
    renderText: (value: string) => value || '未提供',
  },
  {
    title: '版本',
    dataIndex: 'app_version',
    width: 130,
    search: false,
    renderText: (value: string) => value || '-',
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

export function FeedbackPage() {
  const { message } = App.useApp()
  const actionRef = useRef<ActionType>(null)
  const detailRequestRef = useRef<AbortController | null>(null)
  const [summary, setSummary] = useState<FeedbackInboxResponse['summary']>({
    records: 0,
    status_counts: { new: 0, in_progress: 0, resolved: 0 },
    attachment_count: 0,
    attachment_bytes: 0,
    invalid_metadata: 0,
    invalid_state: 0,
  })
  const [detail, setDetail] = useState<FeedbackDetail | null>(null)
  const [detailError, setDetailError] = useState('')
  const [detailLoading, setDetailLoading] = useState(false)
  const [updating, setUpdating] = useState(false)

  const openDetail = async (feedbackId: string) => {
    detailRequestRef.current?.abort()
    const controller = new AbortController()
    detailRequestRef.current = controller
    setDetail(null)
    setDetailError('')
    setDetailLoading(true)
    try {
      setDetail(await getFeedbackDetail(feedbackId, controller.signal))
    } catch (error) {
      if (controller.signal.aborted) return
      setDetailError(error instanceof Error ? error.message : '加载反馈详情失败')
    } finally {
      if (!controller.signal.aborted) setDetailLoading(false)
    }
  }

  useEffect(() => () => detailRequestRef.current?.abort(), [])

  const changeStatus = async (status: FeedbackStatus) => {
    if (!detail) return
    setUpdating(true)
    try {
      setDetail(await updateFeedbackStatus(detail.feedback_id, status))
      message.success(`反馈已标记为${feedbackStatusLabels[status]}`)
      actionRef.current?.reload()
    } catch (error) {
      message.error(error instanceof Error ? error.message : '更新反馈状态失败')
    } finally {
      setUpdating(false)
    }
  }

  const columns: ProColumns<FeedbackItem>[] = [
    ...columnsBase,
    {
      title: '操作',
      search: false,
      width: 90,
      fixed: 'right',
      render: (_, record) => (
        <Button type="link" onClick={() => void openDetail(record.feedback_id)}>查看</Button>
      ),
    },
  ]
  const nextStatus = detail ? nextFeedbackStatus(detail.status) : null

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <Row gutter={[16, 16]}>
        <Col xs={12} lg={6}><Card><Statistic title="全部反馈" value={summary.records} prefix={<InboxOutlined />} /></Card></Col>
        <Col xs={12} lg={6}><Card><Statistic title="待处理" value={summary.status_counts.new} valueStyle={{ color: summary.status_counts.new ? '#cf1322' : undefined }} /></Card></Col>
        <Col xs={12} lg={6}><Card><Statistic title="处理中" value={summary.status_counts.in_progress} prefix={<LoadingOutlined />} /></Card></Col>
        <Col xs={12} lg={6}><Card><Statistic title="诊断附件" value={summary.attachment_count} suffix={humanSize(summary.attachment_bytes)} /></Card></Col>
      </Row>
      {(summary.invalid_metadata > 0 || summary.invalid_state > 0) && (
        <Alert
          type="warning"
          showIcon
          message="存在需要人工检查的历史记录"
          description={`元数据异常 ${summary.invalid_metadata} 条，处理状态异常 ${summary.invalid_state} 条；这些记录仍保留并可查看附件。`}
        />
      )}
      <ProTable<FeedbackItem>
        actionRef={actionRef}
        rowKey="feedback_id"
        columns={columns}
        request={async (params) => {
          const result = await getFeedbackInbox({
            current: params.current,
            pageSize: params.pageSize,
            status: params.status as FeedbackStatus | undefined,
            query: params.query as string | undefined,
          })
          setSummary(result.summary)
          return { data: result.items, success: true, total: result.total }
        }}
        pagination={{ pageSize: 20, showSizeChanger: true, showTotal: (total) => `共 ${total} 条` }}
        options={{ density: true, fullScreen: true, reload: true, setting: true }}
        cardBordered
        headerTitle="反馈收件箱"
        toolBarRender={() => [
          <Typography.Text type="secondary" key="privacy">详情和附件仅对管理员开放，下载会写入审计日志</Typography.Text>,
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
            {detail.status === 'resolved' && <Button onClick={() => void changeStatus('in_progress')} loading={updating}>重新打开</Button>}
            {nextStatus && (
              <Button
                type="primary"
                icon={nextStatus === 'resolved' ? <CheckCircleOutlined /> : undefined}
                loading={updating}
                onClick={() => void changeStatus(nextStatus)}
              >
                {feedbackStatusAction(detail.status)}
              </Button>
            )}
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
              <Descriptions.Item label="提交时间">{shortDate(detail.created_at)}</Descriptions.Item>
              <Descriptions.Item label="提交者">{detail.user_name || '未提供'}</Descriptions.Item>
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
                        href={feedbackAttachmentUrl(detail.feedback_id, attachment.name)}
                      >
                        下载
                      </Button>,
                    ]}
                  >
                    <List.Item.Meta
                      title={attachment.name}
                      description={`${humanSize(attachment.size_bytes)} · ${shortDate(attachment.modified_at)}`}
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
