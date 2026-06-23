import { Progress, Space, Typography } from 'antd'
import { humanSize } from '../utils/format'

type UploadProgressProps = {
  active?: boolean
  file?: File | null
  label?: string
  percent: number
}

export function UploadProgress({ active = false, file, label = '上传任务', percent }: UploadProgressProps) {
  if (!active && percent <= 0) {
    return null
  }

  const rounded = Math.max(0, Math.min(100, Math.round(percent)))
  const statusText = active && rounded >= 100 ? '服务器处理中' : active ? '正在上传' : '上传完成'

  return (
    <div className="upload-progress-panel">
      <Space direction="vertical" size={4} className="wide-space">
        <Progress percent={rounded} status={active ? 'active' : 'success'} />
        <Typography.Text type="secondary" className="upload-progress-meta">
          {file ? `${file.name} · ${humanSize(file.size)}` : label} · {statusText}
        </Typography.Text>
      </Space>
    </div>
  )
}
