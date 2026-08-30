# ColorVision.Rbac

Windows/WPF 的本地账户、角色、权限、会话与审计模块，并提供用户中心使用统计；不是覆盖整个产品的统一认证或授权内核。

## 包与运行前提

- 当前面向 `net10.0-windows7.0` / x64，依赖 SqlSugar、本地 SQLite 及 ColorVision 共享模块；具体依赖和版本以 `ColorVision.Rbac.csproj` 为准。
- 首次构造 `RbacManager` 会创建或初始化本地数据库、引导管理员与权限并启动会话清理定时器；打开管理界面可能触发这些动作，不是只读检查。用户中心的流程统计另读已配置的业务数据库。
- 当前仍有未修复的授权限制：无结构有效缓存时初始化为 `Administrator`，`IsUserLoggedIn()` 只检查缓存字段，不验证会话有效性。自动登录失败不保证清掉缓存权限，登出清本地状态也不保证数据库中的会话已成功撤销。不要把这些现状当作安全设计建议。

## 权威知识与构建

[RBAC：登录缓存、会话与权限边界](../../docs/03-architecture/security/rbac.md)维护服务职责、登录/登出分支、用户中心统计口径和验证缺口。本 README 会进入 NuGet 包根目录，`docs/` 不保证随包存在；链接用于源码仓库，完整契约需读取与包版本匹配的源码知识。

从仓库根目录在 Windows 上执行以下本地构建：会还原依赖、写入产物，并按项目的 `GeneratePackageOnBuild` 配置生成包；不发布，也不验证登录或数据库运行。

```powershell
dotnet build .\UI\ColorVision.Rbac\ColorVision.Rbac.csproj -p:Platform=x64 -v:minimal
```
