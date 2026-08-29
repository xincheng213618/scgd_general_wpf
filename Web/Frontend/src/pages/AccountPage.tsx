import { EditOutlined, KeyOutlined, LaptopOutlined, LogoutOutlined, SafetyCertificateOutlined, UserOutlined } from '@ant-design/icons'
import { Alert, App, Button, Card, Descriptions, Form, Input, List, Popconfirm, Space, Spin, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { Navigate, useSearchParams } from 'react-router-dom'
import { AccountActivityCard } from '../components/AccountActivityCard'
import {
  changeAccountPassword,
  getAccountProfile,
  getAccountSessions,
  revokeAccountSession,
  revokeOtherAccountSessions,
  updateAccountProfile,
} from '../services/auth'
import type { AccountProfile, AuthSession, AuthSessionUpdater, LoginSession } from '../types/site'
import { sessionAfterPasswordChange } from '../utils/authSecurity'
import { sessionAddressLabel, sessionClientLabel } from '../utils/accountSessions'
import { shortDate } from '../utils/format'
import {
  ACCOUNT_PASSWORD_CHANGE_HELP,
  MAX_ACCOUNT_PASSWORD_LENGTH,
  accountPasswordChangeValidationMessage,
  userAccountOriginLabel,
} from '../utils/userAccounts'
import { groupAccountPermissions, sessionAuthorizationKey } from '../utils/permissions'
import { shouldShowRegistrationWelcome } from '../utils/registrationPolicy'

interface PasswordFormValues {
  currentPassword: string
  newPassword: string
  confirmPassword: string
}

interface ProfileFormValues {
  displayName?: string
  email?: string
}

export function AccountPage({
  session,
  onSessionChanged,
}: {
  session: AuthSession | null
  onSessionChanged: AuthSessionUpdater
}) {
  const { message } = App.useApp()
  const [searchParams, setSearchParams] = useSearchParams()
  const authorizationChanged = searchParams.get('access') === 'updated'
  const [showRegistrationWelcome, setShowRegistrationWelcome] = useState(() => (
    shouldShowRegistrationWelcome(searchParams.get('welcome'))
  ))
  const [passwordForm] = Form.useForm<PasswordFormValues>()
  const [profile, setProfile] = useState<AccountProfile | null>(null)
  const [profileFailureKey, setProfileFailureKey] = useState('')
  const [profileReloadToken, setProfileReloadToken] = useState(0)
  const [submitting, setSubmitting] = useState(false)
  const [profileSubmitting, setProfileSubmitting] = useState(false)
  const [sessions, setSessions] = useState<LoginSession[] | null>(null)
  const [sessionsFailed, setSessionsFailed] = useState(false)
  const [sessionAction, setSessionAction] = useState('')
  const [activityRefresh, setActivityRefresh] = useState(0)
  const profileRequestKey = `${sessionAuthorizationKey(session)}:${profileReloadToken}`

  async function refreshSessions(notifyError = true) {
    setSessionsFailed(false)
    try {
      const result = await getAccountSessions()
      setSessions(result.items)
      return true
    } catch (error) {
      setSessionsFailed(true)
      if (notifyError) message.error(error instanceof Error ? error.message : '登录会话加载失败')
      return false
    }
  }

  useEffect(() => {
    if (!session?.authenticated) return
    let active = true
    getAccountProfile()
      .then((result) => {
        if (!active) return
        setProfile(result)
        setProfileFailureKey('')
        setSessions(null)
        setSessionsFailed(false)
        if (result.can_manage_sessions) {
          void getAccountSessions()
            .then((sessionResult) => {
              if (active) {
                setSessions(sessionResult.items)
                setSessionsFailed(false)
              }
            })
            .catch((error) => {
              if (active) {
                setSessionsFailed(true)
                message.error(error instanceof Error ? error.message : '登录会话加载失败')
              }
            })
        }
      })
      .catch((error) => {
        if (active) {
          setProfileFailureKey(profileRequestKey)
          message.error(error instanceof Error ? error.message : '个人资料加载失败')
        }
      })
    return () => {
      active = false
    }
  }, [message, profileRequestKey, session?.authenticated])

  useEffect(() => {
    if (!showRegistrationWelcome || !searchParams.has('welcome')) return
    const next = new URLSearchParams(searchParams)
    next.delete('welcome')
    setSearchParams(next, { replace: true })
  }, [searchParams, setSearchParams, showRegistrationWelcome])

  if (session === null) return <Spin tip="加载个人中心…" />
  if (!session.authenticated) return <Navigate to="/login?next=/account" replace />
  if ((!profile || profile.username !== session.username) && profileFailureKey !== profileRequestKey) {
    return <Spin tip="加载个人中心…" />
  }
  if (!profile || profile.username !== session.username) {
    return (
      <Alert
        type="error"
        showIcon
        message="个人资料不可用"
        description="个人资料暂时无法加载，请检查网络后重试。"
        action={(
          <Button
            size="small"
            onClick={() => {
              setProfileReloadToken((value) => value + 1)
            }}
          >
            重试
          </Button>
        )}
      />
    )
  }
  const passwordChangeRequired = session.must_change_password === true
  const permissionGroups = groupAccountPermissions(profile.permission_details)

  return (
    <div className="page-stack account-page">
      {showRegistrationWelcome && (
        <Alert
          type="success"
          showIcon
          closable
          message="账号注册成功，欢迎使用 ColorVision"
          description="你可以在这里完善昵称和邮箱、查看实际权限、检查登录设备，并随时修改密码。"
          action={<Button type="primary" href="#edit-profile">完善个人资料</Button>}
          onClose={() => setShowRegistrationWelcome(false)}
        />
      )}
      {authorizationChanged && (
        <Alert
          type="info"
          showIcon
          closable
          message="账号权限已更新"
          description="当前页面权限已经发生变化，系统已刷新菜单并将你带回个人中心。下方展示的是账号现在实际拥有的权限。"
          onClose={() => {
            const next = new URLSearchParams(searchParams)
            next.delete('access')
            setSearchParams(next, { replace: true })
          }}
        />
      )}
      {passwordChangeRequired && (
        <Alert
          type="warning"
          showIcon
          message="请先修改临时密码"
          description="此账号由管理员创建或刚完成密码重置。修改为只有你知道的新密码后，管理后台、文件中转和其他功能会自动恢复。"
          action={<Button type="primary" href="#change-password">立即修改</Button>}
        />
      )}
      <Card>
        <Space align="start" size={16}>
          <span className="account-page-avatar"><UserOutlined /></span>
          <div>
            <Typography.Title level={3} style={{ marginBottom: 4 }}>
              {profile.display_name || profile.username}
            </Typography.Title>
            <Typography.Text type="secondary">@{profile.username} · ColorVision 用户个人中心</Typography.Text>
          </div>
        </Space>
        <Descriptions column={{ xs: 1, md: 2 }} bordered style={{ marginTop: 20 }}>
          <Descriptions.Item label="账号角色">
            <Tag color={profile.is_admin ? 'red' : 'blue'}>
              {profile.is_admin ? '管理员' : '注册用户'}
            </Tag>
          </Descriptions.Item>
          <Descriptions.Item label="后台访问">
            {profile.can_access_admin ? '已授权' : '未授权'}
          </Descriptions.Item>
          <Descriptions.Item label="邮箱">{profile.email || '未填写'}</Descriptions.Item>
          <Descriptions.Item label="账号来源">
            {profile.account_origin ? userAccountOriginLabel(profile.account_origin) : '服务配置'}
          </Descriptions.Item>
          <Descriptions.Item label="创建时间">{profile.created_at ? shortDate(profile.created_at) : '配置管理员'}</Descriptions.Item>
          <Descriptions.Item label="最近登录">{profile.last_login_at ? shortDate(profile.last_login_at) : '暂无记录'}</Descriptions.Item>
          <Descriptions.Item label="账号更新">{profile.updated_at ? shortDate(profile.updated_at) : '暂无记录'}</Descriptions.Item>
          <Descriptions.Item label="密码更新">
            {profile.password_changed_at
              ? shortDate(profile.password_changed_at)
              : profile.can_change_password ? '暂无记录' : '服务配置维护'}
          </Descriptions.Item>
        </Descriptions>
      </Card>

      <Card
        title={<Space><LaptopOutlined />登录设备</Space>}
        extra={!passwordChangeRequired && profile.can_manage_sessions && sessions?.some((item) => !item.is_current) ? (
          <Popconfirm
            title="退出其他所有登录？"
            description="其他浏览器和设备将需要重新登录，当前浏览器不受影响。"
            okText="退出其他登录"
            onConfirm={async () => {
              setSessionAction('others')
              try {
                const result = await revokeOtherAccountSessions()
                await refreshSessions(false)
                setActivityRefresh((value) => value + 1)
                message.success(`已退出 ${result.revoked} 个其他登录`)
              } catch (error) {
                message.error(error instanceof Error ? error.message : '其他登录退出失败')
              } finally {
                setSessionAction('')
              }
            }}
          >
            <Button danger icon={<LogoutOutlined />} loading={sessionAction === 'others'}>
              退出其他登录
            </Button>
          </Popconfirm>
        ) : undefined}
      >
        {!profile.can_manage_sessions ? (
          <Alert
            type="info"
            showIcon
            message="该管理员账号仍由服务配置管理"
            description="数据库账号登录后可在此查看并管理每个浏览器会话。"
          />
        ) : sessionsFailed ? (
          <Alert
            type="error"
            showIcon
            message="登录会话加载失败"
            action={(
              <Button
                size="small"
                onClick={() => {
                  setSessions(null)
                  void refreshSessions()
                }}
              >
                重试
              </Button>
            )}
          />
        ) : sessions === null ? (
          <Spin tip="加载登录设备…" />
        ) : (
          <List<LoginSession>
            itemLayout="horizontal"
            dataSource={sessions}
            locale={{ emptyText: '暂无有效登录会话' }}
            renderItem={(item) => (
              <List.Item
                actions={item.is_current || passwordChangeRequired ? [] : [
                  <Popconfirm
                    key="revoke"
                    title="退出这个登录？"
                    description="该浏览器或设备将在下次访问时退出登录。"
                    okText="确认退出"
                    onConfirm={async () => {
                      setSessionAction(item.id)
                      try {
                        await revokeAccountSession(item.id)
                        await refreshSessions(false)
                        setActivityRefresh((value) => value + 1)
                        message.success('该登录会话已退出')
                      } catch (error) {
                        message.error(error instanceof Error ? error.message : '登录会话退出失败')
                      } finally {
                        setSessionAction('')
                      }
                    }}
                  >
                    <Button danger size="small" loading={sessionAction === item.id}>退出</Button>
                  </Popconfirm>,
                ]}
              >
                <List.Item.Meta
                  avatar={<LaptopOutlined style={{ fontSize: 24, marginTop: 4 }} />}
                  title={(
                    <Space wrap>
                      <Typography.Text strong>{sessionClientLabel(item.user_agent)}</Typography.Text>
                      {item.is_current && <Tag color="processing">当前登录</Tag>}
                    </Space>
                  )}
                  description={(
                    <Space direction="vertical" size={0}>
                      <Typography.Text type="secondary">
                        {sessionAddressLabel(item.ip_address)} · 最近活动 {shortDate(item.last_seen_at)} · 登录于 {shortDate(item.created_at)}
                      </Typography.Text>
                      <Typography.Text type="secondary" ellipsis={{ tooltip: item.user_agent || '未记录客户端信息' }}>
                        {item.user_agent || '未记录客户端信息'}
                      </Typography.Text>
                    </Space>
                  )}
                />
              </List.Item>
            )}
          />
        )}
      </Card>

      <AccountActivityCard key={activityRefresh} />

      <Card id="edit-profile" title={<Space><EditOutlined />编辑个人资料</Space>}>
        {passwordChangeRequired ? (
          <Alert type="warning" showIcon message="修改临时密码后即可编辑个人资料" />
        ) : profile.can_edit_profile ? (
          <Form<ProfileFormValues>
            key={`${profile.updated_at || ''}:${profile.display_name}:${profile.email}`}
            layout="vertical"
            style={{ maxWidth: 520 }}
            initialValues={{ displayName: profile.display_name, email: profile.email }}
            onFinish={async (values) => {
              setProfileSubmitting(true)
              try {
                const updated = await updateAccountProfile({
                  display_name: values.displayName?.trim() || '',
                  email: values.email?.trim() || '',
                })
                setProfile(updated)
                setActivityRefresh((value) => value + 1)
                message.success('个人资料已保存')
              } catch (error) {
                message.error(error instanceof Error ? error.message : '个人资料保存失败')
              } finally {
                setProfileSubmitting(false)
              }
            }}
          >
            <Form.Item name="displayName" label="昵称" rules={[{ max: 64, message: '昵称不能超过 64 个字符' }]}>
              <Input autoComplete="name" maxLength={64} />
            </Form.Item>
            <Form.Item name="email" label="邮箱" rules={[{ type: 'email', message: '请输入有效的邮箱地址' }]}>
              <Input autoComplete="email" maxLength={254} />
            </Form.Item>
            <Button type="primary" htmlType="submit" loading={profileSubmitting}>保存资料</Button>
          </Form>
        ) : (
          <Alert type="info" showIcon message="该管理员账号仍由服务配置管理" />
        )}
      </Card>

      <Card
        title={<Space><SafetyCertificateOutlined />我的权限</Space>}
        extra={!passwordChangeRequired ? <Tag color="blue">共 {profile.permission_details.length} 项</Tag> : undefined}
      >
        {passwordChangeRequired ? (
          <Typography.Text type="secondary">完成密码修改后将加载账号权限</Typography.Text>
        ) : permissionGroups.length > 0 ? (
          <Space direction="vertical" size={20} style={{ width: '100%' }}>
            {permissionGroups.map((group) => (
              <section key={group.category}>
                <Space style={{ marginBottom: 10 }}>
                  <Typography.Text strong>{group.category}</Typography.Text>
                  <Tag>{group.permissions.length} 项</Tag>
                </Space>
                <List
                  grid={{ gutter: 12, xs: 1, md: 2, xl: 3 }}
                  dataSource={group.permissions}
                  renderItem={(permission) => (
                    <List.Item>
                      <Card size="small" style={{ height: '100%' }}>
                        <Space direction="vertical" size={2}>
                          <Typography.Text strong>{permission.name}</Typography.Text>
                          <Typography.Text type="secondary">{permission.description}</Typography.Text>
                          <Typography.Text code>{permission.code}</Typography.Text>
                        </Space>
                      </Card>
                    </List.Item>
                  )}
                />
              </section>
            ))}
          </Space>
        ) : (
          <Typography.Text type="secondary">当前没有分配功能权限</Typography.Text>
        )}
      </Card>

      <Card id="change-password" title={<Space><KeyOutlined />{passwordChangeRequired ? '设置新密码' : '修改密码'}</Space>}>
        {profile.can_change_password ? (
          <Form
            form={passwordForm}
            layout="vertical"
            style={{ maxWidth: 520 }}
            onFinish={async (values) => {
              setSubmitting(true)
              try {
                const result = await changeAccountPassword({
                  current_password: values.currentPassword,
                  new_password: values.newPassword,
                })
                await onSessionChanged(sessionAfterPasswordChange(session, result))
                setActivityRefresh((value) => value + 1)
                passwordForm.resetFields()
                message.success(passwordChangeRequired
                  ? '新密码已生效，账号功能已恢复'
                  : '密码已修改，其他登录会话已失效')

                try {
                  const updatedProfile = await getAccountProfile()
                  setProfile(updatedProfile)
                  if (updatedProfile.can_manage_sessions) await refreshSessions(false)
                } catch (refreshError) {
                  message.warning(refreshError instanceof Error
                    ? `密码已修改，但账号信息刷新失败：${refreshError.message}`
                    : '密码已修改，但账号信息刷新失败；请稍后刷新页面')
                }
              } catch (error) {
                message.error(error instanceof Error ? error.message : '密码修改失败')
              } finally {
                setSubmitting(false)
              }
            }}
          >
            <Form.Item name="currentPassword" label="当前密码" rules={[{ required: true, message: '请输入当前密码' }]}>
              <Input.Password autoComplete="current-password" />
            </Form.Item>
            <Form.Item
              name="newPassword"
              label="新密码"
              extra={ACCOUNT_PASSWORD_CHANGE_HELP}
              dependencies={['currentPassword']}
              rules={[
                { required: true, message: '请输入新密码' },
                ({ getFieldValue }) => ({
                  validator(_, value) {
                    if (!value) return Promise.resolve()
                    const error = accountPasswordChangeValidationMessage(
                      getFieldValue('currentPassword'),
                      value,
                    )
                    return error ? Promise.reject(new Error(error)) : Promise.resolve()
                  },
                }),
              ]}
            >
              <Input.Password
                autoComplete="new-password"
                maxLength={MAX_ACCOUNT_PASSWORD_LENGTH * 2}
              />
            </Form.Item>
            <Form.Item
              name="confirmPassword"
              label="确认新密码"
              dependencies={['newPassword']}
              rules={[
                { required: true, message: '请再次输入新密码' },
                ({ getFieldValue }) => ({
                  validator(_, value) {
                    return !value || getFieldValue('newPassword') === value
                      ? Promise.resolve()
                      : Promise.reject(new Error('两次输入的密码不一致'))
                  },
                }),
              ]}
            >
              <Input.Password
                autoComplete="new-password"
                maxLength={MAX_ACCOUNT_PASSWORD_LENGTH * 2}
              />
            </Form.Item>
            <Button type="primary" htmlType="submit" loading={submitting}>保存新密码</Button>
          </Form>
        ) : (
          <Alert
            type="info"
            showIcon
            message="该管理员账号仍由服务配置管理"
            description="请继续使用现有配置方式维护管理员密码。"
          />
        )}
      </Card>
    </div>
  )
}
