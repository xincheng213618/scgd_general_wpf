---
knowledge_id: "platform.rbac"
knowledge_type: "topic"
status: "current"
summary: "本地RBAC的登录缓存、会话校验和权限同步限制，以及自动登录失败、登出撤销和用户中心统计的实际边界。"
aliases: ["登录后没有权限","RBAC","ColorVision.Rbac","RbacManager","IsUserLoggedIn","AuthService","SessionService","PermissionChecker","SessionToken","LoginResultDto","自动登录失败","退出登录后权限","用户中心","UserCenterStatisticsService","ApplicationUsageTracker"]
code_paths: ["UI/ColorVision.Rbac/README.md","UI/ColorVision.Rbac/ColorVision.Rbac.csproj","UI/ColorVision.Rbac/RbacManager.cs","UI/ColorVision.Rbac/RbacManagerConfig.cs","UI/ColorVision.Rbac/Loginwindow.xaml.cs","UI/ColorVision.Rbac/RbacManagerWindow.xaml.cs","UI/ColorVision.Rbac/RegisterWindow.xaml.cs","UI/ColorVision.Rbac/Services/Auth/AuthService.cs","UI/ColorVision.Rbac/Services/Auth/IAuthService.cs","UI/ColorVision.Rbac/Services/IUserService.cs","UI/ColorVision.Rbac/Services/SessionService.cs","UI/ColorVision.Rbac/Services/SessionCleanupService.cs","UI/ColorVision.Rbac/Services/PermissionChecker.cs","UI/ColorVision.Rbac/Services/UserCenterStatisticsService.cs","UI/ColorVision.Rbac/ApplicationUsageTracker.cs","UI/ColorVision.Rbac/Dtos/LoginResultDto.cs","UI/ColorVision.Rbac/Entity","UI/ColorVision.Rbac/Security/PasswordHashing.cs","ColorVision/BuiltInModules.cs","ColorVision/App.xaml.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ModuleCatalogTests.cs"]
related: ["platform.security","ui.common","ui.configuration"]
---

# RBAC：登录缓存、会话与权限边界

RBAC 当前实现集中在独立项目 `UI/ColorVision.Rbac/`，并由 `ColorVision/BuiltInModules.cs` 注册到主程序。账户、角色、权限、会话和审计使用本地 SQLite；用户中心的流程统计另读配置的业务数据库。它不是 Engine 层统一安全内核，也不是远程身份平台。

当前缓存恢复和失败处理存在未修复的授权限制：缓存字段完整不等于会话有效，无缓存时的初始化权限仍为 `Administrator`。本页如实记录这些限制，不将其包装为安全默认值或新增授权入口的设计建议；全局粗粒度权限与细粒度 RBAC 的关系见[安全边界](./overview.md)。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 登录后权限不对 | 当前全局权限来自初始化缓存、密码登录还是自动登录；缓存字段是否完整不代表会话仍有效 |
| 管理窗口打不开 | 窗口按 `Authorization.Instance.PermissionMode > PermissionMode.Administrator` 拒绝；数值较小代表较高权限，不是登录校验 |
| 细权限判断异常 | `PermissionChecker` 缓存、用户角色、角色权限关联 |
| Session 登录失败 | `RbacManagerConfig.SessionToken`、`AuthService.LoginBySessionTokenAsync`、会话过期/撤销和用户启停状态 |
| 新权限保存后不生效 | `PermissionManagerWindow` 是否调用 `InvalidateAllCache()` |
| 用户看不到角色 | `UserRoleEntity`、`RoleEntity`、软删除/启用状态 |
| 审计缺记录 | `AuditLogService` 调用点；当前不是覆盖全业务的统一审计总线 |

## 初始化链路

1. `RbacManager` 构造创建默认 `%AppData%/ColorVision/Config/`；`DirectoryPath` 和 `SqliteDbPath` 均为可设置属性，不能把默认路径当作不可覆盖的部署约束。
2. 打开或创建本地 SQLite 数据库，默认是该目录的 `Rbac.db`。
3. 通过 SqlSugar CodeFirst 初始化 RBAC 实体表。
4. 初始化 `AuthService`、`UserService`、`RoleService`、`PermissionService`、`AuditLogService`、`SessionService` 和 `PermissionChecker`。
5. 缺少内置管理员角色或用户时创建它们，并维护角色关联；这不是只在整个应用首次启动时运行一次的空库检查。
6. 写入预置权限，并把全部权限分配给 `admin` 角色。
7. 根据缓存 DTO 的结构检查同步全局权限；没有通过结构检查的缓存时设为 `Administrator`，不执行会话认证。

构造还创建 `SessionCleanupService`，5 分钟后首次调用清理，然后每小时运行；当前清理是将过期会话标记为撤销，不是删除账户，也不会同步清空 `RbacManagerConfig.LoginResult`。后台清理异常被捕获。取得单例、打开登录或管理窗口都不能当成无文件/数据库副作用的检查方法。

## 服务职责与注册

| 入口 | 当前责任 |
| --- | --- |
| `AuthService` / `IAuthService` | `LoginAndGetDetailAsync` 校验密码并构造登录结果；`LoginBySessionTokenAsync` 校验会话再重新读取用户。没有注册或创建会话接口 |
| `RegisterWindow.BtnRegister_Click` | 调用 `UserService.CreateUserAsync`；实现位于 `Services/IUserService.cs`，不是 `Services/UserService.cs` |
| `LoginWindow.CompleteLogin` | 调用 `SessionService.CreateSessionAsync`，再保存缓存并更新全局权限 |
| `SessionService` | 创建、校验、撤销会话与标记过期会话；这些调用可能读写本地数据库 |
| `PermissionChecker` | 按用户角色与权限码查询并缓存结果，不验证当前 SessionToken |
| `RbacManager` / 管理窗口 | 组合服务、同步缓存权限并在部分入口做粗粒度检查，不代表每个公开服务方法都经过统一授权拦截 |

普通用户创建及登录时补建详情显式使用 `PermissionMode.User`；`UserDetailEntity` 默认也是 `User`，`UserDetailDto` 默认是 `Guest`。内置管理员引导显式写入 `SuperAdministrator`，不能据此推断普通 DTO 或导入用户应默认高权限。新增注册、导入或提权路径仍须核对受控授权入口，不能把当前缓存检查当作授权证明。

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

## 登录链路

`RbacManager.IsUserLoggedIn()` 仅调用 `IsValidLoginResult`：用户 ID 大于 0、用户名非空、详情的 UserId 与用户 ID 相同。它不检查 SessionToken、到期/撤销、数据库中的用户启用/删除状态，甚至不使用 `RememberMe`。初始化从这种缓存恢复权限也不等于认证成功；不能新增“该方法返回 true 就已安全登录”的假设。

密码登录走 `AuthService.LoginAndGetDetailAsync`：查询启用且未软删除的用户，调用 `PasswordHasher` 校验，必要时升级旧密码格式，再构造用户详情与角色。`LoginWindow` 对用户名与密码相同的输入会要求先修改密码；该窗口规则不能扩写为所有服务调用者都会强制执行。

`CompleteLogin` 无论是否勾选“记住我”都会创建会话，并写入 `LoginResult`、`SessionToken` 和全局 `PermissionMode`，随后调用配置保存。`RememberMe=false` 只关闭下次登录窗口的自动登录选择并清空 SavedUsername，不等于没有创建或保存会话。会话默认有效期为 24 小时；显式调用者可以传入其他期限。

### 自动登录的三个结果

只有登录窗口初始化发现 `RememberMe=true` 且 token 非空，才调用 `TryAutoLogin`；`RbacManager` 构造本身不会走这条认证路径。

| 分支 | 代码实际行为 |
| --- | --- |
| `LoginBySessionTokenAsync` 成功 | 检查会话存在、未撤销、未过期及用户启用/未删除；更新 `LastActivityAt`，重新加载详情和角色。窗口更新 LoginResult、SavedUsername、全局权限并请求配置保存；不轮换 token，也不延长 ExpiresAt |
| 返回 null | 窗口只清空 SessionToken、关闭 RememberMe 并请求保存；没有清空 LoginResult，也没有重置全局权限，旧缓存权限可能继续存在 |
| 抛异常 | `TryAutoLogin` 的 catch 只让输入框获得焦点，继续手动登录；不保证凭据、缓存或权限已清理 |

这两种失败分支是当前未修复限制，不是“失败自动降权”的实现。`SessionService.ValidateSessionAsync` 也是另一接口：它检查会话并可能标记过期会话为撤销、更新活动时间，但不重新查询用户启停状态；不能把它与 `AuthService.LoginBySessionTokenAsync` 的完整行为混同。

### 登出与保存不是一项事务

`RbacManagerWindow.BtnLogout_Click` 经确认后先尝试撤销当前 token，再尝试写审计；两处异常都被吞掉。随后清空 LoginResult、SessionToken、SavedUsername，关闭 RememberMe，把当前全局权限设为 `Guest`，调用配置保存并重新显示登录窗口。重新登录可再次改变权限；本地状态清空不能证明数据库会话已成功撤销。

登出当下的 `Guest` 与下次无结构有效缓存时初始化的 `Administrator` 是两条不同分支，不能承诺登出后重启仍保持 Guest。配置保存调用也不能替代对落盘结果的验证，保存层边界见[配置、恢复与保存](../../04-api-reference/ui-components/configuration.md)。

## 权限检查

| 层级 | 当前入口 | 说明 |
| --- | --- | --- |
| 粗粒度 | `Authorization.Instance.PermissionMode`、`AccessControl.Check(...)` | 管理窗口按枚举数值比较；管理员及更高权限可进入，但这种比较本身没有验证登录或会话 |
| 细粒度 | `PermissionChecker` | 查询用户角色和角色权限，带过期时间和 LRU 缓存 |

当前系统是粗细两层并存。不要把 `PermissionMode` 写成已经被 RBAC 完全替换，也不要把权限码写成已覆盖全产品所有入口。

`PermissionChecker` 的缓存期限为 5 分钟，最多缓存约 1000 个用户并按访问时间淘汰。当前数据库查询从 UserRole/RolePermission 连接 Permission，过滤权限自身的启用/删除状态，但不在该查询中核验用户或角色的启停/删除状态。权限管理窗口保存后调用 `InvalidateAllCache()`；其他修改路径不能仅因数据库已更新就假定所有缓存已失效。

## 可见窗口

| 窗口 | 用途 |
| --- | --- |
| `LoginWindow` | 登录和 Session 恢复 |
| `RegisterWindow` | 注册用户 |
| `ChangePasswordWindow` | 修改密码 |
| `UserManagerWindow` | 用户列表、角色查看、启停、删除、重置密码 |
| `PermissionManagerWindow` | 按角色分配权限，保存后清权限缓存 |
| `RbacManagerWindow` | RBAC 总入口和当前登录状态 |

## 用户中心统计：本机使用与业务库计数

这部分不是身份认证审计，也不是当前用户的个人执行历史。`UserCenterStatisticsService.QueryAsync` 没有用户 ID 筛选，查询当前配置业务数据库的 `t_scgd_measure_batch`；不要根据“用户中心”标题把结果归属到登录用户。

- `ApplicationUsageTracker.StartSession` 在主程序通过单实例处理后记录一次启动并请求保存配置；正常退出的 `StopSession` 将本次时长累计到 `RbacManagerConfig`。崩溃、强制结束或保存失败不保证时长落盘。“本次运行”优先取当前进程时长，失败时回退到追踪器起点；总时长为已累计值加当前运行时长。
- 一条批次记录按一次流程执行尝试计数，`result_code = 6` 按已完成计数，不等于检测 OK/合格率。该常量对应持久化的 Completed 值，避免 UI 为统计反向依赖 Engine。
- 累计次数查询全表；活动图查询包含今天在内的最近 364 天（52 周）。Presenter 的“最近”次数与完成率使用其中最近 7 天，平均时长按期间执行次数加权，不是所有窗口都使用同一分母。
- `UserCenterStatisticsPresenter` 补齐缺失日期、合并重复日期、约束异常计数并计算完成率与加权平均；页面代码负责绑定展示。数据库未连接或查询失败时返回 `IsAvailable=false`，本机使用数据和用户资料仍可展示，流程区域标记不可用；失败的空/零结果不能解释为真实零次执行，也不使用模拟记录填充。
- 页面文案使用 `ColorVision.Solution` 资源及 `UserCenterText` 包装；新增资源键应在简体中文、繁体中文和英语资源中保持一致。

打开/刷新用户中心会尝试查询业务库，账户修改、会话撤销与统计查询不是同一种操作。只读代码核验无需启动窗口、连接业务库或访问真实用户数据。

## 边界

- 账户认证依赖本地 SQLite 和本地窗口，不依赖外部认证服务器；用户中心流程统计另依赖业务数据库。
- `PermissionMode` 仍是很多关键入口的第一道判断。
- 细粒度权限主要集中在 RBAC 自己的管理窗口和服务层。
- MFA、证书、IP 白名单、全业务审计总线等能力当前没有落地，不要写进现有能力。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 初始化 | `RbacManager.cs` |
| 实体 | `Entity/` |
| 登录 | `Services/Auth/AuthService.cs`、`Loginwindow.xaml.cs` |
| 注册与用户服务 | `RegisterWindow.xaml.cs`、`Services/IUserService.cs` |
| 会话 | `Services/SessionService.cs`、`Services/SessionCleanupService.cs` |
| 权限缓存 | `Services/PermissionChecker.cs` |
| 用户管理 | `UserManagerWindow.xaml.cs` |
| 权限管理 | `PermissionManagerWindow.xaml.cs` |
| 本机使用与流程统计 | `ApplicationUsageTracker.cs`、`Services/UserCenterStatisticsService.cs`、`RbacManagerWindow.xaml.cs` |

## 验证范围与缺口

`ModuleCatalogTests` 只覆盖 RBAC 模块注册与菜单 provider 可发现，不证明登录、会话、权限缓存或用户中心数据库行为已验证。本页没有声明这些分支的专门自动化覆盖，本次文档校正也没有修复产品中的授权限制。

修改相关代码时，应在隔离配置和合成数据库中覆盖：无缓存与结构完整但过期/撤销的缓存、普通用户创建、记住我开关、自动登录成功/null/异常、撤销失败后本地登出、重启后的默认权限，以及统计全表/日期窗口/不可用结果。需要窗口交互或真实数据库时另行取得运行和数据访问授权，不能用生产账号或业务库验证文档。

目标框架、包依赖和版本以 `UI/ColorVision.Rbac/ColorVision.Rbac.csproj` 为准。README 被打包为 NuGet 包说明；本页与仓库源码不保证随包交付，包使用者须核对对应源码版本。
