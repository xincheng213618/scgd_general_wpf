import { ProTable, type ActionType, type ProColumns } from '@ant-design/pro-components'
import { App, Button, Popconfirm, Space, Tag, Typography } from 'antd'
import { useRef } from 'react'
import { listUsers, setUserEnabled } from '../services/admin'
import type { UserAccount } from '../types/admin'

export function UsersPage() {
  const { message } = App.useApp()
  const actionRef = useRef<ActionType>(null)

  const columns: ProColumns<UserAccount>[] = [
    {
      title: '用户名',
      dataIndex: 'username',
      copyable: true,
    },
    {
      title: '角色',
      dataIndex: 'role',
      width: 110,
      render: (_, record) => (
        <Tag color={record.role === 'admin' ? 'red' : 'blue'}>
          {record.role === 'admin' ? '管理员' : '普通用户'}
        </Tag>
      ),
    },
    {
      title: '状态',
      dataIndex: 'is_active',
      width: 100,
      render: (_, record) => (
        <Tag color={record.is_active ? 'green' : 'default'}>
          {record.is_active ? '启用' : '停用'}
        </Tag>
      ),
    },
    {
      title: '创建时间',
      dataIndex: 'created_at',
      valueType: 'dateTime',
      width: 180,
    },
    {
      title: '最近登录',
      dataIndex: 'last_login_at',
      valueType: 'dateTime',
      width: 180,
    },
    {
      title: '操作',
      valueType: 'option',
      width: 130,
      render: (_, record) => {
        if (record.is_current) {
          return <Tag color="processing">当前账号</Tag>
        }
        const enabled = Boolean(record.is_active)
        return (
          <Popconfirm
            title={`确认${enabled ? '停用' : '启用'}账号 ${record.username}？`}
            description={enabled ? '停用后，该账号的已有会话将在下一次请求时失效。' : undefined}
            onConfirm={async () => {
              try {
                await setUserEnabled(record.id, !enabled)
                message.success(`账号已${enabled ? '停用' : '启用'}`)
                actionRef.current?.reload()
              } catch (error) {
                message.error(error instanceof Error ? error.message : '账号状态更新失败')
              }
            }}
          >
            <Button size="small" danger={enabled}>
              {enabled ? '停用' : '启用'}
            </Button>
          </Popconfirm>
        )
      },
    },
  ]

  return (
    <ProTable<UserAccount>
      actionRef={actionRef}
      rowKey="id"
      columns={columns}
      search={false}
      request={async () => {
        try {
          const data = await listUsers()
          return { data, success: true, total: data.length }
        } catch (error) {
          message.error(error instanceof Error ? error.message : '加载账号失败')
          return { data: [], success: false, total: 0 }
        }
      }}
      options={{ density: true, fullScreen: true, reload: true, setting: true }}
      cardBordered
      headerTitle={(
        <Space size="middle">
          <span>账号管理</span>
          <Typography.Text type="secondary">管理注册账号的访问状态</Typography.Text>
        </Space>
      )}
    />
  )
}
