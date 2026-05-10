# RBAC 模块

本页描述当前仓库中已经实现的 RBAC 子系统，不再继续维护那份把它写成完整企业安全平台的 draft 文档。

## 模块位置

当前 RBAC 实现集中在 `UI/ColorVision.Solution/Rbac/`，而不是 `Engine/`。

这点很重要，因为它说明当前权限系统更接近桌面 Solution 侧的本地用户与权限模块，而不是引擎层统一安全内核。

## 初始化时会发生什么

`RbacManager` 是这套子系统的总入口。当前初始化链大致是：

1. 创建 `%AppData%/ColorVision/Config/` 目录。
2. 打开或创建本地 SQLite 数据库 `Rbac.db`。
3. 用 SqlSugar CodeFirst 初始化实体表。
4. 初始化 `AuthService`、`UserService`、`RoleService`、`PermissionService`、`AuditLogService`、`SessionService`、`PermissionChecker`、`TenantService`。
5. 创建默认管理员角色和管理员用户。
6. 写入预置权限，并把全部权限分配给 `admin` 角色。
7. 如果已有登录缓存，则把当前用户的 `PermissionMode` 同步回全局授权状态。

这条链说明当前 RBAC 的启动是本地、自举式的，不依赖外部认证服务器。

## 当前真实存在的核心实体

从 `Entity/` 目录看，当前最重要的表模型包括：

- `UserEntity` 对应 `sys_user`
- `UserDetailEntity` 对应 `sys_user_detail`
- `RoleEntity` 对应 `sys_role`
- `PermissionEntity` 对应 `sys_permission`
- `RolePermissionEntity` 对应 `sys_role_permission`
- `SessionEntity` 对应 `sys_session`
- `AuditLogEntity` 对应 `sys_audit_log`

另外还有 `TenantEntity` 和 `UserTenantEntity`，说明这套实现已经预留了租户维度，但这不是当前桌面权限入口页最主要的阅读重点。

## 这些实体现在分别承担什么

### 用户与用户详情

`UserEntity` 存用户名、密码哈希、启用状态和软删除状态。

`UserDetailEntity` 额外存：

- `PermissionMode`
- 邮箱、电话、地址、公司、部门、岗位
- 用户头像和备注

这里要特别注意：当前全局粗粒度权限级别不是从角色表即时推导出来的，而是直接保存在 `UserDetailEntity.PermissionMode` 里，并在登录后同步到 `Authorization.Instance.PermissionMode`。

### 角色与权限

`RoleEntity` 管角色基本信息，`PermissionEntity` 管权限码，`RolePermissionEntity` 管角色到权限的关联。

当前权限服务里已经有一组预置权限码，例如：

- `user.create`
- `user.edit`
- `role.assign_permissions`
- `permission.manage`
- `audit.view`

这说明当前细粒度权限已经不只是“管理员/访客”几档，而是开始进入按动作编码控制。

### 会话

`SessionEntity` 保存：

- `SessionToken`
- 用户 ID
- 设备信息和 IP
- 创建时间、过期时间、最后活跃时间
- 是否已撤销

`SessionService` 负责生成 64 字节随机 Token、校验会话、更新活跃时间、撤销会话和清理过期会话。

### 审计

`AuditLogEntity` 当前记录：

- 用户 ID / 用户名
- 动作代码
- 明细说明
- 时间
- IP

`AuditLogService` 当前提供的是面向 RBAC 侧的本地审计记录，而不是覆盖所有业务模块的统一审计总线。

## 当前登录链怎么走

当前认证链大致是：

1. 用户在 `LoginWindow` 输入用户名和密码。
2. `AuthService.LoginAndGetDetailAsync(...)` 查询启用且未软删除的用户。
3. 密码通过 `PasswordHasher` 校验，旧格式密码会在登录时升级。
4. 系统确保 `UserDetailEntity` 存在，并加载角色列表。
5. 登录结果写入 `LoginResultDto`。
6. `SessionService` 可进一步创建和维护 SessionToken。
7. 登录成功后把 `UserDetailEntity.PermissionMode` 同步到 `Authorization.Instance.PermissionMode`。

因此当前登录结果既影响 RBAC 子系统内部状态，也直接影响全局 UI 的粗粒度权限判断。

## 当前权限检查怎么做

当前权限检查有两层：

### 粗粒度入口判断

很多窗口先直接判断：

- `Authorization.Instance.PermissionMode > PermissionMode.Administrator`

例如用户管理和权限管理窗口都会先拦这一步。

### 细粒度权限码判断

更细的权限校验由 `PermissionChecker` 负责。它会：

- 查询用户关联的角色 ID
- 联表查出对应权限码
- 使用带过期时间和 LRU 驱逐的缓存保存结果

所以当前系统并不是“只有 RBAC”或“只有 PermissionMode”，而是粗细两层并存。

## 当前可见的管理界面

从模块目录看，RBAC 目前已经有一组明确的桌面窗口：

- `LoginWindow`
- `RegisterWindow`
- `ChangePasswordWindow`
- `UserManagerWindow`
- `PermissionManagerWindow`
- `RbacManagerWindow`

其中：

- `UserManagerWindow` 负责用户列表、角色查看、启停、删除、重置密码等管理动作
- `PermissionManagerWindow` 负责按角色分配权限，并在保存后失效权限缓存

## 当前设计最需要注意的边界

### 它是本地权限系统，不是统一远程身份平台

当前实现依赖本地 SQLite 和本地窗口，不是外部认证中心。

### 粗粒度 `PermissionMode` 还没有被完全替换

很多关键入口依然先看 `PermissionMode`，再进入 RBAC 管理逻辑。

### 细粒度权限接入是局部的

当前能确认的细粒度权限能力，主要集中在 RBAC 自己的管理窗口和服务层，还不能把整个产品都描述成已经全面接入权限码控制。

## 这页不再做什么

本页不再继续维护这些与当前实现不符的内容：

- 虚构的通用 `AuthService`/`AuditService` 平台分层图
- 假定所有业务模块都被 RBAC 统一拦截
- 多因素认证、网络证书、IP 白名单等未见落地实现的安全能力

如果后续要扩展到全系统安全架构，应基于真实接入点另写专题页。

## 推荐阅读顺序

1. `UI/ColorVision.Solution/Rbac/RbacManager.cs`
2. `UI/ColorVision.Solution/Rbac/Entity/`
3. `UI/ColorVision.Solution/Rbac/Services/Auth/AuthService.cs`
4. `UI/ColorVision.Solution/Rbac/Services/SessionService.cs`
5. `UI/ColorVision.Solution/Rbac/Services/PermissionChecker.cs`
6. `UI/ColorVision.Solution/Rbac/UserManagerWindow.xaml.cs`
7. `UI/ColorVision.Solution/Rbac/PermissionManagerWindow.xaml.cs`

## 继续阅读

- [安全与权限控制](./overview.md)
- [架构运行时](../overview/runtime.md)
- [组件交互](../overview/component-interactions.md)