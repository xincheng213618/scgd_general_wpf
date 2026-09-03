---
knowledge_id: "platform.security"
knowledge_type: "topic"
status: "current"
summary: "区分应用管理员、RBAC会话与权限码、Windows服务身份及远程/工具授权；登录缓存和界面状态不能替代执行入口的权限检查。"
aliases: ["权限边界", "安全架构", "应用管理员", "Windows管理员", "身份与权限", "授权入口", "鉴权入口"]
code_paths: ["UI/ColorVision.Common/Authorizations", "UI/ColorVision.Rbac/RbacManager.cs", "UI/ColorVision.Rbac/Services/PermissionChecker.cs", "UI/ColorVision.Rbac/Services/Auth/AuthService.cs", "UI/ColorVision.Rbac/Services/SessionService.cs"]
test_paths: []
related: ["platform.architecture", "platform.rbac", "ui.common", "ui.menus", "platform.service-host", "copilot.mcp-server", "delivery.backend-operations", "copilot.execution"]
---

# 权限边界与鉴权入口

ColorVision 的权限判断分布在桌面入口、账户服务、本机权限代理、远程接口和工具执行器中。排查“为什么允许或拒绝这个操作”时，需要确认实际调用经过了哪一层；界面显示、登录缓存和同名的“管理员”不能替代执行入口的检查。

本页用于选择责任边界。具体比较规则、会话字段、协议验证和审批流程在各自主题维护，不在这里复制第二份契约。

## 按实际入口查权限

| 入口或问题 | 负责判断的模块 | 对应文档 |
| --- | --- | --- |
| 应用管理员、菜单和窗口访问 | 全局 `Authorization.Instance.PermissionMode`、AccessControl 以及具体命令/窗口中的显式检查 | [Common 权限帮助器](../../04-api-reference/ui-components/ColorVision.Common.md)、[菜单执行](../../04-api-reference/ui-components/menus.md) |
| 用户登录、自动登录和登出 | RBAC 的 AuthService、SessionService 与登录/账户窗口；身份验证、缓存恢复和全局模式同步是不同步骤 | [RBAC 登录与会话](./rbac.md) |
| 用户能否执行某个权限码 | RBAC PermissionChecker 查询角色权限并缓存结果；调用者仍需核对用户身份及检查位置 | [RBAC 权限检查](./rbac.md#权限检查) |
| 本机服务、注册表、关联或安装维护 | ColorVisionServiceHost 的 Windows 调用身份、命令票据和业务参数规则 | [本机权限代理](../components/service-host.md) |
| 入站 MCP 请求 | 本地 MCP server 的监听范围、认证、会话、能力白名单及二次确认 | [ColorVision 入站 MCP](../../02-developer-guide/core-concepts/colorvision-mcp.md) |
| HTTP 运维中继与设备任务 | Backend Operations 的访问认证、设备签名和任务状态链 | [运维中继](../../02-developer-guide/backend/operations-relay.md) |
| Copilot 工具执行 | 请求能力、工具权限、审批决定和执行瞬间的保护 | [Agent 执行与审批](../../02-developer-guide/core-concepts/copilot-agent-execution.md) |

这些入口不会因为模块名称相近或共享同一界面就自动采用同一权限规则。协议已认证、工具已批准和业务执行完成也需要分别判断。

## 应用权限与 Windows 身份

`PermissionMode` 是应用内枚举，不是 Windows 用户组或进程提权状态。它提供 SuperAdministrator、Administrator、PowerUser、User、Guest，数值越小权限越高；多个管理入口用它决定是否允许继续。

`Authorization.Instance` 是可设置的全局配置对象，RBAC 的初始化和登录路径会更新其中的模式。看到 Administrator 不能证明已验证有效 SessionToken、拥有指定 RBAC 权限码，或当前进程具有 Windows 管理员令牌。应用管理员与 ServiceHost 的 Windows 调用身份应分别检查。

AccessControl 的方法特性检查和执行帮助器使用不同判据；类特性、委托包装、直接调用和命令 CanExecute 也不具有自动统一拦截效果。精确规则与空实例行为见 [Common 权限契约](../../04-api-reference/ui-components/ColorVision.Common.md#粗粒度权限的判据不是统一授权拦截)，不要仅因方法名包含 Permission 就认为所有调用路径等价。

## 本地账户与权限码的范围

`UI/ColorVision.Rbac/` 是主程序登记的内置账户模块，使用本地 SQLite 和 SqlSugar CodeFirst 管理用户、角色、权限、会话及相关审计。构造 RbacManager 会初始化存储和服务，不能把调用 GetInstance 当成没有副作用的身份查询。

登录结果缓存、会话有效性、用户启停状态和权限码是不同证据。当前初始化还可能从结构有效的缓存恢复模式，缺少这种缓存时仍设置 Administrator；这是[RBAC 主题记录的现有限制](./rbac.md)，不能把它作为已认证或默认应授权的依据。自动登录失败、登出撤销、缓存失效和落盘结果也应按该主题分别核对。

RBAC 的 AuditLogService 记录其接入的账户与权限动作。其它模块可能有自己的日志、任务记录或审批证据；存在 RBAC 审计表不表示所有应用操作都被统一审计。数据库位置、表结构和会话期限继续以 RBAC 主题为准。

## 定位一次允许或拒绝

1. 确认入口：用户点击菜单、应用搜索执行命令、直接调用服务、pipe/HTTP 请求或 Copilot 工具调用。不要用某个按钮的状态替代实际调用路径。
2. 找到操作执行前的检查代码，辨别它读取的是全局模式、用户权限码、会话、Windows 身份、协议凭据还是单次审批；确认其它入口是否也经过它。
3. 核对身份和权限数据的来源、缓存与更新时机。需要当前有效会话时，不能以登录结果字段齐全或某个应用权限级别代替验证。
4. 分别记录拒绝、审批、命令接纳及业务完成证据；网络连接、窗口打开或成功日志不能跨过后续责任边界。

新增或调整操作入口时，沿以上路径确认必要检查实际覆盖执行点，并按所属模块的契约处理失败。不能仅增加特性、隐藏菜单或启用某个账户服务，就宣称整个产品已接入统一鉴权、传输保护或审计。

## 验证范围

本页是跨模块定位概览，没有声明一套覆盖全产品的权限测试。Common、RBAC、ServiceHost、MCP 和 Copilot 的源码与测试入口随各自主题维护；只验证 UI 可见性或模块注册不足以证明未经授权的直接调用会被拒绝。

文档审查可读取代码和测试，核对入口与责任关系。实际创建用户、修改角色、调用特权服务或执行工具时，还需按操作范围使用获授权的隔离环境；示例与文档本身不授予操作权限。
