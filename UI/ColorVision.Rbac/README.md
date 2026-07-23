# RBAC 模块说明

`UI/ColorVision.Rbac` 提供应用内的用户、角色、权限、会话和审计能力。该模块直接影响应用启动后的全局授权状态，因此默认权限、登录态恢复和会话持久化需要保持保守。

## 核心组件

- `RbacManager`: RBAC 单例入口，负责服务初始化、默认管理员引导、登录态恢复和全局权限同步。
- `AuthService`: 登录、注册、密码校验、会话创建和自动登录校验。
- `SessionService`: 会话令牌、过期时间、吊销和清理。
- `PermissionChecker`: 细粒度权限检查和缓存。
- `UserService` / `RoleService`: 用户、角色、权限关系维护。
- `LoginWindow` / `RbacManagerWindow`: 登录、自动登录、登出和管理入口。

## 安全默认值

- 应用启动且尚未恢复有效登录态时，`Authorization.PermissionMode` 默认保持 `Administrator`，保证受管理员级别保护的功能可直接使用。
- 登录成功后，全局权限会立即切换为当前用户的 `PermissionMode`；用户主动登出后，权限会重置为 `Guest`。
- `LoginResultDto.UserDetail.PermissionMode` 的默认值为 `Guest`，避免空 DTO 被误判为高权限。
- 新建普通用户的 `UserDetailEntity.PermissionMode` 显式设置为 `User`。
- 只有首次引导的内置管理员允许显式赋予 `SuperAdministrator`。
- `RbacManager` 只会从有效登录结果恢复权限：用户 ID 必须有效、用户名不能为空，并且用户详情必须匹配同一个用户。

## 登录态持久化

- 自动登录成功后会保存 `RbacManagerConfig`，保证刷新后的会话令牌和用户详情落盘。
- 自动登录失败或登录态无效时，会清理本地会话凭据并回到默认管理员级全局权限。
- 用户主动登出时，会清理 `LoginResult`、关闭 `RememberMe`，并把全局权限重置为 `Guest` 后保存配置。
- 登出流程会撤销当前会话，随后清空本地登录态。

## 用户中心使用概览

- `ApplicationUsageTracker` 在主程序通过单实例检查后记录一次启动，并在正常退出时把本次时长累计到 `RbacManagerConfig`；页面展示的“本次运行”直接取当前进程时长。
- `UserCenterStatisticsService` 只读查询 `t_scgd_measure_batch`。一条批次记录按一次流程执行计数，`result_code = 6` 按已完成计数；累计次数覆盖全表，活动图和洞察覆盖最近 364 天（完整 52 周）。
- 数据库未连接或查询失败时，用户资料和本机使用数据仍可查看，流程区域显示不可用状态，不使用模拟数据填充。
- `UserCenterStatisticsPresenter` 负责日期补齐、重复日期合并、异常计数约束、完成率和加权平均时长计算，保持窗口代码只承担展示。
- 新增页面文案通过 `ColorVision.Solution` 资源管理器读取，并在简体中文、繁体中文、英语、法语、日语、韩语和俄语资源中保持同键。

## 维护约定

- 新增授权入口时先通过 `RbacManager.IsUserLoggedIn()` 判断有效登录态，再读取用户权限。
- 不要把实体、DTO 或配置对象的默认权限设为 `Administrator`、`SuperAdministrator`。
- 新增注册或导入用户流程时，必须显式写入普通用户权限，管理员权限只能通过受控管理入口授予。
- 修改会话逻辑后，需要验证自动登录成功、自动登录失败、手动登出和配置缺失四种路径。

## 验证建议

```powershell
dotnet build UI/ColorVision.Solution/ColorVision.Solution.csproj -p:Platform=x64 -v:minimal
```

建议在 UI 中补充手工验证：

- 启动无登录态应用，确认权限默认为 `Administrator`。
- 注册普通用户，确认默认权限为 `User`。
- 勾选自动登录后重启，确认有效会话可恢复。
- 登出后重启，确认不会恢复旧权限。
