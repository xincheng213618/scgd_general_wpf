import { KeyOutlined, PlusOutlined, SafetyCertificateOutlined } from '@ant-design/icons'
import {
  ModalForm,
  ProFormSelect,
  ProFormText,
  ProTable,
  type ActionType,
  type ProColumns,
} from '@ant-design/pro-components'
import { Alert, App, Button, Popconfirm, Space, Tag, Typography } from 'antd'
import { useRef, useState } from 'react'
import {
  createUserAccount,
  listUsers,
  resetUserPassword,
  setUserEnabled,
  updateUserRole,
} from '../services/admin'
import type { CreateUserPayload, UserAccount } from '../types/admin'
import {
  MIN_ACCOUNT_PASSWORD_LENGTH,
  oppositeUserRole,
  passwordResetSuccessMessage,
  USER_ROLE_OPTIONS,
  userRoleLabel,
} from '../utils/userAccounts'

interface CreateUserFormValues extends CreateUserPayload {
  confirmPassword: string
}

interface PasswordResetFormValues {
  password: string
  confirmPassword: string
}

const passwordRules = [
  { required: true, message: '请输入密码' },
  { min: MIN_ACCOUNT_PASSWORD_LENGTH, message: `密码至少需要 ${MIN_ACCOUNT_PASSWORD_LENGTH} 位` },
]

function confirmationRules(passwordField: string) {
  return [
    { required: true, message: '请再次输入密码' },
    ({ getFieldValue }: { getFieldValue: (name: string) => unknown }) => ({
      validator(_: unknown, value: string) {
        if (!value || getFieldValue(passwordField) === value) return Promise.resolve()
        return Promise.reject(new Error('两次输入的密码不一致'))
      },
    }),
  ]
}

export function UsersPage() {
  const { message } = App.useApp()
  const actionRef = useRef<ActionType>(null)
  const [passwordTarget, setPasswordTarget] = useState<UserAccount | null>(null)

  const columns: ProColumns<UserAccount>[] = [
    {
      title: '用户名',
      dataIndex: 'username',
      copyable: true,
      render: (_, record) => (
        <Space size={8}>
          <Typography.Text>{record.username}</Typography.Text>
          {record.is_current && <Tag color="processing">当前账号</Tag>}
        </Space>
      ),
    },
    {
      title: '角色',
      dataIndex: 'role',
      width: 120,
      render: (_, record) => (
        <Tag color={record.role === 'admin' ? 'red' : 'blue'}>
          {userRoleLabel(record.role)}
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
      width: 320,
      fixed: 'right',
      render: (_, record) => {
        const enabled = Boolean(record.is_active)
        const nextRole = oppositeUserRole(record.role)
        return (
          <Space size={[4, 4]} wrap>
            <Button
              size="small"
              icon={<KeyOutlined aria-hidden="true" />}
              onClick={() => setPasswordTarget(record)}
            >
              重置密码
            </Button>
            {!record.is_current && (
              <Popconfirm
                title={`确认将 ${record.username} 设为${userRoleLabel(nextRole)}？`}
                description="角色变更后，该账号已有登录会话会失效。"
                onConfirm={async () => {
                  try {
                    await updateUserRole(record.id, nextRole)
                    message.success(`账号已设为${userRoleLabel(nextRole)}；旧会话已失效`)
                    actionRef.current?.reload()
                  } catch (error) {
                    message.error(error instanceof Error ? error.message : '账号角色更新失败')
                  }
                }}
              >
                <Button size="small" icon={<SafetyCertificateOutlined aria-hidden="true" />}>
                  {nextRole === 'admin' ? '设为管理员' : '降为普通用户'}
                </Button>
              </Popconfirm>
            )}
            {!record.is_current && (
              <Popconfirm
                title={`确认${enabled ? '停用' : '启用'}账号 ${record.username}？`}
                description={`${enabled ? '停用' : '重新启用'}后，该账号已有登录会话会失效。`}
                onConfirm={async () => {
                  try {
                    await setUserEnabled(record.id, !enabled)
                    message.success(`账号已${enabled ? '停用' : '启用'}；旧会话已失效`)
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
            )}
          </Space>
        )
      },
    },
  ]

  return (
    <Space direction="vertical" size={16} className="page-stack">
      <Alert
        type="info"
        showIcon
        message="账号变更会撤销旧会话"
        description="停用、启用、角色调整和密码重置都会使该账号已有登录会话失效。当前管理员重置自己的密码后，仅保留当前浏览器会话。"
      />
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
        scroll={{ x: 1180 }}
        headerTitle={(
          <Space size="middle" wrap>
            <span>账号管理</span>
            <Typography.Text type="secondary">创建账号、重置凭据并控制后台权限</Typography.Text>
          </Space>
        )}
        toolBarRender={() => [
          <ModalForm<CreateUserFormValues>
            key="create-user"
            title="创建账号"
            trigger={(
              <Button type="primary" icon={<PlusOutlined aria-hidden="true" />}>创建账号</Button>
            )}
            modalProps={{ destroyOnHidden: true }}
            initialValues={{ role: 'user' }}
            onFinish={async (values) => {
              try {
                await createUserAccount({
                  username: values.username.trim(),
                  password: values.password,
                  role: values.role,
                })
                message.success('账号已创建')
                actionRef.current?.reload()
                return true
              } catch (error) {
                message.error(error instanceof Error ? error.message : '账号创建失败')
                return false
              }
            }}
          >
            <ProFormText
              name="username"
              label="用户名"
              extra="3-32 位字母、数字、下划线、点或连字符"
              rules={[
                { required: true, message: '请输入用户名' },
                { pattern: /^[A-Za-z0-9_.-]{3,32}$/, message: '用户名格式不正确' },
              ]}
            />
            <ProFormText.Password name="password" label="初始密码" rules={passwordRules} />
            <ProFormText.Password
              name="confirmPassword"
              label="确认密码"
              dependencies={['password']}
              rules={confirmationRules('password')}
            />
            <ProFormSelect
              name="role"
              label="角色"
              options={USER_ROLE_OPTIONS}
              fieldProps={{ optionRender: (option) => (
                <Space direction="vertical" size={0}>
                  <span>{option.label}</span>
                  <Typography.Text type="secondary">{option.data.description}</Typography.Text>
                </Space>
              ) }}
              rules={[{ required: true, message: '请选择角色' }]}
            />
          </ModalForm>,
        ]}
      />
      <ModalForm<PasswordResetFormValues>
        key={passwordTarget?.id ?? 'closed-password-reset'}
        open={Boolean(passwordTarget)}
        title={passwordTarget ? `重置 ${passwordTarget.username} 的密码` : '重置密码'}
        modalProps={{ destroyOnHidden: true }}
        onOpenChange={(open) => {
          if (!open) setPasswordTarget(null)
        }}
        onFinish={async (values) => {
          if (!passwordTarget) return false
          try {
            const result = await resetUserPassword(passwordTarget.id, values.password)
            message.success(passwordResetSuccessMessage(result.current_session_preserved))
            setPasswordTarget(null)
            actionRef.current?.reload()
            return true
          } catch (error) {
            message.error(error instanceof Error ? error.message : '密码重置失败')
            return false
          }
        }}
      >
        <Alert
          type="warning"
          showIcon
          message="提交后，该账号的其他登录会话会立即失效"
          style={{ marginBottom: 16 }}
        />
        <ProFormText.Password name="password" label="新密码" rules={passwordRules} />
        <ProFormText.Password
          name="confirmPassword"
          label="确认新密码"
          dependencies={['password']}
          rules={confirmationRules('password')}
        />
      </ModalForm>
    </Space>
  )
}
