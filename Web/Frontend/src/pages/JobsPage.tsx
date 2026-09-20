import { HistoryOutlined, PlayCircleOutlined } from '@ant-design/icons'
import { ProTable, type ActionType, type ProColumns } from '@ant-design/pro-components'
import { Alert, App, Button, Card, Col, Drawer, Popconfirm, Radio, Row, Space, Statistic, Tag, Typography } from 'antd'
import { useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { getJobRuns, listJobs, runJob, setJobEnabled } from '../services/admin'
import type { JobRun, ScheduledJob } from '../types/admin'
import type { AuthSession } from '../types/site'
import { shortDate } from '../utils/format'
import { filterJobs, formatJobDuration, formatJobInterval, jobHistoryPath, jobNeedsAttention, jobStatusMeta, jobTypeLabels, jobViewFromSearch, summarizeJobs, type JobView } from '../utils/jobOperations'
import { getAdminOperationsCapabilities } from '../utils/permissions'

function statusTag(status?: string) {
  if (!status) return <Tag>暂无</Tag>
  const display = jobStatusMeta[status] ?? { color: 'default', label: status }
  return <Tag color={display.color}>{display.label}</Tag>
}

const historyColumns: ProColumns<JobRun>[] = [
  {
    title: '开始时间',
    dataIndex: 'started_at',
    width: 165,
    search: false,
    renderText: shortDate,
  },
  {
    title: '状态',
    dataIndex: 'status',
    width: 110,
    valueType: 'select',
    valueEnum: {
      success: { text: '成功' },
      error: { text: '失败' },
      running: { text: '运行中' },
      interrupted: { text: '已中断' },
    },
    render: (_, record) => statusTag(record.status),
  },
  {
    title: '耗时',
    dataIndex: 'duration_ms',
    width: 110,
    align: 'right',
    search: false,
    renderText: formatJobDuration,
  },
  {
    title: '结果',
    key: 'result',
    search: false,
    render: (_, record) => (
      <Typography.Text
        type={record.error ? 'danger' : 'secondary'}
        ellipsis={{ tooltip: record.error || record.summary || '-' }}
      >
        {record.error || record.summary || '-'}
      </Typography.Text>
    ),
  },
  {
    title: '结束时间',
    dataIndex: 'finished_at',
    width: 165,
    search: false,
    renderText: shortDate,
  },
]

interface JobsPageProps {
  session: AuthSession | null
}

export function JobsPage({ session }: JobsPageProps) {
  const { message } = App.useApp()
  const actionRef = useRef<ActionType>(null)
  const historyActionRef = useRef<ActionType>(null)
  const [jobs, setJobs] = useState<ScheduledJob[]>([])
  const [searchParams, setSearchParams] = useSearchParams()
  const [loaded, setLoaded] = useState(false)
  const [loadError, setLoadError] = useState('')
  const view = jobViewFromSearch(searchParams.toString())
  const selectedJobId = searchParams.get('job') || ''
  const selectedJob = jobs.find((job) => job.id === selectedJobId)
  const [runningJobId, setRunningJobId] = useState('')
  const [changingJobId, setChangingJobId] = useState('')
  const { writeJobs } = getAdminOperationsCapabilities(session)
  const summary = summarizeJobs(jobs)
  const attentionCount = jobs.filter(jobNeedsAttention).length
  const updateSearch = (name: 'job' | 'view', value: string) => {
    setSearchParams((previous) => {
      const next = new URLSearchParams(previous)
      if (value) next.set(name, value)
      else next.delete(name)
      return next
    })
  }

  const reload = async (jobId?: string) => {
    await actionRef.current?.reload()
    if (jobId && selectedJob?.id === jobId) {
      await historyActionRef.current?.reload()
    }
  }

  const runSelectedJob = async (job: ScheduledJob) => {
    setRunningJobId(job.id)
    try {
      const result = await runJob(job.id)
      if (result.status === 'error') message.error(result.error || result.summary || '任务执行失败')
      else if (result.status === 'success') message.success(result.summary || '任务执行完成')
      else message.warning(result.summary || '任务尚未确认成功，请查看运行历史')
      await reload(job.id)
    } catch (error) {
      message.error(error instanceof Error ? error.message : '任务执行失败')
      await reload(job.id)
    } finally {
      setRunningJobId('')
    }
  }

  const changeJobEnabled = async (job: ScheduledJob) => {
    setChangingJobId(job.id)
    const nextEnabled = !job.enabled
    try {
      await setJobEnabled(job.id, nextEnabled)
      message.success(`已${nextEnabled ? '启用' : '禁用'} ${job.name}`)
      await reload(job.id)
    } catch (error) {
      message.error(error instanceof Error ? error.message : '任务状态更新失败')
    } finally {
      setChangingJobId('')
    }
  }

  const columns: ProColumns<ScheduledJob>[] = [
    {
      title: '任务',
      dataIndex: 'name',
      width: 250,
      render: (_, record) => (
        <Space direction="vertical" size={0}>
          <Typography.Text strong>{record.name}</Typography.Text>
          <Typography.Text type="secondary" code copyable={{ text: record.id }}>
            {record.id}
          </Typography.Text>
        </Space>
      ),
    },
    {
      title: '类型',
      dataIndex: 'job_type',
      width: 140,
      renderText: (value: string) => jobTypeLabels[value] || value,
    },
    {
      title: '计划',
      dataIndex: 'interval_seconds',
      width: 130,
      render: (_, record) => (
        <Space direction="vertical" size={0}>
          <Typography.Text>{formatJobInterval(record.interval_seconds)}</Typography.Text>
          <Typography.Text type="secondary">
            {record.interval_seconds > 0 ? shortDate(record.next_run_at) : '随服务启动'}
          </Typography.Text>
        </Space>
      ),
    },
    {
      title: '最近执行',
      key: 'latest_run',
      width: 190,
      render: (_, record) => (
        <Space direction="vertical" size={0}>
          <Space size={4}>
            {statusTag(record.latest_run?.status)}
            <Typography.Text type="secondary">
              {record.latest_run ? formatJobDuration(record.latest_run.duration_ms) : '-'}
            </Typography.Text>
          </Space>
          <Typography.Text type="secondary">
            {shortDate(record.latest_run?.started_at)}
          </Typography.Text>
        </Space>
      ),
    },
    {
      title: '最近结果',
      key: 'latest_result',
      width: 250,
      render: (_, record) => (
        <Typography.Text
          type={record.latest_run?.error ? 'danger' : 'secondary'}
          ellipsis={{ tooltip: record.latest_run?.error || record.latest_run?.summary || '-' }}
        >
          {record.latest_run?.error || record.latest_run?.summary || '-'}
        </Typography.Text>
      ),
    },
    {
      title: '运行历史',
      key: 'history_health',
      width: 230,
      render: (_, record) => (
        <Space size={[0, 4]} wrap>
          <Tag>总计 {record.run_counts.total.toLocaleString()}</Tag>
          <Tag color="green">成功 {record.run_counts.success.toLocaleString()}</Tag>
          {record.run_counts.error > 0 && <Tag color="red">失败 {record.run_counts.error.toLocaleString()}</Tag>}
          {record.run_counts.interrupted > 0 && <Tag color="gold">中断 {record.run_counts.interrupted.toLocaleString()}</Tag>}
          {record.run_counts.running > 0 && <Tag color="blue">运行中 {record.run_counts.running}</Tag>}
        </Space>
      ),
    },
    {
      title: '状态',
      dataIndex: 'enabled',
      width: 90,
      render: (_, record) => (
        <Tag color={record.enabled ? 'green' : 'default'}>{record.enabled ? '启用' : '禁用'}</Tag>
      ),
    },
    {
      title: '操作',
      valueType: 'option',
      width: writeJobs ? 270 : 100,
      fixed: 'right',
      render: (_, record) => (
        <Space>
          {writeJobs && (
            <Button
              size="small"
              icon={<PlayCircleOutlined />}
              loading={runningJobId === record.id}
              disabled={record.latest_run?.status === 'running'}
              onClick={() => runSelectedJob(record)}
            >
              运行
            </Button>
          )}
          <Button
            size="small"
            icon={<HistoryOutlined />}
            onClick={() => updateSearch('job', record.id)}
          >
            历史
          </Button>
          {writeJobs && (
            <Popconfirm
              title={`确认${record.enabled ? '禁用' : '启用'}该任务？`}
              description={record.enabled ? '禁用后不会再按计划自动执行，仍可手工运行。' : '启用后将按当前计划恢复自动执行。'}
              onConfirm={() => changeJobEnabled(record)}
            >
              <Button size="small" loading={changingJobId === record.id}>
                {record.enabled ? '禁用' : '启用'}
              </Button>
            </Popconfirm>
          )}
        </Space>
      ),
    },
  ]

  return (
    <Space direction="vertical" size={16} className="page-stack">
      {!writeJobs && (
        <Alert
          type="info"
          showIcon
          message="当前为任务只读模式"
          description="你可以查看任务状态和运行历史，但当前角色不能运行、启用或禁用任务。"
        />
      )}
      {loadError && <Alert type="error" showIcon message="任务状态加载失败" description={loadError} action={<Button onClick={() => void reload()}>重试</Button>} />}
      {selectedJobId && loaded && !selectedJob && !loadError && <Alert type="warning" showIcon message="未找到链接指定的任务" description={selectedJobId} action={<Button onClick={() => updateSearch('job', '')}>返回列表</Button>} />}
      {attentionCount > 0 && (
        <Alert
          type="warning"
          showIcon
          message={`${attentionCount} 项任务最近执行异常`}
          description="按各任务最近一次执行结果统计。历史失败后已恢复成功的任务不计入当前异常；进入运行历史可查看原因。"
          action={<Button onClick={() => updateSearch('view', 'attention')}>查看异常</Button>}
        />
      )}

      <Row gutter={[16, 16]}>
        <Col xs={12} lg={6}><Card><Statistic title="任务数量" value={loaded && !loadError ? summary.total : '—'} /></Card></Col>
        <Col xs={12} lg={6}><Card><Statistic title="已启用" value={loaded && !loadError ? summary.enabled : '—'} /></Card></Col>
        <Col xs={12} lg={6}><Card><Statistic title="正在运行" value={loaded && !loadError ? summary.running : '—'} valueStyle={{ color: summary.running ? '#1677ff' : undefined }} /></Card></Col>
        <Col xs={12} lg={6}><Card><Statistic title="最近执行异常" value={loaded && !loadError ? attentionCount : '—'} valueStyle={{ color: attentionCount ? '#d46b08' : undefined }} /></Card></Col>
      </Row>

      <Radio.Group value={view} onChange={(event) => updateSearch('view', event.target.value as JobView)} optionType="button" options={[
        { label: '全部任务', value: 'all' }, { label: '最近异常', value: 'attention' },
        { label: '运行中', value: 'running' }, { label: '已停用', value: 'disabled' },
      ]} />

      <ProTable<ScheduledJob>
        actionRef={actionRef}
        rowKey="id"
        columns={columns}
        search={false}
        params={{ view }}
        request={async (params) => {
          try {
            const data = await listJobs(AbortSignal.timeout(15000))
            setJobs(data)
            setLoadError('')
            setLoaded(true)
            const visible = filterJobs(data, params.view)
            return { data: visible, success: true, total: visible.length }
          } catch (error) {
            setJobs([])
            setLoadError(error instanceof Error ? error.message : '加载任务失败')
            return { data: [], success: false, total: 0 }
          }
        }}
        pagination={false}
        options={{ density: true, fullScreen: true, reload: true, setting: true }}
        cardBordered
        headerTitle="任务调度"
        toolBarRender={() => [
          <Typography.Text type="secondary" key="single-flight">
            {writeJobs ? '同一任务只允许一个运行实例' : '运行与启停操作需要“任务执行”权限'}
          </Typography.Text>,
        ]}
        scroll={{ x: 1550 }}
      />

      <Drawer
        title="运行历史"
        open={Boolean(selectedJob)}
        onClose={() => updateSearch('job', '')}
        width="min(920px, 100vw)"
        destroyOnHidden
      >
        {selectedJob && (
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <Typography.Title level={4}>{selectedJob.name}</Typography.Title>
            <Typography.Text code copyable={{ text: `${window.location.origin}${jobHistoryPath(selectedJob.id)}` }}>复制任务详情链接</Typography.Text>
            {(selectedJob.run_counts.error > 0 || selectedJob.run_counts.interrupted > 0) && (
              <Alert
                type="warning"
                showIcon
                message={`历史失败 ${selectedJob.run_counts.error.toLocaleString()} 次 · 中断 ${selectedJob.run_counts.interrupted.toLocaleString()} 次`}
                description="中断记录由服务启动时的恢复流程标记，可结合开始时间和结果核对执行情况。"
              />
            )}
            <ProTable<JobRun>
              key={selectedJob.id}
              actionRef={historyActionRef}
              rowKey="id"
              columns={historyColumns}
              request={async (params) => {
                try {
                  const page = await getJobRuns(selectedJob.id, {
                    current: params.current,
                    pageSize: params.pageSize,
                    status: params.status as string | undefined,
                  })
                  return { data: page.items, success: true, total: page.total }
                } catch (error) {
                  message.error(error instanceof Error ? error.message : '加载运行历史失败')
                  return { data: [], success: false, total: 0 }
                }
              }}
              search={{ labelWidth: 'auto' }}
              pagination={{ pageSize: 20, showSizeChanger: true }}
              options={{ density: true, reload: true, setting: true }}
              cardBordered
              headerTitle={`${selectedJob.run_counts.total.toLocaleString()} 条记录`}
              scroll={{ x: 760 }}
            />
          </Space>
        )}
      </Drawer>
    </Space>
  )
}
