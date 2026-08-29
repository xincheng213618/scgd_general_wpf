import {
  ClockCircleOutlined,
  CloudUploadOutlined,
  CopyOutlined,
  DownloadOutlined,
  FileOutlined,
  InboxOutlined,
} from '@ant-design/icons'
import { Alert, App, Button, Skeleton, Space, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { getTransferShare } from '../services/transferShares'
import type { TransferShare } from '../types/site'
import { humanSize, shortDate } from '../utils/format'

export function TransferSharePage() {
  const { message } = App.useApp()
  const { token = '' } = useParams()
  const [result, setResult] = useState<{ token: string; share: TransferShare | null; error: string } | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    getTransferShare(token, controller.signal)
      .then((share) => setResult({ token, share, error: '' }))
      .catch((reason) => {
        if (!controller.signal.aborted) {
          setResult({
            token,
            share: null,
            error: reason instanceof Error ? reason.message : '分享链接不可用',
          })
        }
      })
    return () => controller.abort()
  }, [token])

  const currentResult = result?.token === token ? result : null
  const share = currentResult?.share || null
  const error = currentResult?.error || ''

  const copyLink = async () => {
    try {
      await navigator.clipboard.writeText(window.location.href)
      message.success('分享链接已复制')
    } catch {
      message.error('复制失败，请从地址栏复制链接')
    }
  }

  return (
    <div className="transfer-share-page">
      <section className="transfer-share-brand">
        <span><InboxOutlined /></span>
        <div>
          <small>COLORVISION TRANSFER</small>
          <Typography.Title level={2}>文件分享</Typography.Title>
        </div>
      </section>

      <section className="transfer-share-card">
        {!share && !error && <Skeleton active paragraph={{ rows: 4 }} />}
        {error && (
          <div className="transfer-share-error">
            <Alert type="warning" showIcon message="分享链接已失效" description={error} />
            <Button type="primary" icon={<CloudUploadOutlined />} href="/transfer">上传新文件</Button>
          </div>
        )}
        {share && (
          <>
            <div className="transfer-share-file-icon"><FileOutlined /></div>
            <Tag color={share.temporary ? 'blue' : 'green'}>{share.temporary ? '24 小时临时分享' : '长期分享'}</Tag>
            <Typography.Title level={3}>{share.name}</Typography.Title>
            <Typography.Text type="secondary">{humanSize(share.size)} · 更新于 {shortDate(share.modified)}</Typography.Text>
            {share.expires_at && (
              <div className="transfer-share-expiry">
                <ClockCircleOutlined /> 到期时间 {shortDate(share.expires_at)}
              </div>
            )}
            <Space wrap size={12} className="transfer-share-actions">
              <Button type="primary" size="large" icon={<DownloadOutlined />} href={share.download_url}>下载文件</Button>
              <Button size="large" icon={<CopyOutlined />} onClick={() => void copyLink()}>复制链接</Button>
            </Space>
            <div className="transfer-share-upload-cta">
              <div>
                <strong>也要发送文件？</strong>
                <span>无需登录，支持大文件和断点续传。</span>
              </div>
              <Button icon={<CloudUploadOutlined />} href="/transfer">我也要传文件</Button>
            </div>
          </>
        )}
      </section>
    </div>
  )
}
