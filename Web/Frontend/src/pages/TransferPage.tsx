import { InboxOutlined } from '@ant-design/icons'
import { Typography } from 'antd'
import { Navigate, useLocation } from 'react-router-dom'
import { TransferPanel } from '../components/TransferPanel'
import type { AuthSession } from '../types/site'
import { getTransferAccessState, getTransferLoginUrl } from '../utils/transferAccess'

export function TransferPage({ session }: { session: AuthSession | null }) {
  const location = useLocation()
  const accessState = getTransferAccessState(session)

  if (session === null || accessState === 'loading') {
    return <div className="route-loading" role="status">正在确认登录状态…</div>
  }
  if (accessState === 'login') {
    return <Navigate to={getTransferLoginUrl(location.pathname, location.search, location.hash)} replace />
  }
  if (accessState === 'password-change') {
    return <Navigate to="/account?password_change=required" replace />
  }
  if (accessState === 'forbidden') {
    return <Navigate to="/account?access=updated" replace />
  }

  const anonymousUpload = !session.authenticated

  return (
    <div className="page-stack transfer-page">
      <section className="transfer-page-header">
        <div className="transfer-page-title">
          <span className="transfer-page-mark"><InboxOutlined /></span>
          <div>
            <span>ColorVision Transfer</span>
            <Typography.Title level={2}>文件中转</Typography.Title>
          </div>
        </div>
        <div className="transfer-account">
          <span className="transfer-account-avatar">{anonymousUpload ? 'V' : (session.username || 'U').slice(0, 1).toUpperCase()}</span>
          <div>
            <strong>{anonymousUpload ? '访客' : session.username || '-'}</strong>
            <span>{anonymousUpload ? '仅上传' : session.is_admin ? '管理员' : '用户'}</span>
          </div>
        </div>
      </section>
      <TransferPanel
        anonymousUpload={anonymousUpload}
        maxUploadBytes={anonymousUpload ? session.anonymous_transfer_max_bytes : undefined}
      />
    </div>
  )
}
