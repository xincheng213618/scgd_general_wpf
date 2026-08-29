import { DeleteOutlined, EditOutlined, EyeOutlined, KeyOutlined, LogoutOutlined, PlusOutlined, SafetyCertificateOutlined, WarningOutlined } from '@ant-design/icons'
import {
  ModalForm,
  ProFormSelect,
  ProFormText,
  ProTable,
  type ActionType,
  type ProColumns,
} from '@ant-design/pro-components'
import { Alert, App, Button, Popconfirm, Space, Tag, Typography } from 'antd'
import { useRef, useState, type Key } from 'react'
import { useSearchParams } from 'react-router-dom'
import { UserDetailsDrawer } from '../components/UserDetailsDrawer'
import {
  bulkUserSecurityAction,
  createUserAccount,
  deleteUserAccount,
  listUsers,
  requireUserPasswordChange,
  resetUserPassword,
  revokeUserSessions,
  setUserEnabled,
  updateUserProfile,
  updateUserRole,
} from '../services/admin'
import type {
  CreateUserPayload,
  UserAccount,
  UserAccountOrigin,
  UserAccountStatus,
  UserAccountSummary,
  UserBulkSecurityAction,
  UserPasswordState,
  UserRecoveryState,
  UserRole,
} from '../types/admin'
import {
  ACCOUNT_PASSWORD_HELP,
  MAX_ACCOUNT_PASSWORD_LENGTH,
  accountStatusSuccessMessage,
  accountPasswordValidationMessage,
  bulkSecurityActionResultMessage,
  canDeleteUserAccount,
  canManageUserAccount,
  forceLogoutSuccessMessage,
  oppositeUserRole,
  passwordChangeRequiredSuccessMessage,
  passwordResetSuccessMessage,
  resolveUserListEntryFilters,
  resolveUserListSort,
  retryableBulkSecurityUserIds,
  USER_ROLE_OPTIONS,
  userAccountOriginLabel,
  userDeletionSuccessMessage,
  userRoleLabel,
} from '../utils/userAccounts'
import { shortDate } from '../utils/format'

interface CreateUserFormValues extends CreateUserPayload {
  confirmPassword: string
}

interface PasswordResetFormValues {
  password: string
  confirmPassword: string
}

interface ProfileFormValues {
  display_name?: string
  email?: string
}

const passwordRules = [
  { required: true, message: '请输入密码' },
  {
    validator(_: unknown, value: string) {
      if (!value) return Promise.resolve()
      const error = accountPasswordValidationMessage(value)
      return error ? Promise.reject(new Error(error)) : Promise.resolve()
    },
  },
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
  const { message, modal } = App.useApp()
  const [searchParams] = useSearchParams()
  const entryFilters = resolveUserListEntryFilters(searchParams)
  const actionRef = useRef<ActionType>(null)
  const [passwordTarget, setPasswordTarget] = useState<UserAccount | null>(null)
  const [profileTarget, setProfileTarget] = useState<UserAccount | null>(null)
  const [detailsTarget, setDetailsTarget] = useState<UserAccount | null>(null)
  const [summary, setSummary] = useState<UserAccountSummary | null>(null)
  const [selectedRowKeys, setSelectedRowKeys] = useState<Key[]>([])
  const [bulkAction, setBulkAction] = useState<UserBulkSecurityAction | null>(null)

  async function runBulkSecurityAction(action: UserBulkSecurityAction) {
    setBulkAction(action)
    try {
      const result = await bulkUserSecurityAction(selectedRowKeys.map(Number), action)
      const resultMessage = bulkSecurityActionResultMessage(result)
      const retryableUserIds = retryableBulkSecurityUserIds(result)
      if (result.failed > 0) {
        modal.warning({
          title: resultMessage,
          content: (
            <Space direction="vertical" size={4} style={{ marginTop: 12 }}>
              {result.results.filter((item) => item.status === 'failed').map((item) => (
                <Typography.Text key={item.user_id}>
                  {item.username || `账号 #${item.user_id}`}：{item.error || '操作失败'}
                </Typography.Text>
              ))}
              {retryableUserIds.length > 0 && (
                <Typography.Text type="secondary">
                  已保留 {retryableUserIds.length} 个可重试账号的选择，请检查后直接重试。
                </Typography.Text>
              )}
            </Space>
          ),
        })
      } else {
        message.success(resultMessage)
      }
      setSelectedRowKeys(retryableUserIds)
      actionRef.current?.reload()
    } catch (error) {
      message.error(error instanceof Error ? error.message : '批量安全操作失败')
    } finally {
      setBulkAction(null)
    }
  }

  const columns: ProColumns<UserAccount>[] = [
    {
      title: '账号关键词',
      dataIndex: 'q',
      hideInTable: true,
      order: 3,
      fieldProps: {
        allowClear: true,
        placeholder: '用户名、昵称或邮箱',
      },
    },
    {
      title: '密码状态',
      dataIndex: 'password_state',
      hideInTable: true,
      order: 1,
      valueType: 'select',
      valueEnum: {
        pending: { text: '待改密' },
        ready: { text: '密码正常' },
      },
    },
    {
      title: '找回状态',
      dataIndex: 'recovery_state',
      hideInTable: true,
      order: 0,
      valueType: 'select',
      valueEnum: {
        pending: { text: '等待处理' },
        none: { text: '无待处理申请' },
      },
    },
    {
      title: '用户名',
      dataIndex: 'username',
      copyable: true,
      search: false,
      sorter: true,
      render: (_, record) => (
        <Space size={8}>
          <Typography.Text>{record.username}</Typography.Text>
          {record.is_config_admin && <Tag color="red">配置管理员</Tag>}
          {record.is_current && <Tag color="processing">当前账号</Tag>}
          {record.must_change_password && <Tag color="gold">待改密</Tag>}
          {record.password_recovery_pending && <Tag color="red">找回申请</Tag>}
        </Space>
      ),
    },
    {
      title: '昵称',
      dataIndex: 'display_name',
      width: 150,
      search: false,
      sorter: true,
      renderText: (value) => value || '—',
    },
    {
      title: '邮箱',
      dataIndex: 'email',
      copyable: true,
      width: 220,
      search: false,
      sorter: true,
      renderText: (value) => value || '—',
    },
    {
      title: '角色',
      dataIndex: 'role',
      width: 120,
      valueType: 'select',
      sorter: true,
      valueEnum: {
        user: { text: '普通用户' },
        admin: { text: '管理员' },
      },
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
      valueType: 'select',
      sorter: true,
      valueEnum: {
        active: { text: '启用' },
        inactive: { text: '停用' },
      },
      render: (_, record) => (
        <Tag color={record.is_active ? 'green' : 'default'}>
          {record.is_active ? '启用' : '停用'}
        </Tag>
      ),
    },
    {
      title: '有效会话',
      dataIndex: 'active_session_count',
      width: 110,
      search: false,
      sorter: true,
      render: (_, record) => (
        <Tag color={record.active_session_count > 0 ? 'processing' : 'default'}>
          {record.active_session_count}
        </Tag>
      ),
    },
    {
      title: '找回申请',
      dataIndex: 'password_recovery_requested_at',
      width: 180,
      search: false,
      sorter: true,
      render: (_, record) => record.password_recovery_pending ? (
        <Space direction="vertical" size={0}>
          <Tag color="red">等待处理 · {record.password_recovery_request_count || 1} 次</Tag>
          <Typography.Text type="secondary">{shortDate(record.password_recovery_requested_at || undefined)}</Typography.Text>
        </Space>
      ) : <Typography.Text type="secondary">—</Typography.Text>,
    },
    {
      title: '来源',
      dataIndex: 'account_origin',
      width: 130,
      order: 2,
      valueType: 'select',
      sorter: true,
      valueEnum: {
        self_registered: { text: '公开注册' },
        administrator_created: { text: '管理员创建' },
        legacy: { text: '历史账号' },
      },
      render: (_, record) => (
        <Tag color={record.account_origin === 'self_registered' ? 'cyan' : record.account_origin === 'administrator_created' ? 'purple' : 'default'}>
          {userAccountOriginLabel(record.account_origin)}
        </Tag>
      ),
    },
    {
      title: '创建时间',
      dataIndex: 'created_at',
      valueType: 'dateTime',
      width: 180,
      search: false,
      sorter: true,
      defaultSortOrder: 'descend',
    },
    {
      title: '最近登录',
      dataIndex: 'last_login_at',
      valueType: 'dateTime',
      width: 180,
      search: false,
      sorter: true,
    },
    {
      title: '操作',
      valueType: 'option',
      width: 830,
      fixed: 'right',
      render: (_, record) => {
        const enabled = Boolean(record.is_active)
        const nextRole = oppositeUserRole(record.role)
        const detailsButton = (
          <Button
            size="small"
            icon={<EyeOutlined aria-hidden="true" />}
            onClick={() => setDetailsTarget(record)}
          >
            查看详情
          </Button>
        )
        if (!canManageUserAccount(record)) {
          return (
            <Space size={4} wrap>
              {detailsButton}
              <Typography.Text type="secondary">由服务配置维护</Typography.Text>
            </Space>
          )
        }
        return (
          <Space size={[4, 4]} wrap>
            {detailsButton}
            <Button
              size="small"
              icon={<EditOutlined aria-hidden="true" />}
              onClick={() => setProfileTarget(record)}
            >
              编辑资料
            </Button>
            <Button
              size="small"
              icon={<KeyOutlined aria-hidden="true" />}
              onClick={() => setPasswordTarget(record)}
            >
              重置密码
            </Button>
            {!record.is_current && !record.must_change_password && (
              <Popconfirm
                title={`确认要求 ${record.username} 下次登录修改密码？`}
                description="现有登录会话会立即失效，但管理员不会改写当前密码。用户可用当前密码重新登录并设置新密码。"
                okText="要求改密"
                onConfirm={async () => {
                  try {
                    const result = await requireUserPasswordChange(record.id)
                    message.success(passwordChangeRequiredSuccessMessage(
                      result.sessions_revoked,
                      result.login_failure_sources_cleared,
                    ))
                    actionRef.current?.reload()
                  } catch (error) {
                    message.error(error instanceof Error ? error.message : '要求改密失败')
                  }
                }}
              >
                <Button size="small" icon={<WarningOutlined aria-hidden="true" />}>
                  要求改密
                </Button>
              </Popconfirm>
            )}
            {!record.is_current && record.active_session_count > 0 && (
              <Popconfirm
                title={`确认强制下线 ${record.username}？`}
                description={`将立即注销该账号的 ${record.active_session_count} 个有效会话，账号仍保持启用。`}
                onConfirm={async () => {
                  try {
                    const result = await revokeUserSessions(record.id)
                    message.success(forceLogoutSuccessMessage(result.revoked))
                    actionRef.current?.reload()
                  } catch (error) {
                    message.error(error instanceof Error ? error.message : '强制下线失败')
                  }
                }}
              >
                <Button size="small" danger icon={<LogoutOutlined aria-hidden="true" />}>
                  强制下线
                </Button>
              </Popconfirm>
            )}
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
                description={enabled
                  ? '停用后，已有会话会失效，待处理找回申请和登录失败记录会同步清理。'
                  : '重新启用不会恢复旧会话，并会清理停用期间产生的登录失败记录。'}
                onConfirm={async () => {
                  try {
                    const result = await setUserEnabled(record.id, !enabled)
                    message.success(accountStatusSuccessMessage(result))
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
            {canDeleteUserAccount(record) && (
              <Popconfirm
                title={`永久删除账号 ${record.username}？`}
                description="数据库账号、会话和密码找回记录会被删除；审计记录保留，用户名之后可以重新注册。"
                okText="永久删除"
                cancelText="取消"
                okButtonProps={{ danger: true }}
                onConfirm={async () => {
                  try {
                    const result = await deleteUserAccount(record.id, record.username)
                    message.success(userDeletionSuccessMessage(result))
                    setSelectedRowKeys((keys) => keys.filter((key) => Number(key) !== record.id))
                    setDetailsTarget((target) => target?.id === record.id ? null : target)
                    actionRef.current?.reload()
                  } catch (error) {
                    message.error(error instanceof Error ? error.message : '账号删除失败')
                  }
                }}
              >
                <Button size="small" danger icon={<DeleteOutlined aria-hidden="true" />}>
                  删除账号
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
        message="注册用户当前默认拥有全部功能权限"
        description="具体能力由“权限管理”中的注册用户角色控制。永久删除前必须先停用账号；配置管理员继续使用现有服务配置且不可删除。"
      />
      <ProTable<UserAccount>
        actionRef={actionRef}
        rowKey="id"
        columns={columns}
        request={async (params, sort) => {
          try {
            const listSort = resolveUserListSort(sort)
            const result = await listUsers({
              current: params.current,
              pageSize: params.pageSize,
              query: params.q as string | undefined,
              role: params.role as UserRole | undefined,
              origin: params.account_origin as UserAccountOrigin | undefined,
              status: params.is_active as UserAccountStatus | undefined,
              passwordState: params.password_state as UserPasswordState | undefined,
              recoveryState: params.recovery_state as UserRecoveryState | undefined,
              ...listSort,
            })
            setSummary(result.summary)
            return { data: result.items, success: true, total: result.total }
          } catch (error) {
            setSummary(null)
            message.error(error instanceof Error ? error.message : '加载账号失败')
            return { data: [], success: false, total: 0 }
          }
        }}
        search={{ labelWidth: 'auto' }}
        form={{ initialValues: entryFilters }}
        rowSelection={{
          selectedRowKeys,
          onChange: (keys) => setSelectedRowKeys(keys),
          getCheckboxProps: (record) => ({
            disabled: record.is_current || !canManageUserAccount(record),
          }),
        }}
        pagination={{
          pageSize: 20,
          showSizeChanger: true,
          showTotal: (total) => `筛选结果共 ${total} 个账号`,
        }}
        options={{ density: true, fullScreen: true, reload: true, setting: true }}
        cardBordered
        scroll={{ x: 2300 }}
        headerTitle={(
          <Space size="middle" wrap>
            <span>账号管理</span>
            <Typography.Text type="secondary">创建账号、维护角色、处置会话并清理停用账号</Typography.Text>
            {summary && (
              <Space size={4} wrap>
                <Tag>总计 {summary.total}</Tag>
                <Tag color="green">启用 {summary.active}</Tag>
                <Tag>停用 {summary.inactive}</Tag>
                <Tag color="red">管理员 {summary.admins}</Tag>
                <Tag color="cyan">公开注册 {summary.self_registered}</Tag>
                <Tag color="purple">管理员创建 {summary.administrator_created}</Tag>
                {summary.legacy > 0 && <Tag>历史账号 {summary.legacy}</Tag>}
                {summary.pending_password_changes > 0 && (
                  <Tag color="gold">待改密 {summary.pending_password_changes}</Tag>
                )}
                {summary.pending_password_recovery > 0 && (
                  <Tag color="red">找回申请 {summary.pending_password_recovery}</Tag>
                )}
              </Space>
            )}
          </Space>
        )}
        toolBarRender={() => [
          ...(selectedRowKeys.length > 0 ? [
            <Space key="bulk-security-actions" wrap>
              <Popconfirm
                title={`确认要求选中的 ${selectedRowKeys.length} 个账号下次登录修改密码？`}
                description="这些账号的现有登录会话会立即失效，但密码本身不会被管理员改写。"
                okText="确认要求改密"
                onConfirm={() => runBulkSecurityAction('require_password_change')}
              >
                <Button
                  icon={<WarningOutlined aria-hidden="true" />}
                  loading={bulkAction === 'require_password_change'}
                  disabled={bulkAction !== null}
                >
                  批量要求改密 ({selectedRowKeys.length})
                </Button>
              </Popconfirm>
              <Popconfirm
                title={`确认强制下线选中的 ${selectedRowKeys.length} 个账号？`}
                description="账号仍保持启用，但所有有效浏览器会话都需要重新登录。"
                okText="确认强制下线"
                onConfirm={() => runBulkSecurityAction('force_logout')}
              >
                <Button
                  danger
                  icon={<LogoutOutlined aria-hidden="true" />}
                  loading={bulkAction === 'force_logout'}
                  disabled={bulkAction !== null}
                >
                  批量强制下线 ({selectedRowKeys.length})
                </Button>
              </Popconfirm>
            </Space>,
          ] : []),
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
                  display_name: values.display_name?.trim() || '',
                  email: values.email?.trim() || '',
                })
                message.success('账号已创建；用户首次登录须修改初始密码')
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
            <ProFormText
              name="display_name"
              label="昵称"
              fieldProps={{ maxLength: 64 }}
              rules={[{ max: 64, message: '昵称不能超过 64 个字符' }]}
            />
            <ProFormText
              name="email"
              label="邮箱"
              fieldProps={{ type: 'email', maxLength: 254 }}
              rules={[{ type: 'email', message: '请输入有效的邮箱地址' }]}
            />
            <ProFormText.Password
              name="password"
              label="初始密码"
              extra={ACCOUNT_PASSWORD_HELP}
              fieldProps={{ maxLength: MAX_ACCOUNT_PASSWORD_LENGTH * 2 }}
              rules={passwordRules}
            />
            <ProFormText.Password
              name="confirmPassword"
              label="确认密码"
              dependencies={['password']}
              fieldProps={{ maxLength: MAX_ACCOUNT_PASSWORD_LENGTH * 2 }}
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
      <ModalForm<ProfileFormValues>
        key={profileTarget?.id ?? 'closed-profile-edit'}
        open={Boolean(profileTarget)}
        title={profileTarget ? `编辑 ${profileTarget.username} 的资料` : '编辑账号资料'}
        modalProps={{ destroyOnHidden: true }}
        initialValues={{
          display_name: profileTarget?.display_name || '',
          email: profileTarget?.email || '',
        }}
        onOpenChange={(open) => {
          if (!open) setProfileTarget(null)
        }}
        onFinish={async (values) => {
          if (!profileTarget) return false
          try {
            await updateUserProfile(profileTarget.id, {
              display_name: values.display_name?.trim() || '',
              email: values.email?.trim() || '',
            })
            message.success('账号资料已更新')
            setProfileTarget(null)
            actionRef.current?.reload()
            return true
          } catch (error) {
            message.error(error instanceof Error ? error.message : '账号资料更新失败')
            return false
          }
        }}
      >
        <ProFormText
          name="display_name"
          label="昵称"
          fieldProps={{ maxLength: 64 }}
          rules={[{ max: 64, message: '昵称不能超过 64 个字符' }]}
        />
        <ProFormText
          name="email"
          label="邮箱"
          fieldProps={{ type: 'email', maxLength: 254 }}
          rules={[{ type: 'email', message: '请输入有效的邮箱地址' }]}
        />
      </ModalForm>
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
            message.success(passwordResetSuccessMessage(
              result.current_session_preserved,
              result.password_recovery_requests_resolved,
              result.login_failure_sources_cleared,
            ))
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
          message={passwordTarget?.is_current
            ? '提交后，其他登录会话会立即失效'
            : '提交后，现有登录会话会立即失效；该账号下次登录须先修改临时密码'}
          style={{ marginBottom: 16 }}
        />
        <ProFormText.Password
          name="password"
          label="新密码"
          extra={ACCOUNT_PASSWORD_HELP}
          fieldProps={{ maxLength: MAX_ACCOUNT_PASSWORD_LENGTH * 2 }}
          rules={passwordRules}
        />
        <ProFormText.Password
          name="confirmPassword"
          label="确认新密码"
          dependencies={['password']}
          fieldProps={{ maxLength: MAX_ACCOUNT_PASSWORD_LENGTH * 2 }}
          rules={confirmationRules('password')}
        />
      </ModalForm>
      <UserDetailsDrawer
        key={detailsTarget?.id ?? 'closed-user-details'}
        target={detailsTarget}
        onResetPassword={(account) => {
          setDetailsTarget(null)
          setPasswordTarget(account)
        }}
        onClose={() => setDetailsTarget(null)}
      />
    </Space>
  )
}
