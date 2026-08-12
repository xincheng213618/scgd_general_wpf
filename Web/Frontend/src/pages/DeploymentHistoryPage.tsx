import { EyeOutlined, SafetyCertificateOutlined } from '@ant-design/icons'
import { ProTable, type ProColumns } from '@ant-design/pro-components'
import { Alert, Button, Card, Col, Descriptions, Drawer, Empty, List, Row, Space, Statistic, Tag, Typography } from 'antd'
import { useState } from 'react'
import { getDeploymentHistory } from '../services/admin'
import type { DeploymentHistoryEntry, DeploymentHistoryResponse, DeploymentRetentionSummary } from '../types/admin'
import {
  deploymentCheckDisplay,
  deploymentFailureDisplay,
  deploymentNotice,
  deploymentRecoveryDisplay,
  deploymentSourceDisplay,
  deploymentStatusDisplay,
  deploymentStatusLabels,
} from '../utils/deploymentHistory'
import { humanSize, shortDate } from '../utils/format'

function resultTag(label: string, value: string | boolean | null | undefined, successValues: ReadonlyArray<string | boolean>) {
  const display = deploymentCheckDisplay(value, successValues)
  return <Tag color={display.color}>{label} {display.text}</Tag>
}

function retentionText(summary?: DeploymentRetentionSummary | null) {
  if (!summary) return '未记录'
  const parts = [
    summary.after_count === undefined ? null : `现有 ${summary.after_count}`,
    summary.removed_count === undefined ? null : `清理 ${summary.removed_count}`,
    summary.removed_bytes ? `释放 ${humanSize(summary.removed_bytes)}` : null,
  ].filter(Boolean)
  return parts.join(' · ') || summary.status || '已执行'
}

export function DeploymentHistoryPage() {
  const [summary, setSummary] = useState<DeploymentHistoryResponse['summary']>({
    records: 0,
    malformed_records: 0,
    retention_limit: 500,
    statuses: {},
    sources: {},
  })
  const [selected, setSelected] = useState<DeploymentHistoryEntry | null>(null)

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
      valueEnum: Object.fromEntries(Object.entries(deploymentStatusLabels).map(([key, value]) => [key, { text: value.text }])),
      render: (_, record) => {
        const display = deploymentStatusDisplay(record.status)
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
      renderText: (value: string | null) => deploymentSourceDisplay(value),
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
      width: 300,
      render: (_, record) => (
        <Space size={[0, 4]} wrap>
          {resultTag('构建', record.frontend_build, ['success'])}
          {resultTag('测试', record.backend_targeted_tests, ['passed'])}
          {resultTag('健康', record.health, ['ok'])}
          {resultTag('就绪', record.ready, [true])}
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
        if (record.failure_reason) return <Tag color="red">{deploymentFailureDisplay(record.failure_reason)}</Tag>
        const pid = record.new_pid ? `PID ${record.old_pid ?? '-'} → ${record.new_pid}` : null
        return <Typography.Text type="secondary" ellipsis>{[record.backup_name, pid].filter(Boolean).join(' · ') || '-'}</Typography.Text>
      },
    },
    {
      title: '操作',
      search: false,
      fixed: 'right',
      width: 90,
      render: (_, record) => <Button type="link" icon={<EyeOutlined />} onClick={() => setSelected(record)}>详情</Button>,
    },
  ]

  const selectedNotice = selected ? deploymentNotice(selected) : null
  const selectedStatus = selected ? deploymentStatusDisplay(selected.status) : null

  return (
    <Space direction="vertical" size="middle" className="page-stack">
      <Row gutter={[16, 16]}>
        <Col xs={12} lg={6}><Card><Statistic title="保留记录" value={summary.records} suffix={`/ ${summary.retention_limit}`} /></Card></Col>
        <Col xs={12} lg={6}><Card><Statistic title="成功部署" value={summary.statuses.success ?? 0} /></Card></Col>
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
        pagination={{ pageSize: 20, showSizeChanger: true, showTotal: (total) => `共 ${total} 条` }}
        options={{ density: true, fullScreen: true, reload: true, setting: true }}
        cardBordered
        headerTitle="部署历史"
        toolBarRender={() => [<Typography.Text type="secondary" key="privacy">详情保留诊断结论，但不暴露服务器路径与原始错误</Typography.Text>]}
        scroll={{ x: 1380 }}
      />

      <Drawer
        title="部署诊断详情"
        open={Boolean(selected)}
        onClose={() => setSelected(null)}
        width="min(760px, 100vw)"
        destroyOnHidden
      >
        {selected && selectedNotice && selectedStatus && (
          <Space direction="vertical" size={16} className="page-stack">
            <Alert
              type={selectedNotice.tone}
              showIcon
              icon={<SafetyCertificateOutlined />}
              message={selectedNotice.title}
              description={selectedNotice.description}
            />

            <Descriptions bordered size="small" column={1}>
              <Descriptions.Item label="状态"><Tag color={selectedStatus.color}>{selectedStatus.text}</Tag></Descriptions.Item>
              <Descriptions.Item label="时间">{selected.timestamp ? shortDate(selected.timestamp) : '未记录'}</Descriptions.Item>
              <Descriptions.Item label="来源">{deploymentSourceDisplay(selected.source)}</Descriptions.Item>
              <Descriptions.Item label="部署提交">
                <Typography.Text code copyable={Boolean(selected.commit)}>{selected.commit || '未记录'}</Typography.Text>
              </Descriptions.Item>
              <Descriptions.Item label="上一提交">
                <Typography.Text code copyable={Boolean(selected.previous_commit)}>{selected.previous_commit || '未记录'}</Typography.Text>
              </Descriptions.Item>
              <Descriptions.Item label="备份">{selected.backup_name || '未记录'}</Descriptions.Item>
              <Descriptions.Item label="进程切换">
                {selected.new_pid ? `PID ${selected.old_pid ?? '-'} → ${selected.new_pid}` : '未记录'}
              </Descriptions.Item>
            </Descriptions>

            <Card size="small" title="验证证据">
              <Space size={[0, 8]} wrap>
                {resultTag('前端构建', selected.frontend_build, ['success'])}
                {resultTag('后端测试', selected.backend_targeted_tests, ['passed'])}
                {resultTag('健康检查', selected.health, ['ok'])}
                {resultTag('就绪检查', selected.ready, [true])}
                {resultTag('运行日志', selected.runtime_log_verified, [true])}
              </Space>
            </Card>

            {selected.status === 'failed' && (
              <Card size="small" title={`恢复动作（${selected.recovery.length}）`}>
                {selected.recovery.length > 0 ? (
                  <List
                    size="small"
                    dataSource={selected.recovery}
                    renderItem={(item) => <List.Item>{deploymentRecoveryDisplay(item)}</List.Item>}
                  />
                ) : <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="未记录自动恢复动作" />}
              </Card>
            )}

            <Descriptions title="保留策略执行结果" bordered size="small" column={1}>
              <Descriptions.Item label="部署历史">{retentionText(selected.history_retention)}</Descriptions.Item>
              <Descriptions.Item label="部署备份">{retentionText(selected.backup_retention)}</Descriptions.Item>
              <Descriptions.Item label="Git Bundle">{retentionText(selected.git_bundle_retention)}</Descriptions.Item>
            </Descriptions>
          </Space>
        )}
      </Drawer>
    </Space>
  )
}
