import { EyeOutlined, SafetyCertificateOutlined } from '@ant-design/icons'
import { ProTable, type ProColumns } from '@ant-design/pro-components'
import { Alert, Button, Descriptions, Drawer, Space, Tag, Typography } from 'antd'
import { useState } from 'react'
import { getAuditLog } from '../services/admin'
import type { AuditLogEntry } from '../types/admin'
import {
  auditActionMeta,
  auditActionValueEnum,
  auditActorLabel,
  auditDetailSummary,
  auditTargetLabel,
  parseAuditDetail,
} from '../utils/auditLog'
import { shortDate } from '../utils/format'

function actorDisplay(record: AuditLogEntry) {
  return (
    <Space direction="vertical" size={0}>
      <Typography.Text>{auditActorLabel(record.actor_type)}</Typography.Text>
      <Typography.Text type="secondary" copyable={{ text: record.actor_id }}>
        {record.actor_id || '-'}
      </Typography.Text>
    </Space>
  )
}

function targetDisplay(record: AuditLogEntry) {
  return (
    <Space direction="vertical" size={0}>
      <Typography.Text>{auditTargetLabel(record.target_type)}</Typography.Text>
      {record.target_id && (
        <Typography.Text type="secondary" copyable={{ text: record.target_id }}>
          {record.target_id}
        </Typography.Text>
      )}
    </Space>
  )
}

export function AuditPage() {
  const [selected, setSelected] = useState<AuditLogEntry | null>(null)

  const columns: ProColumns<AuditLogEntry>[] = [
    {
      title: '事件',
      dataIndex: 'action',
      width: 230,
      valueType: 'select',
      valueEnum: auditActionValueEnum(),
      fieldProps: { showSearch: true, allowClear: true },
      render: (_, record) => {
        const meta = auditActionMeta(record.action)
        return (
          <Space direction="vertical" size={0}>
            <Space size={4}>
              <Tag color={meta.color}>{meta.label}</Tag>
              <Typography.Text type="secondary">{meta.category}</Typography.Text>
            </Space>
            <Typography.Text type="secondary" code>{record.action}</Typography.Text>
          </Space>
        )
      },
    },
    {
      title: '操作者',
      dataIndex: 'actor_id',
      width: 190,
      render: (_, record) => actorDisplay(record),
    },
    {
      title: '目标',
      dataIndex: 'target_id',
      width: 190,
      render: (_, record) => targetDisplay(record),
    },
    {
      title: '摘要',
      dataIndex: 'detail',
      search: false,
      render: (_, record) => {
        const summary = auditDetailSummary(record.detail)
        return <Typography.Text ellipsis={{ tooltip: summary }}>{summary}</Typography.Text>
      },
    },
    {
      title: '时间',
      dataIndex: 'created_at',
      width: 180,
      search: false,
      renderText: shortDate,
    },
    {
      title: '时间范围',
      dataIndex: 'created_at_range',
      valueType: 'dateTimeRange',
      hideInTable: true,
      search: {
        transform: (value: string[]) => ({
          since: new Date(value[0]).toISOString(),
          until: new Date(value[1]).toISOString(),
        }),
      },
    },
    {
      title: '操作',
      valueType: 'option',
      width: 82,
      fixed: 'right',
      render: (_, record) => (
        <Button size="small" type="link" icon={<EyeOutlined />} onClick={() => setSelected(record)}>
          查看
        </Button>
      ),
    },
  ]

  const selectedMeta = selected ? auditActionMeta(selected.action) : null
  const detailFields = parseAuditDetail(selected?.detail)

  return (
    <>
      <ProTable<AuditLogEntry>
        rowKey={(record, index) => String(record.id ?? index)}
        columns={columns}
        request={async (params) => {
          const result = await getAuditLog({
            current: params.current,
            pageSize: params.pageSize,
            action: params.action as string | undefined,
            actor: params.actor_id as string | undefined,
            target: params.target_id as string | undefined,
            since: params.since as string | undefined,
            until: params.until as string | undefined,
          })
          return {
            data: result.entries,
            success: true,
            total: result.total,
          }
        }}
        search={{ labelWidth: 'auto' }}
        pagination={{
          pageSize: 20,
          showSizeChanger: true,
          showTotal: (total) => `共 ${total} 条`,
        }}
        options={{ density: true, fullScreen: true, reload: true, setting: true }}
        cardBordered
        headerTitle="审计日志"
        toolBarRender={() => [
          <Typography.Text type="secondary" key="hint">
            红色事件需要结合来源地址与客户端信息排查
          </Typography.Text>,
        ]}
        scroll={{ x: 1180 }}
      />

      <Drawer
        title="审计事件详情"
        open={Boolean(selected)}
        onClose={() => setSelected(null)}
        width="min(760px, 100vw)"
        destroyOnHidden
      >
        {selected && selectedMeta && (
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <Alert
              type={selectedMeta.security ? 'warning' : 'info'}
              showIcon
              icon={<SafetyCertificateOutlined />}
              message={`${selectedMeta.category} · ${selectedMeta.label}`}
              description={selectedMeta.security
                ? '这是安全相关事件。请核对发生时间、来源地址、操作者和客户端信息。'
                : '该记录由服务端审计链路生成，页面未对记录内容进行修改。'}
            />

            <Descriptions bordered size="small" column={1}>
              <Descriptions.Item label="事件代码">
                <Typography.Text code copyable>{selected.action}</Typography.Text>
              </Descriptions.Item>
              <Descriptions.Item label="操作者">{actorDisplay(selected)}</Descriptions.Item>
              <Descriptions.Item label="目标">{targetDisplay(selected)}</Descriptions.Item>
              <Descriptions.Item label="发生时间">{shortDate(selected.created_at)}</Descriptions.Item>
              <Descriptions.Item label="来源地址">
                <Typography.Text code copyable={Boolean(selected.ip)}>{selected.ip || '未记录'}</Typography.Text>
              </Descriptions.Item>
              <Descriptions.Item label="客户端">
                <Typography.Text copyable={Boolean(selected.user_agent)}>{selected.user_agent || '未记录'}</Typography.Text>
              </Descriptions.Item>
            </Descriptions>

            <Descriptions title="事件详情" bordered size="small" column={1}>
              {detailFields.map((field) => (
                <Descriptions.Item label={field.label} key={field.key}>
                  <Typography.Text copyable>{field.value}</Typography.Text>
                </Descriptions.Item>
              ))}
              {detailFields.length === 0 && (
                <Descriptions.Item label="摘要">
                  <Typography.Text copyable>{auditDetailSummary(selected.detail)}</Typography.Text>
                </Descriptions.Item>
              )}
            </Descriptions>
          </Space>
        )}
      </Drawer>
    </>
  )
}
