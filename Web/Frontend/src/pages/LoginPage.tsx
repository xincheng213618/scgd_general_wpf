import { LockOutlined, UserOutlined } from '@ant-design/icons'
import { App, Button, Card, Form, Input, Typography } from 'antd'
import { useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { login } from '../services/auth'

export function LoginPage({ onLoggedIn }: { onLoggedIn: () => Promise<void> }) {
  const { message } = App.useApp()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const [submitting, setSubmitting] = useState(false)
  const next = useMemo(() => searchParams.get('next') || '/admin', [searchParams])

  return (
    <div className="login-screen">
      <Card className="login-card">
        <div className="login-brand">
          <span className="pro-brand-mark">CV</span>
          <Typography.Title level={3}>后台登录</Typography.Title>
          <Typography.Paragraph type="secondary">进入发布、缓存、任务和权限管理。</Typography.Paragraph>
        </div>
        <Form
          layout="vertical"
          onFinish={async (values) => {
            setSubmitting(true)
            try {
              const result = await login({ username: values.username, password: values.password, next })
              await onLoggedIn()
              navigate(result.next || next, { replace: true })
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
      </Card>
    </div>
  )
}
