# RBAC 模块说明

`UI/ColorVision.Solution/Rbac` 提供解决方案模块内的用户、角色、权限、会话和审计能力。该模块直接影响应用启动后的全局授权状态，因此默认权限、登录态恢复和会话持久化需要保持保守。

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
