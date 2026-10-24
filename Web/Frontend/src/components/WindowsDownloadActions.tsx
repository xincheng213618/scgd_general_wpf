import { CloudDownloadOutlined, DownOutlined, FileDoneOutlined } from '@ant-design/icons'
import { Button, Dropdown, Space } from 'antd'
import type { DownloadAssistant, ReleaseArtifact } from '../types/site'
import { downloadPath, humanSize } from '../utils/format'

interface WindowsDownloadActionsProps {
  assistant?: DownloadAssistant | null
  installer?: ReleaseArtifact
  className?: string
  size?: 'large' | 'middle'
}

export function WindowsDownloadActions({ assistant, installer, className, size = 'middle' }: WindowsDownloadActionsProps) {
  const items = [
    {
      key: 'assistant',
      icon: <CloudDownloadOutlined />,
      disabled: !assistant?.relative_path,
      label: assistant?.relative_path ? (
        <a className="windows-download-option" href={downloadPath(assistant.relative_path)} download>
          <strong>下载助手 <small>{humanSize(assistant.size)}</small></strong>
          <span>联网获取最新版 · 需要 .NET Framework 4.8</span>
        </a>
      ) : (
        <span className="windows-download-option">
          <strong>下载助手</strong>
          <span>暂未提供，可直接下载完整安装包</span>
        </span>
      ),
    },
    {
      key: 'installer',
      icon: <FileDoneOutlined />,
      disabled: !installer?.relative_path,
      label: installer?.relative_path ? (
        <a className="windows-download-option" href={downloadPath(installer.relative_path)} download>
          <strong>完整安装包 <small>{installer.size ? humanSize(installer.size) : ''}</small></strong>
          <span>{installer.version ? `v${installer.version} · ` : ''}下载后直接运行，也可保存到 U 盘</span>
        </a>
      ) : (
        <span className="windows-download-option">
          <strong>完整安装包</strong>
          <span>暂无可下载版本</span>
        </span>
      ),
    },
  ]

  return (
    <Space.Compact className="windows-download-actions" size={size}>
      <Button
        className={className}
        type="primary"
        size={size}
        shape="round"
        icon={<CloudDownloadOutlined />}
        href={installer?.relative_path ? downloadPath(installer.relative_path) : undefined}
        download
        disabled={!installer?.relative_path}
        title={installer?.relative_path ? `完整安装包${installer.version ? ` v${installer.version}` : ''}${installer.size ? ` · ${humanSize(installer.size)}` : ''}` : '暂无可下载版本'}
      >
        下载 Windows 桌面端
      </Button>
      <Dropdown menu={{ items, className: 'windows-download-menu' }} trigger={['click']} placement="bottomRight">
        <Button
          className={`windows-download-toggle ${className || ''}`}
          type="primary"
          size={size}
          shape="round"
          icon={<DownOutlined />}
          aria-label="选择 Windows 下载方式"
          disabled={!assistant?.relative_path && !installer?.relative_path}
        />
      </Dropdown>
    </Space.Compact>
  )
}
