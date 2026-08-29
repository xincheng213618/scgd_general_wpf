import { LockOutlined, UnlockOutlined } from '@ant-design/icons'
import {
  ProTable,
  type ActionType,
  type ProColumns,
} from '@ant-design/pro-components'
import { Alert, App, Button, Popconfirm, Space, Tag, Typography } from 'antd'
import { useEffect, useRef, useState } from 'react'
import { clearRegistrationSecurity, listRegistrationSecurity } from '../services/admin'
import type {
  RegistrationSecurityEntry,
  RegistrationSecurityStatus,
  RegistrationSecuritySummary,
} from '../types/admin'
import {
  formatLoginRetryCountdown,
  loginLockRemainingSeconds,
} from '../utils/authSecurity'
import { shortDate } from '../utils/format'
import {
  registrationClearSuccessMessage,
  registrationLimitReasonLabel,
} from '../utils/loginSecurity'

export function RegistrationSecurityCard() {
  const { message } = App.useApp()
  const actionRef = useRef<ActionType>(null)
  const [summary, setSummary] = useState<RegistrationSecuritySummary | null>(null)
  const [clock, setClock] = useState(() => Date.now())
  const [nextRefreshAt, setNextRefreshAt] = useState<number | null>(null)

  useEffect(() => {
    const timer = window.setInterval(() => setClock(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [])

  useEffect(() => {
    if (nextRefreshAt === null) return undefined
    const delay = Math.max(250, nextRefreshAt - Date.now() + 250)
    const timer = window.setTimeout(() => actionRef.current?.reload(), delay)
    return () => window.clearTimeout(timer)
  }, [nextRefreshAt])

  const columns: ProColumns<RegistrationSecurityEntry>[] = [
    {
      title: '来源地址',
      dataIndex: 'q',
      hideInTable: true,
      order: 2,
      fieldProps: { allowClear: true, placeholder: 'IP 地址' },
    },
    {
      title: '来源地址',
      dataIndex: 'ip_address',
      search: false,
      width: 190,
      copyable: true,
    },
    {
      title: '状态',
      dataIndex: 'status',
      valueType: 'select',
      width: 210,
      valueEnum: {
        blocked: { text: '已限制' },
        tracking: { text: '计数中' },
      },
      render: (_, record) => {
        if (!record.blocked) {
          return (
            <Space direction="vertical" size={2}>
              <Tag color="gold">计数中</Tag>
              <Typography.Text type="secondary">窗口到期后自动清除</Typography.Text>
            </Space>
          )
        }
        const remaining = loginLockRemainingSeconds(record.blocked_until, clock)
        return (
          <Space direction="vertical" size={2}>
            <Tag color="red" icon={<LockOutlined aria-hidden="true" />}>
              {registrationLimitReasonLabel(record.reason)}
            </Tag>
            <Typography.Text type="danger">
              剩余 {formatLoginRetryCountdown(remaining)}
            </Typography.Text>
          </Space>
        )
      },
    },
    {
      title: '尝试次数',
      dataIndex: 'attempt_count',
      search: false,
      width: 135,
      render: (_, record) => (
        <Space direction="vertical" size={2}>
          <Tag color={record.attempts_remaining === 0 ? 'red' : 'default'}>
            {record.attempt_count} / {record.attempt_limit}
          </Tag>
          <Typography.Text type="secondary">剩余 {record.attempts_remaining}</Typography.Text>
        </Space>
      ),
    },
    {
      title: '成功注册',
      dataIndex: 'success_count',
      search: false,
      width: 145,
      render: (_, record) => (
        <Space direction="vertical" size={2}>
          <Tag color={record.successes_remaining === 0 ? 'red' : 'blue'}>
            {record.success_count} / {record.success_limit}
          </Tag>
          {record.pending_count > 0 ? (
            <Typography.Text type="warning">处理中 {record.pending_count}</Typography.Text>
          ) : (
            <Typography.Text type="secondary">剩余 {record.successes_remaining}</Typography.Text>
          )}
        </Space>
      ),
    },
    {
      title: '最近请求',
      dataIndex: 'last_attempt_at',
      valueType: 'dateTime',
      search: false,
      width: 180,
      renderText: (value) => shortDate(String(value || '')),
    },
    {
      title: '操作',
      valueType: 'option',
      width: 130,
      fixed: 'right',
      render: (_, record) => (
        <Popconfirm
          title={`清除 ${record.ip_address} 的注册限制？`}
          description={record.pending_count > 0
            ? `已有计数会清除，但 ${record.pending_count} 个正在处理的请求会保留。`
            : '该来源可立即重新尝试注册。'}
          onConfirm={async () => {
            try {
              const result = await clearRegistrationSecurity(record.ip_address)
              message.success(registrationClearSuccessMessage(result))
              actionRef.current?.reload()
            } catch (error) {
              message.error(error instanceof Error ? error.message : '清除注册限制失败')
            }
          }}
        >
          <Button size="small" icon={<UnlockOutlined aria-hidden="true" />}>清除限制</Button>
        </Popconfirm>
      ),
    },
  ]

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Alert
        type="info"
        showIcon
        message="注册保护策略：10 分钟最多尝试 20 次，每小时最多成功注册 5 个账号"
        description="计数按来源地址持久化。窗口到期会自动清除；管理员也可手工解除误伤，正在处理的请求预留不会被丢弃。"
      />
      <ProTable<RegistrationSecurityEntry>
        actionRef={actionRef}
        rowKey="ip_address"
        columns={columns}
        request={async (params) => {
          try {
            const result = await listRegistrationSecurity({
              current: params.current,
              pageSize: params.pageSize,
              query: params.q as string | undefined,
              status: params.status as RegistrationSecurityStatus | undefined,
            })
            setSummary(result.summary)
            const refreshTimes = result.items
              .flatMap((item) => [
                item.attempt_window_expires_at,
                item.success_window_expires_at,
              ])
              .filter((value): value is string => Boolean(value))
              .map((value) => new Date(value).getTime())
              .filter((value) => Number.isFinite(value) && value > Date.now())
            setNextRefreshAt(refreshTimes.length > 0 ? Math.min(...refreshTimes) : null)
            return { data: result.items, success: true, total: result.total }
          } catch (error) {
            setSummary(null)
            setNextRefreshAt(null)
            message.error(error instanceof Error ? error.message : '注册安全状态加载失败')
            return { data: [], success: false, total: 0 }
          }
        }}
        search={{ labelWidth: 'auto' }}
        pagination={{
          pageSize: 20,
          showSizeChanger: true,
          showTotal: (total) => `当前共 ${total} 个来源存在注册计数`,
        }}
        options={{ density: true, fullScreen: true, reload: true, setting: true }}
        cardBordered
        scroll={{ x: 1090 }}
        locale={{ emptyText: '当前没有活跃的注册计数或受限来源' }}
        headerTitle={(
          <Space size="middle" wrap>
            <span>注册保护</span>
            <Typography.Text type="secondary">查看来源计数并解除注册限制</Typography.Text>
            {summary && (
              <Space size={4} wrap>
                <Tag>来源 {summary.total}</Tag>
                <Tag color="red">已限制 {summary.blocked}</Tag>
                <Tag color="gold">计数中 {summary.tracking}</Tag>
                {summary.pending > 0 && <Tag color="processing">处理中 {summary.pending}</Tag>}
              </Space>
            )}
          </Space>
        )}
      />
    </Space>
  )
}
