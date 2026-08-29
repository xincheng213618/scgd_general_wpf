import { LockOutlined, ReloadOutlined, SaveOutlined, SafetyCertificateOutlined, UndoOutlined } from '@ant-design/icons'
import { Alert, App, Button, Card, Checkbox, Col, Popconfirm, Row, Space, Spin, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { getPermissionMatrix, updateRolePermissions } from '../services/admin'
import type { PermissionDefinition, PermissionMatrix, RolePermissionItem } from '../types/admin'
import {
  changePermissionSelection,
  isPermissionRevisionConflict,
  permissionUpdateSuccessMessage,
  reviewPermissionSelection,
  summarizePermissionChanges,
} from '../utils/permissions'

function RolePermissionCard({
  role,
  matrix,
  saving,
  onSave,
}: {
  role: RolePermissionItem
  matrix: PermissionMatrix
  saving: boolean
  onSave: (permissions: string[]) => Promise<void>
}) {
  const [selected, setSelected] = useState<string[]>(role.permissions)
  const grouped = new Map<string, PermissionDefinition[]>()
  matrix.permissions.forEach((permission) => {
    const items = grouped.get(permission.category) ?? []
    items.push(permission)
    grouped.set(permission.category, items)
  })
  const categories = [...grouped.entries()]
  const changes = summarizePermissionChanges(role.permissions, selected)
  const unchanged = changes.added.length === 0 && changes.removed.length === 0
  const permissionNames = new Map(matrix.permissions.map((permission) => [permission.code, permission.name]))
  const allPermissionCodes = matrix.permissions.map((permission) => permission.code)
  const selectedSet = new Set(selected)
  const removedNames = changes.removed.map((code) => permissionNames.get(code) ?? code)
  const addedNames = changes.added.map((code) => permissionNames.get(code) ?? code)
  const removesHighRiskPermission = changes.highRiskRemoved.length > 0
  const selectionReview = reviewPermissionSelection(selected)

  return (
    <Card
      title={(
        <Space>
          {role.editable ? <SafetyCertificateOutlined /> : <LockOutlined />}
          <span>{role.name}</span>
          <Tag color={role.code === 'admin' ? 'red' : 'blue'}>{role.code}</Tag>
        </Space>
      )}
      extra={(
        <Space wrap>
          <Tag>数据库账号：启用 {role.active_member_count} / 共 {role.member_count}</Tag>
          {role.editable ? (
            <Popconfirm
              title={removesHighRiskPermission ? '确认移除关键权限？' : `确认保存${role.name}权限？`}
              description={(
                <Space direction="vertical" size={2}>
                  <Typography.Text>
                    保存后将立即影响 {role.active_member_count} 个启用账号（共 {role.member_count} 个）。
                  </Typography.Text>
                  {addedNames.length > 0 && (
                    <Typography.Text>新增：{addedNames.join('、')}</Typography.Text>
                  )}
                  {removedNames.length > 0 && (
                    <Typography.Text type={removesHighRiskPermission ? 'danger' : undefined}>
                      移除：{removedNames.join('、')}
                    </Typography.Text>
                  )}
                  {removesHighRiskPermission && (
                    <Typography.Text type="danger" strong>
                      相关账号可能立即失去后台或权限管理入口，包括当前登录的注册用户。
                    </Typography.Text>
                  )}
                </Space>
              )}
              okText="确认保存"
              cancelText="取消"
              okButtonProps={{ danger: removesHighRiskPermission }}
              onConfirm={() => onSave(selected)}
            >
              <Button
                type="primary"
                icon={<SaveOutlined />}
                loading={saving}
                disabled={saving || unchanged}
              >
                保存权限
              </Button>
            </Popconfirm>
          ) : <Tag>固定全权</Tag>}
        </Space>
      )}
    >
      <Typography.Paragraph type="secondary">{role.description}</Typography.Paragraph>
      <Space wrap size={[8, 8]} style={{ marginBottom: 20 }}>
        <Tag color={unchanged ? 'default' : 'orange'}>
          已选 {selected.length} / {matrix.permissions.length}
        </Tag>
        {role.editable && (
          <Space wrap size={[8, 8]}>
            <Button
              size="small"
              disabled={saving || selected.length === matrix.permissions.length}
              onClick={() => setSelected(changePermissionSelection(selected, allPermissionCodes, true))}
            >
              全部选择
            </Button>
            <Button
              size="small"
              disabled={saving || selected.length === 0}
              onClick={() => setSelected(changePermissionSelection(selected, allPermissionCodes, false))}
            >
              全部清空
            </Button>
            <Button
              size="small"
              icon={<UndoOutlined />}
              disabled={saving || unchanged}
              onClick={() => setSelected(role.permissions)}
            >
              撤销未保存更改
            </Button>
            {!unchanged && (
              <Tag color="orange">未保存：新增 {changes.added.length}，移除 {changes.removed.length}</Tag>
            )}
          </Space>
        )}
      </Space>
      {role.editable && !unchanged && (
        <Alert
          type={selectionReview.warnings.length > 0 ? 'warning' : 'success'}
          showIcon
          message={selectionReview.canAccessAdmin
            ? `保存后可进入 ${selectionReview.accessibleAdminRoutes.length} 个后台页面`
            : '保存后将关闭注册用户的后台入口'}
          description={selectionReview.warnings.length > 0 ? (
            <Space direction="vertical" size={2}>
              {selectionReview.warnings.map((warning) => (
                <Typography.Text key={warning}>{warning}</Typography.Text>
              ))}
            </Space>
          ) : '未发现会阻断 Web 管理操作的权限组合。'}
          style={{ marginBottom: 20 }}
        />
      )}
      <Space direction="vertical" size={20} style={{ width: '100%' }}>
        {categories.map(([category, permissions]) => {
          const categoryCodes = permissions.map((permission) => permission.code)
          const selectedCategoryCount = categoryCodes.filter((code) => selectedSet.has(code)).length
          return (
            <section key={category}>
              <Space align="center" style={{ marginBottom: 12 }}>
                <Checkbox
                  checked={selectedCategoryCount === categoryCodes.length}
                  indeterminate={selectedCategoryCount > 0 && selectedCategoryCount < categoryCodes.length}
                  disabled={!role.editable || saving}
                  onChange={(event) => setSelected(changePermissionSelection(
                    selected,
                    categoryCodes,
                    event.target.checked,
                  ))}
                >
                  <Typography.Text strong>{category}</Typography.Text>
                </Checkbox>
                <Tag>{selectedCategoryCount} / {categoryCodes.length}</Tag>
              </Space>
              <Row gutter={[12, 12]}>
                {permissions.map((permission) => (
                  <Col xs={24} md={12} xl={8} key={permission.code}>
                    <Card size="small">
                      <Checkbox
                        checked={selectedSet.has(permission.code)}
                        disabled={!role.editable || saving}
                        onChange={(event) => setSelected(changePermissionSelection(
                          selected,
                          [permission.code],
                          event.target.checked,
                        ))}
                      >
                        <Space direction="vertical" size={0}>
                          <Typography.Text strong>{permission.name}</Typography.Text>
                          <Typography.Text type="secondary">{permission.description}</Typography.Text>
                          <Typography.Text code>{permission.code}</Typography.Text>
                        </Space>
                      </Checkbox>
                    </Card>
                  </Col>
                ))}
              </Row>
            </section>
          )
        })}
      </Space>
    </Card>
  )
}

export function PermissionsPage({ onPermissionsChanged }: { onPermissionsChanged?: () => Promise<boolean> }) {
  const { message } = App.useApp()
  const [matrix, setMatrix] = useState<PermissionMatrix | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [reloadToken, setReloadToken] = useState(0)
  const [savingRole, setSavingRole] = useState<string | null>(null)

  function reloadPermissions() {
    setLoadError('')
    setLoading(true)
    setReloadToken((value) => value + 1)
  }

  useEffect(() => {
    let active = true
    getPermissionMatrix()
      .then((result) => {
        if (active) {
          setMatrix(result)
          setLoadError('')
        }
      })
      .catch((error) => {
        if (active) {
          setLoadError(error instanceof Error ? error.message : '权限数据加载失败')
        }
      })
      .finally(() => {
        if (active) setLoading(false)
      })
    return () => {
      active = false
    }
  }, [reloadToken])

  if (loading && !matrix) return <Spin tip="加载权限配置…" />
  if (!matrix) {
    return (
      <Alert
        type="error"
        showIcon
        message="权限数据不可用"
        description={loadError || '权限配置暂时无法加载，请稍后重试。'}
        action={<Button size="small" onClick={reloadPermissions}>重试</Button>}
      />
    )
  }

  return (
    <Space direction="vertical" size={16} className="page-stack">
      {loadError && (
        <Alert
          type="warning"
          showIcon
          message="当前显示的权限配置可能已过期"
          description={loadError}
          action={<Button size="small" onClick={reloadPermissions}>重新加载权限</Button>}
        />
      )}
      <Alert
        type="info"
        showIcon
        message="注册用户当前默认与管理员拥有相同功能权限"
        description="管理员权限保持固定；可在这里调整“注册用户”角色，保存后服务端接口会立即按新权限校验。"
        action={(
          <Button
            size="small"
            icon={<ReloadOutlined aria-hidden="true" />}
            loading={loading}
            disabled={savingRole !== null}
            onClick={reloadPermissions}
          >
            刷新权限
          </Button>
        )}
      />
      {matrix.roles.map((role) => (
        <RolePermissionCard
          key={`${role.code}:${role.revision}`}
          role={role}
          matrix={matrix}
          saving={loading || savingRole === role.code}
          onSave={async (permissions) => {
            setSavingRole(role.code)
            try {
              const updated = await updateRolePermissions(role.code, permissions, role.revision)
              setMatrix(updated)
              setLoadError('')
              let sessionRefreshed = true
              try {
                sessionRefreshed = (await onPermissionsChanged?.()) !== false
              } catch {
                sessionRefreshed = false
              }
              const feedback = permissionUpdateSuccessMessage(
                role.name,
                updated.change,
                sessionRefreshed,
              )
              if (sessionRefreshed) message.success(feedback)
              else message.warning(feedback)
            } catch (error) {
              if (isPermissionRevisionConflict(error)) {
                try {
                  setMatrix(await getPermissionMatrix())
                  setLoadError('')
                  message.warning('权限已被其他管理员修改，页面已刷新；请核对最新配置后重试')
                } catch (refreshError) {
                  const detail = refreshError instanceof Error ? refreshError.message : '权限数据刷新失败'
                  setLoadError(detail)
                  message.error(`${detail}；请重新加载权限后再操作`)
                }
              } else {
                message.error(error instanceof Error ? error.message : '权限更新失败')
              }
            } finally {
              setSavingRole(null)
            }
          }}
        />
      ))}
    </Space>
  )
}
