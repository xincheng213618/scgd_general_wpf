import { HistoryOutlined, KeyOutlined, LaptopOutlined, ReloadOutlined, SafetyCertificateOutlined } from '@ant-design/icons'
import { Alert, Button, Descriptions, Drawer, Empty, List, Pagination, Space, Spin, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { getUserDetails } from '../services/admin'
import type { UserAccount, UserAccountDetails, UserDetailActivityEntry } from '../types/admin'
import { accountActivitySourceLabel } from '../utils/accountActivity'
import { sessionAddressLabel, sessionClientLabel } from '../utils/accountSessions'
import { auditActionMeta, auditDetailSummary } from '../utils/auditLog'
import { shortDate } from '../utils/format'
import { userAccountOriginLabel, userRoleLabel } from '../utils/userAccounts'

const ACTIVITY_PAGE_SIZE = 8

interface UserDetailsDrawerProps {
  target: UserAccount | null
  onClose: () => void
  onResetPassword: (account: UserAccount) => void
}

function ActivityItem({ item }: { item: UserDetailActivityEntry }) {
  const meta = auditActionMeta(item.action)
  return (
    <List.Item>
      <List.Item.Meta
        avatar={<SafetyCertificateOutlined style={{ color: item.security ? '#cf1322' : '#1677ff', fontSize: 20 }} />}
        title={(
          <Space size={[6, 6]} wrap>
            <Tag color={meta.color}>{meta.label}</Tag>
            <Tag color={item.source === 'anonymous' ? 'volcano' : 'default'}>
              {accountActivitySourceLabel(item.source)}
            </Tag>
            <Typography.Text type="secondary">{shortDate(item.created_at)}</Typography.Text>
          </Space>
        )}
        description={(
          <Space direction="vertical" size={2} style={{ width: '100%' }}>
            <Typography.Text>{auditDetailSummary(item.detail)}</Typography.Text>
            <Typography.Text type="secondary">
              {sessionAddressLabel(item.ip)} · {sessionClientLabel(item.user_agent)}
            </Typography.Text>
          </Space>
        )}
      />
    </List.Item>
  )
}

export function UserDetailsDrawer({ target, onClose, onResetPassword }: UserDetailsDrawerProps) {
  const [page, setPage] = useState(1)
  const [reloadToken, setReloadToken] = useState(0)
  const [requestState, setRequestState] = useState<{
    key: string
    details: UserAccountDetails | null
    error: string
  }>({ key: '', details: null, error: '' })
  const targetId = target?.id
  const requestKey = targetId ? `${targetId}:${page}:${reloadToken}` : ''

  useEffect(() => {
    if (!targetId) return
    let active = true
    getUserDetails(targetId, { current: page, pageSize: ACTIVITY_PAGE_SIZE })
      .then((result) => {
        if (active) setRequestState({ key: requestKey, details: result, error: '' })
      })
      .catch((error) => {
        if (active) {
          setRequestState({
            key: requestKey,
            details: null,
            error: error instanceof Error ? error.message : '账号详情加载失败',
          })
        }
      })
    return () => {
      active = false
    }
  }, [page, reloadToken, requestKey, targetId])

  const details = requestState.key === requestKey ? requestState.details : null
  const loadError = requestState.key === requestKey ? requestState.error : ''
  const user = details?.user
  const activity = details?.activity
  return (
    <Drawer
      title={target ? `账号详情 · ${target.username}` : '账号详情'}
      open={Boolean(target)}
      onClose={onClose}
      width="min(820px, 100vw)"
      destroyOnHidden
      extra={target ? (
        <Button
          size="small"
          icon={<ReloadOutlined aria-hidden="true" />}
          onClick={() => setReloadToken((value) => value + 1)}
        >
          刷新
        </Button>
      ) : undefined}
    >
      {loadError ? (
        <Alert
          type="error"
          showIcon
          message="账号详情加载失败"
          description={loadError}
          action={(
            <Button size="small" onClick={() => setReloadToken((value) => value + 1)}>
              重试
            </Button>
          )}
        />
      ) : !details || !user || !activity ? (
        <Spin tip="加载账号详情…" />
      ) : (
        <Space direction="vertical" size={20} style={{ width: '100%' }}>
          {details.password_recovery && (
            <Alert
              type="warning"
              showIcon
              message="用户正在等待管理员协助找回密码"
              description={(
                <span>
                  已申请 {details.password_recovery.request_count} 次，最近于 {shortDate(details.password_recovery.last_requested_at)}
                  {' '}从 {sessionAddressLabel(details.password_recovery.last_ip)} 提交。
                  {details.password_recovery.expires_at && (
                    <>若仍未处理，将于 {shortDate(details.password_recovery.expires_at)} 自动过期。</>
                  )}
                  重置后申请会自动标记为已处理。
                </span>
              )}
              action={(
                <Button
                  type="primary"
                  danger
                  icon={<KeyOutlined aria-hidden="true" />}
                  onClick={() => onResetPassword(user)}
                >
                  重置临时密码
                </Button>
              )}
            />
          )}
          <Descriptions bordered size="small" column={{ xs: 1, sm: 2 }}>
            <Descriptions.Item label="用户名">
              <Typography.Text copyable>{user.username}</Typography.Text>
            </Descriptions.Item>
            <Descriptions.Item label="账号状态">
              <Space size={4} wrap>
                <Tag color={user.is_active ? 'green' : 'default'}>
                  {user.is_active ? '启用' : '停用'}
                </Tag>
                {user.must_change_password && <Tag color="gold">待改密</Tag>}
                {user.is_current && <Tag color="processing">当前账号</Tag>}
              </Space>
            </Descriptions.Item>
            <Descriptions.Item label="昵称">{user.display_name || '未填写'}</Descriptions.Item>
            <Descriptions.Item label="邮箱">{user.email || '未填写'}</Descriptions.Item>
            <Descriptions.Item label="角色">
              <Tag color={user.role === 'admin' ? 'red' : 'blue'}>{userRoleLabel(user.role)}</Tag>
            </Descriptions.Item>
            <Descriptions.Item label="账号来源">{userAccountOriginLabel(user.account_origin)}</Descriptions.Item>
            <Descriptions.Item label="创建时间">{shortDate(user.created_at)}</Descriptions.Item>
            <Descriptions.Item label="账号更新">{shortDate(user.updated_at || undefined)}</Descriptions.Item>
            <Descriptions.Item label="密码更新">{shortDate(user.password_changed_at || undefined)}</Descriptions.Item>
            <Descriptions.Item label="最近登录">{shortDate(user.last_login_at || undefined)}</Descriptions.Item>
            <Descriptions.Item label="有效会话">{details.sessions.total}</Descriptions.Item>
          </Descriptions>

          <section aria-labelledby="user-detail-permissions">
            <Space align="baseline" wrap>
              <Typography.Title id="user-detail-permissions" level={5} style={{ margin: 0 }}>实际权限</Typography.Title>
              <Typography.Text type="secondary">由{userRoleLabel(user.role)}角色统一授予，共 {details.permissions.length} 项</Typography.Text>
            </Space>
            <div style={{ marginTop: 12 }}>
              <Space size={[6, 6]} wrap>
                {details.permissions.map((permission) => (
                  <Tag color="blue" key={permission.code} title={permission.description}>
                    {permission.category} · {permission.name}
                  </Tag>
                ))}
                {details.permissions.length === 0 && <Typography.Text type="secondary">当前角色没有已授予权限</Typography.Text>}
              </Space>
            </div>
          </section>

          <section aria-labelledby="user-detail-sessions">
            <Space align="baseline">
              <Typography.Title id="user-detail-sessions" level={5} style={{ margin: 0 }}>
                <Space><LaptopOutlined />有效登录</Space>
              </Typography.Title>
              <Typography.Text type="secondary">共 {details.sessions.total} 个</Typography.Text>
            </Space>
            <List
              style={{ marginTop: 8 }}
              dataSource={details.sessions.items}
              locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="当前没有有效登录" /> }}
              renderItem={(item) => (
                <List.Item>
                  <List.Item.Meta
                    title={(
                      <Space wrap>
                        <Typography.Text strong>{sessionClientLabel(item.user_agent)}</Typography.Text>
                        {item.is_current && <Tag color="processing">当前登录</Tag>}
                      </Space>
                    )}
                    description={(
                      <Space direction="vertical" size={0}>
                        <Typography.Text type="secondary">
                          {sessionAddressLabel(item.ip_address)} · 最近活动 {shortDate(item.last_seen_at)} · 登录于 {shortDate(item.created_at)}
                        </Typography.Text>
                        <Typography.Text type="secondary" ellipsis={{ tooltip: item.user_agent || '未记录客户端信息' }}>
                          {item.user_agent || '未记录客户端信息'}
                        </Typography.Text>
                      </Space>
                    )}
                  />
                </List.Item>
              )}
            />
          </section>

          <section aria-labelledby="user-detail-activity">
            <Space align="baseline" wrap>
              <Typography.Title id="user-detail-activity" level={5} style={{ margin: 0 }}>
                <Space><HistoryOutlined />近期账号活动</Space>
              </Typography.Title>
              <Typography.Text type="secondary">共 {activity.total} 条</Typography.Text>
              {activity.summary.security_events > 0 && <Tag color="red">安全事件 {activity.summary.security_events}</Tag>}
            </Space>
            {(activity.summary.failed_logins > 0 || activity.summary.throttled_logins > 0) && (
              <Alert
                type="warning"
                showIcon
                style={{ marginTop: 12 }}
                message={`记录中有 ${activity.summary.failed_logins} 次失败登录，触发 ${activity.summary.throttled_logins} 次临时锁定`}
              />
            )}
            <List<UserDetailActivityEntry>
              style={{ marginTop: 8 }}
              dataSource={activity.entries}
              locale={{ emptyText: '暂无账号活动' }}
              renderItem={(item) => <ActivityItem item={item} />}
            />
            {activity.total > ACTIVITY_PAGE_SIZE && (
              <Pagination
                current={page}
                pageSize={ACTIVITY_PAGE_SIZE}
                total={activity.total}
                showSizeChanger={false}
                showTotal={(total) => `共 ${total} 条活动`}
                onChange={setPage}
              />
            )}
          </section>
        </Space>
      )}
    </Drawer>
  )
}
