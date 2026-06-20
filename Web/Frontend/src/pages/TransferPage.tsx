import { InboxOutlined } from '@ant-design/icons'
import { Space, Tag, Typography } from 'antd'
import { useNavigate } from 'react-router-dom'
import { TransferPanel } from '../components/TransferPanel'
import type { AuthSession } from '../types/site'

export function TransferPage({ session }: { session: AuthSession | null }) {
  const navigate = useNavigate()

  return (
    <Space direction="vertical" size={16} className="page-stack">
      <section className="compact-page-hero">
        <div>
          <span className="hero-kicker light">
            <InboxOutlined />
            Transfer
          </span>
          <Typography.Title level={2}>文件中转</Typography.Title>
          <Typography.Paragraph>
            登录后上传、下载和清理临时文件。普通用户只开放中转，发布和系统维护仍在后台处理。
          </Typography.Paragraph>
        </div>
        <div className="compact-stat-strip">
          <span>
            <strong>{session?.username || '-'}</strong>
            当前账号
          </span>
          <Tag color={session?.is_admin ? 'blue' : 'green'}>{session?.is_admin ? '管理员' : '普通用户'}</Tag>
        </div>
      </section>
      <TransferPanel onAuthRequired={() => navigate('/login?next=/transfer', { replace: true })} />
    </Space>
  )
}
