import { HomeOutlined, IdcardOutlined, LockOutlined, LoginOutlined, MailOutlined, SafetyCertificateOutlined, UserAddOutlined, UserOutlined } from '@ant-design/icons'
import { Alert, App, Button, Card, Form, Input, Modal, Segmented, Spin, Typography } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import { Navigate, useNavigate, useSearchParams } from 'react-router-dom'
import { login, register, requestPasswordRecovery } from '../services/auth'
import { ApiRequestError } from '../services/request'
import type { AuthSession, AuthSessionUpdater } from '../types/site'
import { formatLoginRetryCountdown, normalizeRetryAfter } from '../utils/authSecurity'
import {
  authenticatedEntryRedirect,
  authEntryDescription,
  REGISTRATION_WELCOME_PATH,
  resolveAuthEntryMode,
  shouldShowRegistrationDisabledNotice,
} from '../utils/registrationPolicy'
import {
  ACCOUNT_PASSWORD_HELP,
  MAX_ACCOUNT_PASSWORD_LENGTH,
  accountPasswordValidationMessage,
} from '../utils/userAccounts'

export function LoginPage({
  session,
  onLoggedIn,
}: {
  session: AuthSession | null
  onLoggedIn: AuthSessionUpdater
}) {
  const { message } = App.useApp()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const [mode, setMode] = useState<'login' | 'register'>(() => (
    searchParams.get('mode') === 'register' ? 'register' : 'login'
  ))
  const [submitting, setSubmitting] = useState(false)
  const [loginRetryAfter, setLoginRetryAfter] = useState(() => (
    normalizeRetryAfter(searchParams.get('retry_after'))
  ))
  const [registrationRetryAfter, setRegistrationRetryAfter] = useState(0)
  const [loginError, setLoginError] = useState('')
  const [registrationError, setRegistrationError] = useState('')
  const [recoveryOpen, setRecoveryOpen] = useState(false)
  const [recoverySubmitting, setRecoverySubmitting] = useState(false)
  const [recoveryError, setRecoveryError] = useState('')
  const [recoveryRetryAfter, setRecoveryRetryAfter] = useState(0)
  const [recoveryForm] = Form.useForm<{ identifier: string }>()
  const next = useMemo(() => searchParams.get('next') || '/transfer', [searchParams])
  const registrationEnabled = session?.public_registration_enabled === true
  const effectiveMode = resolveAuthEntryMode(mode, registrationEnabled)
  const showRegistrationDisabledNotice = shouldShowRegistrationDisabledNotice(
    searchParams.get('mode'),
    registrationEnabled,
  )

  useEffect(() => {
    if (loginRetryAfter <= 0) return undefined
    const timer = window.setTimeout(() => {
      setLoginRetryAfter((value) => Math.max(0, value - 1))
    }, 1000)
    return () => window.clearTimeout(timer)
  }, [loginRetryAfter])

  useEffect(() => {
    if (registrationRetryAfter <= 0) return undefined
    const timer = window.setTimeout(() => {
      setRegistrationRetryAfter((value) => Math.max(0, value - 1))
    }, 1000)
    return () => window.clearTimeout(timer)
  }, [registrationRetryAfter])

  useEffect(() => {
    if (recoveryRetryAfter <= 0) return undefined
    const timer = window.setTimeout(() => {
      setRecoveryRetryAfter((value) => Math.max(0, value - 1))
    }, 1000)
    return () => window.clearTimeout(timer)
  }, [recoveryRetryAfter])

  const resolveNext = (result: AuthSession & { next?: string }) => {
    return authenticatedEntryRedirect(result, result.next || next)
  }

  if (session === null) {
    return (
      <div className="login-screen">
        <Card className="login-card">
          <div style={{ textAlign: 'center' }}><Spin tip="正在确认登录状态…" /></div>
        </Card>
      </div>
    )
  }
  if (session.authenticated) {
    return <Navigate to={authenticatedEntryRedirect(session, searchParams.get('next'))} replace />
  }

  return (
    <div className="login-screen">
      <Card className="login-card">
        <div className="login-brand">
          <span className="pro-brand-mark">
            <img src="/brand/colorvision-icon.png" alt="" />
          </span>
          <Typography.Title level={3}>ColorVision 账号</Typography.Title>
          <Typography.Paragraph type="secondary">
            {authEntryDescription(registrationEnabled)}
          </Typography.Paragraph>
        </div>
        {registrationEnabled ? (
          <Segmented
            block
            value={effectiveMode}
            disabled={submitting || recoverySubmitting}
            onChange={(value) => {
              setMode(value as 'login' | 'register')
              setLoginError('')
              setRegistrationError('')
            }}
            options={[
              { label: '登录', value: 'login', icon: <LoginOutlined aria-hidden="true" /> },
              { label: '注册', value: 'register', icon: <UserAddOutlined aria-hidden="true" /> },
            ]}
          />
        ) : session && showRegistrationDisabledNotice && (
          <Alert
            type="info"
            showIcon
            message="公开注册已关闭"
            description="请使用管理员创建的账号登录。"
          />
        )}
        {effectiveMode === 'login' ? (
          <Form
            layout="vertical"
            className="auth-form"
            onValuesChange={() => setLoginError('')}
            onFinish={async (values) => {
              if (loginRetryAfter > 0) return
              setLoginError('')
              setSubmitting(true)
              try {
                const result = await login({ username: values.username, password: values.password, next })
                setLoginRetryAfter(0)
                await onLoggedIn(result)
                navigate(resolveNext(result), { replace: true })
              } catch (error) {
                if (error instanceof ApiRequestError && error.status === 429) {
                  setLoginRetryAfter(Math.max(1, normalizeRetryAfter(error.retryAfter)))
                } else {
                  setLoginError(error instanceof Error ? error.message : '登录失败')
                }
              } finally {
                setSubmitting(false)
              }
            }}
          >
            {loginError && (
              <Alert
                type="error"
                showIcon
                closable
                message="登录失败"
                description={loginError}
                onClose={() => setLoginError('')}
              />
            )}
            {loginRetryAfter > 0 && (
              <Alert
                type="warning"
                showIcon
                message="登录暂时受限"
                description={`失败尝试次数过多，请在 ${formatLoginRetryCountdown(loginRetryAfter)} 后重试。`}
              />
            )}
            <Form.Item
              name="username"
              label="用户名"
              extra="用户名不区分大小写"
              rules={[{ required: true, message: '请输入用户名' }]}
            >
              <Input prefix={<UserOutlined aria-hidden="true" />} autoComplete="username" />
            </Form.Item>
            <Form.Item name="password" label="密码" rules={[{ required: true, message: '请输入密码' }]}>
              <Input.Password prefix={<LockOutlined aria-hidden="true" />} autoComplete="current-password" />
            </Form.Item>
            <Button
              type="primary"
              htmlType="submit"
              block
              loading={submitting}
              disabled={submitting || loginRetryAfter > 0}
            >
              {loginRetryAfter > 0
                ? `请等待 ${formatLoginRetryCountdown(loginRetryAfter)}`
                : '登录'}
            </Button>
            <Button
              type="link"
              block
              icon={<SafetyCertificateOutlined aria-hidden="true" />}
              disabled={submitting}
              onClick={() => {
                setRecoveryError('')
                setRecoveryOpen(true)
              }}
            >
              忘记密码？申请管理员协助
            </Button>
          </Form>
        ) : (
          <Form
            layout="vertical"
            className="auth-form"
            onValuesChange={() => setRegistrationError('')}
            onFinish={async (values) => {
              if (registrationRetryAfter > 0) return
              setRegistrationError('')
              setSubmitting(true)
              try {
                const result = await register({
                  username: values.username,
                  password: values.password,
                  display_name: values.displayName?.trim() || '',
                  email: values.email?.trim() || '',
                  next: REGISTRATION_WELCOME_PATH,
                })
                setRegistrationRetryAfter(0)
                await onLoggedIn(result)
                navigate(resolveNext(result), { replace: true })
              } catch (error) {
                if (error instanceof ApiRequestError && error.status === 429) {
                  setRegistrationRetryAfter(Math.max(1, normalizeRetryAfter(error.retryAfter)))
                } else {
                  setRegistrationError(error instanceof Error ? error.message : '注册失败')
                }
              } finally {
                setSubmitting(false)
              }
            }}
          >
            {registrationError && (
              <Alert
                type="error"
                showIcon
                closable
                message="注册失败"
                description={registrationError}
                onClose={() => setRegistrationError('')}
              />
            )}
            {registrationRetryAfter > 0 && (
              <Alert
                type="warning"
                showIcon
                message="注册暂时受限"
                description={`当前来源的注册请求过于频繁，请在 ${formatLoginRetryCountdown(registrationRetryAfter)} 后重试。`}
              />
            )}
            <Form.Item
              name="username"
              label="用户名"
              rules={[
                { required: true, message: '请输入用户名' },
                { pattern: /^[A-Za-z0-9_.-]{3,32}$/, message: '3-32 位字母、数字、下划线、点或连字符' },
              ]}
            >
              <Input prefix={<UserOutlined aria-hidden="true" />} autoComplete="username" />
            </Form.Item>
            <Form.Item
              name="displayName"
              label="昵称"
              rules={[{ max: 64, message: '昵称不能超过 64 个字符' }]}
            >
              <Input prefix={<IdcardOutlined aria-hidden="true" />} autoComplete="name" maxLength={64} />
            </Form.Item>
            <Form.Item
              name="email"
              label="邮箱"
              rules={[{ type: 'email', message: '请输入有效的邮箱地址' }]}
            >
              <Input prefix={<MailOutlined aria-hidden="true" />} autoComplete="email" maxLength={254} />
            </Form.Item>
            <Form.Item
              name="password"
              label="密码"
              extra={ACCOUNT_PASSWORD_HELP}
              rules={[
                { required: true, message: '请输入密码' },
                {
                  validator(_, value) {
                    if (!value) return Promise.resolve()
                    const error = accountPasswordValidationMessage(value)
                    return error ? Promise.reject(new Error(error)) : Promise.resolve()
                  },
                },
              ]}
            >
              <Input.Password
                prefix={<LockOutlined aria-hidden="true" />}
                autoComplete="new-password"
                maxLength={MAX_ACCOUNT_PASSWORD_LENGTH * 2}
              />
            </Form.Item>
            <Form.Item
              name="confirm"
              label="确认密码"
              dependencies={['password']}
              rules={[
                { required: true, message: '请再次输入密码' },
                ({ getFieldValue }) => ({
                  validator(_, value) {
                    if (!value || getFieldValue('password') === value) {
                      return Promise.resolve()
                    }
                    return Promise.reject(new Error('两次密码不一致'))
                  },
                }),
              ]}
            >
              <Input.Password
                prefix={<LockOutlined aria-hidden="true" />}
                autoComplete="new-password"
                maxLength={MAX_ACCOUNT_PASSWORD_LENGTH * 2}
              />
            </Form.Item>
            <Button
              type="primary"
              htmlType="submit"
              block
              loading={submitting}
              disabled={submitting || registrationRetryAfter > 0}
            >
              {registrationRetryAfter > 0
                ? `请等待 ${formatLoginRetryCountdown(registrationRetryAfter)}`
                : '注册并进入个人中心'}
            </Button>
          </Form>
        )}
        <Button
          className="login-home-link"
          type="link"
          block
          icon={<HomeOutlined aria-hidden="true" />}
          onClick={() => navigate('/')}
        >
          返回站点首页
        </Button>
      </Card>
      <Modal
        title="申请找回密码"
        open={recoveryOpen}
        okText={recoveryRetryAfter > 0
          ? `请等待 ${formatLoginRetryCountdown(recoveryRetryAfter)}`
          : '提交申请'}
        cancelText="取消"
        confirmLoading={recoverySubmitting}
        okButtonProps={{ disabled: recoveryRetryAfter > 0 }}
        destroyOnHidden
        onCancel={() => {
          if (recoverySubmitting) return
          setRecoveryOpen(false)
          setRecoveryError('')
          recoveryForm.resetFields()
        }}
        onOk={async () => {
          if (recoveryRetryAfter > 0) return
          try {
            const values = await recoveryForm.validateFields()
            setRecoveryError('')
            setRecoverySubmitting(true)
            const result = await requestPasswordRecovery(values.identifier.trim())
            setRecoveryRetryAfter(0)
            message.success(result.message)
            setRecoveryOpen(false)
            recoveryForm.resetFields()
          } catch (error) {
            if (error && typeof error === 'object' && 'errorFields' in error) return
            if (error instanceof ApiRequestError && error.status === 429) {
              setRecoveryRetryAfter(Math.max(1, normalizeRetryAfter(error.retryAfter)))
              setRecoveryError('')
            } else {
              setRecoveryError(error instanceof Error ? error.message : '找回申请提交失败')
            }
          } finally {
            setRecoverySubmitting(false)
          }
        }}
      >
        {recoveryRetryAfter > 0 && (
          <Alert
            type="warning"
            showIcon
            message="找回申请暂时受限"
            description={`当前来源的找回请求过于频繁，请在 ${formatLoginRetryCountdown(recoveryRetryAfter)} 后重试。`}
            style={{ marginBottom: 16 }}
          />
        )}
        {recoveryError && (
          <Alert
            type="error"
            showIcon
            message="找回申请提交失败"
            description={recoveryError}
            style={{ marginBottom: 16 }}
          />
        )}
        <Alert
          type="info"
          showIcon
          message="管理员协助重置"
          description="提交后请联系管理员。为保护账号隐私，无论用户名或邮箱是否存在，页面都会显示相同结果。"
          style={{ marginBottom: 16 }}
        />
        <Form form={recoveryForm} layout="vertical" onValuesChange={() => setRecoveryError('')}>
          <Form.Item
            name="identifier"
            label="用户名或邮箱"
            rules={[
              { required: true, message: '请输入用户名或邮箱' },
              { max: 254, message: '用户名或邮箱不能超过 254 个字符' },
            ]}
          >
            <Input
              prefix={<MailOutlined aria-hidden="true" />}
              autoComplete="username"
              maxLength={254}
              autoFocus
            />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  )
}
