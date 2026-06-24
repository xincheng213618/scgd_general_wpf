# RBAC 模块

RBAC 当前实现集中在 `UI/ColorVision.Solution/Rbac/`。它是桌面 Solution 侧的本地用户、角色、权限、会话和审计模块，不是 Engine 层统一安全内核，也不是远程身份平台。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 登录后权限不对 | `UserDetailEntity.PermissionMode` 是否同步到 `Authorization.Instance.PermissionMode` |
| 管理窗口打不开 | 当前 `PermissionMode` 是否高于 `Administrator`，窗口入口会先做粗粒度拦截 |
| 细权限判断异常 | `PermissionChecker` 缓存、用户角色、角色权限关联 |
| Session 登录失败 | `RbacManagerConfig.SessionToken`、`SessionService` 校验、过期/撤销状态 |
| 新权限保存后不生效 | `PermissionManagerWindow` 是否调用 `InvalidateAllCache()` |
| 用户看不到角色 | `UserRoleEntity`、`RoleEntity`、软删除/启用状态 |
| 审计缺记录 | `AuditLogService` 调用点；当前不是覆盖全业务的统一审计总线 |

## 初始化链路

1. 创建 `%AppData%/ColorVision/Config/`。
2. 打开或创建本地 SQLite 数据库 `Rbac.db`。
3. 通过 SqlSugar CodeFirst 初始化 RBAC 实体表。
4. 初始化 `AuthService`、`UserService`、`RoleService`、`PermissionService`、`AuditLogService`、`SessionService`、`PermissionChecker`、`TenantService`。
5. 创建默认管理员角色和管理员用户。
6. 写入预置权限，并把全部权限分配给 `admin` 角色。
7. 如果有登录缓存，把用户 `PermissionMode` 同步到全局授权状态。

## 当前实体

| 实体 | 表 | 说明 |
| --- | --- | --- |
| `UserEntity` | `sys_user` | 用户名、密码哈希、启用状态、软删除状态 |
| `UserDetailEntity` | `sys_user_detail` | `PermissionMode`、联系方式、组织信息、头像、备注 |
| `RoleEntity` | `sys_role` | 角色基本信息 |
| `PermissionEntity` | `sys_permission` | 权限码，如 `user.create`、`role.assign_permissions`、`audit.view` |
| `RolePermissionEntity` | `sys_role_permission` | 角色到权限码的关联 |
| `SessionEntity` | `sys_session` | SessionToken、设备、IP、创建/过期/活跃/撤销状态 |
| `AuditLogEntity` | `sys_audit_log` | 用户、动作、明细、时间、IP |
| `TenantEntity` / `UserTenantEntity` | 租户相关表 | 已预留租户维度，但不是当前桌面入口主线 |

## 登录链路

1. `LoginWindow` 输入用户名和密码，或读取缓存 SessionToken。
2. `AuthService.LoginAndGetDetailAsync(...)` 查询启用且未软删除的用户。
3. `PasswordHasher` 校验密码，旧格式密码会在登录时升级。
4. 确保 `UserDetailEntity` 存在，并加载角色列表。
5. 返回 `LoginResultDto`，必要时由 `SessionService` 创建/维护 SessionToken。
6. 登录成功后写入 `RbacManagerConfig`。
7. `UserDetailEntity.PermissionMode` 同步到 `Authorization.Instance.PermissionMode`。

## 权限检查

| 层级 | 当前入口 | 说明 |
| --- | --- | --- |
| 粗粒度 | `Authorization.Instance.PermissionMode`、`AccessControl.Check(...)` | 很多窗口先判断是否高于 `Administrator` |
| 细粒度 | `PermissionChecker` | 查询用户角色和角色权限，带过期时间和 LRU 缓存 |

当前系统是粗细两层并存。不要把 `PermissionMode` 写成已经被 RBAC 完全替换，也不要把权限码写成已覆盖全产品所有入口。

## 可见窗口

| 窗口 | 用途 |
| --- | --- |
| `LoginWindow` | 登录和 Session 恢复 |
| `RegisterWindow` | 注册用户 |
| `ChangePasswordWindow` | 修改密码 |
| `UserManagerWindow` | 用户列表、角色查看、启停、删除、重置密码 |
| `PermissionManagerWindow` | 按角色分配权限，保存后清权限缓存 |
| `RbacManagerWindow` | RBAC 总入口和当前登录状态 |

## 边界

- 当前依赖本地 SQLite 和本地窗口，不依赖外部认证服务器。
- `PermissionMode` 仍是很多关键入口的第一道判断。
- 细粒度权限主要集中在 RBAC 自己的管理窗口和服务层。
- MFA、证书、IP 白名单、全业务审计总线等能力当前没有落地，不要写进现有能力。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 初始化 | `RbacManager.cs` |
| 实体 | `Entity/` |
| 登录 | `Services/Auth/AuthService.cs`、`LoginWindow.xaml.cs` |
| 会话 | `Services/SessionService.cs`、`Services/SessionCleanupService.cs` |
| 权限缓存 | `Services/PermissionChecker.cs` |
| 用户管理 | `UserManagerWindow.xaml.cs` |
| 权限管理 | `PermissionManagerWindow.xaml.cs` |
