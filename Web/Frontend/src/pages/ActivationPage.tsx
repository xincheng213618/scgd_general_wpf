import { EyeOutlined } from '@ant-design/icons'
import { ProTable, type ProColumns } from '@ant-design/pro-components'
import { App, Button, Drawer, Space, Tag, Typography } from 'antd'
import { useState } from 'react'
import { getActivationDevices, getActivationLicenses } from '../services/admin'
import type { ActivationDevice, ActivationLicense } from '../types/admin'

const dateFormat = new Intl.DateTimeFormat('zh-CN', {
  year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit',
})

function formatTime(seconds: number | null) {
  return seconds === null ? '—' : dateFormat.format(seconds * 1000)
}

const deviceColumns: ProColumns<ActivationDevice>[] = [
  {
    title: '设备', dataIndex: 'device_name', width: 170,
    render: (_, record) => (
      <Space direction="vertical" size={0}>
        <Typography.Text>{record.device_name}</Typography.Text>
        <Typography.Text type="secondary" copyable={{ text: record.device_id }}>{record.device_id.slice(0, 12)}</Typography.Text>
      </Space>
    ),
  },
  {
    title: '模式', dataIndex: 'mode', width: 170,
    render: (_, record) => record.mode === 'offline' ? '离线（客户端报告 TPM）' : '在线',
  },
  {
    title: '状态', dataIndex: 'status', width: 84,
    render: (_, record) => record.status === 'active'
      ? <Tag color="green">使用中</Tag>
      : <Tag>{record.status === 'released' ? '已释放' : '已到期'}</Tag>,
  },
  { title: '激活时间', width: 170, render: (_, record) => formatTime(record.activated_at) },
  { title: '最近验证', width: 170, render: (_, record) => formatTime(record.last_seen_at) },
  { title: '占用到期', width: 170, render: (_, record) => formatTime(record.expires_at) },
  { title: '释放时间', width: 170, render: (_, record) => formatTime(record.released_at) },
]

export function ActivationPage() {
  const { message } = App.useApp()
  const [selected, setSelected] = useState<ActivationLicense | null>(null)
  const columns: ProColumns<ActivationLicense>[] = [
    { title: '许可证 ID 或设备名称', dataIndex: 'q', hideInTable: true, fieldProps: { maxLength: 100 } },
    {
      title: '许可证 ID', dataIndex: 'id', search: false, width: 190,
      render: (_, record) => <Typography.Text code copyable={{ text: record.id }}>{record.id.slice(0, 12)}</Typography.Text>,
    },
    {
      title: '状态', dataIndex: 'status', width: 100, valueType: 'select',
      valueEnum: { active: { text: '有效' }, expired: { text: '已到期' } },
      render: (_, record) => record.status === 'active' ? <Tag color="green">有效</Tag> : <Tag>已到期</Tag>,
    },
    { title: '设备占用', width: 110, search: false, render: (_, record) => `${record.active_devices} / ${record.device_limit}` },
    { title: '签发时间', width: 170, search: false, render: (_, record) => formatTime(record.issued_at) },
    { title: '许可证到期', width: 170, search: false, render: (_, record) => formatTime(record.expires_at) },
    {
      title: '操作', width: 100, search: false,
      render: (_, record) => <Button type="link" icon={<EyeOutlined />} onClick={() => setSelected(record)}>查看设备</Button>,
    },
  ]

  return (
    <>
      <ProTable<ActivationLicense>
        rowKey="id"
        headerTitle="已签发许可证"
        columns={columns}
        request={async (params) => {
          const result = await getActivationLicenses(params)
          return { data: result.licenses, total: result.total, success: true }
        }}
        onRequestError={(error) => void message.error(`加载许可证失败 · ${error.message}`)}
        pagination={{ pageSize: 20, showSizeChanger: true, showTotal: (total) => `共 ${total} 条` }}
        scroll={{ x: 840 }}
      />
      <Drawer
        title="许可证设备"
        open={selected !== null}
        onClose={() => setSelected(null)}
        width="min(1160px, 100vw)"
        destroyOnHidden
      >
        {selected && (
          <ProTable<ActivationDevice>
            key={selected.id}
            headerTitle={<Typography.Text code copyable>{selected.id}</Typography.Text>}
            rowKey="device_id"
            columns={deviceColumns}
            search={false}
            request={async ({ current, pageSize }) => {
              const result = await getActivationDevices(selected.id, current, pageSize)
              return { data: result.devices, total: result.total, success: true }
            }}
            onRequestError={(error) => void message.error(`加载设备失败 · ${error.message}`)}
            pagination={{ pageSize: 20, showSizeChanger: true, showTotal: (total) => `共 ${total} 台` }}
            scroll={{ x: 1100 }}
          />
        )}
      </Drawer>
    </>
  )
}
