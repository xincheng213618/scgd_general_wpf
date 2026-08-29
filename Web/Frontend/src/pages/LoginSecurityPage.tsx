import { LockOutlined, UnlockOutlined } from '@ant-design/icons'
import {
  ProTable,
  type ActionType,
  type ProColumns,
} from '@ant-design/pro-components'
import { Alert, App, Button, Popconfirm, Space, Tabs, Tag, Tooltip, Typography } from 'antd'
import { useEffect, useRef, useState } from 'react'
import { RegistrationSecurityCard } from '../components/RegistrationSecurityCard'
import { listLoginSecurity, unlockLoginSecurity } from '../services/admin'
import type {
  LoginSecurityEntry,
  LoginSecurityStatus,
  LoginSecuritySummary,
} from '../types/admin'
import {
  formatLoginRetryCountdown,
  loginLockRemainingSeconds,
} from '../utils/authSecurity'
import { shortDate } from '../utils/format'
import { loginSecurityAccountTypeLabel } from '../utils/loginSecurity'

const accountTypeColors = {
  registered: 'blue',
  config_admin: 'red',
  unknown: 'default',
} as const

export function LoginSecurityPage() {
  const { message } = App.useApp()
  const actionRef = useRef<ActionType>(null)
  const [summary, setSummary] = useState<LoginSecuritySummary | null>(null)
  const [clock, setClock] = useState(() => Date.now())
  const [nextUnlockAt, setNextUnlockAt] = useState<number | null>(null)

  useEffect(() => {
    const timer = window.setInterval(() => setClock(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [])

  useEffect(() => {
    if (nextUnlockAt === null) return undefined
    const delay = Math.max(250, nextUnlockAt - Date.now() + 250)
    const timer = window.setTimeout(() => actionRef.current?.reload(), delay)
    return () => window.clearTimeout(timer)
  }, [nextUnlockAt])

  const columns: ProColumns<LoginSecurityEntry>[] = [
    {
      title: '账号关键词',
      dataIndex: 'q',
      hideInTable: true,
      order: 2,
      fieldProps: { allowClear: true, placeholder: '用户名、昵称或邮箱' },
    },
    {
      title: '账号',
      dataIndex: 'username',
      search: false,
      width: 240,
      render: (_, record) => (
        <Space direction="vertical" size={2}>
          <Space size={6} wrap>
            <Typography.Text copyable>{record.username}</Typography.Text>
            <Tag color={accountTypeColors[record.account_type]}>
              {loginSecurityAccountTypeLabel(record.account_type)}
            </Tag>
            {record.is_active === false && <Tag>已停用</Tag>}
          </Space>
          {record.display_name && (
            <Typography.Text type="secondary">{record.display_name}</Typography.Text>
          )}
        </Space>
      ),
    },
    {
      title: '状态',
      dataIndex: 'status',
      valueType: 'select',
      width: 190,
      valueEnum: {
        locked: { text: '已锁定' },
        tracking: { text: '失败计数中' },
      },
      render: (_, record) => {
        if (!record.locked) {
          return (
            <Space direction="vertical" size={2}>
              <Tag color="gold">失败计数中</Tag>
              <Typography.Text type="secondary">
                还可失败 {record.attempts_remaining} 次
              </Typography.Text>
            </Space>
          )
        }
        const remaining = loginLockRemainingSeconds(record.locked_until, clock)
        return (
          <Space direction="vertical" size={2}>
            <Tag color="red" icon={<LockOutlined aria-hidden="true" />}>已锁定</Tag>
            <Typography.Text type="danger">
              剩余 {formatLoginRetryCountdown(remaining)}
            </Typography.Text>
          </Space>
        )
      },
    },
    {
      title: '失败次数',
      dataIndex: 'failed_count',
      search: false,
      width: 100,
      render: (_, record) => (
        <Tag color={record.locked ? 'red' : 'gold'}>{record.failed_count} 次</Tag>
      ),
    },
    {
      title: '失败来源',
      dataIndex: 'source_count',
      search: false,
      width: 125,
      render: (_, record) => (
        <Tooltip
          placement="topLeft"
          title={(
            <Space direction="vertical" size={4}>
              {record.sources.map((source) => (
                <span key={source.ip_address}>
                  {source.ip_address} · {source.failed_count} 次 · {shortDate(source.last_failed_at)}
                </span>
              ))}
            </Space>
          )}
        >
          <Tag>{record.source_count} 个地址</Tag>
        </Tooltip>
      ),
    },
    {
      title: '最近失败',
      dataIndex: 'last_failed_at',
      valueType: 'dateTime',
      search: false,
      width: 180,
    },
    {
      title: '操作',
      valueType: 'option',
      width: 130,
      fixed: 'right',
      render: (_, record) => (
        <Popconfirm
          title={record.locked ? `解除 ${record.username} 的登录锁定？` : `清除 ${record.username} 的失败计数？`}
          description={`将清除来自 ${record.source_count} 个地址的失败记录。`}
          onConfirm={async () => {
            try {
              const result = await unlockLoginSecurity(record.username)
              message.success(`已清除 ${result.cleared_sources} 个来源的登录限制`)
              actionRef.current?.reload()
            } catch (error) {
              message.error(error instanceof Error ? error.message : '解除登录限制失败')
            }
          }}
        >
          <Button size="small" icon={<UnlockOutlined aria-hidden="true" />}>
            {record.locked ? '解除锁定' : '清除计数'}
          </Button>
        </Popconfirm>
      ),
    },
  ]

  return (
    <Tabs
      className="page-stack"
      items={[
        {
          key: 'login',
          label: '登录保护',
          children: (
            <Space direction="vertical" size={16} style={{ width: '100%' }}>
              <Alert
                type="info"
                showIcon
                message="登录保护策略：15 分钟内失败 5 次，临时锁定 15 分钟"
                description="失败次数按账号跨来源地址累计。成功登录、观察窗口到期或管理员手工清除后，记录会从这里消失；所有手工操作都会写入审计日志。"
              />
              <ProTable<LoginSecurityEntry>
                actionRef={actionRef}
                rowKey="username"
                columns={columns}
                request={async (params) => {
                  try {
                    const result = await listLoginSecurity({
                      current: params.current,
                      pageSize: params.pageSize,
                      query: params.q as string | undefined,
                      status: params.status as LoginSecurityStatus | undefined,
                    })
                    setSummary(result.summary)
                    const unlockTimes = result.items
                      .filter((item) => item.locked && item.locked_until)
                      .map((item) => new Date(item.locked_until || '').getTime())
                      .filter((value) => Number.isFinite(value) && value > Date.now())
                    setNextUnlockAt(unlockTimes.length > 0 ? Math.min(...unlockTimes) : null)
                    return { data: result.items, success: true, total: result.total }
                  } catch (error) {
                    setSummary(null)
                    setNextUnlockAt(null)
                    message.error(error instanceof Error ? error.message : '登录安全状态加载失败')
                    return { data: [], success: false, total: 0 }
                  }
                }}
                search={{ labelWidth: 'auto' }}
                pagination={{
                  pageSize: 20,
                  showSizeChanger: true,
                  showTotal: (total) => `当前共 ${total} 个账号存在失败计数`,
                }}
                options={{ density: true, fullScreen: true, reload: true, setting: true }}
                cardBordered
                scroll={{ x: 1080 }}
                locale={{ emptyText: '当前没有活跃的登录失败计数或锁定账号' }}
                headerTitle={(
                  <Space size="middle" wrap>
                    <span>登录保护</span>
                    <Typography.Text type="secondary">查看失败来源并解除账号登录限制</Typography.Text>
                    {summary && (
                      <Space size={4} wrap>
                        <Tag>涉及账号 {summary.total}</Tag>
                        <Tag color="red">已锁定 {summary.locked}</Tag>
                        <Tag color="gold">计数中 {summary.tracking}</Tag>
                        <Tag>来源地址 {summary.sources}</Tag>
                      </Space>
                    )}
                  </Space>
                )}
              />
            </Space>
          ),
        },
        {
          key: 'registration',
          label: '注册保护',
          children: <RegistrationSecurityCard />,
        },
      ]}
    />
  )
}
