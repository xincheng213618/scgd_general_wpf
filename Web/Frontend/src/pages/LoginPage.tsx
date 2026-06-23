import { LockOutlined, LoginOutlined, UserAddOutlined, UserOutlined } from '@ant-design/icons'
import { App, Button, Card, Form, Input, Segmented, Typography } from 'antd'
import { useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { login, register } from '../services/auth'
import type { AuthSession } from '../types/site'

export function LoginPage({ onLoggedIn }: { onLoggedIn: () => Promise<void> }) {
  const { message } = App.useApp()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [submitting, setSubmitting] = useState(false)
  const next = useMemo(() => searchParams.get('next') || '/transfer', [searchParams])

  const resolveNext = (result: AuthSession & { next?: string }) => {
    const target = result.next || next
    if (!result.is_admin && target.startsWith('/admin')) {
      return '/transfer'
    }
    return target
  }

  return (
    <div className="login-screen">
      <Card className="login-card">
        <div className="login-brand">
          <span className="pro-brand-mark">
            <img src="/brand/colorvision-icon.png" alt="" />
          </span>
          <Typography.Title level={3}>ColorVision 账号</Typography.Title>
          <Typography.Paragraph type="secondary">普通账号用于文件中转；管理员账号继续进入后台。</Typography.Paragraph>
        </div>
        <Segmented
          block
          value={mode}
          onChange={(value) => setMode(value as 'login' | 'register')}
          options={[
            { label: '登录', value: 'login', icon: <LoginOutlined /> },
            { label: '注册', value: 'register', icon: <UserAddOutlined /> },
          ]}
        />
        {mode === 'login' ? (
          <Form
            layout="vertical"
            className="auth-form"
            onFinish={async (values) => {
              setSubmitting(true)
              try {
                const result = await login({ username: values.username, password: values.password, next })
                await onLoggedIn()
                navigate(resolveNext(result), { replace: true })
              } catch (error) {
                message.error(error instanceof Error ? error.message : '登录失败')
              } finally {
                setSubmitting(false)
              }
            }}
          >
            <Form.Item name="username" label="用户名" rules={[{ required: true, message: '请输入用户名' }]}>
              <Input prefix={<UserOutlined />} autoComplete="username" />
            </Form.Item>
            <Form.Item name="password" label="密码" rules={[{ required: true, message: '请输入密码' }]}>
              <Input.Password prefix={<LockOutlined />} autoComplete="current-password" />
            </Form.Item>
            <Button type="primary" htmlType="submit" block loading={submitting}>
              登录
            </Button>
          </Form>
        ) : (
          <Form
            layout="vertical"
            className="auth-form"
            onFinish={async (values) => {
              setSubmitting(true)
              try {
                const result = await register({ username: values.username, password: values.password, next: '/transfer' })
                await onLoggedIn()
                navigate(resolveNext(result), { replace: true })
              } catch (error) {
                message.error(error instanceof Error ? error.message : '注册失败')
              } finally {
                setSubmitting(false)
              }
            }}
          >
            <Form.Item
              name="username"
              label="用户名"
              rules={[
                { required: true, message: '请输入用户名' },
                { pattern: /^[A-Za-z0-9_.-]{3,32}$/, message: '3-32 位字母、数字、下划线、点或连字符' },
              ]}
            >
              <Input prefix={<UserOutlined />} autoComplete="username" />
            </Form.Item>
            <Form.Item
              name="password"
              label="密码"
              rules={[
                { required: true, message: '请输入密码' },
                { min: 6, message: '密码至少需要 6 位' },
              ]}
            >
              <Input.Password prefix={<LockOutlined />} autoComplete="new-password" />
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
              <Input.Password prefix={<LockOutlined />} autoComplete="new-password" />
            </Form.Item>
            <Button type="primary" htmlType="submit" block loading={submitting}>
              注册并进入中转
            </Button>
          </Form>
        )}
      </Card>
    </div>
  )
}
