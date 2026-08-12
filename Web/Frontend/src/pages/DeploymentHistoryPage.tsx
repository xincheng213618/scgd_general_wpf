import { ProTable, type ProColumns } from '@ant-design/pro-components'
import { Card, Col, Row, Space, Statistic, Tag, Typography } from 'antd'
import { useState } from 'react'
import { getDeploymentHistory } from '../services/admin'
import type { DeploymentHistoryEntry, DeploymentHistoryResponse } from '../types/admin'

const statusLabels: Record<string, { color: string; text: string }> = {
  success: { color: 'green', text: '成功' },
  failed: { color: 'red', text: '失败' },
  already_current: { color: 'blue', text: '已是当前版本' },
}

const failureLabels: Record<string, string> = {
  source_control: '代码同步',
  frontend_build: '前端构建',
  tests: '自动测试',
  service_health: '服务健康',
  backup: '备份',
  deployment: '部署流程',
}

function resultTag(label: string, value: string | null | undefined, successValue = 'success') {
  if (!value) return <Tag>{label} 未记录</Tag>
  const passed = value === successValue || value === 'passed' || value === 'ok'
  return <Tag color={passed ? 'green' : value === 'skipped' ? 'gold' : 'red'}>{label} {value}</Tag>
}

const columns: ProColumns<DeploymentHistoryEntry>[] = [
  {
    title: '时间',
    dataIndex: 'timestamp',
    valueType: 'dateTime',
    width: 180,
    search: false,
  },
  {
    title: '状态',
    dataIndex: 'status',
    width: 140,
    valueType: 'select',
    valueEnum: {
      success: { text: '成功' },
      failed: { text: '失败' },
      already_current: { text: '已是当前版本' },
    },
    render: (_, record) => {
      const display = statusLabels[record.status] ?? { color: 'default', text: record.status }
      return <Tag color={display.color}>{display.text}</Tag>
    },
  },
  {
    title: '来源',
    dataIndex: 'source',
    width: 120,
    valueType: 'select',
    valueEnum: {
      origin: { text: 'Git 远端' },
      git_bundle: { text: 'Git Bundle' },
      legacy: { text: '早期记录' },
    },
    renderText: (value: string | null) => value || '早期记录',
  },
  {
    title: '提交',
    dataIndex: 'commit',
    width: 150,
    ellipsis: true,
    render: (_, record) => record.commit ? (
      <Typography.Text copyable={{ text: record.commit }} code>{record.commit.slice(0, 10)}</Typography.Text>
    ) : '-',
  },
  {
    title: '验证',
    search: false,
    width: 270,
    render: (_, record) => (
      <Space size={[0, 4]} wrap>
        {resultTag('构建', record.frontend_build)}
        {resultTag('测试', record.backend_targeted_tests)}
        {resultTag('健康', record.health, 'ok')}
        {record.ready === true && <Tag color="green">ready</Tag>}
      </Space>
    ),
  },
  {
    title: '保留清理',
    search: false,
    width: 210,
    render: (_, record) => (
      <Space size={[0, 4]} wrap>
        <Tag>历史 {record.history_retention?.removed_count ?? '-'}</Tag>
        <Tag>备份 {record.backup_retention?.removed_count ?? '-'}</Tag>
        <Tag>Bundle {record.git_bundle_retention?.removed_count ?? '-'}</Tag>
      </Space>
    ),
  },
  {
    title: '部署信息',
    search: false,
    render: (_, record) => {
      if (record.failure_reason) {
        return <Tag color="red">{failureLabels[record.failure_reason] ?? record.failure_reason}</Tag>
      }
      const pid = record.new_pid ? `PID ${record.old_pid ?? '-'} → ${record.new_pid}` : null
      return (
        <Typography.Text type="secondary" ellipsis>
          {[record.backup_name, pid].filter(Boolean).join(' · ') || '-'}
        </Typography.Text>
      )
    },
  },
]

export function DeploymentHistoryPage() {
  const [summary, setSummary] = useState<DeploymentHistoryResponse['summary']>({
    records: 0,
    malformed_records: 0,
    retention_limit: 500,
    statuses: {},
    sources: {},
  })

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <Row gutter={[16, 16]}>
        <Col xs={12} lg={6}><Card><Statistic title="保留记录" value={summary.records} suffix={`/ ${summary.retention_limit}`} /></Card></Col>
        <Col xs={12} lg={6}><Card><Statistic title="成功" value={summary.statuses.success ?? 0} /></Card></Col>
        <Col xs={12} lg={6}><Card><Statistic title="失败" value={summary.statuses.failed ?? 0} valueStyle={{ color: summary.statuses.failed ? '#cf1322' : undefined }} /></Card></Col>
        <Col xs={12} lg={6}><Card><Statistic title="异常记录" value={summary.malformed_records} valueStyle={{ color: summary.malformed_records ? '#d46b08' : undefined }} /></Card></Col>
      </Row>
      <ProTable<DeploymentHistoryEntry>
        rowKey="sequence"
        columns={columns}
        request={async (params) => {
          const result = await getDeploymentHistory({
            current: params.current,
            pageSize: params.pageSize,
            status: params.status as string | undefined,
            source: params.source as string | undefined,
            commit: params.commit as string | undefined,
          })
          setSummary(result.summary)
          return { data: result.entries, success: true, total: result.total }
        }}
        pagination={{ pageSize: 20, showSizeChanger: true }}
        options={{ density: true, fullScreen: true, reload: true, setting: true }}
        cardBordered
        headerTitle="部署历史"
        toolBarRender={() => [<Typography.Text type="secondary" key="privacy">已隐藏服务器路径与原始错误详情</Typography.Text>]}
        scroll={{ x: 1280 }}
      />
    </Space>
  )
}
